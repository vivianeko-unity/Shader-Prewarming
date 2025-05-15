using System.Collections.Generic;
using UnityEngine;

public class ShaderPreCompilerSettings : ScriptableObject
{
    [Header("Log Parsing")]
    [Tooltip("Path to ShadersLog.txt file.")]
    public string logFilePath = "Packages/com.shaderprewarming.shaderprewarming/Editor/ShadersLog.txt";
    [Tooltip("The beginning part of each line when a shader variant is uploaded to the gpu.")]
    public string lineBeginning = "Uploaded shader variant to the GPU driver:";

    [Header("Filtering")]
    [Tooltip("Minimum upload time (in ms) for a shader variant to be included.")]
    public float minUploadTime = 0;
    [Tooltip("Skip shader variants that were uploaded multiple times, indicating potential differences in vertex layout data.")]
    public bool skipMultipleUploads = true;

    [Tooltip("The line when parsing should start so we only pre-compile variants uploaded after the loading screen")]
    public string startingLine = "ShaderPreCompiler: Disabled, debugging variants to pre-compile.";

    [Tooltip("If adding variants manually")]
    public List<ShaderVariantData> manualShaderVariantsData;

    [Tooltip("The shader variant collection to be pre-compiled.")]
    public string SVCPath = "Packages/com.shaderprewarming.shaderprewarming/Runtime/ShaderVariantsToPreCompile.shadervariants";
    public ShaderVariantCollection shaderVariantCollection;
}
