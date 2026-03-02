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
/// This will also extract the global keywords used at runtime to use later when stripping.
/// </summary>
public static class ShaderVariantParser
{
    private const string EndLine = "// new log entries below this line in case keeping the previous logs is necessary";
    private static ShaderVariantToolingSettings _settings;
    private static readonly HashSet<string> GlobalKeywordsFoundInLog = new();

    public static List<ShaderVariantData> ParseShaderVariantsFromFile()
    {
        _settings = ShaderVariantToolingSettings.Instance;
        GlobalKeywordsFoundInLog.Clear();
        var allGlobalKeywords = new HashSet<string>(Shader.globalKeywords.Select(keyword => keyword.name));

        List<ShaderVariantData> variantDataList = new();
        Dictionary<string, ShaderVariantData> variantOccurrences = new();

        List<string> filteredLines = PreprocessAndCleanupLogFile();
        foreach (string line in filteredLines)
        {
            CollectGlobalKeywordsFromLine(line, allGlobalKeywords);

            ShaderVariantData variantData = ParseLine(line);
            if (variantData == null) continue;

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

        AddGlobalKeywordsToSettings();
        return variantDataList;
    }

    private static List<string> PreprocessAndCleanupLogFile()
    {
        try
        {
            string[] lines = File.ReadAllLines(_settings.LogFilePath);
            int startingLineIndex = Array.FindIndex(lines, line => line == _settings.startingLine);
            bool hasStartingLine = startingLineIndex >= 0;
            List<string> afterStart = hasStartingLine ? lines.Skip(startingLineIndex + 1).ToList() : lines.ToList();

            int markerIndex = afterStart.FindLastIndex(line => line.Trim() == EndLine);
            List<string> oldRaw, newRaw;

            if (markerIndex >= 0)
            {
                oldRaw = afterStart.Take(markerIndex).ToList();
                newRaw = afterStart.Skip(markerIndex + 1).ToList();
            }
            else
            {
                oldRaw = afterStart;
                newRaw = null;
            }

            List<string> oldSection = oldRaw.Where(line => line.Contains(_settings.lineBeginning))
                .Select(line => {
                    int index = line.IndexOf(_settings.lineBeginning, StringComparison.Ordinal);
                    return index >= 0 ? line.Substring(index).Trim() : line.Trim();
                }).ToList();

            List<string> finalVariantLines;

            if (markerIndex < 0 || newRaw == null)
            {
                finalVariantLines = oldSection;
            }
            else
            {
                List<string> newSection = newRaw.Where(line => line.Contains(_settings.lineBeginning))
                    .Select(line => {
                        int index = line.IndexOf(_settings.lineBeginning, StringComparison.Ordinal);
                        return index >= 0 ? line.Substring(index).Trim() : line.Trim();
                    }).ToList();

                var newKeys = new HashSet<string>();
                foreach (string line in newSection)
                {
                    ShaderVariantData variantData = ParseLine(line);
                    if (variantData == null) continue;
                    newKeys.Add(GenerateUniqueKey(variantData));
                }

                var cleanedOld = new List<string>(oldSection.Count);
                foreach (string line in oldSection)
                {
                    ShaderVariantData variantData = ParseLine(line);
                    if (variantData == null) continue;

                    string key = GenerateUniqueKey(variantData);
                    if (!newKeys.Contains(key)) cleanedOld.Add(line);
                }

                finalVariantLines = new List<string>(newSection.Count + cleanedOld.Count);
                finalVariantLines.AddRange(cleanedOld);
                finalVariantLines.AddRange(newSection);
            }

            CleanupLogFile(finalVariantLines);
            return finalVariantLines;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to preprocess the log file: {ex.Message}");
            return null;
        }
    }

    private static void CleanupLogFile(List<string> filteredLines)
    {
        StringBuilder sb = new();
        sb.AppendLine(_settings.startingLine);
        sb.Append(string.Join(Environment.NewLine, filteredLines));

        if (filteredLines.Count > 0) sb.AppendLine();
        sb.AppendLine(EndLine);

        var newContent = sb.ToString();
        string existingContent = File.ReadAllText(_settings.LogFilePath);
        if (existingContent == newContent) return;

        File.SetAttributes(_settings.LogFilePath, FileAttributes.Normal);
        // if perforce make sure to check out file _settings.logFilePath here
        File.WriteAllText(_settings.LogFilePath, newContent);
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
                Debug.LogWarning($"Shader {shaderName} was not found.");
                return null;
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
        var lightMode = "";
        ShaderData shaderData = ShaderUtil.GetShaderData(shader);
        ShaderData.Subshader subshader = shaderData.ActiveSubshader;

        for (var i = 0; i < subshader.PassCount; i++)
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
            "Meta" => PassType.Meta,
            "" => PassType.Normal,
            _ => PassType.ScriptableRenderPipeline
        };

        return (passType, lightMode);
    }

    private static string GenerateUniqueKey(ShaderVariantData shaderVariantData) =>
        $"{shaderVariantData.shader.name}|" + $"{shaderVariantData.passType.ToString()}|" +
        $"{string.Join(" ", shaderVariantData.keywords)}";

    private static void CollectGlobalKeywordsFromLine(string line, HashSet<string> allGlobalKeywords)
    {
        try
        {
            int keywordsStart = line.IndexOf("keywords ", StringComparison.Ordinal);
            if (keywordsStart == -1) return;

            keywordsStart += "keywords ".Length;
            string keywords;

            if (line.Contains(", time:"))
            {
                int keywordsEnd = line.IndexOf(", time", keywordsStart, StringComparison.Ordinal);
                keywords = line.Substring(keywordsStart, keywordsEnd - keywordsStart).Trim();
            }
            else
            {
                keywords = line.Substring(keywordsStart).Trim();
            }

            if (keywords == "<no keywords>") return;

            string[] keywordArray = keywords.Split(' ');
            foreach (string keyword in keywordArray)
            {
                string cleanKeyword = keyword.Trim();
                if (allGlobalKeywords.Contains(cleanKeyword)) GlobalKeywordsFoundInLog.Add(cleanKeyword);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to collect global keywords from line: {line}\nError: {ex.Message}");
        }
    }

    private static void AddGlobalKeywordsToSettings()
    {
        _settings.enabledGlobalKeywords ??= new List<string>();

        foreach (string globalKeyword in GlobalKeywordsFoundInLog)
        {
            if (!_settings.enabledGlobalKeywords.Contains(globalKeyword))
                _settings.enabledGlobalKeywords.Add(globalKeyword);
        }

        EditorUtility.SetDirty(_settings);
        AssetDatabase.SaveAssets();
    }
}