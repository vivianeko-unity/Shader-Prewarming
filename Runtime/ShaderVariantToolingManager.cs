using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking.PlayerConnection;

public class ShaderVariantToolingManager : MonoBehaviour
{
    private static ShaderVariantToolingManager _instance;
    private bool _sentOnQuit;

    private ShaderVariantCollectionPreCompiler _shaderVariantCollectionPreCompiler;
    private GraphicsStateCollectionPreCompiler _graphicsStateCollectionPreCompiler;


    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        TryGetComponent(out _shaderVariantCollectionPreCompiler);
        TryGetComponent(out _graphicsStateCollectionPreCompiler);
    }

    private void OnEnable() => Application.wantsToQuit += OnWantsToQuit;
    private void OnDisable() => Application.wantsToQuit -= OnWantsToQuit;

    private void OnApplicationFocus(bool focus)
    {
        if (!focus) Send(false);
    }

    private bool OnWantsToQuit()
    {
        if (_sentOnQuit) return true;

        Send(true);
        _sentOnQuit = true;

        StartCoroutine(QuitNextFrame());
        return false;
    }

    private void Send(bool endTrace)
    {
        if (!PlayerConnection.instance.isConnected) return;
        byte[] logData = _shaderVariantCollectionPreCompiler?.GetLogData() ?? Array.Empty<byte>();
        
        PlayerConnection.instance.Send(ShaderVariantToolingConstants.DataSentMessageId, logData);
        Debug.Log($"[ShaderVariantToolingManager] Sent DataSentMessageId, data: {logData.Length} bytes");
        
        _graphicsStateCollectionPreCompiler?.SendGsc(endTrace);
    }

    private IEnumerator QuitNextFrame()
    {
        yield return null;
        Application.Quit();
    }
}