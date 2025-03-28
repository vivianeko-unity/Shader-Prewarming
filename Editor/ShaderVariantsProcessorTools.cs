using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

/// <summary>
/// Example editor tools for processing shader variants and integrating with build pipeline
/// ShaderVariantsProcessor.UpdateVariantListToPreCompile(); can be added to any existing build scripts instead.
/// </summary>

public class ShaderVariantsProcessorTools : IPreprocessBuildWithReport
{
    [MenuItem("Tools/Shader Variants Processor")]
    public static void ProcessShaderVariants()
    {
        Debug.Log("Processing shader variants...");
        ShaderVariantsProcessor.UpdateVariantListToPreCompile();
        Debug.Log("Shader variant processing complete.");
    }

    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report)
    {
#if DEBUG_SHADER_VARIANTS || DEBUG_SHADER_PRECOMPILER
        GraphicsSettings.logWhenShaderIsCompiled = true;
#endif
        ProcessShaderVariants();
    }
}