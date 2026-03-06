using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

/// <summary>
/// Stripped variants at build time for addressables and player builds
/// </summary>
public class StripShaderVariants : IPreprocessShaders
{
    private static ShaderVariantToolingSettings _settings;
    private static HashSet<string> _globalKeywords;
    private static bool _initialized;

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (!_initialized)
        {
            File.SetAttributes(ShaderVariantsProcessor.ReportPath, FileAttributes.Normal);
            _settings = ShaderVariantToolingSettings.Instance;
            _globalKeywords = new HashSet<string>(_settings.enabledGlobalKeywords ?? new List<string>());
            _initialized = true;
        }

        for (int i = data.Count - 1; i >= 0; i--)
        {
            string[] allKeywords = data[i].shaderKeywordSet.GetShaderKeywords().Select(k => k.name).ToArray();
            string[] localKeywords = allKeywords.Where(k => !_globalKeywords.Contains(k)).ToArray();

            if (ShouldStripVariant(shader, localKeywords))
            {
                data.RemoveAt(i);
            }
            else
            {
                string variantLog = $"Compiled: {shader.name}|" +
                                    $"Graphics:{data[i].graphicsTier}|" +
                                    $"Platform:{data[i].shaderCompilerPlatform}|BuildTarget:{data[i].buildTarget}|" +
                                    $"{snippet.passType}|{snippet.passName}|{snippet.shaderType}|" +
                                    $"{string.Join(" ", allKeywords)}\n";

                File.AppendAllText(ShaderVariantsProcessor.ReportPath, variantLog);
            }
        }
    }

    private static bool ShouldStripVariant(Shader shader, string[] localKeywords)
    {
        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants) return false;
        
        if (!_settings.strippingEnabled) return false;

        if (ShaderVariantsProcessor.IgnoreShader(shader, localKeywords)) return false;

        if (ShaderVariantsProcessor.StripShader(shader)) return true;

        var hasMatch = false;
        foreach (ShaderKeywordsData keywordData in _settings.localKeywords)
        {
            if (keywordData.shader != shader) continue;
            if (new HashSet<string>(keywordData.keywords).SetEquals(new HashSet<string>(localKeywords)))
            {
                hasMatch = true;
                break;
            }
        }

        return !hasMatch;
    }
}