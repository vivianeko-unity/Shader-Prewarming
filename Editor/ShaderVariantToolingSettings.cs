using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class ShaderVariantToolingSettings : ScriptableObject
{
    private static ShaderVariantToolingSettings _instance;
    public static ShaderVariantToolingSettings Instance => LoadOrCreate();
    
    [HideInInspector] public string shaderVariantCollectionPath = "Assets/Shaders/ShaderVariantsToPreCompile.shadervariants";
    [HideInInspector] public string logFilePath = "Assets/Editor/ShadersLog.txt";
    [HideInInspector] public string precompilerPrefabPath = "Assets/ShaderVariantToolingPrecompiler.prefab";

    [Header("Log Parsing")]
    [Tooltip("Shader log file")]
    [SerializeField] private TextAsset logFile;

    [Header("Filtering")]
    [Tooltip("Minimum upload time (in ms) for a shader variant to be included.")]
    public float minUploadTime = 0;

    [Tooltip(
        "Skip shader variants that were uploaded multiple times, indicating potential differences in vertex layout data.")]
    public bool skipMultipleUploads = true;

    [Header("Pre-warming")]
    [Tooltip("Path in project to store generated graphics state collections.")]
    public string gscFolderPath = "Shaders/GraphicsStateCollections/";

    [Tooltip("ShaderVariantCollection used for pre-warming.")]
    [SerializeField] private ShaderVariantCollection warmupSvc;
    public ShaderVariantCollection WarmupSvc => warmupSvc;

    [Tooltip("If adding variants manually to warmup")]
    public List<ShaderVariantData> manualShaderVariantsData;

    [Tooltip("Precompiler Prefab")]
    [SerializeField] private GameObject precompilerPrefab;

    [Header("Stripping")]
    [Tooltip("Enable shader variant stripping at build time for addressables and player builds.")]
    public bool strippingEnabled = true;

    [Tooltip(
        "Disabled: variants found in the current log and project materials only and remove manually added variants.")]
    public bool keepExistingLocalKeywords = true;

    [Space] public List<string> enabledGlobalKeywords;
    [Space] public List<ShaderKeywordsData> localKeywords;

    private static ShaderVariantToolingSettings LoadOrCreate()
    {
        if (_instance) return _instance;

        string[] guids = AssetDatabase.FindAssets("t:ShaderVariantToolingSettings");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            _instance = AssetDatabase.LoadAssetAtPath<ShaderVariantToolingSettings>(path);
            return _instance;
        }

        const string directoryName = "Assets/Editor";
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        _instance = CreateInstance<ShaderVariantToolingSettings>();
        AssetDatabase.CreateAsset(_instance, "Assets/Editor/ShaderVariantToolingSettings.asset");
        AssetDatabase.SaveAssets();

        _instance.ValidateLogFile();
        _instance.ValidateWarmupSvc();
        EditorApplication.delayCall += _instance.ValidatePrecompilerPrefab;

        return _instance;
    }

    private void Reset()
    {
        ValidateLogFile();
        ValidateWarmupSvc();
        EditorApplication.delayCall -= ValidatePrecompilerPrefab;
        EditorApplication.delayCall += ValidatePrecompilerPrefab;
    }

    private void OnValidate()
    {
        ValidateLogFile();
        ValidateWarmupSvc();
        EditorApplication.delayCall -= ValidatePrecompilerPrefab;
        EditorApplication.delayCall += ValidatePrecompilerPrefab;
    }

    private void ValidateLogFile()
    {
        if (logFile)
        {
            logFilePath = AssetDatabase.GetAssetPath(logFile);
            return;
        }

        string path = logFilePath;
        logFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        if (logFile) return;

        File.WriteAllText(path, ShaderVariantToolingConstants.LogRecordingMarker + Environment.NewLine);
        AssetDatabase.Refresh();
        logFile = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        AssetDatabase.ImportAsset(path);
    }

    private void ValidateWarmupSvc()
    {
        if (warmupSvc)
        {
            shaderVariantCollectionPath = AssetDatabase.GetAssetPath(warmupSvc);
            return;
        }

        string path = shaderVariantCollectionPath;
        warmupSvc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        if (warmupSvc) return;

        string directoryName = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        warmupSvc = new ShaderVariantCollection();
        AssetDatabase.CreateAsset(warmupSvc, path);
        AssetDatabase.SaveAssets();
    }

    public void ValidatePrecompilerPrefab()
    {
        EditorApplication.delayCall -= ValidatePrecompilerPrefab;
        if (precompilerPrefab)
        {
            precompilerPrefabPath = AssetDatabase.GetAssetPath(precompilerPrefab);

            UpdatePrecompilerPrefab(precompilerPrefab);
            PrefabUtility.SavePrefabAsset(precompilerPrefab);
            AssetDatabase.SaveAssets();

            return;
        }

        string path = precompilerPrefabPath;
        precompilerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (precompilerPrefab)
        {
            UpdatePrecompilerPrefab(precompilerPrefab);
            PrefabUtility.SavePrefabAsset(precompilerPrefab);
            AssetDatabase.SaveAssets();
            return;
        }

        var prefab = new GameObject("ShaderVariantToolingPrecompiler");

        UpdatePrecompilerPrefab(prefab);
        precompilerPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, path);
        DestroyImmediate(prefab);
        AssetDatabase.SaveAssets();
    }

    private void UpdatePrecompilerPrefab(GameObject prefab)
    {
        prefab.TryGetComponent(out ShaderVariantToolingManager manager);
        if (!manager) manager = prefab.AddComponent<ShaderVariantToolingManager>();
        
        prefab.TryGetComponent(out ShaderVariantCollectionPreCompiler svcPreCompiler);
        if (!svcPreCompiler) svcPreCompiler = prefab.AddComponent<ShaderVariantCollectionPreCompiler>();
        svcPreCompiler.shaderVariantCollection = warmupSvc;

        prefab.TryGetComponent(out GraphicsStateCollectionPreCompiler gscPreCompiler);
        if (!gscPreCompiler) gscPreCompiler = prefab.AddComponent<GraphicsStateCollectionPreCompiler>();
        gscPreCompiler.graphicsStateCollectionFolderPath = gscFolderPath;

        // Update graphicsStateCollections List
        string directoryName = "Assets/" + gscFolderPath.TrimEnd('/');
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);
        AssetDatabase.Refresh();

        string[] guids = AssetDatabase.FindAssets("t:GraphicsStateCollection",
                                                  new[] { "Assets/" + gscFolderPath });
        gscPreCompiler.graphicsStateCollections = new GraphicsStateCollection[guids.Length];
        for (var i = 0; i < guids.Length; i++)
        {
            gscPreCompiler.graphicsStateCollections[i] =
                AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(AssetDatabase.GUIDToAssetPath(guids[i]));
        }
    }
}