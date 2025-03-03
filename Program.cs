﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Azure.Core;
using Azure.AI.DocumentIntelligence;
using Azure;
using System.ComponentModel;
using System.Net.Http.Headers;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using Azure.AI.OpenAI.Chat;
using System.Text.Json;
using System.Net.Http.Json;
using System.Threading.Tasks;

using OpenAI;


namespace PlanetTech.AI.DocIntelFlowPoc
{
    class Program
    {
#region "Azure Document Intelligence Declarations"
        static string DOCINTEL_API_KEY = "XXX";
        static string DOCINTEL_ENDPOINT = "https://demo-ak-dot.cognitiveservices.azure.com/";
        static AzureKeyCredential _docIntelCredential = new AzureKeyCredential(DOCINTEL_API_KEY);
        static DocumentIntelligenceClient _docIntelClient = new DocumentIntelligenceClient(new Uri(DOCINTEL_ENDPOINT), _docIntelCredential);
#endregion

#region "Azure OpenAI Declarations"
        static string OPENAI_API_KEY = "XXX";
        static string OPENAI_ENDPOINT = "https://deedr-ai.openai.azure.com/";
        static string? SYSTEM_PROMPT;
        static readonly HttpClient _openAIClient = new HttpClient();   // LLM to massage raw information after first pass
        //static short _apiTimeout = 300; // Azure OpenAI API timeout in seconds (might be used in future)
#endregion
static Program()
{
    Console.Clear();

    _openAIClient.DefaultRequestHeaders.Clear();
    _openAIClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    _openAIClient.BaseAddress = new Uri(OPENAI_ENDPOINT);
    _openAIClient.DefaultRequestHeaders.Add("api-key", OPENAI_API_KEY);
}

public Program()
        {
            // Create a Document Intelligence client
            _docIntelClient = new DocumentIntelligenceClient(new Uri(DOCINTEL_ENDPOINT), _docIntelCredential);
            SYSTEM_PROMPT = File.ReadAllText("system_prompt.txt");
        }

        static void Main(string[] args)
        {
            if (!Directory.Exists("output"))
            {
                Directory.CreateDirectory("output");
                Console.WriteLine("Created output directory...");
            }
            
            // Parse command-line arguments for directoryPath, filePattern, and scanLimit
            string directoryPath = @"docs";
            string filePattern = "*.tif";
            int scanLimit = int.MaxValue;

            // Check command-line param values
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-scanpath" && i + 1 < args.Length)
                {
                    directoryPath = args[i + 1];
                    i++;
                }
                else if (args[i] == "-pattern" && i + 1 < args.Length)
                {
                    filePattern = args[i + 1];
                    i++;
                }
                else if (args[i] == "-scanlimit" && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1], out scanLimit))
                    {
                        scanLimit = int.MaxValue;
                    }
                    i++;
                }
            }

            // No additional checks; defaults will be used if -pattern is not provided.
            int totalFilesFound;
            IEnumerable<string> files = FindFiles(directoryPath, filePattern, out totalFilesFound, scanLimit);
            Console.WriteLine($"Found {totalFilesFound} files matching pattern '{filePattern}' in '{directoryPath}'.");

            // Found files?
            if (files.Any())
            {
                if (scanLimit < int.MaxValue) // user specified a limit to scan
                {
                    Console.WriteLine($"Scanning only the first {scanLimit} files.");
                    totalFilesFound = scanLimit;
                }
                Console.WriteLine("Beginning analysis...");
            }

            DateTime startTime = DateTime.Now;
            foreach (string file in files)
            {
                Console.WriteLine(file);
                AnalyzeDocumentAsync(file).Wait();
            }
            TimeSpan duration = DateTime.Now - startTime;
            
            Console.WriteLine($"Processed {scanLimit} documents successfully in {duration.Seconds} seconds.");
        }

        /// <summary>
        /// Sends the document to the Azure Document Intelligence service for analysis.
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static async Task AnalyzeDocumentAsync(string document)
        {
            //Uri fileUri = new Uri("https://github.com/PlanetMarc/DocIntelFlowPoc/blob/main/docs/sample1.TIF");
            //Operation<AnalyzeResult> operation = await _docIntelClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", fileUri);
            
            var docFileBytes = File.ReadAllBytes(document);
            var cts = new CancellationTokenSource();

            // Give the user a status update as the operation completes
            var spinnerTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    Console.Write(".");
                    await Task.Delay(100);
                }
            });

            Operation<AnalyzeResult> operation = await _docIntelClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", BinaryData.FromBytes(docFileBytes), cts.Token);
            cts.Cancel();
            await spinnerTask;

            AnalyzeResult result = operation.Value;

            // Write the content to a file
            //File.WriteAllText("output/output.txt", result.Content);
           

            // What type of document is it?
            var llm = await LlmProcessDocument(PromptRole.ClassifyDocument, result.Content);
            Console.WriteLine("\n\n" + "Document Type: " + llm);

            // Extract key value pairs
            llm = await LlmProcessDocument(PromptRole.ExtractKeyValuePairs, result.Content);
            Console.WriteLine("\n" + "Key Value Pairs: \n\n" + llm);

            Console.WriteLine($"\nDocument was analyzed and has {result.Pages.Count} pages and {result.Paragraphs.Count} paragraphs.\n");

            foreach (DocumentLanguage language in result.Languages)
            {
                Console.WriteLine($"Found language '{language.Locale}' with confidence {language.Confidence}.");
            }
        }


        public static async Task<string> LlmProcessDocument(PromptRole promptRole, string document)
        {

            // This keeps the context for the LLM
            var history = new List<ChatMessage>();

            history.Add(new ChatMessage { Role = "user", PromptRole = promptRole, Document = document });

            // Create chat completion options  
            var options = new ChatCompletionOptions();

            var response = await _openAIClient.PostAsJsonAsync("openai/deployments/o3-mini/chat/completions?api-version=2024-12-01-preview", new
            {
                model = "o3-mini",
                messages = history,
                stream = false
            });

            var completion = await response.Content.ReadFromJsonAsync<LlmResponse>();

            // <!-- NOTE: this section only needs to be included if max tokens are configured -->
            // Setting MaxOutputTokenCount requires a temporary workaround using 2.2.0-beta.1
            // See related:
            // https://github.com/Azure/azure-sdk-for-net/pull/48218#issuecomment-2652005055
            //
            options
              .GetType()
              .GetProperty(
                  "SerializedAdditionalRawData",
                  System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
              .SetValue(options, new System.Collections.Generic.Dictionary<string, BinaryData>());
            options.MaxOutputTokenCount = 100000;

            
            // check for a null completion...
            var answer = completion?.Choices?[0]?.Message?.Content ?? "No response from DeedR LLM.";

            return answer;
        }


        /// <summary>
        /// Finds files in the specified directoryPath that match the specified filePattern.
        /// </summary>
        /// <param name="directoryPath">Absolute filepath</param>
        /// <param name="filePattern">A file pattern, such as *.PDF, *.TIF, etc.</param>
        /// <param name="totalFiles">Output parameter for total files found</param>
        /// <param name="scanLimit">How deep to scan.</param>
        /// <returns></returns>
        public static IEnumerable<string> FindFiles(string directoryPath, string filePattern, out int totalFiles, int scanLimit = int.MaxValue)
        {
            try
            {
                string[] allFiles = Directory.GetFiles(directoryPath, filePattern, SearchOption.AllDirectories);
                totalFiles = allFiles.Length;
                return allFiles.Take(scanLimit);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
                totalFiles = 0;
                return new List<string>();
            }
        }
    }
}
