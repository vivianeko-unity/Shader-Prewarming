using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ShaderVariantToolingSettings : ScriptableObject
{
    private static ShaderVariantToolingSettings _instance;
    public static ShaderVariantToolingSettings Instance => LoadOrCreate();

    [Header("Log Parsing")]
    [Tooltip("Path to ShadersLog.txt file.")]
    public string logFilePath = "Assets/Editor/ShadersLog.txt";

    [Tooltip("The beginning part of each line when a shader variant is uploaded to the gpu.")]
    public string lineBeginning = "Uploaded shader variant to the GPU driver:";

    [Header("Filtering")]
    [Tooltip("Minimum upload time (in ms) for a shader variant to be included.")]
    public float minUploadTime = 0;

    [Tooltip(
        "Skip shader variants that were uploaded multiple times, indicating potential differences in vertex layout data.")]
    public bool skipMultipleUploads = true;

    [Tooltip("The line when parsing should start so we only pre-compile variants uploaded after the loading screen")]
    public string startingLine = "ShaderPreCompiler: Disabled, debugging variants to pre-compile.";

    [Tooltip("If adding variants manually to warmup")]
    public List<ShaderVariantData> manualShaderVariantsData;

    [Tooltip("The shader variant collection to be pre-compiled.")]
    private const string WarmupSvcPath = "Assets/Shaders/ShaderVariantsToPreCompile.shadervariants";

    public ShaderVariantCollection warmupSvc;

    [Header("Shader variants stripping settings.")]
    public bool strippingEnabled = true;

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

        return _instance;
    }

    private void ValidateLogFile()
    {
        if (File.Exists(logFilePath)) return;
        File.WriteAllText(logFilePath, startingLine + Environment.NewLine);
        AssetDatabase.Refresh();
    }

    private void ValidateWarmupSvc()
    {
        warmupSvc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(WarmupSvcPath);
        if (warmupSvc) return;

        string directoryName = Path.GetDirectoryName(WarmupSvcPath);
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        warmupSvc = new ShaderVariantCollection();
        AssetDatabase.CreateAsset(warmupSvc, WarmupSvcPath);
        AssetDatabase.SaveAssets();
    }
}