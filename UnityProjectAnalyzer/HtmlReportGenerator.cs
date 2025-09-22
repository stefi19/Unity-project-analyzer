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
        private readonly string _projectPath;
        private readonly DateTime _analysisTime;

        public HtmlReportGenerator(List<SceneAnalysisResult> sceneResults, 
                                 List<ScriptInfo> unusedScripts, 
                                 string projectPath)
        {
            _sceneResults = sceneResults;
            _unusedScripts = unusedScripts;
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