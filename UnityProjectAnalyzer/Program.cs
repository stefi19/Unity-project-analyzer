using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Main entry point for the Unity Project Analyzer application.
    /// This tool provides comprehensive analysis of Unity game projects including scene hierarchy,
    /// component usage, missing references, project health metrics, and multiple export formats.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Application entry point that handles command-line arguments and orchestrates the analysis process.
        /// Supports multiple export formats (HTML, JSON, XML) for different integration scenarios.
        /// </summary>
        /// <param name="args">Command line arguments: project path, output path, and optional export flags</param>
        static async Task Main(string[] args)
        {
            // Check if we have the minimum required arguments
            if (args.Length < 2)
            {
                // Display helpful usage information when arguments are missing
                Console.WriteLine("Unity Project Analyzer v2.0");
                Console.WriteLine("============================");
                Console.WriteLine();
                Console.WriteLine("Usage: UnityProjectAnalyzer.exe <unity_project_path> <output_folder_path> [--json] [--xml]");
                Console.WriteLine();
                Console.WriteLine("Options:");
                Console.WriteLine("  --json    Export analysis results to JSON format");
                Console.WriteLine("  --xml     Export analysis results to XML format");
                Console.WriteLine();
                Console.WriteLine("Examples:");
                Console.WriteLine("  UnityProjectAnalyzer.exe \"C:\\MyUnityProject\" \"C:\\Reports\"");
                Console.WriteLine("  UnityProjectAnalyzer.exe \"C:\\MyUnityProject\" \"C\\Reports\" --json");
                Console.WriteLine("  UnityProjectAnalyzer.exe \"C:\\MyUnityProject\" \"C:\\Reports\" --json --xml");
                return;
            }

            // Parse command line arguments to determine what analysis and exports to perform
            string unityProjectPath = args[0];
            string outputFolderPath = args[1];
            bool exportJson = args.Contains("--json");  // Check if JSON export was requested
            bool exportXml = args.Contains("--xml");    // Check if XML export was requested

            // Validate that the Unity project path actually exists before proceeding
            if (!Directory.Exists(unityProjectPath))
            {
                Console.WriteLine($"Unity project path does not exist: {unityProjectPath}");
                return;
            }

            // Ensure the output directory exists, creating it if necessary
            Directory.CreateDirectory(outputFolderPath);

            // Create and run the analyzer with the specified options
            var analyzer = new UnityProjectAnalyzer();
            await analyzer.AnalyzeProject(unityProjectPath, outputFolderPath, exportJson, exportXml);
            
            Console.WriteLine("Analysis completed successfully!");
        }
    }
}
