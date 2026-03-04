#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking.PlayerConnection;

/// <summary>
/// Trace and warm up pipeline state objects (PSOs) in a GraphicsStateCollection object.
/// The recommended best practice is to trace a separate collection per graphics API and build platform, because GPU states can vary per API.
/// </summary>
public class GraphicsStateCollectionTrace : MonoBehaviour
{
    private static GraphicsStateCollectionTrace _instance;

    public GraphicsStateCollection[] graphicsStateCollections;
    [SerializeField] private GraphicsStateCollection graphicsStateCollection;
    private string _collectionName;

#if UNITY_EDITOR
    private void Reset()
    {
        UpdateCollectionList();
    }

    // Right-click on the component to update the collection files list.
    [ContextMenu("Update collection list")]
    public void UpdateCollectionList()
    {
        string directoryName = "Assets/" + ShaderVariantToolingConstants.CollectionFolderPath.TrimEnd('/');
        if (!string.IsNullOrEmpty(directoryName) && !Directory.Exists(directoryName))
            Directory.CreateDirectory(directoryName);
        AssetDatabase.Refresh();

        string[] collectionGUIDs =
            AssetDatabase.FindAssets("t:GraphicsStateCollection",
                                     new[] { "Assets/" + ShaderVariantToolingConstants.CollectionFolderPath });
        graphicsStateCollections = new GraphicsStateCollection[collectionGUIDs.Length];
        for (var i = 0; i < graphicsStateCollections.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(collectionGUIDs[i]);
            graphicsStateCollections[i] = AssetDatabase.LoadAssetAtPath<GraphicsStateCollection>(path);
        }

        EditorUtility.SetDirty(this);
    }
#endif

    // Find the available collection file that matches the current platform and quality level.
    private GraphicsStateCollection FindExistingCollection()
    {
        for (var i = 0; i < graphicsStateCollections.Length; i++)
        {
            if (!graphicsStateCollections[i]) continue;
            if (graphicsStateCollections[i].runtimePlatform == Application.platform &&
                graphicsStateCollections[i].graphicsDeviceType == SystemInfo.graphicsDeviceType &&
                graphicsStateCollections[i].qualityLevelName ==
                QualitySettings.names[QualitySettings.GetQualityLevel()])
                return graphicsStateCollections[i];
        }

        return null;
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Debug.LogError(
                "[GraphicsStateCollectionTrace] Only one instance of GraphicsStateCollectionTrace is allowed!");
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // For mobile platforms, data is additionally saved when focus is lost as OnDestroy() is not guaranteed to be called.
    private void OnApplicationFocus(bool focus)
    {
        if (focus || !PlayerConnection.instance.isConnected) return;

        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants && graphicsStateCollection != null)
        {
            Debug.Log("[GraphicsStateCollectionTrace] Sending collection to Editor with " +
                      graphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
            graphicsStateCollection.SendToEditor(_collectionName);
        }
    }

    private void OnDestroy()
    {
        if (!PlayerConnection.instance.isConnected) return;

        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants && graphicsStateCollection)
        {
            graphicsStateCollection.EndTrace();
            Debug.Log("[GraphicsStateCollectionTrace] Sending collection to Editor with "
                      + graphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
            graphicsStateCollection.SendToEditor(_collectionName);
        }
    }

    private void Start()
    {
        // NOTE[Addressables]:
        // Wait for shaders bundle (with GSCs in it) to be downloaded first here before.
        LoadGsc();
    }

    private void LoadGsc()
    {
        // NOTE[Addressables]:
        // Load the GSCs from addressables here.

        graphicsStateCollection = FindExistingCollection();

        if (ShaderVariantToolingConstants.IsCollectingRuntimeVariants)
        {
            StartTracing();
        }
        else
        {
            if (!graphicsStateCollection)
            {
                Debug.LogError("[GraphicsStateCollectionTrace] GraphicsStateCollection FAILED to load");
                return;
            }

            WarmUpGsc();
        }
    }

    private void StartTracing()
    {
        if (graphicsStateCollection)
        {
            _collectionName = ShaderVariantToolingConstants.CollectionFolderPath + graphicsStateCollection.name;
        }
        else
        {
            int qualityLevelIndex = QualitySettings.GetQualityLevel();
            string qualityLevelName = QualitySettings.names[qualityLevelIndex];
            qualityLevelName = qualityLevelName.Replace(" ", "");

            _collectionName = string.Concat(ShaderVariantToolingConstants.CollectionFolderPath, "GfxState_",
                                            Application.platform, "_",
                                            SystemInfo.graphicsDeviceType.ToString(), "_", qualityLevelName);
            graphicsStateCollection = new GraphicsStateCollection();
        }

        Debug.Log("[GraphicsStateCollectionTrace] Tracing started.");
        graphicsStateCollection.BeginTrace();
    }

    private void WarmUpGsc()
    {
        Debug.Log("[GraphicsStateCollectionTrace] Starting to warm up GraphicsStateCollection.");

        graphicsStateCollection.WarmUp();

        Debug.Log(
            $"[GraphicsStateCollectionTrace] GraphicsStateCollection is warmed up: {graphicsStateCollection.isWarmedUp}, " +
            $"warmed up: {graphicsStateCollection.completedWarmupCount} variants out of {graphicsStateCollection.variantCount}");
    }
}