using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Generates new shader variants to be pre-warmed by ShaderPreCompiler.
/// Filters variants based on upload time and occurrence count.
/// Generates shader variants to be kept during stripping, based on project materials.
/// </summary>
public class ShaderVariantsProcessor
{
    public static readonly string ReportPath = Path.GetFullPath("Artifact/ShaderVariantsCompiled.txt");
    private const string SettingsAssetPath = "Assets/Editor/ShaderPrewarming/ShaderPreCompilerSettings.asset";
    private static ShaderPreCompilerSettings _settings;
    
    private const string RuntimeCollectionEnabledKey = "ShaderVariantsProcessor_RuntimeCollectionEnabled";
    private static readonly HashSet<string> RuntimeCollectedVariants = new();
    private static readonly HashSet<string> RuntimeCollectedGlobalKeywords = new();
    private static bool _isCollectingRuntime;
    private static bool _initialized;
    
    public static ShaderPreCompilerSettings GetSettings()
    {
        if (!_settings)
        {
            _settings = AssetDatabase.LoadAssetAtPath<ShaderPreCompilerSettings>(SettingsAssetPath);
        }

        return _settings;
    }

    private static List<ShaderVariantData> _variantWarmupDataList =  new();

    [MenuItem("Tools/Shader Optimization/Add EnabledGlobalKeywords From Scenes")]
    public static void GetAllEnabledGlobalKeywords()
    {
        Setup();
        foreach (var keyword in Shader.enabledGlobalKeywords)
        {
            if(_settings.enabledGlobalKeywords.Contains(keyword.name)) continue;
            _settings.enabledGlobalKeywords.Add(keyword.name);
        }

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Shader Optimization/Clear Local Keywords")]
    public static void ClearALlCurrentLocalKeywords()
    {
        Setup();
        _settings.localKeywords.Clear();
    }

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _isCollectingRuntime = EditorPrefs.GetBool(RuntimeCollectionEnabledKey, false);
        Menu.SetChecked("Tools/Shader Optimization/Enable Runtime Collection", _isCollectingRuntime);

        if (_isCollectingRuntime)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += CollectRuntimeKeywords;
            Debug.Log("<color=green>Runtime keyword collection is ENABLED.</color>");
        }
    }

    [MenuItem("Tools/Shader Optimization/Enable Runtime Collection")]
    public static void EnableRuntimeCollection()
    {
        _isCollectingRuntime = !_isCollectingRuntime;
        EditorPrefs.SetBool(RuntimeCollectionEnabledKey, _isCollectingRuntime);
        Menu.SetChecked("Tools/Shader Optimization/Enable Runtime Collection", _isCollectingRuntime);

        if (_isCollectingRuntime)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += CollectRuntimeKeywords;
        }
        else
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= CollectRuntimeKeywords;
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            RuntimeCollectedVariants.Clear();
            Debug.Log("Started collecting runtime keywords");
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SaveRuntimeCollectedKeywords();
        }
    }

    private static void CollectRuntimeKeywords()
    {
        if (!EditorApplication.isPlaying || !_isCollectingRuntime) return;
        
        foreach (var keyword in Shader.enabledGlobalKeywords)
        {
            RuntimeCollectedGlobalKeywords.Add(keyword.name);
        }

        var activeMaterials = Resources.FindObjectsOfTypeAll<Material>().ToList();
        
        foreach (var material in activeMaterials)
        {
            if (IgnoreShader(material.shader)) continue;

            var keywords = material.enabledKeywords.Select(k => k.name).ToArray();
            var line = $"{material.shader.name}|{string.Join(" ", keywords)}";
            RuntimeCollectedVariants.Add(line);
        }
    }
    
    private static void SaveRuntimeCollectedKeywords()
    {
        Debug.Log($"Exiting play mode. Collected {RuntimeCollectedVariants.Count} unique variants.");
    
        if (RuntimeCollectedVariants.Count == 0 && RuntimeCollectedGlobalKeywords.Count == 0)
            return;

        Setup();

        _settings.enabledGlobalKeywords ??= new List<string>();
        foreach (var keyword in RuntimeCollectedGlobalKeywords)
        {
            if (!_settings.enabledGlobalKeywords.Contains(keyword))
            {
                _settings.enabledGlobalKeywords.Add(keyword);
            }
        }
        RuntimeCollectedGlobalKeywords.Clear();

        if (RuntimeCollectedVariants.Count > 0)
        {
            var outputPath = "Temp/RuntimeCollectedKeywords.txt";
            File.WriteAllLines(outputPath, RuntimeCollectedVariants);
            RuntimeCollectedVariants.Clear();
        }
        UpdateVariantListToStrip(true);
    }

    // This is called during builds
    [MenuItem("Tools/Shader Optimization/Shader Variants Processor")]
    public static void ProcessShaderVariants()
    {
        Debug.Log("Processing shader variants...");

        Setup();
        UpdateVariantListToPreCompile();
        UpdateVariantListToStrip();

        Debug.Log("Shader variant processing complete.");
    }

    private static void Setup()
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            Debug.Log($"Created directory: {directory}");
        }
        
        if (File.Exists(ReportPath))
        {
            File.SetAttributes(ReportPath, FileAttributes.Normal);
        }
        
        File.WriteAllText(ReportPath, string.Empty);

        _settings = GetSettings();
        if (!_settings)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Editor")) AssetDatabase.CreateFolder("Assets", "Editor");
            if (!AssetDatabase.IsValidFolder("Assets/Editor/ShaderPrewarming"))
                AssetDatabase.CreateFolder("Assets/Editor", "ShaderPrewarming");

            _settings = ScriptableObject.CreateInstance<ShaderPreCompilerSettings>();
            AssetDatabase.CreateAsset(_settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
        }
        
        if (!_settings.warmupSvc)
        {
            _settings.warmupSvc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(_settings.warmupSvcPath);
            
            if (_settings.warmupSvc == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Shaders"))
                {
                    AssetDatabase.CreateFolder("Assets", "Shaders");
                }

                _settings.warmupSvc = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(_settings.warmupSvc, _settings.warmupSvcPath);
            }
        }
        
        if (!File.Exists(_settings.logFilePath))
        {
            File.WriteAllText(_settings.logFilePath, string.Empty);
        }
        
        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }

    private static void UpdateVariantListToPreCompile()
    {
        _variantWarmupDataList = ShaderVariantParser.ParseShaderVariantsFromFile(_settings);
        var variantDatas = new List<ShaderVariantData>(_settings.manualShaderVariantsData ?? new List<ShaderVariantData>());

        foreach (ShaderVariantData variantData in _variantWarmupDataList)
        {
            if (variantData.uploadTime < _settings.minUploadTime)
            {
                Debug.LogWarning(
                    $"[SKIPPED] Shader: {variantData.shader.name}\n" +
                    $"Upload time ({variantData.uploadTime} ms) is below the threshold ({_settings.minUploadTime} ms).");
                continue;
            }

            if (_settings.skipMultipleUploads && variantData.uploadCount > 1)
            {
                Debug.LogWarning(
                    $"[SKIPPED] Shader: {variantData.shader.name}\n" +
                    $"Uploaded {variantData.uploadCount} times, indicating potential differences in vertex layout data.");
                continue;
            }

            // Uncomment to only include Addressables shaders: If svc is in addressables, any shaders included will be addressables
            
            // var shaderPath = AssetDatabase.GetAssetPath(variantData.shader);
            // var shaderGuid = AssetDatabase.AssetPathToGUID(shaderPath);
            // var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(shaderGuid);
            //
            // if (entry == null)
            // {
            //     Debug.LogWarning($"[SKIPPED] Shader: {variantData.shader.name} is not addressable");
            //     continue;
            // }

            variantDatas.Add(variantData);
        }

        _settings.warmupSvc.Clear();
        foreach (var variant in variantDatas)
        {
            _settings.warmupSvc.Add(new ShaderVariantCollection.ShaderVariant
            {
                shader = variant.shader, passType = variant.passType, keywords = variant.keywords
            });
        }

        EditorUtility.SetDirty(_settings.warmupSvc);
        AssetDatabase.SaveAssets();
    }

    private static void UpdateVariantListToStrip(bool importRuntimeKeywords = false)
    {
        if (!_settings.strippingEnabled) return;
        _settings.localKeywords ??= new List<ShaderKeywordsData>();

        var uniqueVariants = new HashSet<string>();
        var variantList = new List<ShaderKeywordsData>();

        if (importRuntimeKeywords)
        {
            ImportRuntimeCollectedKeywords(variantList, uniqueVariants);
        }
        else
        {
            foreach (var existing in _settings.localKeywords)
            {
                if (existing.shader == null) continue;
                var localKeywords = existing.keywords.Where(k => !_settings.enabledGlobalKeywords.Contains(k))
                    .OrderBy(k => k).ToArray();
                var uniqueKey = $"{existing.shader.name}|{string.Join("|", localKeywords)}";

                if (uniqueVariants.Add(uniqueKey)) variantList.Add(existing);
            }
        }

        foreach (var variant in _variantWarmupDataList)
        {
            AddKeywordsUnique(variantList, uniqueVariants, variant.shader, variant.keywords);
        }

        foreach (var variant in _settings.manualShaderVariantsData)
        {
            AddKeywordsUnique(variantList, uniqueVariants, variant.shader, variant.keywords);
        }

        var materialGuids = AssetDatabase.FindAssets("t:Material");
        foreach (var guid in materialGuids)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            AddKeywordsUnique(variantList, uniqueVariants, material.shader,
                material.enabledKeywords.Select(keyword => keyword.name).ToArray());
        }

        var resourcesMaterials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (var material in resourcesMaterials)
        {
            AddKeywordsUnique(variantList, uniqueVariants, material.shader,
                material.enabledKeywords.Select(keyword => keyword.name).ToArray());
        }

        _settings.localKeywords = variantList
            .OrderBy(x => x.shader != null ? x.shader.name : "")
            .ToList();

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }

    private static void ImportRuntimeCollectedKeywords(List<ShaderKeywordsData> variantList, HashSet<string> uniqueVariants)
    {
        string runtimeDataPath = "Temp/RuntimeCollectedKeywords.txt";
        if (!File.Exists(runtimeDataPath))
            return;

        string[] lines = File.ReadAllLines(runtimeDataPath);

        foreach (var line in lines)
        {
            var parts = line.Split('|');
            if (parts.Length != 2) continue;

            var shaderName = parts[0];
            var keywords = parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var shader = Shader.Find(shaderName);
            AddKeywordsUnique(variantList, uniqueVariants, shader, keywords);
        }
    }

    private static void AddKeywordsUnique(List<ShaderKeywordsData> variantList, HashSet<string> uniqueVariants,
        Shader shader, string[] keywords)
    {
        if (IgnoreShader(shader)) return;

        var localKeywords = keywords.Where(k => !_settings.enabledGlobalKeywords.Contains(k))
            .OrderBy(k => k).ToArray();

        var uniqueKey = $"{shader.name}|{string.Join("|", localKeywords)}";

        if (uniqueVariants.Add(uniqueKey))
            variantList.Add(new ShaderKeywordsData { shader = shader, keywords = localKeywords });
    }

    private static readonly string[] IgnoredShaders = {
        "Unlit/Texture",
        "UI/Default", "UI/Additive",
        "Sprites/Default", "Sprites/Mask",
        "Skybox/Cubemap", "Skybox/Procedural"
    };

    public static bool IgnoreShader(Shader shader)
    {
        return IgnoredShaders.Contains(shader.name) ||
               shader.name.Contains("TextMeshPro") || shader.name.StartsWith("Hidden");
    }
}
