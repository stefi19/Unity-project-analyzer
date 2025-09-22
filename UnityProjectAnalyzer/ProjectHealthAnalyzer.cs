using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Comprehensive project health analyzer that evaluates Unity projects across multiple dimensions
    /// to provide actionable insights and an overall health grade (A-F scale).
    /// Analyzes scene complexity, script usage patterns, component distribution, and asset quality.
    /// </summary>
    public class ProjectHealthAnalyzer
    {
        /// <summary>
        /// Performs comprehensive health analysis of a Unity project across multiple quality dimensions.
        /// Generates an overall health score and actionable recommendations for improvement.
        /// </summary>
        /// <param name="sceneResults">Analyzed scene data with GameObject hierarchies</param>
        /// <param name="allScripts">Complete list of all C# scripts in the project</param>
        /// <param name="unusedScripts">Scripts that are not referenced by any scene</param>
        /// <param name="componentAnalysis">Component usage statistics and categorization</param>
        /// <param name="missingReferences">Broken reference detection results</param>
        /// <param name="projectPath">Path to the Unity project root</param>
        /// <returns>Comprehensive health metrics with grading and recommendations</returns>
        public async Task<ProjectHealthMetrics> AnalyzeProjectHealth(
            List<SceneAnalysisResult> sceneResults, 
            List<ScriptInfo> allScripts, 
            List<ScriptInfo> unusedScripts,
            ComponentAnalysisResult componentAnalysis,
            MissingReferenceResult missingReferences,
            string projectPath)
        {
            var metrics = new ProjectHealthMetrics();

            // Analyze scene structure and complexity to identify potential performance issues
            await AnalyzeSceneComplexity(sceneResults, metrics);

            // Evaluate script organization and usage efficiency
            await AnalyzeScriptUsagePatterns(allScripts, unusedScripts, metrics);

            // Assess component usage patterns and potential optimization opportunities
            await AnalyzeComponentDistribution(componentAnalysis, metrics);

            // Review project folder organization and naming conventions
            await AnalyzeProjectOrganization(projectPath, metrics);

            // Check asset integrity and reference consistency
            await AnalyzeAssetQuality(missingReferences, metrics);

            // Combine all metrics into a comprehensive health score and grade
            CalculateOverallHealthScore(metrics);

            return metrics;
        }

        private async Task AnalyzeSceneComplexity(List<SceneAnalysisResult> sceneResults, ProjectHealthMetrics metrics)
        {
            var sceneMetrics = new List<SceneComplexityInfo>();

            foreach (var scene in sceneResults)
            {
                var sceneInfo = new SceneComplexityInfo
                {
                    SceneName = scene.SceneName,
                    GameObjectCount = scene.GameObjects.Count,
                    MaxDepth = scene.GameObjects.Any() ? scene.GameObjects.Max(go => go.Depth) : 0,
                    AvgDepth = scene.GameObjects.Any() ? scene.GameObjects.Average(go => go.Depth) : 0,
                    RootObjectCount = scene.GameObjects.Count(go => go.Depth == 0)
                };

                // Calculate complexity score (0-100)
                sceneInfo.ComplexityScore = CalculateSceneComplexityScore(sceneInfo);
                sceneMetrics.Add(sceneInfo);
            }

            metrics.SceneComplexity = sceneMetrics;
            metrics.AverageSceneComplexity = sceneMetrics.Any() ? sceneMetrics.Average(s => s.ComplexityScore) : 0;
            metrics.MostComplexScene = sceneMetrics.OrderByDescending(s => s.ComplexityScore).FirstOrDefault()?.SceneName ?? "None";
        }

        private double CalculateSceneComplexityScore(SceneComplexityInfo sceneInfo)
        {
            double score = 0;

            // Base score from object count (0-40 points)
            score += Math.Min(sceneInfo.GameObjectCount / 100.0 * 40, 40);

            // Hierarchy depth penalty (0-30 points)
            if (sceneInfo.MaxDepth > 10)
                score += Math.Min((sceneInfo.MaxDepth - 10) * 3, 30);

            // Average depth consideration (0-20 points)
            if (sceneInfo.AvgDepth > 3)
                score += Math.Min((sceneInfo.AvgDepth - 3) * 5, 20);

            // Root object organization (0-10 points)
            if (sceneInfo.RootObjectCount > 20)
                score += Math.Min((sceneInfo.RootObjectCount - 20) * 0.5, 10);

            return Math.Min(score, 100);
        }

        private async Task AnalyzeScriptUsagePatterns(List<ScriptInfo> allScripts, List<ScriptInfo> unusedScripts, ProjectHealthMetrics metrics)
        {
            var usedScripts = allScripts.Where(s => !unusedScripts.Contains(s)).ToList();

            metrics.TotalScripts = allScripts.Count;
            metrics.UsedScripts = usedScripts.Count;
            metrics.UnusedScripts = unusedScripts.Count;
            metrics.ScriptUtilizationRate = allScripts.Count > 0 ? (usedScripts.Count / (double)allScripts.Count) * 100 : 100;

            // Analyze script distribution by folder
            var folderDistribution = new Dictionary<string, int>();
            foreach (var script in allScripts)
            {
                var folder = Path.GetDirectoryName(script.RelativePath) ?? "Root";
                folder = folder.Replace("\\", "/");
                folderDistribution[folder] = folderDistribution.GetValueOrDefault(folder, 0) + 1;
            }

            metrics.ScriptFolderDistribution = folderDistribution.OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Analyze MonoBehaviour vs other scripts
            var monoBehaviours = allScripts.Where(s => s.SerializedFields.Any()).ToList();
            metrics.MonoBehaviourScripts = monoBehaviours.Count;
            metrics.UtilityScripts = allScripts.Count - monoBehaviours.Count;
        }

        private async Task AnalyzeComponentDistribution(ComponentAnalysisResult componentAnalysis, ProjectHealthMetrics metrics)
        {
            metrics.TotalComponents = componentAnalysis.AllComponents.Sum(c => c.UsageCount);
            metrics.UniqueComponentTypes = componentAnalysis.AllComponents.Count;

            // Most used components
            metrics.MostUsedComponents = componentAnalysis.AllComponents
                .OrderByDescending(c => c.UsageCount)
                .Take(10)
                .ToDictionary(c => c.ComponentType, c => c.UsageCount);

            // Component category distribution
            metrics.ComponentCategoryDistribution = componentAnalysis.ComponentCategories
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Sum(c => c.UsageCount));

            // Performance impact analysis
            var heavyComponents = new[] { "ParticleSystem", "MeshRenderer", "SkinnedMeshRenderer", "Camera", "Light" };
            metrics.PerformanceImpactComponents = componentAnalysis.AllComponents
                .Where(c => heavyComponents.Contains(c.ComponentType))
                .Sum(c => c.UsageCount);
        }

        private async Task AnalyzeProjectOrganization(string projectPath, ProjectHealthMetrics metrics)
        {
            var assetsPath = Path.Combine(projectPath, "Assets");
            if (!Directory.Exists(assetsPath))
            {
                metrics.ProjectOrganizationScore = 0;
                return;
            }

            var directories = Directory.GetDirectories(assetsPath, "*", SearchOption.TopDirectoryOnly);
            var standardFolders = new[] { "Scripts", "Scenes", "Prefabs", "Materials", "Textures", "Audio", "Models", "Animations" };
            
            var organizationScore = 0.0;
            var foundStandardFolders = 0;

            foreach (var standardFolder in standardFolders)
            {
                if (directories.Any(d => Path.GetFileName(d).Equals(standardFolder, StringComparison.OrdinalIgnoreCase)))
                {
                    foundStandardFolders++;
                }
            }

            // Base score from standard folder structure
            organizationScore = (foundStandardFolders / (double)standardFolders.Length) * 60;

            // Bonus for not having too many root folders (max 40 points)
            var rootFolderCount = directories.Length;
            if (rootFolderCount <= 10)
                organizationScore += 40 - (rootFolderCount * 2);

            metrics.ProjectOrganizationScore = Math.Min(organizationScore, 100);
            metrics.RootFolderCount = rootFolderCount;
        }

        private async Task AnalyzeAssetQuality(MissingReferenceResult missingReferences, ProjectHealthMetrics metrics)
        {
            metrics.BrokenReferences = missingReferences.TotalBrokenReferences;
            metrics.ScenesWithIssues = missingReferences.AffectedScenes;

            // Asset quality score (higher is better)
            if (missingReferences.TotalBrokenReferences == 0)
            {
                metrics.AssetQualityScore = 100;
            }
            else
            {
                // Deduct points based on number of broken references
                var penalty = Math.Min(missingReferences.TotalBrokenReferences * 5, 80);
                metrics.AssetQualityScore = Math.Max(100 - penalty, 20);
            }
        }

        private void CalculateOverallHealthScore(ProjectHealthMetrics metrics)
        {
            var scores = new[]
            {
                (100 - metrics.AverageSceneComplexity) * 0.25, // Lower complexity is better
                metrics.ScriptUtilizationRate * 0.25,         // Higher utilization is better
                metrics.ProjectOrganizationScore * 0.25,      // Better organization is better
                metrics.AssetQualityScore * 0.25              // Higher quality is better
            };

            metrics.OverallHealthScore = scores.Sum();

            // Determine health grade
            metrics.HealthGrade = metrics.OverallHealthScore switch
            {
                >= 90 => "A",
                >= 80 => "B",
                >= 70 => "C",
                >= 60 => "D",
                _ => "F"
            };

            // Generate recommendations
            GenerateRecommendations(metrics);
        }

        private void GenerateRecommendations(ProjectHealthMetrics metrics)
        {
            var recommendations = new List<string>();

            if (metrics.AverageSceneComplexity > 70)
            {
                recommendations.Add("Consider simplifying complex scenes by breaking them into smaller, more manageable pieces or using prefabs to reduce hierarchy depth.");
            }

            if (metrics.ScriptUtilizationRate < 80)
            {
                recommendations.Add($"Remove {metrics.UnusedScripts} unused scripts to improve project maintainability and reduce build size.");
            }

            if (metrics.ProjectOrganizationScore < 70)
            {
                recommendations.Add("Improve project organization by creating standard folders (Scripts, Scenes, Prefabs, Materials, etc.) and organizing assets appropriately.");
            }

            if (metrics.BrokenReferences > 0)
            {
                recommendations.Add($"Fix {metrics.BrokenReferences} broken asset references to prevent runtime errors and improve project stability.");
            }

            if (metrics.PerformanceImpactComponents > 50)
            {
                recommendations.Add("Review performance-heavy components (Cameras, Lights, Particle Systems) and optimize their usage for better runtime performance.");
            }

            if (metrics.RootFolderCount > 15)
            {
                recommendations.Add("Consider consolidating root-level folders to improve project navigation and organization.");
            }

            metrics.Recommendations = recommendations;
        }
    }

    public class ProjectHealthMetrics
    {
        // Scene Complexity
        public List<SceneComplexityInfo> SceneComplexity { get; set; } = new();
        public double AverageSceneComplexity { get; set; }
        public string MostComplexScene { get; set; } = string.Empty;

        // Script Usage
        public int TotalScripts { get; set; }
        public int UsedScripts { get; set; }
        public int UnusedScripts { get; set; }
        public double ScriptUtilizationRate { get; set; }
        public Dictionary<string, int> ScriptFolderDistribution { get; set; } = new();
        public int MonoBehaviourScripts { get; set; }
        public int UtilityScripts { get; set; }

        // Component Distribution
        public int TotalComponents { get; set; }
        public int UniqueComponentTypes { get; set; }
        public Dictionary<string, int> MostUsedComponents { get; set; } = new();
        public Dictionary<string, int> ComponentCategoryDistribution { get; set; } = new();
        public int PerformanceImpactComponents { get; set; }

        // Project Organization
        public double ProjectOrganizationScore { get; set; }
        public int RootFolderCount { get; set; }

        // Asset Quality
        public int BrokenReferences { get; set; }
        public int ScenesWithIssues { get; set; }
        public double AssetQualityScore { get; set; }

        // Overall Health
        public double OverallHealthScore { get; set; }
        public string HealthGrade { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }

    public class SceneComplexityInfo
    {
        public string SceneName { get; set; } = string.Empty;
        public int GameObjectCount { get; set; }
        public int MaxDepth { get; set; }
        public double AvgDepth { get; set; }
        public int RootObjectCount { get; set; }
        public double ComplexityScore { get; set; }
    }
}