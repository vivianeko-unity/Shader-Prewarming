using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates new shader variants to be pre-warmed by ShaderPreCompiler.
/// Filters variants based on upload time and occurrence count.
/// </summary>
public class ShaderVariantsProcessor
{
    public static void UpdateVariantListToPreCompile()
    {
        var settings = Resources.Load<ShaderPreCompilerSettings>($"{nameof(ShaderPreCompilerSettings)}");

        List<ShaderVariantData> variantDataList = ShaderVariantParser.ParseShaderVariantsFromFile(settings);
        List<ShaderVariantData> variants = new (settings.manualShaderVariantsData);

        foreach (ShaderVariantData variantData in variantDataList)
        {
            if (variantData.uploadTime < settings.minUploadTime)
            {
                Debug.LogWarning(
                    $"[SKIPPED] Shader: {variantData.shader.name}\n" +
                    $"Upload time ({variantData.uploadTime} ms) is below the threshold ({settings.minUploadTime} ms).");
            }
            else if (settings.skipMultipleUploads && variantData.uploadCount > 1)
            {
                Debug.LogWarning(
                    $"[SKIPPED] Shader: {variantData.shader.name}\n" +
                    $"Uploaded {variantData.uploadCount} times, indicating potential differences in vertex layout data.");
            }
            else
            {
                variants.Add(variantData);
            }
        }

        settings.shaderVariantCollection.Clear();
        foreach (var variant in variants)
        {
            settings.shaderVariantCollection.Add(new ShaderVariantCollection.ShaderVariant
            {
                shader = variant.shader, passType = variant.passType, keywords = variant.keywords
            });
        }
    }
}
