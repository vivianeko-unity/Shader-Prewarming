using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Parses the ShadersLog.txt file to extract shader variant data uploaded at runtime.
/// To update the ShadersLog.txt file check stopPreCompiling in ShaderPreCompilerSettings asset.
/// </summary>
public static class ShaderVariantParser
{
    private static ShaderPreCompilerSettings _settings;
    public static List<ShaderVariantData> ParseShaderVariantsFromFile(ShaderPreCompilerSettings settings)
    {
        _settings = settings;

        if (string.IsNullOrEmpty(_settings.logFilePath) || !File.Exists(_settings.logFilePath))
        {
            Debug.LogError("ShadersLog file is missing or empty.");
            return null;
        }

        List<ShaderVariantData> variantDataList = new();
        Dictionary<string, ShaderVariantData> variantOccurrences = new();

        string[] filteredLines = PreprocessLogFile();
        foreach (string line in filteredLines)
        {
            ShaderVariantData variantData = ParseLine(line);
            if (variantData == null)
            {
                continue;
            }

            string uniqueKey = GenerateUniqueKey(variantData);
            if (variantOccurrences.TryGetValue(uniqueKey, out ShaderVariantData existingVariantData))
            {
                existingVariantData.uploadCount++;
            }
            else
            {
                variantData.uploadCount = 1;
                variantOccurrences[uniqueKey] = variantData;
                variantDataList.Add(variantData);
            }
        }

        return variantDataList;
    }

    private static string[] PreprocessLogFile()
    {
        try
        {
            string[] lines = File.ReadAllLines(_settings.logFilePath);
            int startingLineIndex = Array.FindIndex(lines, line => line == _settings.startingLine);

            List<string> filteredLines =
                lines.Skip(startingLineIndex + 1).Where(line => line.Contains(_settings.lineBeginning)).ToList();

            // cleanup log file
            StringBuilder sb = new ();
            sb.AppendLine(_settings.startingLine);
            sb.Append(string.Join(Environment.NewLine, filteredLines));
            File.WriteAllText(_settings.logFilePath, sb.ToString());

            return filteredLines.ToArray();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to preprocess the log file: {ex.Message}");
            return null;
        }
    }

    private static ShaderVariantData ParseLine(string line)
    {
        try
        {
            // extract shader
            int shaderNameStart = line.IndexOf(_settings.lineBeginning, StringComparison.Ordinal) +
                                  _settings.lineBeginning.Length;
            int shaderNameEnd = line.Contains("(instance")
                ? line.IndexOf(" (instance", shaderNameStart, StringComparison.Ordinal)
                : line.IndexOf(", pass:", shaderNameStart, StringComparison.Ordinal);
            string shaderName = line.Substring(shaderNameStart, shaderNameEnd - shaderNameStart).Trim();
            Shader shader = Shader.Find(shaderName);
            if (shader is null)
            {
                Debug.LogWarning($"Shader {shader.name} was not found.");
            }
            
            // extract passType
            int passStart = line.IndexOf("pass: ", StringComparison.Ordinal) + "pass: ".Length;            
            int passEnd = line.Contains(", stage:")
                ? line.IndexOf(", stage:", passStart, StringComparison.Ordinal)
                : line.IndexOf(", keywords", passStart, StringComparison.Ordinal);
            string passName = line.Substring(passStart, passEnd - passStart).Trim();
            (PassType passType, string lightMode) = GetPassType(shader, passName);

            // extract keywords
            int keywordsStart = line.IndexOf("keywords ", StringComparison.Ordinal) + "keywords ".Length;        
            string keywords;
            if (line.Contains(", time:"))
            {
                int keywordsEnd = line.IndexOf(", time", keywordsStart, StringComparison.Ordinal);
                keywords = line.Substring(keywordsStart, keywordsEnd - keywordsStart).Trim();
            }
            else
            {
                // No time field
                keywords = line.Substring(keywordsStart).Trim();
            }
            string[] keywordArray = keywords == "<no keywords>" ? Array.Empty<string>() : keywords.Split(' ');

            // extract upload time
            float duration = 0;
            if (line.Contains(", time:"))
            {
                int timeStart = line.IndexOf("time: ", StringComparison.Ordinal) + "time: ".Length;
                int timeEnd = line.IndexOf(" ms", timeStart, StringComparison.Ordinal);
                string timeString = line.Substring(timeStart, timeEnd - timeStart).Trim();
                duration = float.Parse(timeString);
            }

            /*Debug.Log(
                $"Found variant in log: {shaderName} | passType: {passType} | PassName: {passName} | " +
                $"LightMode: {lightMode} | keywords: {string.Join(" ", keywordArray)} | duration: {duration} ms");*/

            return new ShaderVariantData
            {
                shader = shader, passType = passType, keywords = keywordArray, uploadTime = duration
            };
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to parse variant for line: {line}\nError: {ex.Message}");
            return null;
        }
    }

    private static (PassType passType, string lightMode) GetPassType(Shader shader, string passName)
    {
        string lightMode = "";
        ShaderData shaderData = ShaderUtil.GetShaderData(shader);
        ShaderData.Subshader subshader = shaderData.ActiveSubshader;

        for (int i = 0; i < subshader.PassCount; i++)
        {
            ShaderData.Pass pass = subshader.GetPass(i);
            if (pass.Name == passName)
            {
                ShaderTagId lightModeTag = pass.FindTagValue((ShaderTagId)"LightMode");
                lightMode = lightModeTag != ShaderTagId.none ? lightModeTag.name : "";
                break;
            }
        }

        PassType passType = lightMode.ToUpperInvariant() switch
        {
            "SRPDEFAULTUNLIT" => PassType.ScriptableRenderPipelineDefaultUnlit,
            "UNIVERSALFORWARD" => PassType.ScriptableRenderPipeline,
            "SHADOWCASTER" => PassType.ShadowCaster,
            "" => PassType.Normal,
            _ => PassType.ScriptableRenderPipeline
        };

        return (passType, lightMode);
    }

    private static string GenerateUniqueKey(ShaderVariantData shaderVariantData)
    {
        return $"{shaderVariantData.shader.name}|" + $"{shaderVariantData.passType.ToString()}|" +
               $"{string.Join(" ", shaderVariantData.keywords)}";
    }
}
