using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Analyzes C# MonoBehaviour scripts using Microsoft Roslyn compiler services.
    /// Extracts class information, inheritance relationships, and serialized field data
    /// to determine which scripts are actually used in Unity scenes.
    /// </summary>
    public class ScriptAnalyzer
    {
        /// <summary>
        /// Performs comprehensive analysis of all C# script files in parallel for better performance.
        /// Uses Roslyn to parse syntax trees and extract meaningful script metadata.
        /// </summary>
        /// <param name="scriptFiles">Array of C# script file paths to analyze</param>
        /// <returns>Complete analysis results with script information and metadata</returns>
        public async Task<ScriptAnalysisResult> AnalyzeScripts(string[] scriptFiles)
        {
            var scripts = new List<ScriptInfo>();
            
            // Process scripts in parallel to improve performance with large codebases
            var tasks = scriptFiles.Select(async scriptFile =>
            {
                var scriptInfo = await AnalyzeScript(scriptFile);
                return scriptInfo;
            });

            var results = await Task.WhenAll(tasks);
            // Filter out any scripts that failed to parse or aren't MonoBehaviours
            scripts.AddRange(results.Where(r => r != null));

            return new ScriptAnalysisResult
            {
                Scripts = scripts
            };
        }

        /// <summary>
        /// Analyzes a single C# script file using Roslyn to extract class and field information.
        /// Determines if the script is a MonoBehaviour and extracts serialized field data.
        /// </summary>
        /// <param name="scriptFilePath">Path to the C# script file to analyze</param>
        /// <returns>Script information if it's a valid MonoBehaviour, null otherwise</returns>
        private async Task<ScriptInfo> AnalyzeScript(string scriptFilePath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(scriptFilePath);
                var tree = CSharpSyntaxTree.ParseText(content);     // Parse C# syntax tree
                var root = await tree.GetRootAsync();              // Get syntax tree root node

                // Find MonoBehaviour classes
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(c => IsMonoBehaviourClass(c))
                    .ToList();

                if (classes.Any())
                {
                    var className = classes.First().Identifier.ValueText;
                    var fields = GetSerializedFields(classes.First());
                    
                    return new ScriptInfo
                    {
                        FilePath = scriptFilePath,
                        RelativePath = GetRelativePath(scriptFilePath),
                        ClassName = className,
                        SerializedFields = fields
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing script {scriptFilePath}: {ex.Message}");
            }

            return null;
        }

        private bool IsMonoBehaviourClass(ClassDeclarationSyntax classDecl)
        {
            if (classDecl.BaseList == null) return false;

            foreach (var baseType in classDecl.BaseList.Types)
            {
                var typeName = baseType.Type.ToString();
                if (typeName == "MonoBehaviour" || typeName.EndsWith(".MonoBehaviour"))
                {
                    return true;
                }
            }

            return false;
        }

        private List<FieldInfo> GetSerializedFields(ClassDeclarationSyntax classDecl)
        {
            var fields = new List<FieldInfo>();

            foreach (var member in classDecl.Members.OfType<FieldDeclarationSyntax>())
            {
                // Check if field is public or has SerializeField attribute
                bool isPublic = member.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
                bool hasSerializeField = member.AttributeLists
                    .SelectMany(al => al.Attributes)
                    .Any(a => a.Name.ToString().Contains("SerializeField"));

                if (isPublic || hasSerializeField)
                {
                    foreach (var variable in member.Declaration.Variables)
                    {
                        fields.Add(new FieldInfo
                        {
                            Name = variable.Identifier.ValueText,
                            Type = member.Declaration.Type.ToString()
                        });
                    }
                }
            }

            return fields;
        }

        private string GetRelativePath(string absolutePath)
        {
            // Find Assets folder and return relative path from there
            var assetsIndex = absolutePath.IndexOf("Assets");
            if (assetsIndex >= 0)
            {
                return absolutePath.Substring(assetsIndex).Replace('\\', '/');
            }
            return Path.GetFileName(absolutePath);
        }

        public async Task<bool> ValidateFieldExists(string scriptFilePath, string fieldName)
        {
            try
            {
                var content = await File.ReadAllTextAsync(scriptFilePath);
                var tree = CSharpSyntaxTree.ParseText(content);
                var root = await tree.GetRootAsync();

                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(c => IsMonoBehaviourClass(c));

                foreach (var classDecl in classes)
                {
                    var fields = GetSerializedFields(classDecl);
                    if (fields.Any(f => f.Name == fieldName))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating field {fieldName} in {scriptFilePath}: {ex.Message}");
            }

            return false;
        }
    }

    public class ScriptAnalysisResult
    {
        public List<ScriptInfo> Scripts { get; set; } = new List<ScriptInfo>();
    }

    public class ScriptInfo
    {
        public string FilePath { get; set; }
        public string RelativePath { get; set; }
        public string ClassName { get; set; }
        public List<FieldInfo> SerializedFields { get; set; } = new List<FieldInfo>();
    }

    public class FieldInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
}