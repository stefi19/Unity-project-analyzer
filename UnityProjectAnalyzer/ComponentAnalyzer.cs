using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace UnityProjectAnalyzer
{
    /// <summary>
    /// Analyzes Unity components across all scenes in a project, providing detailed statistics
    /// about component usage, categorization, and distribution patterns.
    /// Uses official Unity Type IDs for accurate component identification.
    /// </summary>
    public class ComponentAnalyzer
    {
        private readonly IDeserializer _deserializer;

        /// <summary>
        /// Initializes the component analyzer with YamlDotNet deserializer for parsing Unity scene files.
        /// Unity scenes are stored in YAML format with component references using Type IDs.
        /// </summary>
        public ComponentAnalyzer()
        {
            _deserializer = new DeserializerBuilder().Build();
        }

        /// <summary>
        /// Performs comprehensive analysis of all Unity components across multiple scene files.
        /// Aggregates usage statistics, categorizes components, and tracks scene distribution.
        /// </summary>
        /// <param name="sceneFiles">Array of Unity scene file paths to analyze</param>
        /// <returns>Complete analysis results with component statistics and categorization</returns>
        public async Task<ComponentAnalysisResult> AnalyzeComponents(string[] sceneFiles)
        {
            var result = new ComponentAnalysisResult();
            var allComponents = new Dictionary<string, ComponentInfo>();

            // Process each scene file to extract component information
            foreach (var sceneFile in sceneFiles)
            {
                var sceneComponents = await AnalyzeSceneComponents(sceneFile);
                var sceneName = Path.GetFileNameWithoutExtension(sceneFile);
                
                // Store per-scene component data for detailed reporting
                result.SceneComponents[sceneName] = sceneComponents;

                // Build project-wide component usage statistics by aggregating across scenes
                foreach (var component in sceneComponents)
                {
                    var key = component.ComponentType;
                    if (allComponents.ContainsKey(key))
                    {
                        // Component already seen in other scenes - increment usage and add scene
                        allComponents[key].UsageCount += component.UsageCount;
                        allComponents[key].ScenesUsed.Add(sceneName);
                    }
                    else
                    {
                        // First time seeing this component type - create new entry
                        allComponents[key] = new ComponentInfo
                        {
                            ComponentType = component.ComponentType,
                            Category = component.Category,
                            UsageCount = component.UsageCount,
                            ScenesUsed = new HashSet<string> { sceneName }
                        };
                    }
                }
            }

            result.AllComponents = allComponents.Values.OrderByDescending(c => c.UsageCount).ToList();
            result.ComponentCategories = GroupComponentsByCategory(result.AllComponents);
            
            return result;
        }

        private async Task<List<ComponentInfo>> AnalyzeSceneComponents(string sceneFilePath)
        {
            var content = await File.ReadAllTextAsync(sceneFilePath);
            var components = new Dictionary<string, int>();

            // Parse YAML documents - Unity uses "--- !u!" followed by a number to separate documents
            var documents = content.Split(new[] { "--- !u!" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var doc in documents)
            {
                if (string.IsNullOrWhiteSpace(doc)) continue;

                try
                {
                    var lines = doc.Split('\n');
                    if (lines.Length < 2) continue;

                    // Extract component type ID from the header (e.g., "--- !u!1 &123456")
                    var headerLine = lines[0].Trim();
                    var componentTypeId = ExtractComponentTypeId(headerLine);
                    
                    if (componentTypeId > 0)
                    {
                        var componentType = GetComponentTypeName(componentTypeId);
                        if (!string.IsNullOrEmpty(componentType))
                        {
                            components[componentType] = components.GetValueOrDefault(componentType, 0) + 1;
                        }
                    }
                    
                    // Also check for MonoBehaviour scripts by looking for m_Script references
                    if (doc.Contains("m_Script:"))
                    {
                        components["MonoBehaviour Script"] = components.GetValueOrDefault("MonoBehaviour Script", 0) + 1;
                    }
                }
                catch
                {
                    // Skip malformed documents
                    continue;
                }
            }

            return components.Select(kvp => new ComponentInfo
            {
                ComponentType = kvp.Key,
                Category = CategorizeComponent(kvp.Key),
                UsageCount = kvp.Value,
                ScenesUsed = new HashSet<string>()
            }).ToList();
        }

        private int ExtractComponentTypeId(string headerLine)
        {
            // Extract the number after "!u!" (e.g., "1 &123456" -> 1)
            if (string.IsNullOrEmpty(headerLine)) return 0;
            
            var parts = headerLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0 && int.TryParse(parts[0], out int typeId))
            {
                return typeId;
            }
            return 0;
        }

        private string GetComponentTypeName(int typeId)
        {
            // Unity's built-in component type IDs
            var typeMapping = new Dictionary<int, string>
            {
                { 1, "GameObject" },
                { 2, "Component" },
                { 3, "LevelGameManager" },
                { 4, "Transform" },
                { 5, "TimeManager" },
                { 6, "GlobalGameManager" },
                { 8, "Behaviour" },
                { 9, "GameManager" },
                { 11, "AudioManager" },
                { 12, "ParticleAnimator" },
                { 13, "InputManager" },
                { 15, "EllipsoidParticleEmitter" },
                { 17, "Pipeline" },
                { 18, "EditorExtension" },
                { 19, "Physics2DSettings" },
                { 20, "Camera" },
                { 21, "Material" },
                { 23, "MeshRenderer" },
                { 25, "Renderer" },
                { 26, "ParticleRenderer" },
                { 27, "Texture" },
                { 28, "Texture2D" },
                { 29, "SceneSettings" },
                { 30, "GraphicsSettings" },
                { 33, "MeshFilter" },
                { 41, "OcclusionPortal" },
                { 43, "Mesh" },
                { 45, "Skybox" },
                { 47, "QualitySettings" },
                { 48, "Shader" },
                { 49, "TextAsset" },
                { 50, "Rigidbody2D" },
                { 53, "CollisionDetection2D" },
                { 54, "Rigidbody" },
                { 55, "PhysicsManager" },
                { 56, "Collider" },
                { 57, "Joint" },
                { 58, "CircleCollider2D" },
                { 59, "HingeJoint" },
                { 60, "PolygonCollider2D" },
                { 61, "BoxCollider2D" },
                { 62, "PhysicsMaterial2D" },
                { 64, "MeshCollider" },
                { 65, "BoxCollider" },
                { 68, "EdgeCollider2D" },
                { 70, "CapsuleCollider2D" },
                { 72, "ComputeShader" },
                { 74, "AnimationClip" },
                { 75, "ConstantForce" },
                { 76, "WorldParticleCollider" },
                { 78, "TagManager" },
                { 81, "AudioListener" },
                { 82, "AudioSource" },
                { 83, "AudioClip" },
                { 84, "RenderTexture" },
                { 87, "MeshParticleEmitter" },
                { 88, "ParticleEmitter" },
                { 89, "Cubemap" },
                { 90, "Avatar" },
                { 91, "AnimatorController" },
                { 92, "GUILayer" },
                { 93, "RuntimeAnimatorController" },
                { 94, "ScriptMapper" },
                { 95, "Animator" },
                { 96, "TrailRenderer" },
                { 98, "DelayedCallManager" },
                { 102, "TextMesh" },
                { 104, "RenderSettings" },
                { 108, "Light" },
                { 109, "CGProgram" },
                { 110, "BaseAnimationTrack" },
                { 111, "Animation" },
                { 114, "MonoBehaviour" },
                { 115, "MonoScript" },
                { 116, "MonoManager" },
                { 117, "Texture3D" },
                { 118, "NewAnimationTrack" },
                { 119, "Projector" },
                { 120, "LineRenderer" },
                { 121, "Flare" },
                { 122, "Halo" },
                { 123, "LensFlare" },
                { 124, "FlareLayer" },
                { 125, "HaloLayer" },
                { 126, "NavMeshAreas" },
                { 127, "HaloManager" },
                { 128, "Font" },
                { 129, "PlayerSettings" },
                { 130, "NamedObject" },
                { 131, "GUITexture" },
                { 132, "GUIText" },
                { 133, "GUIElement" },
                { 134, "PhysicMaterial" },
                { 135, "SphereCollider" },
                { 136, "CapsuleCollider" },
                { 137, "SkinnedMeshRenderer" },
                { 138, "FixedJoint" },
                { 141, "BuildSettings" },
                { 142, "AssetBundle" },
                { 143, "CharacterController" },
                { 144, "CharacterJoint" },
                { 145, "SpringJoint" },
                { 146, "WheelCollider" },
                { 147, "ResourceManager" },
                { 148, "NetworkView" },
                { 149, "NetworkManager" },
                { 150, "PreloadData" },
                { 152, "MovieTexture" },
                { 153, "ConfigurableJoint" },
                { 154, "TerrainCollider" },
                { 155, "MasterServerInterface" },
                { 156, "TerrainData" },
                { 157, "LightmapSettings" },
                { 158, "WebCamTexture" },
                { 159, "EditorSettings" },
                { 160, "InteractiveCloth" },
                { 161, "ClothRenderer" },
                { 162, "EditorUserSettings" },
                { 163, "SkinnedCloth" },
                { 164, "AudioReverbFilter" },
                { 165, "AudioHighPassFilter" },
                { 166, "AudioChorusFilter" },
                { 167, "AudioReverbZone" },
                { 168, "AudioEchoFilter" },
                { 169, "AudioLowPassFilter" },
                { 170, "AudioDistortionFilter" },
                { 171, "SparseTexture" },
                { 180, "AudioBehaviour" },
                { 181, "AudioFilter" },
                { 182, "WindZone" },
                { 183, "Cloth" },
                { 184, "SubstanceArchive" },
                { 185, "ProceduralMaterial" },
                { 186, "ProceduralTexture" },
                { 191, "OffMeshLink" },
                { 192, "OcclusionArea" },
                { 193, "Tree" },
                { 194, "NavMeshObstacle" },
                { 195, "NavMeshAgent" },
                { 196, "NavMeshSettings" },
                { 197, "LightProbesLegacy" },
                { 198, "ParticleSystem" },
                { 199, "ParticleSystemRenderer" },
                { 200, "ShaderVariantCollection" },
                { 205, "LODGroup" },
                { 206, "BlendTree" },
                { 207, "Motion" },
                { 208, "NavMeshObstacle" },
                { 210, "SortingGroup" },
                { 212, "SpriteRenderer" },
                { 213, "Sprite" },
                { 214, "CachedSpriteAtlas" },
                { 215, "ReflectionProbe" },
                { 218, "Terrain" },
                { 220, "LightProbeGroup" },
                { 221, "AnimatorOverrideController" },
                { 222, "CanvasRenderer" },
                { 223, "Canvas" },
                { 224, "RectTransform" },
                { 225, "CanvasGroup" },
                { 226, "BillboardAsset" },
                { 227, "BillboardRenderer" },
                { 228, "SpeedTreeWindAsset" },
                { 229, "AnchoredJoint2D" },
                { 230, "Joint2D" },
                { 231, "SpringJoint2D" },
                { 232, "DistanceJoint2D" },
                { 233, "HingeJoint2D" },
                { 234, "SliderJoint2D" },
                { 235, "WheelJoint2D" },
                { 238, "PlatformEffector2D" },
                { 239, "AreaEffector2D" },
                { 240, "PointEffector2D" },
                { 241, "Effector2D" },
                { 243, "SurfaceEffector2D" },
                { 245, "LightProbes" },
                { 246, "LightProbeProxyVolume" },
                { 247, "ParticleSystemForceField" },
                { 248, "VideoPlayer" },
                { 249, "VideoClip" },
                { 319, "AimConstraint" },
                { 320, "VisionCamera" },
                { 321, "ParentConstraint" },
                { 322, "FBXImporter" },
                { 323, "PositionConstraint" },
                { 324, "RotationConstraint" },
                { 325, "ScaleConstraint" }
            };

            return typeMapping.GetValueOrDefault(typeId, $"Unknown ({typeId})");
        }



        private string CategorizeComponent(string componentType)
        {
            var renderingComponents = new[] { "MeshRenderer", "SpriteRenderer", "LineRenderer", "TrailRenderer", "ParticleSystem", "CanvasRenderer", "ParticleSystemRenderer", "SkinnedMeshRenderer", "BillboardRenderer" };
            var physicsComponents = new[] { "BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider", "Rigidbody", "TerrainCollider", "CharacterController", "WheelCollider", "BoxCollider2D", "CircleCollider2D", "PolygonCollider2D", "EdgeCollider2D", "CapsuleCollider2D", "Rigidbody2D" };
            var audioComponents = new[] { "AudioSource", "AudioListener", "AudioReverbFilter", "AudioHighPassFilter", "AudioChorusFilter", "AudioReverbZone", "AudioEchoFilter", "AudioLowPassFilter", "AudioDistortionFilter" };
            var uiComponents = new[] { "Canvas", "CanvasGroup", "RectTransform" };
            var animationComponents = new[] { "Animator", "Animation" };
            var navigationComponents = new[] { "NavMeshAgent", "NavMeshObstacle", "OffMeshLink" };
            var lightingComponents = new[] { "Light", "ReflectionProbe", "LightProbeGroup", "LightProbes", "LightProbeProxyVolume" };
            var transformComponents = new[] { "Transform" };
            var cameraComponents = new[] { "Camera" };
            var effectsComponents = new[] { "WindZone", "ParticleSystemForceField", "LODGroup" };
            var terrainComponents = new[] { "Terrain", "Tree", "TerrainData" };
            var jointComponents = new[] { "HingeJoint", "SpringJoint", "FixedJoint", "ConfigurableJoint", "CharacterJoint", "WheelJoint2D", "HingeJoint2D", "SpringJoint2D", "DistanceJoint2D", "SliderJoint2D" };

            if (renderingComponents.Contains(componentType)) return "Rendering";
            if (physicsComponents.Contains(componentType)) return "Physics";
            if (audioComponents.Contains(componentType)) return "Audio";
            if (uiComponents.Contains(componentType)) return "UI";
            if (animationComponents.Contains(componentType)) return "Animation";
            if (navigationComponents.Contains(componentType)) return "Navigation";
            if (lightingComponents.Contains(componentType)) return "Lighting";
            if (transformComponents.Contains(componentType)) return "Transform";
            if (cameraComponents.Contains(componentType)) return "Camera";
            if (effectsComponents.Contains(componentType)) return "Effects";
            if (terrainComponents.Contains(componentType)) return "Terrain";
            if (jointComponents.Contains(componentType)) return "Joints";
            if (componentType.Contains("Script") || componentType == "MonoBehaviour") return "Scripting";
            if (componentType == "GameObject") return "Core";

            return "Other";
        }

        private Dictionary<string, List<ComponentInfo>> GroupComponentsByCategory(List<ComponentInfo> components)
        {
            return components.GroupBy(c => c.Category)
                           .ToDictionary(g => g.Key, g => g.ToList());
        }
    }

    public class ComponentAnalysisResult
    {
        public Dictionary<string, List<ComponentInfo>> SceneComponents { get; set; } = new();
        public List<ComponentInfo> AllComponents { get; set; } = new();
        public Dictionary<string, List<ComponentInfo>> ComponentCategories { get; set; } = new();
    }

    public class ComponentInfo
    {
        public string ComponentType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int UsageCount { get; set; }
        public HashSet<string> ScenesUsed { get; set; } = new();
    }
}