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
    /// Core orchestrator class that coordinates all aspects of Unity project analysis.
    /// Handles scene parsing, script analysis, component detection, missing reference detection,
    /// health metrics calculation, and report generation in multiple formats.
    /// </summary>
    public class UnityProjectAnalyzer
    {
        // Specialized analyzers for different aspects of the Unity project
        private readonly SceneParser _sceneParser;                    // Handles Unity scene file parsing and hierarchy extraction
        private readonly ScriptAnalyzer _scriptAnalyzer;              // Analyzes C# MonoBehaviour scripts using Roslyn
        private readonly ComponentAnalyzer _componentAnalyzer;        // Categorizes and analyzes Unity components
        private readonly MissingReferenceDetector _missingReferenceDetector;  // Detects broken asset references
        private readonly ProjectHealthAnalyzer _projectHealthAnalyzer;  // Calculates project health metrics and grades
        private readonly ExportManager _exportManager;                // Handles JSON/XML export functionality

        /// <summary>
        /// Initializes all the specialized analyzer components needed for comprehensive project analysis.
        /// Each analyzer is responsible for a specific aspect of the Unity project structure.
        /// </summary>
        public UnityProjectAnalyzer()
        {
            _sceneParser = new SceneParser();
            _scriptAnalyzer = new ScriptAnalyzer();
            _componentAnalyzer = new ComponentAnalyzer();
            _missingReferenceDetector = new MissingReferenceDetector();
            _projectHealthAnalyzer = new ProjectHealthAnalyzer();
            _exportManager = new ExportManager();
        }

        /// <summary>
        /// Performs comprehensive analysis of a Unity project, generating detailed reports and insights.
        /// This is the main entry point that orchestrates all analysis phases and output generation.
        /// </summary>
        /// <param name="unityProjectPath">Path to the Unity project root directory</param>
        /// <param name="outputFolderPath">Directory where all reports and exports will be generated</param>
        /// <param name="exportJson">Whether to generate JSON export for CI/CD integration</param>
        /// <param name="exportXml">Whether to generate XML export for external tools</param>
        public async Task AnalyzeProject(string unityProjectPath, string outputFolderPath, bool exportJson = false, bool exportXml = false)
        {
            // Track total analysis time for performance monitoring
            using var totalTimer = new PerformanceTimer("Unity Project Analysis");
            
            Console.WriteLine("Unity Project Analyzer v2.0");
            Console.WriteLine("================================");
            
            // Discover all Unity scene files in the project's Assets directory
            // Unity scenes are stored as .unity files and contain the serialized game world data
            var sceneFiles = Directory.GetFiles(Path.Combine(unityProjectPath, "Assets"), "*.unity", SearchOption.AllDirectories);
            ProgressBar.ShowInfo($"Found {sceneFiles.Length} scene files");
            
            // Discover all C# script files that could potentially be MonoBehaviour components
            var scriptFiles = Directory.GetFiles(Path.Combine(unityProjectPath, "Assets"), "*.cs", SearchOption.AllDirectories);
            ProgressBar.ShowInfo($"Found {scriptFiles.Length} script files");

            // Prepare collections to store analysis results for reporting
            var sceneResults = new List<SceneAnalysisResult>();
            var processedScenes = 0;

            // Process all scenes in parallel for better performance with large projects
            // Each scene is parsed independently to extract hierarchy and component information
            var sceneParsingTasks = sceneFiles.Select(async sceneFile =>
            {
                using var sceneTimer = new PerformanceTimer($"Scene: {Path.GetFileNameWithoutExtension(sceneFile)}");
                
                // Extract readable hierarchy from Unity's YAML scene format
                var hierarchy = await _sceneParser.ParseSceneHierarchy(sceneFile);
                var sceneName = Path.GetFileNameWithoutExtension(sceneFile);
                var outputFile = Path.Combine(outputFolderPath, $"{sceneName}.unity.dump");
                await File.WriteAllTextAsync(outputFile, hierarchy);
                
                // Collect detailed scene information for comprehensive HTML reporting
                var gameObjects = await _sceneParser.GetGameObjectsForReport(sceneFile);
                lock (sceneResults)  // Thread-safe access to shared collection
                {
                    sceneResults.Add(new SceneAnalysisResult
                    {
                        SceneName = $"{sceneName}.unity",
                        GameObjects = gameObjects
                    });
                    
                    // Update progress bar with thread-safe counter
                    processedScenes++;
                    ProgressBar.Show(processedScenes, sceneFiles.Length, "Processing scenes");
                }
                
                return sceneFile;
            });

            // Parse scripts, components, and missing references in parallel
            using (new PerformanceTimer("Script and Component Analysis"))
            {
                var scriptParsingTask = _scriptAnalyzer.AnalyzeScripts(scriptFiles);
                var componentAnalysisTask = _componentAnalyzer.AnalyzeComponents(sceneFiles);
                var missingRefTask = _missingReferenceDetector.DetectMissingReferences(sceneFiles, unityProjectPath);

                // Wait for scene parsing to complete
                await Task.WhenAll(sceneParsingTasks);
                
                // Get script analysis results
                var scriptAnalysisResult = await scriptParsingTask;
                ProgressBar.ShowSuccess($"Analyzed {scriptAnalysisResult.Scripts.Count} scripts");
                
                // Get component analysis results
                var componentAnalysisResult = await componentAnalysisTask;
                ProgressBar.ShowSuccess($"Analyzed {componentAnalysisResult.AllComponents.Count} component types");
                
                // Get missing reference analysis results
                var missingRefResult = await missingRefTask;
                if (missingRefResult.TotalBrokenReferences > 0)
                {
                    ProgressBar.ShowWarning($"Found {missingRefResult.TotalBrokenReferences} broken references in {missingRefResult.AffectedScenes} scenes");
                }
                else
                {
                    ProgressBar.ShowSuccess("No missing references found - all assets are properly linked");
                }

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

                // Generate project health metrics
                var healthMetrics = await _projectHealthAnalyzer.AnalyzeProjectHealth(
                    sceneResults, scriptAnalysisResult.Scripts, unusedScripts, 
                    componentAnalysisResult, missingRefResult, unityProjectPath);
                
                ProgressBar.ShowInfo($"Project Health Grade: {healthMetrics.HealthGrade} ({healthMetrics.OverallHealthScore:F1}/100)");
                
                if (healthMetrics.Recommendations.Any())
                {
                    ProgressBar.ShowInfo($"Generated {healthMetrics.Recommendations.Count} improvement recommendations");
                }

                // Generate interactive HTML report with full analysis results
                using (new PerformanceTimer("HTML Report Generation"))
                {
                    var htmlGenerator = new HtmlReportGenerator(sceneResults, unusedScripts, componentAnalysisResult, missingRefResult, healthMetrics, unityProjectPath);
                    await htmlGenerator.GenerateReportAsync(outputFolderPath);
                }

                // Export to JSON/XML if requested
                if (exportJson || exportXml)
                {
                    using (new PerformanceTimer("Export Generation"))
                    {
                        if (exportJson)
                        {
                            await _exportManager.ExportAnalysisResults(
                                sceneResults, unusedScripts, componentAnalysisResult, 
                                missingRefResult, healthMetrics, outputFolderPath, ExportFormat.Json);
                        }

                        if (exportXml)
                        {
                            await _exportManager.ExportAnalysisResults(
                                sceneResults, unusedScripts, componentAnalysisResult, 
                                missingRefResult, healthMetrics, outputFolderPath, ExportFormat.Xml);
                        }
                    }
                }
            }
            
            Console.WriteLine();
            ProgressBar.ShowSuccess("Analysis complete");
            Console.WriteLine("Check 'UnityProjectReport.html' for an interactive overview");
            Console.WriteLine("Text reports available in the output folder");
            if (exportJson || exportXml)
            {
                Console.WriteLine("Structured data exports generated for CI/CD integration");
            }
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