﻿﻿﻿﻿﻿﻿using System;
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
        static string key = "205a573dee714306b532d1cadb3e86f4";
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
            IEnumerable<string> files = FindFiles(directoryPath, filePattern, scanLimit);

            foreach (string file in files)
            {
                Console.WriteLine(file);
                AnalyzeDocumentAsync(file).Wait();
            }
        }

        public static async Task AnalyzeDocumentAsync(string document)
        {
            Uri fileUri = new Uri("https://raw.githubusercontent.com/Azure-Samples/cognitive-services-REST-api-samples/master/curl/form-recognizer/rest-api/read.png");
            Operation<AnalyzeResult> operation = await _docIntelClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", fileUri);
            
            //Operation<AnalyzeResult> operation = await _docIntelClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", BinaryData.FromString(document));
            AnalyzeResult result = operation.Value;
        }

        /// <summary>
        /// Finds files in the specified directoryPath that match the specified filePattern.
        /// </summary>
        /// <param name="directoryPath">Absolute filepath</param>
        /// <param name="filePattern">A file pattern, such as *.PDF, *.TIF, etc.</param>
        /// <param name="scanLimit">How deep to scan.</param>
        /// <returns></returns>
        public static IEnumerable<string> FindFiles(string directoryPath, string filePattern, int scanLimit = int.MaxValue)
        {
            try
            {
                return Directory.GetFiles(directoryPath, filePattern, SearchOption.AllDirectories).Take(scanLimit);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred: " + ex.Message);
                return new List<string>();
            }
        }
    }
}
