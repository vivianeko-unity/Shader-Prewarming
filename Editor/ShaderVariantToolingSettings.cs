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
    [Tooltip("ShadersLog.txt file.")]
    public TextAsset logFile;
    public string LogFilePath => logFile ? AssetDatabase.GetAssetPath(logFile) : "Assets/Editor/ShadersLog.txt";

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

    [Tooltip("The shader variant collection to be pre-compiled.")]
    public ShaderVariantCollection warmupSvc;
    private string WarmupSvcPath => warmupSvc ? AssetDatabase.GetAssetPath(warmupSvc) : "Assets/Shaders/ShaderVariantsToPreCompile.shadervariants";

    [Tooltip("If adding variants manually to warmup")]
    public List<ShaderVariantData> manualShaderVariantsData;

    [Header("Stripping")]
    [Tooltip(
        "When enabled, preserves manually added keyword variants. When disabled, resets to only variants found in the current log and project materials.")]
    public bool keepExistingLocalKeywords = true;

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
    
    private void Reset()
    {
        if (!_instance) return;
        _instance.ValidateLogFile();
        _instance.ValidateWarmupSvc();
    }

    private void ValidateLogFile()
    {
        logFile = AssetDatabase.LoadAssetAtPath<TextAsset>(LogFilePath);
        if (logFile) return;

        File.WriteAllText(LogFilePath, startingLine + Environment.NewLine);
        AssetDatabase.Refresh();
        logFile = AssetDatabase.LoadAssetAtPath<TextAsset>(LogFilePath);
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