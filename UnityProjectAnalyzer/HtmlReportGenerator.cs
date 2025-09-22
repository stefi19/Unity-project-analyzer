using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnityProjectAnalyzer
{
    public class HtmlReportGenerator
    {
        private readonly List<SceneAnalysisResult> _sceneResults;
        private readonly List<ScriptInfo> _unusedScripts;
        private readonly ComponentAnalysisResult _componentAnalysis;
        private readonly MissingReferenceResult _missingReferences;
        private readonly ProjectHealthMetrics _healthMetrics;
        private readonly string _projectPath;
        private readonly DateTime _analysisTime;

        public HtmlReportGenerator(List<SceneAnalysisResult> sceneResults, 
                                 List<ScriptInfo> unusedScripts,
                                 ComponentAnalysisResult componentAnalysis,
                                 MissingReferenceResult missingReferences,
                                 ProjectHealthMetrics healthMetrics,
                                 string projectPath)
        {
            _sceneResults = sceneResults;
            _unusedScripts = unusedScripts;
            _componentAnalysis = componentAnalysis;
            _missingReferences = missingReferences;
            _healthMetrics = healthMetrics;
            _projectPath = projectPath;
            _analysisTime = DateTime.Now;
        }

        public async Task GenerateReportAsync(string outputPath)
        {
            var reportPath = Path.Combine(outputPath, "UnityProjectReport.html");
            var htmlContent = GenerateHtmlContent();
            
            await File.WriteAllTextAsync(reportPath, htmlContent);
            Console.WriteLine($"Interactive HTML report generated: {reportPath}");
        }

        private string GenerateHtmlContent()
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html lang=\"en\">");
            html.AppendLine(GenerateHead());
            html.AppendLine("<body>");
            html.AppendLine(GenerateHeader());
            html.AppendLine(GenerateOverviewSection());
            html.AppendLine(GenerateProjectHealthSection());
            html.AppendLine(GenerateComponentsSection());
            html.AppendLine(GenerateMissingReferencesSection());
            html.AppendLine(GenerateScenesSection());
            html.AppendLine(GenerateUnusedScriptsSection());  
            html.AppendLine(GenerateScripts());
            html.AppendLine("</body>");
            html.AppendLine("</html>");
            
            return html.ToString();
        }

        private string GenerateHead()
        {
            return @"
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Unity Project Analysis Report</title>
    <style>
        * {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }
        
        body {
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
        }
        
        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }
        
        .header {
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 30px;
            margin-bottom: 30px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
            text-align: center;
        }
        
        .header h1 {
            color: #2c3e50;
            font-size: 2.5em;
            margin-bottom: 10px;
            background: linear-gradient(45deg, #667eea, #764ba2);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            background-clip: text;
        }
        
        .header .subtitle {
            color: #7f8c8d;
            font-size: 1.2em;
        }
        
        .section {
            background: rgba(255, 255, 255, 0.95);
            backdrop-filter: blur(10px);
            border-radius: 15px;
            padding: 25px;
            margin-bottom: 25px;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.1);
        }
        
        .section h2 {
            color: #2c3e50;
            margin-bottom: 20px;
            font-size: 1.8em;
            border-bottom: 3px solid #667eea;
            padding-bottom: 10px;
        }
        
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            margin-bottom: 25px;
        }
        
        .stat-card {
            background: linear-gradient(135deg, #667eea, #764ba2);
            color: white;
            padding: 20px;
            border-radius: 10px;
            text-align: center;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.2);
        }
        
        .stat-value {
            font-size: 2.5em;
            font-weight: bold;
            margin-bottom: 5px;
        }
        
        .stat-label {
            font-size: 0.95em;
            opacity: 0.9;
        }
        
        .scene-item {
            border: 1px solid #ddd;
            border-radius: 8px;
            margin-bottom: 15px;
            overflow: hidden;
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
        }
        
        .scene-header {
            background: linear-gradient(135deg, #f8f9fa, #e9ecef);
            padding: 15px 20px;
            cursor: pointer;
            border-bottom: 1px solid #ddd;
            transition: background-color 0.3s;
        }
        
        .scene-header:hover {
            background: linear-gradient(135deg, #e9ecef, #dee2e6);
        }
        
        .scene-header h3 {
            margin: 0;
            color: #495057;
            display: flex;
            align-items: center;
            justify-content: space-between;
        }
        
        .scene-content {
            padding: 20px;
            background: #f8f9fa;
            display: none;
        }
        
        .scene-content.active {
            display: block;
        }
        
        .hierarchy {
            font-family: 'Courier New', monospace;
            background: #2d3748;
            color: #e2e8f0;
            padding: 20px;
            border-radius: 8px;
            overflow-x: auto;
            line-height: 1.4;
        }
        
        .hierarchy .game-object {
            padding: 2px 0;
            border-left: 2px solid transparent;
        }
        
        .hierarchy .game-object:hover {
            background: rgba(255, 255, 255, 0.1);
            border-left: 2px solid #667eea;
        }
        
        .toggle-icon {
            transition: transform 0.3s;
        }
        
        .toggle-icon.rotated {
            transform: rotate(90deg);
        }
        
        .unused-scripts-list {
            max-height: 400px;
            overflow-y: auto;
            border: 1px solid #ddd;
            border-radius: 8px;
        }
        
        .script-item {
            padding: 12px 15px;
            border-bottom: 1px solid #eee;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }
        
        .script-item:last-child {
            border-bottom: none;
        }
        
        .script-item:hover {
            background: #f8f9fa;
        }
        
        .script-path {
            font-family: 'Courier New', monospace;
            color: #495057;
        }
        
        .script-guid {
            font-family: 'Courier New', monospace;
            color: #6c757d;
            font-size: 0.85em;
        }
        
        .search-box {
            width: 100%;
            padding: 12px;
            border: 2px solid #ddd;
            border-radius: 8px;
            font-size: 16px;
            margin-bottom: 20px;
            transition: border-color 0.3s;
        }
        
        .search-box:focus {
            outline: none;
            border-color: #667eea;
        }
        
        .no-results {
            text-align: center;
            color: #6c757d;
            font-style: italic;
            padding: 20px;
        }
        
        @media (max-width: 768px) {
            .container {
                padding: 10px;
            }
            
            .header h1 {
                font-size: 2em;
            }
            
            .stats-grid {
                grid-template-columns: 1fr;
            }
        }
    </style>
</head>";
        }

        private string GenerateHeader()
        {
            return $@"
<div class=""container"">
    <div class=""header"">
        <h1>Unity Project Analysis Report</h1>
        <div class=""subtitle"">
            <strong>Project:</strong> {Path.GetFileName(_projectPath)}<br>
            <strong>Analysis Date:</strong> {_analysisTime:yyyy-MM-dd HH:mm:ss}<br>
            <strong>Generated by:</strong> Unity Project Analyzer v2.0
        </div>
    </div>";
        }

        private string GenerateOverviewSection()
        {
            var totalGameObjects = _sceneResults.Sum(s => s.GameObjects.Count);
            var totalScenes = _sceneResults.Count;
            var totalUnusedScripts = _unusedScripts.Count;
            var avgObjectsPerScene = totalScenes > 0 ? totalGameObjects / (double)totalScenes : 0;

            return $@"
    <div class=""section"">
        <h2>Project Overview</h2>
        <div class=""stats-grid"">
            <div class=""stat-card"">
                <div class=""stat-value"">{totalScenes}</div>
                <div class=""stat-label"">Total Scenes</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{totalGameObjects}</div>
                <div class=""stat-label"">Total GameObjects</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{totalUnusedScripts}</div>
                <div class=""stat-label"">Unused Scripts</div>
            </div>
            <div class=""stat-card"">
                <div class=""stat-value"">{avgObjectsPerScene:F1}</div>
                <div class=""stat-label"">Avg Objects/Scene</div>
            </div>
        </div>
    </div>";
        }

        private string GenerateProjectHealthSection()
        {
            var html = new StringBuilder();
            html.AppendLine(@"    <div class=""section"">");
            html.AppendLine(@"        <h2>Project Health Analysis</h2>");
            
            // Overall health score with grade styling
            var gradeColor = _healthMetrics.HealthGrade switch
            {
                "A" => "#27ae60",
                "B" => "#2ecc71", 
                "C" => "#f39c12",
                "D" => "#e67e22",
                "F" => "#e74c3c",
                _ => "#95a5a6"
            };

            html.AppendLine($@"        <div style=""text-align: center; margin-bottom: 30px;"">");
            html.AppendLine($@"            <div style=""display: inline-block; background: {gradeColor}; color: white; padding: 20px 40px; border-radius: 50%; font-size: 3em; font-weight: bold; margin-bottom: 10px;"">");
            html.AppendLine($@"                {_healthMetrics.HealthGrade}");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div style=""font-size: 1.5em; color: #2c3e50; margin-bottom: 5px;"">Overall Health Score</div>");
            html.AppendLine($@"            <div style=""font-size: 2em; font-weight: bold; color: {gradeColor};"">{_healthMetrics.OverallHealthScore:F1}/100</div>");
            html.AppendLine($@"        </div>");

            // Health metrics grid
            html.AppendLine($@"        <div class=""stats-grid"" style=""margin-bottom: 30px;"">");
            html.AppendLine($@"            <div class=""stat-card"" style=""background: linear-gradient(135deg, #3498db, #2980b9);"">");
            html.AppendLine($@"                <div class=""stat-value"">{_healthMetrics.ScriptUtilizationRate:F1}%</div>");
            html.AppendLine($@"                <div class=""stat-label"">Script Utilization</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"" style=""background: linear-gradient(135deg, #9b59b6, #8e44ad);"">");
            html.AppendLine($@"                <div class=""stat-value"">{_healthMetrics.AverageSceneComplexity:F1}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Avg Scene Complexity</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"" style=""background: linear-gradient(135deg, #1abc9c, #16a085);"">");
            html.AppendLine($@"                <div class=""stat-value"">{_healthMetrics.ProjectOrganizationScore:F1}/100</div>");
            html.AppendLine($@"                <div class=""stat-label"">Organization Score</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"" style=""background: linear-gradient(135deg, #e67e22, #d35400);"">");
            html.AppendLine($@"                <div class=""stat-value"">{_healthMetrics.AssetQualityScore:F1}/100</div>");
            html.AppendLine($@"                <div class=""stat-label"">Asset Quality</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"        </div>");

            // Recommendations section
            if (_healthMetrics.Recommendations.Any())
            {
                html.AppendLine($@"        <div class=""scene-item"" style=""margin-bottom: 20px;"">");
                html.AppendLine($@"            <div class=""scene-header"" onclick=""toggleScene('recommendations')"">");
                html.AppendLine($@"                <h3 style=""color: #e67e22;"">");
                html.AppendLine($@"                    Improvement Recommendations");
                html.AppendLine($@"                    <span>");
                html.AppendLine($@"                        <small>({_healthMetrics.Recommendations.Count} items)</small>");
                html.AppendLine($@"                        <span class=""toggle-icon"" id=""icon_recommendations"">▶</span>");
                html.AppendLine($@"                    </span>");
                html.AppendLine($@"                </h3>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"            <div class=""scene-content"" id=""content_recommendations"">");
                html.AppendLine($@"                <div style=""display: grid; gap: 12px; margin-top: 15px;"">");
                
                for (int i = 0; i < _healthMetrics.Recommendations.Count; i++)
                {
                    var recommendation = _healthMetrics.Recommendations[i];
                    html.AppendLine($@"                    <div style=""padding: 12px 16px; background: rgba(230,126,34,0.1); border-radius: 8px; border-left: 4px solid #e67e22;"">");
                    html.AppendLine($@"                        <div style=""display: flex; align-items: flex-start; gap: 12px;"">");
                    html.AppendLine($@"                            <div style=""background: #e67e22; color: white; border-radius: 50%; width: 24px; height: 24px; display: flex; align-items: center; justify-content: center; font-size: 0.8em; font-weight: bold; flex-shrink: 0;"">{i + 1}</div>");
                    html.AppendLine($@"                            <div style=""color: #2c3e50; line-height: 1.5;"">{recommendation}</div>");
                    html.AppendLine($@"                        </div>");
                    html.AppendLine($@"                    </div>");
                }
                
                html.AppendLine($@"                </div>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"        </div>");
            }

            // Detailed metrics breakdown
            html.AppendLine($@"        <div class=""scene-item"" style=""margin-bottom: 20px;"">");
            html.AppendLine($@"            <div class=""scene-header"" onclick=""toggleScene('detailed_metrics')"">");
            html.AppendLine($@"                <h3>");
            html.AppendLine($@"                    Detailed Metrics");
            html.AppendLine($@"                    <span>");
            html.AppendLine($@"                        <small>(Scene Complexity, Script Distribution, Component Usage)</small>");
            html.AppendLine($@"                        <span class=""toggle-icon"" id=""icon_detailed_metrics"">▶</span>");
            html.AppendLine($@"                    </span>");
            html.AppendLine($@"                </h3>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""scene-content"" id=""content_detailed_metrics"">");
            html.AppendLine($@"                <div style=""margin-top: 15px;"">");
            
            // Scene complexity breakdown
            html.AppendLine($@"                    <h4 style=""margin-bottom: 15px; color: #2c3e50;"">Scene Complexity Analysis</h4>");
            html.AppendLine($@"                    <div style=""display: grid; gap: 8px; margin-bottom: 25px;"">");
            
            foreach (var scene in _healthMetrics.SceneComplexity.OrderByDescending(s => s.ComplexityScore))
            {
                var complexityColor = scene.ComplexityScore switch
                {
                    <= 30 => "#27ae60",
                    <= 60 => "#f39c12", 
                    _ => "#e74c3c"
                };
                
                html.AppendLine($@"                        <div style=""padding: 8px 12px; background: rgba(255,255,255,0.05); border-radius: 6px; border-left: 3px solid {complexityColor};"">");
                html.AppendLine($@"                            <div style=""display: flex; justify-content: space-between; align-items: center;"">");
                html.AppendLine($@"                                <div>");
                html.AppendLine($@"                                    <strong>{scene.SceneName}</strong>");
                html.AppendLine($@"                                    <div style=""color: #666; font-size: 0.9em;"">Objects: {scene.GameObjectCount}, Max Depth: {scene.MaxDepth}, Roots: {scene.RootObjectCount}</div>");
                html.AppendLine($@"                                </div>");
                html.AppendLine($@"                                <div style=""text-align: right;"">");
                html.AppendLine($@"                                    <div style=""color: {complexityColor}; font-weight: bold;"">{scene.ComplexityScore:F1}/100</div>");
                html.AppendLine($@"                                </div>");
                html.AppendLine($@"                            </div>");
                html.AppendLine($@"                        </div>");
            }
            
            html.AppendLine($@"                </div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"        </div>");
            
            html.AppendLine(@"    </div>");
            return html.ToString();
        }

        private string GenerateComponentsSection()
        {
            var html = new StringBuilder();
            html.AppendLine(@"    <div class=""section"">");
            html.AppendLine(@"        <h2>Component Analysis</h2>");
            
            // Component overview stats
            var totalComponents = _componentAnalysis.AllComponents.Sum(c => c.UsageCount);
            var totalComponentTypes = _componentAnalysis.AllComponents.Count;
            var categoriesCount = _componentAnalysis.ComponentCategories.Count;
            
            html.AppendLine($@"        <div class=""stats-grid"" style=""margin-bottom: 20px;"">");
            html.AppendLine($@"            <div class=""stat-card"">");
            html.AppendLine($@"                <div class=""stat-value"">{totalComponents}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Total Components</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"">");
            html.AppendLine($@"                <div class=""stat-value"">{totalComponentTypes}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Component Types</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"">");
            html.AppendLine($@"                <div class=""stat-value"">{categoriesCount}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Categories</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"        </div>");

            // Component categories
            foreach (var category in _componentAnalysis.ComponentCategories.OrderByDescending(c => c.Value.Sum(comp => comp.UsageCount)))
            {
                var categoryId = category.Key.Replace(" ", "_").ToLower();
                var categoryTotal = category.Value.Sum(c => c.UsageCount);
                
                html.AppendLine($@"        <div class=""scene-item"" style=""margin-bottom: 15px;"">");
                html.AppendLine($@"            <div class=""scene-header"" onclick=""toggleScene('{categoryId}_components')"">");
                html.AppendLine($@"                <h3>");
                html.AppendLine($@"                    {category.Key} Components");
                html.AppendLine($@"                    <span>");
                html.AppendLine($@"                        <small>({categoryTotal} total, {category.Value.Count} types)</small>");
                html.AppendLine($@"                        <span class=""toggle-icon"" id=""icon_{categoryId}_components"">▶</span>");
                html.AppendLine($@"                    </span>");
                html.AppendLine($@"                </h3>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"            <div class=""scene-content"" id=""content_{categoryId}_components"">");
                html.AppendLine($@"                <div style=""display: grid; gap: 10px; margin-top: 10px;"">");
                
                foreach (var component in category.Value.OrderByDescending(c => c.UsageCount))
                {
                    var percentage = totalComponents > 0 ? (component.UsageCount * 100.0 / totalComponents) : 0;
                    var scenesText = string.Join(", ", component.ScenesUsed.Take(3));
                    if (component.ScenesUsed.Count > 3)
                        scenesText += $" (+{component.ScenesUsed.Count - 3} more)";
                    
                    html.AppendLine($@"                    <div style=""padding: 8px 12px; background: rgba(255,255,255,0.05); border-radius: 6px; border-left: 3px solid #667eea;"">");
                    html.AppendLine($@"                        <div style=""display: flex; justify-content: space-between; align-items: center;"">");
                    html.AppendLine($@"                            <strong>{component.ComponentType}</strong>");
                    html.AppendLine($@"                            <span style=""color: #28a745; font-weight: bold;"">{component.UsageCount} ({percentage:F1}%)</span>");
                    html.AppendLine($@"                        </div>");
                    html.AppendLine($@"                        <div style=""color: #6c757d; font-size: 0.9em; margin-top: 4px;"">");
                    html.AppendLine($@"                            Used in: {scenesText}");
                    html.AppendLine($@"                        </div>");
                    html.AppendLine($@"                    </div>");
                }
                
                html.AppendLine($@"                </div>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"        </div>");
            }
            
            html.AppendLine(@"    </div>");
            return html.ToString();
        }

        private string GenerateMissingReferencesSection()
        {
            var html = new StringBuilder();
            html.AppendLine(@"    <div class=""section"">");
            html.AppendLine(@"        <h2>Missing References</h2>");
            
            // Missing references overview stats
            var totalBroken = _missingReferences.TotalBrokenReferences;
            var affectedScenes = _missingReferences.AffectedScenes;
            var assetTypes = _missingReferences.MissingAssetTypes.Count;
            
            html.AppendLine($@"        <div class=""stats-grid"" style=""margin-bottom: 20px;"">");
            html.AppendLine($@"            <div class=""stat-card"" style=""background: {(totalBroken > 0 ? "linear-gradient(135deg, #e74c3c, #c0392b)" : "linear-gradient(135deg, #27ae60, #229954)")};"">");
            html.AppendLine($@"                <div class=""stat-value"">{totalBroken}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Broken References</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"">");
            html.AppendLine($@"                <div class=""stat-value"">{affectedScenes}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Affected Scenes</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"            <div class=""stat-card"">");
            html.AppendLine($@"                <div class=""stat-value"">{assetTypes}</div>");
            html.AppendLine($@"                <div class=""stat-label"">Asset Types</div>");
            html.AppendLine($@"            </div>");
            html.AppendLine($@"        </div>");

            if (totalBroken > 0)
            {
                // Group issues by asset type
                var issuesByType = _missingReferences.SceneIssues.Values
                    .SelectMany(issues => issues)
                    .GroupBy(issue => issue.AssetType)
                    .OrderByDescending(g => g.Count());

                foreach (var typeGroup in issuesByType)
                {
                    var typeId = typeGroup.Key.Replace(" ", "_").ToLower();
                    var typeCount = typeGroup.Count();
                    
                    html.AppendLine($@"        <div class=""scene-item"" style=""margin-bottom: 15px;"">");
                    html.AppendLine($@"            <div class=""scene-header"" onclick=""toggleScene('{typeId}_issues')"">");
                    html.AppendLine($@"                <h3 style=""color: #e74c3c;"">");
                    html.AppendLine($@"                    Missing {typeGroup.Key} References");
                    html.AppendLine($@"                    <span>");
                    html.AppendLine($@"                        <small>({typeCount} issues)</small>");
                    html.AppendLine($@"                        <span class=""toggle-icon"" id=""icon_{typeId}_issues"">▶</span>");
                    html.AppendLine($@"                    </span>");
                    html.AppendLine($@"                </h3>");
                    html.AppendLine($@"            </div>");
                    html.AppendLine($@"            <div class=""scene-content"" id=""content_{typeId}_issues"">");
                    html.AppendLine($@"                <div style=""display: grid; gap: 8px; margin-top: 10px;"">");
                    
                    foreach (var issue in typeGroup.Take(20)) // Limit to first 20 to avoid overwhelming the UI
                    {
                        html.AppendLine($@"                    <div style=""padding: 8px 12px; background: rgba(231,76,60,0.1); border-radius: 6px; border-left: 3px solid #e74c3c;"">");
                        html.AppendLine($@"                        <div style=""display: flex; justify-content: space-between; align-items: flex-start;"">");
                        html.AppendLine($@"                            <div>");
                        html.AppendLine($@"                                <strong>{issue.ReferenceType}</strong>");
                        html.AppendLine($@"                                <div style=""color: #666; font-size: 0.9em; margin-top: 2px;"">{issue.Description}</div>");
                        html.AppendLine($@"                            </div>");
                        html.AppendLine($@"                            <div style=""text-align: right; font-size: 0.85em; color: #999;"">");
                        html.AppendLine($@"                                <div>Scene: {issue.SceneName}</div>");
                        html.AppendLine($@"                                <div>Object: {issue.GameObjectName}</div>");
                        html.AppendLine($@"                            </div>");
                        html.AppendLine($@"                        </div>");
                        html.AppendLine($@"                        <div style=""margin-top: 8px; font-family: 'Courier New', monospace; font-size: 0.8em; color: #777; background: rgba(0,0,0,0.05); padding: 4px 8px; border-radius: 4px;"">");
                        html.AppendLine($@"                            GUID: {issue.MissingGuid}");
                        html.AppendLine($@"                        </div>");
                        html.AppendLine($@"                    </div>");
                    }
                    
                    if (typeGroup.Count() > 20)
                    {
                        html.AppendLine($@"                    <div style=""text-align: center; padding: 10px; color: #666; font-style: italic;"">");
                        html.AppendLine($@"                        ... and {typeGroup.Count() - 20} more {typeGroup.Key.ToLower()} reference issues");
                        html.AppendLine($@"                    </div>");
                    }
                    
                    html.AppendLine($@"                </div>");
                    html.AppendLine($@"            </div>");
                    html.AppendLine($@"        </div>");
                }
            }
            else
            {
                html.AppendLine(@"        <div style=""text-align: center; padding: 30px; background: rgba(39,174,96,0.1); border-radius: 8px; border: 1px solid #27ae60;"">");
                html.AppendLine(@"            <div style=""color: #27ae60; font-size: 1.2em; font-weight: bold; margin-bottom: 8px;"">No Missing References Found</div>");
                html.AppendLine(@"            <div style=""color: #666;"">All asset references in your scenes are properly linked. Your project is in excellent condition.</div>");
                html.AppendLine(@"        </div>");
            }
            
            html.AppendLine(@"    </div>");
            return html.ToString();
        }

        private string GenerateScenesSection()
        {
            var html = new StringBuilder();
            html.AppendLine(@"    <div class=""section"">");
            html.AppendLine(@"        <h2>Scene Hierarchies</h2>");
            html.AppendLine(@"        <input type=""text"" class=""search-box"" id=""sceneSearch"" placeholder=""Search scenes..."" />");

            foreach (var scene in _sceneResults)
            {
                var sceneId = scene.SceneName.Replace(" ", "_").Replace(".", "_");
                html.AppendLine($@"        <div class=""scene-item"" data-scene-name=""{scene.SceneName.ToLower()}"">");
                html.AppendLine($@"            <div class=""scene-header"" onclick=""toggleScene('{sceneId}')"">");
                html.AppendLine($@"                <h3>");
                html.AppendLine($@"                    {scene.SceneName}");
                html.AppendLine($@"                    <span>");
                html.AppendLine($@"                        <small>({scene.GameObjects.Count} objects)</small>");
                html.AppendLine($@"                        <span class=""toggle-icon"" id=""icon_{sceneId}"">▶</span>");
                html.AppendLine($@"                    </span>");
                html.AppendLine($@"                </h3>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"            <div class=""scene-content"" id=""content_{sceneId}"">");
                html.AppendLine($@"                <div class=""hierarchy"">");
                
                foreach (var gameObject in scene.GameObjects)
                {
                    var indent = new string(' ', gameObject.Depth * 4);
                    html.AppendLine($@"                    <div class=""game-object"">{indent}{gameObject.Name}</div>");
                }
                
                html.AppendLine($@"                </div>");
                html.AppendLine($@"            </div>");
                html.AppendLine($@"        </div>");
            }

            html.AppendLine(@"    </div>");
            return html.ToString();
        }

        private string GenerateUnusedScriptsSection()
        {
            var html = new StringBuilder();
            html.AppendLine(@"    <div class=""section"">");
            html.AppendLine(@"        <h2>Unused Scripts</h2>");
            
            if (_unusedScripts.Any())
            {
                html.AppendLine(@"        <input type=""text"" class=""search-box"" id=""scriptSearch"" placeholder=""Search unused scripts..."" />");
                html.AppendLine(@"        <div class=""unused-scripts-list"" id=""scriptsList"">");
                
                foreach (var script in _unusedScripts)
                {
                    html.AppendLine($@"            <div class=""script-item"" data-script-path=""{script.RelativePath.ToLower()}"">");
                    html.AppendLine($@"                <div class=""script-path"">{script.RelativePath}</div>");
                    html.AppendLine($@"                <div class=""script-guid"">{script.ClassName}</div>");
                    html.AppendLine($@"            </div>");
                }
                
                html.AppendLine(@"        </div>");
            }
            else
            {
                html.AppendLine(@"        <div class=""no-results"">No unused scripts found. Your project is clean.</div>");
            }
            
            html.AppendLine(@"    </div>");
            html.AppendLine(@"</div>");
            return html.ToString();
        }

        private string GenerateScripts()
        {
            return @"
<script>
    function toggleScene(sceneId) {
        const content = document.getElementById('content_' + sceneId);
        const icon = document.getElementById('icon_' + sceneId);
        
        if (content.classList.contains('active')) {
            content.classList.remove('active');
            icon.classList.remove('rotated');
            icon.textContent = '▶';
        } else {
            content.classList.add('active');
            icon.classList.add('rotated');
            icon.textContent = '▼';
        }
    }
    
    // Search functionality for scenes
    document.getElementById('sceneSearch')?.addEventListener('input', function(e) {
        const searchTerm = e.target.value.toLowerCase();
        const sceneItems = document.querySelectorAll('.scene-item');
        
        sceneItems.forEach(item => {
            const sceneName = item.getAttribute('data-scene-name');
            if (sceneName.includes(searchTerm)) {
                item.style.display = 'block';
            } else {
                item.style.display = 'none';
            }
        });
    });
    
    // Search functionality for scripts
    document.getElementById('scriptSearch')?.addEventListener('input', function(e) {
        const searchTerm = e.target.value.toLowerCase();
        const scriptItems = document.querySelectorAll('.script-item');
        
        scriptItems.forEach(item => {
            const scriptPath = item.getAttribute('data-script-path');
            if (scriptPath.includes(searchTerm)) {
                item.style.display = 'flex';
            } else {
                item.style.display = 'none';
            }
        });
    });
    
    // Add smooth scrolling
    document.querySelectorAll('a[href^=""#""]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            e.preventDefault();
            document.querySelector(this.getAttribute('href')).scrollIntoView({
                behavior: 'smooth'
            });
        });
    });
</script>";
        }
    }

    public class SceneAnalysisResult
    {
        public string SceneName { get; set; } = string.Empty;
        public List<GameObjectInfo> GameObjects { get; set; } = new();
    }

    public class GameObjectInfo
    {
        public string Name { get; set; } = string.Empty;
        public int Depth { get; set; }
        public string FileId { get; set; } = string.Empty;
        public string? ParentId { get; set; }
    }
}