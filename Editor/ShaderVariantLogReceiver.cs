#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Networking.PlayerConnection;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

[InitializeOnLoad]
public static class ShaderVariantLogReceiver
{
    static ShaderVariantLogReceiver()
    {
        EditorApplication.delayCall += Register;
    }

    private static void Register()
    {
        EditorConnection.instance.Initialize();
        EditorConnection.instance.Unregister(ShaderVariantToolingConstants.PlayerConnectionMessageId, OnLogReceived);
        EditorConnection.instance.Register(ShaderVariantToolingConstants.PlayerConnectionMessageId, OnLogReceived);
        Debug.Log("[ShaderVariantLogReceiver] Registered for shader variant log messages.");
    }

    private static void OnLogReceived(MessageEventArgs args)
    {
        string newLines = Encoding.UTF8.GetString(args.data).TrimEnd();
        if (string.IsNullOrEmpty(newLines)) return;

        string path = ShaderVariantToolingSettings.Instance.LogFilePath;

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[ShaderVariantLogReceiver] Log file not found at {path}.");
            return;
        }

        File.AppendAllText(path, newLines + Environment.NewLine);
        AssetDatabase.Refresh();

        int count = newLines.Split('\n').Length;
        Debug.Log($"[ShaderVariantLogReceiver] Appended {count} shader variant log lines to {path}");
    }
}
#endif