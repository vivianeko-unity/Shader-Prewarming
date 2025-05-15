using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;

public class LogCompiledShaderVariants : IPreprocessShaders
{
    private readonly string _logFilePath;

    public LogCompiledShaderVariants()
    {
        _logFilePath = Path.Combine("Assets/CompiledShaderVariantsReport.txt");
        if (File.Exists(_logFilePath))
        {
            File.Delete(_logFilePath);
        }
    }

    public int callbackOrder => 0;

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        using var writer = new StreamWriter(_logFilePath, true);
        
        writer.WriteLine($"- {shader.name} | {snippet.passName} | {snippet.passType} | {snippet.shaderType}");
        for (var i = data.Count - 1; i >= 0; i--)
        {
            writer.WriteLine($"      - " +
                             $"{data[i].graphicsTier} | " +
                             $"{data[i].shaderCompilerPlatform} | " +
                             $"{data[i].buildTarget} | " +
                             $"{string.Join(" ", data[i].shaderKeywordSet.GetShaderKeywords())}");
        }
    }
}
