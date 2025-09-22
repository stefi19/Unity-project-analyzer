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
    public class UnityProjectAnalyzer
    {
        private readonly SceneParser _sceneParser;
        private readonly ScriptAnalyzer _scriptAnalyzer;

        public UnityProjectAnalyzer()
        {
            _sceneParser = new SceneParser();
            _scriptAnalyzer = new ScriptAnalyzer();
        }

        public async Task AnalyzeProject(string unityProjectPath, string outputFolderPath)
        {
            using var totalTimer = new PerformanceTimer("Unity Project Analysis");
            
            Console.WriteLine("Unity Project Analyzer v2.0");
            Console.WriteLine("================================");
            
            // Find all scene files
            var sceneFiles = Directory.GetFiles(Path.Combine(unityProjectPath, "Assets"), "*.unity", SearchOption.AllDirectories);
            ProgressBar.ShowInfo($"Found {sceneFiles.Length} scene files");
            
            // Find all script files
            var scriptFiles = Directory.GetFiles(Path.Combine(unityProjectPath, "Assets"), "*.cs", SearchOption.AllDirectories);
            ProgressBar.ShowInfo($"Found {scriptFiles.Length} script files");

            // Store scene analysis results for HTML report
            var sceneResults = new List<SceneAnalysisResult>();
            var processedScenes = 0;

            // Parse scenes in parallel
            var sceneParsingTasks = sceneFiles.Select(async sceneFile =>
            {
                using var sceneTimer = new PerformanceTimer($"Scene: {Path.GetFileNameWithoutExtension(sceneFile)}");
                
                var hierarchy = await _sceneParser.ParseSceneHierarchy(sceneFile);
                var sceneName = Path.GetFileNameWithoutExtension(sceneFile);
                var outputFile = Path.Combine(outputFolderPath, $"{sceneName}.unity.dump");
                await File.WriteAllTextAsync(outputFile, hierarchy);
                
                // Get detailed scene info for HTML report
                var gameObjects = await _sceneParser.GetGameObjectsForReport(sceneFile);
                lock (sceneResults)
                {
                    sceneResults.Add(new SceneAnalysisResult
                    {
                        SceneName = $"{sceneName}.unity",
                        GameObjects = gameObjects
                    });
                    
                    processedScenes++;
                    ProgressBar.Show(processedScenes, sceneFiles.Length, "Processing scenes");
                }
                
                return sceneFile;
            });

            // Parse scripts in parallel
            using (new PerformanceTimer("Script Analysis"))
            {
                var scriptParsingTask = _scriptAnalyzer.AnalyzeScripts(scriptFiles);

                // Wait for scene parsing to complete
                await Task.WhenAll(sceneParsingTasks);
                
                // Get script analysis results
                var scriptAnalysisResult = await scriptParsingTask;
                ProgressBar.ShowSuccess($"Analyzed {scriptAnalysisResult.Scripts.Count} scripts");

            // Find scripts referenced in scenes
            var referencedScripts = new HashSet<string>();
            foreach (var sceneFile in sceneFiles)
            {
                var references = await _sceneParser.GetScriptReferences(sceneFile);
                foreach (var reference in references)
                {
                    referencedScripts.Add(reference);
                }
            }

            // Find unused scripts
            var unusedScripts = scriptAnalysisResult.Scripts
                .Where(script => !referencedScripts.Contains(script.ClassName))
                .ToList();

                // Generate unused scripts report
                await GenerateUnusedScriptsReport(unusedScripts, outputFolderPath);
                
                if (unusedScripts.Count > 0)
                {
                    ProgressBar.ShowWarning($"Found {unusedScripts.Count} unused scripts");
                }
                else
                {
                    ProgressBar.ShowSuccess("No unused scripts found - project is clean");
                }

                // Generate interactive HTML report
                using (new PerformanceTimer("HTML Report Generation"))
                {
                    var htmlGenerator = new HtmlReportGenerator(sceneResults, unusedScripts, unityProjectPath);
                    await htmlGenerator.GenerateReportAsync(outputFolderPath);
                }
            }
            
            Console.WriteLine();
            ProgressBar.ShowSuccess("Analysis complete");
            Console.WriteLine("Check 'UnityProjectReport.html' for an interactive overview");
            Console.WriteLine("Text reports available in the output folder");
        }

        private async Task GenerateUnusedScriptsReport(List<ScriptInfo> unusedScripts, string outputFolderPath)
        {
            var outputFile = Path.Combine(outputFolderPath, "UnusedScripts.csv");
            var lines = new List<string> { "Relative Path,GUID" };

            foreach (var script in unusedScripts)
            {
                var guid = await GetScriptGuid(script.FilePath);
                var relativePath = script.RelativePath;
                lines.Add($"{relativePath},{guid}");
            }

            await File.WriteAllLinesAsync(outputFile, lines);
        }

        private async Task<string> GetScriptGuid(string scriptPath)
        {
            var metaFile = scriptPath + ".meta";
            if (File.Exists(metaFile))
            {
                var content = await File.ReadAllTextAsync(metaFile);
                var lines = content.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("guid:"))
                    {
                        return line.Substring(5).Trim();
                    }
                }
            }
            return "unknown";
        }
    }
}