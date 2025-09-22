using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Detects broken asset references and missing GUIDs across Unity scenes.
    /// Validates that all referenced assets (scripts, prefabs, materials, etc.) actually exist
    /// in the project and provides detailed reporting of any integrity issues.
    /// </summary>
    public class MissingReferenceDetector
    {
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// Initializes the missing reference detector with YAML deserializer for parsing Unity scene files.
        /// Unity uses GUID-based references to maintain asset links across the project.
        /// </summary>
        public MissingReferenceDetector()
        {
            _deserializer = new DeserializerBuilder().Build();
        }

        /// <summary>
        /// Performs comprehensive detection of missing asset references across all project scenes.
        /// Builds a complete asset GUID map and validates all references for integrity.
        /// </summary>
        /// <param name="sceneFiles">Array of Unity scene files to analyze</param>
        /// <param name="projectPath">Path to the Unity project root directory</param>
        /// <returns>Detailed results of missing reference analysis with statistics</returns>
        public async Task<MissingReferenceResult> DetectMissingReferences(string[] sceneFiles, string projectPath)
        {
            var result = new MissingReferenceResult();
            
            // Build comprehensive map of all asset GUIDs in the project for validation
            var allAssetGuids = await BuildAssetGuidMap(projectPath);

            // Analyze each scene for broken references
            foreach (var sceneFile in sceneFiles)
            {
                var sceneIssues = await AnalyzeSceneReferences(sceneFile, allAssetGuids);
                if (sceneIssues.Any())
                {
                    // Only store scenes that actually have missing references
                    result.SceneIssues[Path.GetFileNameWithoutExtension(sceneFile)] = sceneIssues;
                }
            }

            // Generate summary statistics for reporting and dashboard display
            result.TotalBrokenReferences = result.SceneIssues.Values.SelectMany(issues => issues).Count();
            result.AffectedScenes = result.SceneIssues.Count;
            result.MissingAssetTypes = result.SceneIssues.Values
                .SelectMany(issues => issues)
                .GroupBy(issue => issue.AssetType)
                .ToDictionary(g => g.Key, g => g.Count());

            return result;
        }

        private async Task<Dictionary<string, string>> BuildAssetGuidMap(string projectPath)
        {
            var assetGuids = new Dictionary<string, string>();
            var assetsPath = Path.Combine(projectPath, "Assets");

            if (!Directory.Exists(assetsPath))
                return assetGuids;

            // Find all .meta files to build GUID to file mapping
            var metaFiles = Directory.GetFiles(assetsPath, "*.meta", SearchOption.AllDirectories);

            foreach (var metaFile in metaFiles)
            {
                try
                {
                    var content = await File.ReadAllTextAsync(metaFile);
                    var guid = ExtractGuidFromMeta(content);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        var assetFile = metaFile.Substring(0, metaFile.Length - 5); // Remove .meta extension
                        assetGuids[guid] = assetFile;
                    }
                }
                catch
                {
                    // Skip corrupted meta files
                    continue;
                }
            }

            return assetGuids;
        }

        private string ExtractGuidFromMeta(string metaContent)
        {
            var lines = metaContent.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("guid:"))
                {
                    return line.Substring(5).Trim();
                }
            }
            return string.Empty;
        }

        private async Task<List<MissingReferenceIssue>> AnalyzeSceneReferences(string sceneFile, Dictionary<string, string> assetGuids)
        {
            var issues = new List<MissingReferenceIssue>();
            var content = await File.ReadAllTextAsync(sceneFile);

            // Parse YAML documents in the scene
            var documents = content.Split(new[] { "--- !u!" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var doc in documents)
            {
                if (string.IsNullOrWhiteSpace(doc)) continue;

                try
                {
                    // Look for MonoBehaviour components (type 114)
                    if (doc.StartsWith("114 "))
                    {
                        await AnalyzeMonoBehaviourReferences(doc, assetGuids, issues, Path.GetFileNameWithoutExtension(sceneFile));
                    }
                    
                    // Look for other component references
                    await AnalyzeComponentReferences(doc, assetGuids, issues, Path.GetFileNameWithoutExtension(sceneFile));
                }
                catch
                {
                    // Skip malformed documents
                    continue;
                }
            }

            return issues;
        }

        private async Task AnalyzeMonoBehaviourReferences(string document, Dictionary<string, string> assetGuids, 
            List<MissingReferenceIssue> issues, string sceneName)
        {
            var lines = document.Split('\n');
            string currentGameObject = "Unknown GameObject";
            string currentScript = "Unknown Script";

            // Extract GameObject name and script reference
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for script reference
                if (trimmedLine.StartsWith("m_Script: {fileID:") && trimmedLine.Contains("guid:"))
                {
                    var guid = ExtractGuidFromReference(trimmedLine);
                    if (!string.IsNullOrEmpty(guid))
                    {
                        if (!assetGuids.ContainsKey(guid))
                        {
                            issues.Add(new MissingReferenceIssue
                            {
                                SceneName = sceneName,
                                GameObjectName = currentGameObject,
                                AssetType = "Script",
                                ReferenceType = "MonoBehaviour Script",
                                MissingGuid = guid,
                                Description = $"Missing script reference in MonoBehaviour component"
                            });
                        }
                        else
                        {
                            currentScript = Path.GetFileName(assetGuids[guid]);
                        }
                    }
                }
                
                // Look for serialized field references
                if (trimmedLine.Contains("fileID:") && trimmedLine.Contains("guid:"))
                {
                    var guid = ExtractGuidFromReference(trimmedLine);
                    if (!string.IsNullOrEmpty(guid) && !assetGuids.ContainsKey(guid))
                    {
                        var fieldName = ExtractFieldName(line);
                        var assetType = InferAssetType(trimmedLine);
                        
                        issues.Add(new MissingReferenceIssue
                        {
                            SceneName = sceneName,
                            GameObjectName = currentGameObject,
                            AssetType = assetType,
                            ReferenceType = $"Serialized Field: {fieldName}",
                            MissingGuid = guid,
                            Description = $"Missing asset reference in {currentScript}.{fieldName}"
                        });
                    }
                }
            }
        }

        private async Task AnalyzeComponentReferences(string document, Dictionary<string, string> assetGuids,
            List<MissingReferenceIssue> issues, string sceneName)
        {
            var lines = document.Split('\n');
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Look for material references in renderers
                if ((trimmedLine.StartsWith("m_Materials:") || trimmedLine.Contains("- {fileID:")) && 
                    trimmedLine.Contains("guid:"))
                {
                    var guid = ExtractGuidFromReference(trimmedLine);
                    if (!string.IsNullOrEmpty(guid) && !assetGuids.ContainsKey(guid))
                    {
                        issues.Add(new MissingReferenceIssue
                        {
                            SceneName = sceneName,
                            GameObjectName = "Unknown GameObject",
                            AssetType = "Material",
                            ReferenceType = "Renderer Material",
                            MissingGuid = guid,
                            Description = "Missing material reference in renderer component"
                        });
                    }
                }
                
                // Look for mesh references
                if (trimmedLine.StartsWith("m_Mesh:") && trimmedLine.Contains("guid:"))
                {
                    var guid = ExtractGuidFromReference(trimmedLine);
                    if (!string.IsNullOrEmpty(guid) && !assetGuids.ContainsKey(guid))
                    {
                        issues.Add(new MissingReferenceIssue
                        {
                            SceneName = sceneName,
                            GameObjectName = "Unknown GameObject",
                            AssetType = "Mesh",
                            ReferenceType = "MeshFilter Mesh",
                            MissingGuid = guid,
                            Description = "Missing mesh reference in MeshFilter component"
                        });
                    }
                }
                
                // Look for texture references
                if ((trimmedLine.StartsWith("m_Texture:") || trimmedLine.StartsWith("m_MainTexture:")) && 
                    trimmedLine.Contains("guid:"))
                {
                    var guid = ExtractGuidFromReference(trimmedLine);
                    if (!string.IsNullOrEmpty(guid) && !assetGuids.ContainsKey(guid))
                    {
                        issues.Add(new MissingReferenceIssue
                        {
                            SceneName = sceneName,
                            GameObjectName = "Unknown GameObject",
                            AssetType = "Texture",
                            ReferenceType = "Texture Reference",
                            MissingGuid = guid,
                            Description = "Missing texture reference"
                        });
                    }
                }
            }
        }

        private string ExtractGuidFromReference(string line)
        {
            var guidIndex = line.IndexOf("guid:");
            if (guidIndex >= 0)
            {
                var guidStart = guidIndex + 5;
                var guidEnd = line.IndexOf(',', guidStart);
                if (guidEnd < 0) guidEnd = line.IndexOf('}', guidStart);
                if (guidEnd < 0) guidEnd = line.Length;
                
                return line.Substring(guidStart, guidEnd - guidStart).Trim(' ', '"', '\t');
            }
            return string.Empty;
        }

        private string ExtractFieldName(string line)
        {
            // Try to extract field name from YAML structure
            var trimmed = line.Trim();
            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex > 0)
            {
                return trimmed.Substring(0, colonIndex).Trim();
            }
            return "Unknown Field";
        }

        private string InferAssetType(string line)
        {
            var lowerLine = line.ToLower();
            
            if (lowerLine.Contains("material")) return "Material";
            if (lowerLine.Contains("texture")) return "Texture";
            if (lowerLine.Contains("mesh")) return "Mesh";
            if (lowerLine.Contains("audio")) return "AudioClip";
            if (lowerLine.Contains("sprite")) return "Sprite";
            if (lowerLine.Contains("prefab")) return "Prefab";
            if (lowerLine.Contains("script")) return "Script";
            
            return "Unknown Asset";
        }
    }

    public class MissingReferenceResult
    {
        public Dictionary<string, List<MissingReferenceIssue>> SceneIssues { get; set; } = new();
        public int TotalBrokenReferences { get; set; }
        public int AffectedScenes { get; set; }
        public Dictionary<string, int> MissingAssetTypes { get; set; } = new();
    }

    public class MissingReferenceIssue
    {
        public string SceneName { get; set; } = string.Empty;
        public string GameObjectName { get; set; } = string.Empty;
        public string AssetType { get; set; } = string.Empty;
        public string ReferenceType { get; set; } = string.Empty;
        public string MissingGuid { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}