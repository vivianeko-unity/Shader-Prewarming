using System;
using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

/// <summary>
/// Example for processing shader variants and integrating with build pipeline
/// Can be added to any existing build scripts instead.
/// </summary>

public class ShaderVariantsProcessorTools : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
        try
        {
#if DEVELOPMENT_BUILD || DEBUG_SHADER_PRECOMPILER
            GraphicsSettings.logWhenShaderIsCompiled = true;
#endif
            ShaderVariantsProcessor.ProcessShaderVariants();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to update ShaderVariantsCollections: {ex.Message}\n{ex.StackTrace}");
        }
    }
}