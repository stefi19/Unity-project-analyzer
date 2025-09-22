using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace UnityProjectAnalyzer
{
    public class SceneParser
    {
        private readonly IDeserializer _deserializer;

        public SceneParser()
        {
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
        }

        public async Task<string> ParseSceneHierarchy(string sceneFilePath)
        {
            var content = await File.ReadAllTextAsync(sceneFilePath);
            var gameObjects = ParseGameObjects(content);
            var hierarchy = BuildHierarchy(gameObjects);
            var orderedHierarchy = GetOrderedRoots(content, hierarchy);
            return FormatHierarchy(orderedHierarchy);
        }

        public async Task<List<string>> GetScriptReferences(string sceneFilePath)
        {
            var content = await File.ReadAllTextAsync(sceneFilePath);
            var references = new HashSet<string>();

            // Look for MonoBehaviour script references
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("m_Script:") && line.Contains("guid:"))
                {
                    // Extract GUID directly from the line
                    var guidStart = line.IndexOf("guid: ") + 6;
                    var guidEnd = line.IndexOf(",", guidStart);
                    if (guidEnd == -1) guidEnd = line.IndexOf("}", guidStart);
                    
                    if (guidStart > 5 && guidEnd > guidStart)
                    {
                        var guid = line.Substring(guidStart, guidEnd - guidStart).Trim();
                        var scriptName = await FindScriptNameByGuid(guid, sceneFilePath);
                        if (!string.IsNullOrEmpty(scriptName))
                        {
                            references.Add(scriptName);
                        }
                    }
                }
            }

            return references.ToList();
        }

        private async Task<string> FindScriptNameByGuid(string guid, string sceneFilePath)
        {
            // Find the Unity project root
            var projectRoot = FindProjectRoot(sceneFilePath);
            if (projectRoot == null) return null;

            // Search for .meta files with this GUID
            var metaFiles = Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.cs.meta", SearchOption.AllDirectories);
            
            foreach (var metaFile in metaFiles)
            {
                var content = await File.ReadAllTextAsync(metaFile);
                if (content.Contains($"guid: {guid}"))
                {
                    var scriptFile = metaFile.Substring(0, metaFile.Length - 5); // Remove .meta extension
                    if (File.Exists(scriptFile))
                    {
                        return await ExtractClassNameFromScript(scriptFile);
                    }
                }
            }

            return null;
        }

        private async Task<string> ExtractClassNameFromScript(string scriptFilePath)
        {
            var content = await File.ReadAllTextAsync(scriptFilePath);
            var lines = content.Split('\n');
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("public class ") && trimmed.Contains(": MonoBehaviour"))
                {
                    var parts = trimmed.Split(' ');
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (parts[i] == "class")
                        {
                            return parts[i + 1];
                        }
                    }
                }
            }
            
            return null;
        }

        private string FindProjectRoot(string filePath)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(filePath));
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "Assets")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "ProjectSettings")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            return null;
        }

        private List<GameObject> ParseGameObjects(string content)
        {
            var gameObjects = new List<GameObject>();
            var documents = content.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var document in documents)
            {
                if (document.Contains("GameObject:"))
                {
                    var gameObject = ParseGameObject(document);
                    if (gameObject != null)
                    {
                        gameObjects.Add(gameObject);
                    }
                }
            }

            // Parse Transform components to establish parent-child relationships
            ParseTransformRelationships(content, gameObjects);

            return gameObjects;
        }

        private void ParseTransformRelationships(string content, List<GameObject> gameObjects)
        {
            var documents = content.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
            var gameObjectLookup = gameObjects.ToDictionary(go => go.FileId, go => go);
            var transformToGameObject = new Dictionary<string, string>();
            var childrenOrder = new Dictionary<string, List<string>>();

            // First pass: build Transform to GameObject mapping and collect children order
            foreach (var document in documents)
            {
                if (document.Contains("Transform:") || document.Contains("RectTransform:"))
                {
                    var lines = document.Split('\n');
                    string transformId = null;
                    string gameObjectId = null;
                    var childrenTransforms = new List<string>();

                    // Extract Transform fileID from header
                    foreach (var line in lines)
                    {
                        if (line.Contains("!u!4 &") || line.Contains("!u!224 &")) // Transform or RectTransform
                        {
                            var parts = line.Split('&');
                            if (parts.Length > 1)
                            {
                                transformId = parts[1].Trim();
                            }
                            break;
                        }
                    }

                    bool inChildren = false;
                    // Parse the Transform data
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("m_GameObject:") && trimmed.Contains("fileID:"))
                        {
                            var start = trimmed.IndexOf("fileID: ") + 8;
                            var end = trimmed.IndexOf("}", start);
                            if (end > start)
                            {
                                gameObjectId = trimmed.Substring(start, end - start).Trim();
                            }
                        }
                        else if (trimmed.StartsWith("m_Children:"))
                        {
                            inChildren = true;
                            if (trimmed.Contains("[]"))
                            {
                                // Empty children array
                                inChildren = false;
                            }
                        }
                        else if (inChildren && trimmed.StartsWith("- {fileID:"))
                        {
                            var start = trimmed.IndexOf("fileID: ") + 8;
                            var end = trimmed.IndexOf("}", start);
                            if (end > start)
                            {
                                var childTransformId = trimmed.Substring(start, end - start).Trim();
                                childrenTransforms.Add(childTransformId);
                            }
                        }
                        else if (inChildren && (trimmed.StartsWith("m_") || trimmed.StartsWith("---")))
                        {
                            inChildren = false;
                        }
                    }

                    if (transformId != null && gameObjectId != null)
                    {
                        transformToGameObject[transformId] = gameObjectId;
                        if (childrenTransforms.Any())
                        {
                            childrenOrder[gameObjectId] = childrenTransforms;
                        }
                    }
                }
            }

            // Second pass: establish parent-child relationships with proper ordering
            foreach (var document in documents)
            {
                if (document.Contains("Transform:") || document.Contains("RectTransform:"))
                {
                    var lines = document.Split('\n');
                    string gameObjectId = null;
                    string parentTransformId = null;

                    // Find the GameObject this Transform belongs to
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("m_GameObject:") && trimmed.Contains("fileID:"))
                        {
                            var start = trimmed.IndexOf("fileID: ") + 8;
                            var end = trimmed.IndexOf("}", start);
                            if (end > start)
                            {
                                gameObjectId = trimmed.Substring(start, end - start).Trim();
                            }
                        }
                        else if (trimmed.StartsWith("m_Father:") && trimmed.Contains("fileID:"))
                        {
                            var start = trimmed.IndexOf("fileID: ") + 8;
                            var end = trimmed.IndexOf("}", start);
                            if (end > start)
                            {
                                var fatherId = trimmed.Substring(start, end - start).Trim();
                                if (fatherId != "0")
                                {
                                    parentTransformId = fatherId;
                                }
                            }
                        }
                    }

                    // Update the GameObject with parent information
                    if (gameObjectId != null && gameObjectLookup.ContainsKey(gameObjectId))
                    {
                        string parentGameObjectId = null;
                        if (parentTransformId != null && transformToGameObject.ContainsKey(parentTransformId))
                        {
                            parentGameObjectId = transformToGameObject[parentTransformId];
                        }
                        gameObjectLookup[gameObjectId].ParentId = parentGameObjectId;
                    }
                }
            }

            // Third pass: arrange children in the correct order based on m_Children arrays
            foreach (var kvp in childrenOrder)
            {
                var parentGameObjectId = kvp.Key;
                var childrenTransformIds = kvp.Value;
                
                if (gameObjectLookup.ContainsKey(parentGameObjectId))
                {
                    var parent = gameObjectLookup[parentGameObjectId];
                    parent.Children.Clear(); // Clear any existing children
                    
                    foreach (var childTransformId in childrenTransformIds)
                    {
                        if (transformToGameObject.ContainsKey(childTransformId))
                        {
                            var childGameObjectId = transformToGameObject[childTransformId];
                            if (gameObjectLookup.ContainsKey(childGameObjectId))
                            {
                                parent.Children.Add(gameObjectLookup[childGameObjectId]);
                            }
                        }
                    }
                }
            }
        }

        private GameObject ParseGameObject(string document)
        {
            try
            {
                var lines = document.Split('\n');
                string fileId = null;
                string name = null;
                string parentId = null;

                // Extract fileID from the document header (e.g., "--- !u!1 &136406834")
                foreach (var line in lines)
                {
                    if (line.Contains("!u!1 &"))
                    {
                        var parts = line.Split('&');
                        if (parts.Length > 1)
                        {
                            fileId = parts[1].Trim();
                        }
                        break;
                    }
                }

                // Find m_Name
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("m_Name:"))
                    {
                        name = trimmed.Substring(7).Trim();
                        if (string.IsNullOrWhiteSpace(name))
                        {
                            return null; // Skip objects with empty names
                        }
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(name))
                {
                    return new GameObject
                    {
                        FileId = fileId,
                        Name = name,
                        ParentId = null // We'll determine parent relationships later
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing GameObject: {ex.Message}");
            }

            return null;
        }

        private List<GameObject> BuildHierarchy(List<GameObject> gameObjects)
        {
            // Find root objects (no parent or parent is "0" or empty)
            var roots = gameObjects.Where(go => string.IsNullOrEmpty(go.ParentId) || go.ParentId == "0").ToList();
            return roots;
        }

        private string FormatHierarchy(List<GameObject> rootObjects)
        {
            var sb = new StringBuilder();
            
            foreach (var root in rootObjects)
            {
                FormatGameObject(root, 0, sb);
            }

            return sb.ToString().TrimEnd();
        }

        private void FormatGameObject(GameObject gameObject, int depth, StringBuilder sb)
        {
            var indent = new string('-', depth * 2);
            var formattedName = FormatGameObjectName(gameObject.Name);
            sb.AppendLine($"{indent}{formattedName}");

            foreach (var child in gameObject.Children)
            {
                FormatGameObject(child, depth + 1, sb);
            }
        }

        private string FormatGameObjectName(string name)
        {
            // Add spaces before numbers in names like "Child1" -> "Child 1"
            var result = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsDigit(c) && i > 0 && char.IsLetter(name[i - 1]))
                {
                    result.Append(' ');
                }
                result.Append(c);
            }
            return result.ToString();
        }

        private List<GameObject> GetOrderedRoots(string content, List<GameObject> roots)
        {
            // First, build a mapping from Transform fileID to GameObject fileID
            var transformToGameObject = BuildTransformToGameObjectMapping(content);
            
            // Try to find SceneRoots section for proper ordering
            var lines = content.Split('\n');
            var sceneRootsOrder = new List<string>();
            bool inSceneRoots = false;
            
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains("SceneRoots:"))
                {
                    inSceneRoots = true;
                    continue;
                }
                
                if (inSceneRoots)
                {
                    if (trimmed.StartsWith("- {fileID:"))
                    {
                        var start = trimmed.IndexOf("fileID: ") + 8;
                        var end = trimmed.IndexOf("}", start);
                        if (end > start)
                        {
                            var transformFileId = trimmed.Substring(start, end - start).Trim();
                            // Convert Transform fileID to GameObject fileID
                            if (transformToGameObject.ContainsKey(transformFileId))
                            {
                                var gameObjectFileId = transformToGameObject[transformFileId];
                                sceneRootsOrder.Add(gameObjectFileId);
                            }
                        }
                    }
                    else if (trimmed.StartsWith("---") || string.IsNullOrWhiteSpace(trimmed))
                    {
                        break;
                    }
                }
            }

            // Order roots according to SceneRoots if found
            if (sceneRootsOrder.Any())
            {
                var lookup = roots.ToDictionary(r => r.FileId, r => r);
                var orderedRoots = new List<GameObject>();
                
                foreach (var fileId in sceneRootsOrder)
                {
                    if (lookup.ContainsKey(fileId))
                    {
                        orderedRoots.Add(lookup[fileId]);
                    }
                }
                
                return orderedRoots;
            }

            // Fallback to alphabetical order if no SceneRoots found
            return roots.OrderBy(r => r.Name).ToList();
        }

        private Dictionary<string, string> BuildTransformToGameObjectMapping(string content)
        {
            var mapping = new Dictionary<string, string>();
            var documents = content.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var document in documents)
            {
                if (document.Contains("Transform:") || document.Contains("RectTransform:"))
                {
                    var lines = document.Split('\n');
                    string transformId = null;
                    string gameObjectId = null;

                    // Extract Transform fileID from header
                    foreach (var line in lines)
                    {
                        if (line.Contains("!u!4 &") || line.Contains("!u!224 &")) // Transform or RectTransform
                        {
                            var parts = line.Split('&');
                            if (parts.Length > 1)
                            {
                                transformId = parts[1].Trim();
                            }
                            break;
                        }
                    }

                    // Find the GameObject this Transform belongs to
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("m_GameObject:") && trimmed.Contains("fileID:"))
                        {
                            var start = trimmed.IndexOf("fileID: ") + 8;
                            var end = trimmed.IndexOf("}", start);
                            if (end > start)
                            {
                                gameObjectId = trimmed.Substring(start, end - start).Trim();
                            }
                            break;
                        }
                    }

                    if (transformId != null && gameObjectId != null)
                    {
                        mapping[transformId] = gameObjectId;
                    }
                }
            }

            return mapping;
        }

    public async Task<List<GameObjectInfo>> GetGameObjectsForReport(string sceneFilePath)
    {
        var content = await File.ReadAllTextAsync(sceneFilePath);
        var gameObjects = ParseGameObjects(content);
        var hierarchy = BuildHierarchy(gameObjects);
        var orderedHierarchy = GetOrderedRoots(content, hierarchy);
        
        var result = new List<GameObjectInfo>();
        FlattenHierarchy(orderedHierarchy, result, 0);
        
        return result;
    }        private void FlattenHierarchy(List<GameObject> hierarchy, List<GameObjectInfo> result, int depth)
        {
            foreach (var item in hierarchy)
            {
                result.Add(new GameObjectInfo
                {
                    Name = item.Name,
                    Depth = depth,
                    FileId = item.FileId,
                    ParentId = item.ParentId
                });
                
                if (item.Children.Any())
                {
                    FlattenHierarchy(item.Children, result, depth + 1);
                }
            }
        }
    }

    public class GameObject
    {
        public string FileId { get; set; }
        public string Name { get; set; }
        public string ParentId { get; set; }
        public List<GameObject> Children { get; set; } = new List<GameObject>();
    }
}