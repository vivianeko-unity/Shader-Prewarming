using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// Generates new shader variants to be pre-warmed by ShaderVariantCollectionPreCompiler.
/// Filters variants based on upload time and occurrence count.
/// Generates shader variants to be kept during stripping, based on project materials.
/// </summary>
public class ShaderVariantsProcessor
{
    public static readonly string ReportPath = Path.GetFullPath("Artifact/ShaderVariantsCompiled.txt");
    private static ShaderVariantToolingSettings _settings;
    private static List<ShaderVariantData> _variantWarmupDataList = new();

    // This should be called during builds
    [MenuItem("Tools/Shader Variants Tools/Shader Variants Processor")]
    public static void ProcessShaderVariants()
    {
        Debug.Log("[ShaderVariantsProcessor] Processing shader variants...");

        Setup();
        UpdateVariantListToPreCompile();
        Debug.Log("[ShaderVariantsProcessor] Variant list to pre-compile updated");

        UpdateVariantListToStrip();
        Debug.Log("[ShaderVariantsProcessor] Variant list to strip updated");

        Debug.Log("[ShaderVariantsProcessor] Shader variant processing complete");
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = _settings;
        EditorGUIUtility.PingObject(_settings);
    }

    private static void Setup()
    {
        _settings = ShaderVariantToolingSettings.Instance;

        string directoryName = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);

        if (File.Exists(ReportPath)) File.SetAttributes(ReportPath, FileAttributes.Normal);

        File.WriteAllText(ReportPath, string.Empty);
    }

    private static void UpdateVariantListToPreCompile()
    {
        _variantWarmupDataList = ShaderVariantParser.ParseShaderVariantsFromFile();
        _settings.manualShaderVariantsData ??= new List<ShaderVariantData>();
        var variantDatas = new List<ShaderVariantData>(_settings.manualShaderVariantsData);

        foreach (ShaderVariantData variantData in _variantWarmupDataList)
        {
            if (variantData.uploadTime < _settings.minUploadTime) continue;

            if (_settings.skipMultipleUploads && variantData.uploadCount > 1) continue;

            // NOTE[Addressables]:
            // Uncomment below to only include Addressables shaders If svc is in addressables, any shaders included will be addressables

            // var shaderPath = AssetDatabase.GetAssetPath(variantData.shader);
            // var shaderGuid = AssetDatabase.AssetPathToGUID(shaderPath);
            // var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(shaderGuid);
            //
            // if (entry == null)
            // {
            //     Debug.LogWarning($"[ShaderVariantsProcessor] [SKIPPED] Shader: {variantData.shader.name} is not addressable");
            //     continue;
            // }

            variantDatas.Add(variantData);
        }

        _settings.WarmupSvc.Clear();
        foreach (ShaderVariantData variant in variantDatas)
        {
            _settings.WarmupSvc.Add(new ShaderVariantCollection.ShaderVariant
            {
                shader = variant.shader, passType = variant.passType, keywords = variant.keywords
            });
        }

        EditorUtility.SetDirty(_settings.WarmupSvc);
        AssetDatabase.SaveAssets();
    }

    private static void UpdateVariantListToStrip()
    {
        foreach (GlobalKeyword keyword in Shader.enabledGlobalKeywords)
        {
            if (_settings.enabledGlobalKeywords.Contains(keyword.name)) continue;
            _settings.enabledGlobalKeywords.Add(keyword.name);
        }

        _settings.localKeywords ??= new List<ShaderKeywordsData>();

        var uniqueVariants = new HashSet<string>();
        var variantList = new List<ShaderKeywordsData>();

        foreach (ShaderVariantData variant in _variantWarmupDataList)
        {
            AddKeywordsUnique(variantList, uniqueVariants, variant.shader, variant.keywords);
        }

        foreach (ShaderVariantData variant in _settings.manualShaderVariantsData)
        {
            AddKeywordsUnique(variantList, uniqueVariants, variant.shader, variant.keywords);
        }

        string[] materialGuids = AssetDatabase.FindAssets("t:Material");
        foreach (string guid in materialGuids)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            AddKeywordsUnique(variantList, uniqueVariants, material.shader,
                              material.enabledKeywords.Select(keyword => keyword.name).ToArray());
        }

        Material[] resourcesMaterials = Resources.FindObjectsOfTypeAll<Material>();
        foreach (Material material in resourcesMaterials)
        {
            AddKeywordsUnique(variantList, uniqueVariants, material.shader,
                              material.enabledKeywords.Select(keyword => keyword.name).ToArray());
        }

        foreach (ShaderKeywordsData existing in _settings.localKeywords)
        {
            AddKeywordsUnique(variantList, uniqueVariants, existing.shader, existing.keywords,
                              _settings.keepExistingLocalKeywords);
        }

        _settings.localKeywords = variantList
            .OrderBy(x => x.shader != null ? x.shader.name : "")
            .ToList();

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }

    private static void AddKeywordsUnique(List<ShaderKeywordsData> variantList, HashSet<string> uniqueVariants,
        Shader shader, string[] keywords, bool? keepExisting = null)
    {
        string[] localKeywords = keywords.Where(k => !_settings.enabledGlobalKeywords.Contains(k))
            .OrderBy(k => k).ToArray();

        if (shader == null) return;
        if (StripShader(shader)) return;
        if (IgnoreShader(shader, localKeywords)) return;

        var uniqueKey = $"{shader.name}|{string.Join("|", localKeywords)}";
        if (uniqueVariants.Contains(uniqueKey)) return;
        if (keepExisting == false)
        {
            Debug.Log($"[ShaderVariantsProcessor] Removed existing variant: {uniqueKey}");
            return;
        }

        uniqueVariants.Add(uniqueKey);
        variantList.Add(new ShaderKeywordsData { shader = shader, keywords = localKeywords });
        if (keepExisting == true) Debug.Log($"[ShaderVariantsProcessor] Kept existing variant: {uniqueKey}");
    }

    private static readonly string[] IgnoredShaders =
    {
        "Unlit/Texture",
        "UI/Default", "UI/Additive",
        "Sprites/Default", "Sprites/Mask",
        "Skybox/Cubemap", "Skybox/Procedural"
    };

    public static bool IgnoreShader(Shader shader, string[] localKeywords = null) =>
        IgnoredShaders.Contains(shader.name) ||
        shader.name.Contains("TextMeshPro") || shader.name.StartsWith("Hidden") ||
        localKeywords == null || localKeywords.Length == 0;

    private static readonly string[] StripShaders =
    {
        // Add shaders to strip completely, regardless of keywords or usage in materials.
        //"Universal Render Pipeline/Nature/SpeedTree8_PBRLit",
        //"Universal Render Pipeline/Nature/SpeedTree9_URP"
    };

    public static bool StripShader(Shader shader) => StripShaders.Contains(shader.name);
}