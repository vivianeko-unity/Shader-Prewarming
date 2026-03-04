using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

/// <summary>
/// Example Shader Pre warming implementation.
/// If shaders referenced in the SVC are not loaded first, Unity will duplicate them in the build and pre-warm incorrect versions.
/// Ensure all referenced shaders are loaded before attempting to load and warmup the SVC.
/// If using addressables or asset bundles, make sure to set the SVC as addressable, and load all referenced shaders first
/// </summary>
public class ShaderPreCompiler : MonoBehaviour
{
    [SerializeField] private ShaderVariantCollection shaderVariantCollection;
    
    private readonly StringBuilder _buffer = new();
    private string _logPath;
    private long _lastPosition;
    private bool _foundStartMarker;
    
    private void Start()
    {
        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants)
        {
            Debug.Log(ShaderVariantToolingConstants.LogRecordingMarker);
            InitLogSender();
        }
        else
        {
            // NOTE[Addressables]:
            // Wait for shaders bundle to be downloaded first here before
            LoadSvc();
        }
    }

    private void InitLogSender()
    {
        _logPath = Application.consoleLogPath;
        if (string.IsNullOrEmpty(_logPath))
        {
            Debug.LogWarning("[ShaderPreCompiler] Platform log path not found.");
            return;
        }

        if (File.Exists(_logPath)) _lastPosition = new FileInfo(_logPath).Length;
        Debug.Log($"[ShaderPreCompiler] Monitoring: {_logPath}, Connected: {PlayerConnection.instance.isConnected}");
    }

    private void ReadNewLines()
    {
        if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath)) return;

        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= _lastPosition)
            {
                if (fs.Length < _lastPosition)
                    _lastPosition = 0;
                else
                    return;
            }

            fs.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!_foundStartMarker)
                {
                    if (line.Contains(ShaderVariantToolingConstants.LogRecordingMarker)) _foundStartMarker = true;
                    continue;
                }

                if (line.Contains(ShaderVariantToolingConstants.ShaderUploadLinePrefix)) _buffer.AppendLine(line);
            }

            _lastPosition = fs.Position;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ShaderPreCompiler] Error reading log: {e.Message}");
        }
    }

    private void Flush()
    {
        if (_buffer.Length == 0) return;

        if (!PlayerConnection.instance.isConnected)
        {
            Debug.LogWarning("[ShaderPreCompiler] Not connected to Editor.");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(_buffer.ToString());
        PlayerConnection.instance.Send(ShaderVariantToolingConstants.PlayerConnectionMessageId, data);
        int count = _buffer.ToString().Split('\n').Length - 1;
        Debug.Log($"[ShaderPreCompiler] Sent {count} shader variant lines to Editor.");
        _buffer.Clear();
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus || !PlayerConnection.instance.isConnected) return;
        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants)
        {
            ReadNewLines();
            Flush();
        }
    }

    private void OnDestroy()
    {
        if (!PlayerConnection.instance.isConnected) return;
        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants)
        {
            ReadNewLines();
            Flush();
        }
    }

    private void LoadSvc()
    {
        // NOTE[Addressables]:
        // Load the svc from addressables here

        if (!shaderVariantCollection)
        {
            Debug.LogError("[ShaderPreCompiler] ShaderVariantsToPreCompile FAILED to load");
            return;
        }

        WarmupSvc();
    }

    private void WarmupSvc()
    {
        Debug.Log("Starting to warm up ShaderVariantCollection");

        shaderVariantCollection.WarmUp();

        Debug.Log($"[ShaderPreCompiler] ShaderVariantCollection is warmed up: {shaderVariantCollection.isWarmedUp}, " +
                  $"warmed up: {shaderVariantCollection.warmedUpVariantCount} variants out of {shaderVariantCollection.variantCount}");
    }
}