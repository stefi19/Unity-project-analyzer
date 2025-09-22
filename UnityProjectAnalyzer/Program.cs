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
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ./tool.exe unity_project_path output_folder_path");
                return;
            }

            string unityProjectPath = args[0];
            string outputFolderPath = args[1];

            if (!Directory.Exists(unityProjectPath))
            {
                Console.WriteLine($"Unity project path does not exist: {unityProjectPath}");
                return;
            }

            Directory.CreateDirectory(outputFolderPath);

            var analyzer = new UnityProjectAnalyzer();
            await analyzer.AnalyzeProject(unityProjectPath, outputFolderPath);
            
            Console.WriteLine("Analysis completed successfully!");
        }
    }
}
