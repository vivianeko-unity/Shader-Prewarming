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

    public enum WarmupMethod { ShaderVariantCollection, GraphicsStateCollection, Both }

    [Header("Main Settings")]
    [Tooltip("Method used for pre-warming.")]
    public WarmupMethod warmupMethod = WarmupMethod.Both;

    [Tooltip("Enable shader variant stripping at build time for addressables and player builds.")]
    public bool strippingEnabled = true;

    [Header("Assets")]
    [Tooltip("Shader log file")]
    [SerializeField] private TextAsset logFile;

    [Tooltip("Precompiler Prefab")]
    [SerializeField] private GameObject precompilerPrefab;

    [Tooltip("Path in project to store generated graphics state collections.")]
    public string graphicsStateCollectionFolderPath = "Shaders/GraphicsStateCollections/";

    [Tooltip("ShaderVariantCollection used for pre-warming.")]
    [SerializeField] private ShaderVariantCollection warmupShaderVariantCollection;
    public ShaderVariantCollection WarmupShaderVariantCollection => warmupShaderVariantCollection;

    [Header("SVC Warmup Settings")]
    [Tooltip("Minimum upload time (in ms) for a shader variant to be included in warmupShaderVariantCollection.")]
    public float minUploadTime = 0;

    [Tooltip(
        "Does not include shader variants that were uploaded multiple times to warmupShaderVariantCollection, " +
        "indicating potential differences in vertex layout data.")]
    public bool skipMultipleUploads = true;

    [Header("Stripping Data")]

    [Tooltip(
        "Keep added local keywords from previous processing. When disabled, only use keywords from current log and project materials.")]
    public bool keepExistingLocalKeywords = true;
    [Tooltip("Per shader local keyword combinations. Used only for stripping to keep matching variants.")]
    public List<ShaderKeywordsData> localKeywords;

    [Tooltip("Project wide global keywords. Used only for stripping to filter out global keywords when matching local combinations.")]
    public List<string> enabledGlobalKeywords;

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
        EditorApplication.delayCall += ShaderVariantsProcessor.GetAllEnabledGlobalKeywords;
        EditorApplication.delayCall += ShaderVariantsProcessor.ProcessShaderVariants;
        return _instance;
    }

    private void Reset()
    {
        ValidateLogFile();
        ValidateWarmupSvc();
        EditorApplication.delayCall += ValidatePrecompilerPrefab;
        EditorApplication.delayCall += ShaderVariantsProcessor.GetAllEnabledGlobalKeywords;
        EditorApplication.delayCall += ShaderVariantsProcessor.ProcessShaderVariants;
    }

    private void OnValidate()
    {
        ValidateLogFile();
        ValidateWarmupSvc();
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
        if (warmupShaderVariantCollection)
        {
            shaderVariantCollectionPath = AssetDatabase.GetAssetPath(warmupShaderVariantCollection);
            return;
        }

        string path = shaderVariantCollectionPath;
        warmupShaderVariantCollection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        if (warmupShaderVariantCollection) return;

        string directoryName = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        warmupShaderVariantCollection = new ShaderVariantCollection();
        AssetDatabase.CreateAsset(warmupShaderVariantCollection, path);
        AssetDatabase.SaveAssets();
    }

    public void ValidatePrecompilerPrefab()
    {
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
        svcPreCompiler.shaderVariantCollection = warmupShaderVariantCollection;
        svcPreCompiler.methodEnabled =
            warmupMethod == WarmupMethod.ShaderVariantCollection || warmupMethod == WarmupMethod.Both;

        prefab.TryGetComponent(out GraphicsStateCollectionPreCompiler gscPreCompiler);
        if (!gscPreCompiler) gscPreCompiler = prefab.AddComponent<GraphicsStateCollectionPreCompiler>();
        gscPreCompiler.graphicsStateCollectionFolderPath = graphicsStateCollectionFolderPath;
        gscPreCompiler.methodEnabled =
            warmupMethod == WarmupMethod.GraphicsStateCollection || warmupMethod == WarmupMethod.Both;

        // Update graphicsStateCollections List
        string directoryName = "Assets/" + graphicsStateCollectionFolderPath.TrimEnd('/');
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);
        AssetDatabase.Refresh();

        string[] guids = AssetDatabase.FindAssets("t:GraphicsStateCollection",
                                                  new[] { "Assets/" + graphicsStateCollectionFolderPath });
        gscPreCompiler.graphicsStateCollections = new GraphicsStateCollection[guids.Length];
        for (var i = 0; i < guids.Length; i++)
        {
            gscPreCompiler.graphicsStateCollections[i] =
                AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(AssetDatabase.GUIDToAssetPath(guids[i]));
        }
    }

    [ContextMenu("Collect Global Keywords From Editor")]
    private void RefreshGlobalKeywords()
    {
        ShaderVariantsProcessor.GetAllEnabledGlobalKeywords();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [ContextMenu("Process Shader Variants")]
    private void ReprocessShaderVariants()
    {
        ShaderVariantsProcessor.ProcessShaderVariants();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}