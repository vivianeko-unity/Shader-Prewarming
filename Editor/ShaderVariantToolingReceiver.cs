using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine.Networking.PlayerConnection;

[InitializeOnLoad]
public static class ShaderVariantToolingReceiver
{
    static ShaderVariantToolingReceiver() => EditorApplication.delayCall += Register;

    private static void Register()
    {
        EditorConnection.instance.Initialize();
        
        EditorConnection.instance.Unregister(ShaderVariantToolingConstants.DataSentMessageId, OnAllDataReceived);
        EditorConnection.instance.Register(ShaderVariantToolingConstants.DataSentMessageId, OnAllDataReceived);
    }
    
    private static void OnAllDataReceived(MessageEventArgs args)
    {
        if (args.data == null || args.data.Length == 0)
            return;

        string newLines = Encoding.UTF8.GetString(args.data).TrimEnd();
        if (!string.IsNullOrEmpty(newLines))
            File.AppendAllText(ShaderVariantToolingSettings.Instance.logFilePath, newLines + Environment.NewLine);

        var settings = ShaderVariantToolingSettings.Instance;
        EditorApplication.delayCall += () =>
        {
            AssetDatabase.Refresh();
            EditorApplication.delayCall -= settings.ValidatePrecompilerPrefab;
            EditorApplication.delayCall += settings.ValidatePrecompilerPrefab;
            EditorApplication.delayCall += ShaderVariantsProcessor.ProcessShaderVariants;
        };
    }
}