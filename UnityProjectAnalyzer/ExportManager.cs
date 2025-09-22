using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Handles exporting Unity project analysis results to structured formats (JSON/XML)
    /// for integration with CI/CD pipelines, external tools, and automated reporting systems.
    /// Provides comprehensive data serialization with proper handling of complex types.
    /// </summary>
    public class ExportManager
    {
        /// <summary>
        /// Exports complete analysis results to the specified format with comprehensive data transformation.
        /// Converts all analysis results into serializable data structures suitable for external consumption.
        /// </summary>
        /// <param name="sceneResults">Scene hierarchy and GameObject analysis data</param>
        /// <param name="unusedScripts">List of unused MonoBehaviour scripts with metadata</param>
        /// <param name="componentAnalysis">Component usage statistics and categorization</param>
        /// <param name="missingReferences">Broken reference detection results</param>
        /// <param name="healthMetrics">Project health assessment and grading</param>
        /// <param name="outputPath">Directory where export files will be generated</param>
        /// <param name="format">Target export format (JSON or XML)</param>
        public async Task ExportAnalysisResults(
            List<SceneAnalysisResult> sceneResults,
            List<ScriptInfo> unusedScripts,
            ComponentAnalysisResult componentAnalysis,
            MissingReferenceResult missingReferences,
            ProjectHealthMetrics healthMetrics,
            string outputPath,
            ExportFormat format)
        {
            // Transform all analysis results into a unified data structure for export
            // This consolidates data from multiple analyzers into a single serializable object
            var analysisData = new UnityProjectAnalysisData
            {
                AnalysisDate = DateTime.Now,
                ProjectPath = Path.GetDirectoryName(outputPath) ?? "",
                Summary = new AnalysisSummary
                {
                    // Calculate high-level project statistics for quick overview
                    TotalScenes = sceneResults.Count,
                    TotalGameObjects = sceneResults.Sum(s => s.GameObjects.Count),
                    TotalComponents = componentAnalysis.AllComponents.Sum(c => c.UsageCount),
                    UnusedScriptsCount = unusedScripts.Count,
                    BrokenReferencesCount = missingReferences.TotalBrokenReferences,
                    HealthGrade = healthMetrics.HealthGrade,
                    HealthScore = healthMetrics.OverallHealthScore
                },
                // Transform complex analysis objects into serializable formats
                Scenes = sceneResults.Select(ConvertSceneResult).ToList(),
                UnusedScripts = unusedScripts.Select(ConvertScriptInfo).ToList(),
                Components = ConvertComponentAnalysis(componentAnalysis),
                MissingReferences = ConvertMissingReferences(missingReferences),
                HealthMetrics = ConvertHealthMetrics(healthMetrics)
            };

            // Route to appropriate export method based on requested format
            switch (format)
            {
                case ExportFormat.Json:
                    await ExportToJson(analysisData, outputPath);
                    break;
                case ExportFormat.Xml:
                    await ExportToXml(analysisData, outputPath);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }
        }

        private async Task ExportToJson(UnityProjectAnalysisData data, string outputPath)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                Converters = { new JsonStringEnumConverter() }
            };

            var jsonPath = Path.Combine(outputPath, "UnityProjectAnalysis.json");
            var jsonString = JsonSerializer.Serialize(data, options);
            await File.WriteAllTextAsync(jsonPath, jsonString);
            
            Console.WriteLine($"JSON export generated: {jsonPath}");
        }

        private async Task ExportToXml(UnityProjectAnalysisData data, string outputPath)
        {
            var xmlPath = Path.Combine(outputPath, "UnityProjectAnalysis.xml");
            var serializer = new XmlSerializer(typeof(UnityProjectAnalysisData));
            
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\n",
                Async = true
            };

            using (var writer = XmlWriter.Create(xmlPath, settings))
            {
                serializer.Serialize(writer, data);
            }
            
            Console.WriteLine($"XML export generated: {xmlPath}");
        }

        private SceneData ConvertSceneResult(SceneAnalysisResult scene)
        {
            return new SceneData
            {
                Name = scene.SceneName,
                GameObjectCount = scene.GameObjects.Count,
                GameObjects = scene.GameObjects.Select(go => new GameObjectData
                {
                    Name = go.Name,
                    Depth = go.Depth
                }).ToList()
            };
        }

        private ScriptData ConvertScriptInfo(ScriptInfo script)
        {
            return new ScriptData
            {
                ClassName = script.ClassName,
                FilePath = script.FilePath,
                RelativePath = script.RelativePath,
                SerializedFields = script.SerializedFields.Select(field => new SerializedFieldData
                {
                    Name = field.Name,
                    Type = field.Type
                }).ToList()
            };
        }

        private ComponentAnalysisData ConvertComponentAnalysis(ComponentAnalysisResult analysis)
        {
            return new ComponentAnalysisData
            {
                TotalComponents = analysis.AllComponents.Sum(c => c.UsageCount),
                UniqueComponentTypes = analysis.AllComponents.Count,
                Components = analysis.AllComponents.Select(c => new ComponentData
                {
                    ComponentType = c.ComponentType,
                    Category = c.Category,
                    UsageCount = c.UsageCount,
                    ScenesUsed = c.ScenesUsed.ToList()
                }).ToList(),
                Categories = analysis.ComponentCategories.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Sum(c => c.UsageCount)
                )
            };
        }

        private MissingReferenceData ConvertMissingReferences(MissingReferenceResult references)
        {
            return new MissingReferenceData
            {
                TotalBrokenReferences = references.TotalBrokenReferences,
                AffectedScenes = references.AffectedScenes,
                Issues = references.SceneIssues.SelectMany(kvp =>
                    kvp.Value.Select(issue => new MissingReferenceIssueData
                    {
                        SceneName = issue.SceneName,
                        GameObjectName = issue.GameObjectName,
                        AssetType = issue.AssetType,
                        ReferenceType = issue.ReferenceType,
                        MissingGuid = issue.MissingGuid,
                        Description = issue.Description
                    })
                ).ToList(),
                AssetTypeBreakdown = references.MissingAssetTypes
            };
        }

        private HealthMetricsData ConvertHealthMetrics(ProjectHealthMetrics metrics)
        {
            return new HealthMetricsData
            {
                OverallHealthScore = metrics.OverallHealthScore,
                HealthGrade = metrics.HealthGrade,
                ScriptUtilizationRate = metrics.ScriptUtilizationRate,
                AverageSceneComplexity = metrics.AverageSceneComplexity,
                ProjectOrganizationScore = metrics.ProjectOrganizationScore,
                AssetQualityScore = metrics.AssetQualityScore,
                Recommendations = metrics.Recommendations,
                SceneComplexity = metrics.SceneComplexity.Select(sc => new SceneComplexityData
                {
                    SceneName = sc.SceneName,
                    GameObjectCount = sc.GameObjectCount,
                    MaxDepth = sc.MaxDepth,
                    AverageDepth = sc.AvgDepth,
                    RootObjectCount = sc.RootObjectCount,
                    ComplexityScore = sc.ComplexityScore
                }).ToList(),
                ComponentStats = new ComponentStatsData
                {
                    TotalComponents = metrics.TotalComponents,
                    UniqueComponentTypes = metrics.UniqueComponentTypes,
                    PerformanceImpactComponents = metrics.PerformanceImpactComponents,
                    MostUsedComponents = metrics.MostUsedComponents
                }
            };
        }
    }

    public enum ExportFormat
    {
        Json,
        Xml
    }

    // Data transfer objects for serialization
    [Serializable]
    public class UnityProjectAnalysisData
    {
        public DateTime AnalysisDate { get; set; }
        public string ProjectPath { get; set; } = string.Empty;
        public AnalysisSummary Summary { get; set; } = new();
        public List<SceneData> Scenes { get; set; } = new();
        public List<ScriptData> UnusedScripts { get; set; } = new();
        public ComponentAnalysisData Components { get; set; } = new();
        public MissingReferenceData MissingReferences { get; set; } = new();
        public HealthMetricsData HealthMetrics { get; set; } = new();
    }

    [Serializable]
    public class AnalysisSummary
    {
        public int TotalScenes { get; set; }
        public int TotalGameObjects { get; set; }
        public int TotalComponents { get; set; }
        public int UnusedScriptsCount { get; set; }
        public int BrokenReferencesCount { get; set; }
        public string HealthGrade { get; set; } = string.Empty;
        public double HealthScore { get; set; }
    }

    [Serializable]
    public class SceneData
    {
        public string Name { get; set; } = string.Empty;
        public int GameObjectCount { get; set; }
        public List<GameObjectData> GameObjects { get; set; } = new();
    }

    [Serializable]
    public class GameObjectData
    {
        public string Name { get; set; } = string.Empty;
        public int Depth { get; set; }
    }

    [Serializable]
    public class ScriptData
    {
        public string ClassName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public List<SerializedFieldData> SerializedFields { get; set; } = new();
    }

    [Serializable]
    public class SerializedFieldData
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }

    [Serializable]
    public class ComponentAnalysisData
    {
        public int TotalComponents { get; set; }
        public int UniqueComponentTypes { get; set; }
        public List<ComponentData> Components { get; set; } = new();
        
        [XmlIgnore]
        public Dictionary<string, int> Categories { get; set; } = new();
        
        // XML-serializable version of Categories
        [XmlArray("Categories")]
        [XmlArrayItem("Category")]
        public CategoryData[] CategoriesArray
        {
            get => Categories?.Select(kvp => new CategoryData { Name = kvp.Key, Count = kvp.Value }).ToArray() ?? Array.Empty<CategoryData>();
            set => Categories = value?.ToDictionary(c => c.Name, c => c.Count) ?? new Dictionary<string, int>();
        }
    }

    [Serializable]
    public class ComponentData
    {
        public string ComponentType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public List<string> ScenesUsed { get; set; } = new();
    }

    [Serializable]
    public class MissingReferenceData
    {
        public int TotalBrokenReferences { get; set; }
        public int AffectedScenes { get; set; }
        public List<MissingReferenceIssueData> Issues { get; set; } = new();
        
        [XmlIgnore]
        public Dictionary<string, int> AssetTypeBreakdown { get; set; } = new();
        
        // XML-serializable version of AssetTypeBreakdown
        [XmlArray("AssetTypeBreakdown")]
        [XmlArrayItem("AssetType")]
        public AssetTypeData[] AssetTypeBreakdownArray
        {
            get => AssetTypeBreakdown?.Select(kvp => new AssetTypeData { Type = kvp.Key, Count = kvp.Value }).ToArray() ?? Array.Empty<AssetTypeData>();
            set => AssetTypeBreakdown = value?.ToDictionary(a => a.Type, a => a.Count) ?? new Dictionary<string, int>();
        }
    }

    [Serializable]
    public class MissingReferenceIssueData
    {
        public string SceneName { get; set; } = string.Empty;
        public string GameObjectName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public string MissingGuid { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    [Serializable]
    public class HealthMetricsData
    {
        public double OverallHealthScore { get; set; }
        public string HealthGrade { get; set; } = string.Empty;
        public double ScriptUtilizationRate { get; set; }
        public double AverageSceneComplexity { get; set; }
        public double ProjectOrganizationScore { get; set; }
        public double AssetQualityScore { get; set; }
        public List<string> Recommendations { get; set; } = new();
        public List<SceneComplexityData> SceneComplexity { get; set; } = new();
        public ComponentStatsData ComponentStats { get; set; } = new();
    }

    [Serializable]
    public class SceneComplexityData
    {
        public string SceneName { get; set; } = string.Empty;
        public int GameObjectCount { get; set; }
        public int MaxDepth { get; set; }
        public double AverageDepth { get; set; }
        public int RootObjectCount { get; set; }
        public double ComplexityScore { get; set; }
    }

    [Serializable]
    public class ComponentStatsData
    {
        public int TotalComponents { get; set; }
        public int UniqueComponentTypes { get; set; }
        public int PerformanceImpactComponents { get; set; }
        
        [XmlIgnore]
        public Dictionary<string, int> MostUsedComponents { get; set; } = new();
        
        // XML-serializable version of MostUsedComponents
        [XmlArray("MostUsedComponents")]
        [XmlArrayItem("Component")]
        public ComponentUsageData[] MostUsedComponentsArray
        {
            get => MostUsedComponents?.Select(kvp => new ComponentUsageData { Type = kvp.Key, Count = kvp.Value }).ToArray() ?? Array.Empty<ComponentUsageData>();
            set => MostUsedComponents = value?.ToDictionary(c => c.Type, c => c.Count) ?? new Dictionary<string, int>();
        }
    }

    // Helper classes for XML serialization
    [Serializable]
    public class CategoryData
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [Serializable]
    public class AssetTypeData
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [Serializable]
    public class ComponentUsageData
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}