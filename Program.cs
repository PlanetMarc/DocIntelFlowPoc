﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Azure.Core;
using Azure.AI.DocumentIntelligence;
using Azure;

namespace PlanetTech.AI.DocIntelFlowPoc
{
    class Program
    {
        static string key = "XXX";
        static string endpoint = "https://demo-ak-dot.cognitiveservices.azure.com/";
        static AzureKeyCredential _credential = new AzureKeyCredential(key);
        static DocumentIntelligenceClient _docIntelClient = new DocumentIntelligenceClient(new Uri(endpoint), _credential);

        public Program()
        {
            // Create a Document Intelligence client
            _docIntelClient = new DocumentIntelligenceClient(new Uri(endpoint), _credential);
        }

        static void Main(string[] args)
        {
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
            var content = result.Content;

            Console.WriteLine($"\nDocument was analyzed and has {result.Pages.Count} pages and {result.Paragraphs.Count} paragraphs.\n");

            foreach (DocumentLanguage language in result.Languages)
            {
                Console.WriteLine($"Found language '{language.Locale}' with confidence {language.Confidence}.");
            }
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
