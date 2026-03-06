using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Example Shader Pre warming implementation.
/// If shaders referenced in the SVC are not loaded first, Unity will duplicate them in the build and pre-warm incorrect versions.
/// Ensure all referenced shaders are loaded before attempting to load and warmup the SVC.
/// If using addressables or asset bundles, make sure to set the SVC as addressable, and load all referenced shaders first
/// </summary>
public class ShaderVariantCollectionPreCompiler : MonoBehaviour
{
    public ShaderVariantCollection shaderVariantCollection;
    private readonly StringBuilder _buffer = new();
    private string _logPath;
    private long _lastPosition;
    private bool _isCollectingRuntimeVariants;
    
    private void Awake()
    {
        _isCollectingRuntimeVariants = ShaderVariantToolingConstants.IsCollectingRuntimeVariants;
    }
    
    private void Start()
    {
        // NOTE[Addressables]:
        // Wait for shaders bundle (with SVC in it) to be downloaded first here before\
        LoadSvc();
    }
    
    public byte[] GetLogData()
    {
        if (!_isCollectingRuntimeVariants) return null;

        ReadNewLines();
        if (_buffer.Length == 0) return null;

        byte[] data = Encoding.UTF8.GetBytes(_buffer.ToString());
        int count = _buffer.ToString().Split('\n').Length - 1;
        Debug.Log($"[ShaderVariantCollectionPreCompiler] Sending {count} shader variant lines to Editor.");
        _buffer.Clear();
        return data;
    }

    private void ReadNewLines()
    {
        if (string.IsNullOrEmpty(_logPath) || !File.Exists(_logPath)) return;

        var foundStartMarker = false;
        try
        {
            using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length <= _lastPosition)
            {
                if (stream.Length < _lastPosition) _lastPosition = 0;
                else return;
            }

            stream.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!foundStartMarker)
                {
                    if (line.Contains(ShaderVariantToolingConstants.LogRecordingMarker)) foundStartMarker = true;
                    continue;
                }

                if (line.Contains(ShaderVariantToolingConstants.ShaderUploadLinePrefix)) _buffer.AppendLine(line);
            }

            _lastPosition = stream.Position;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ShaderVariantCollectionPreCompiler] Error reading log: {e.Message}");
        }
    }

    private void LoadSvc()
    {
        // NOTE[Addressables]:
        // Load the svc from addressables here
        
        if (_isCollectingRuntimeVariants)
        {
            InitializeLogSender();
        }
        else
        {
            if (!shaderVariantCollection)
            {
                Debug.LogError("[ShaderVariantCollectionPreCompiler] ShaderVariantsToPreCompile FAILED to load");
                return;
            }

            WarmupSvc();
        }
    }
    
    private void InitializeLogSender()
    {
        _logPath = Application.consoleLogPath;
        if (string.IsNullOrEmpty(_logPath))
        {
            Debug.LogWarning("[ShaderVariantCollectionPreCompiler] Platform log path not found.");
            return;
        }

        if (File.Exists(_logPath)) _lastPosition = new FileInfo(_logPath).Length;
        Debug.Log(ShaderVariantToolingConstants.LogRecordingMarker);
        Debug.Log($"[ShaderVariantCollectionPreCompiler] Monitoring started: {_logPath}");
    }

    private void WarmupSvc()
    {
        Debug.Log("Starting to warm up ShaderVariantCollection");

        shaderVariantCollection.WarmUp();

        Debug.Log($"[ShaderVariantCollectionPreCompiler] ShaderVariantCollection is warmed up: {shaderVariantCollection.isWarmedUp}, " +
                  $"warmed up: {shaderVariantCollection.warmedUpVariantCount} variants out of {shaderVariantCollection.variantCount}");
    }
}