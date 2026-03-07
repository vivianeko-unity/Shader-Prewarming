using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// Trace and warm up pipeline state objects (PSOs) in a GraphicsStateCollection object.
/// The recommended best practice is to trace a separate collection per graphics API and build platform, because GPU states can vary per API.
/// </summary>
public class GraphicsStateCollectionPreCompiler : MonoBehaviour
{
    [HideInInspector] public bool methodEnabled = true;
    [HideInInspector] public string graphicsStateCollectionFolderPath;
    [HideInInspector] public GraphicsStateCollection[] graphicsStateCollections;
    
    private GraphicsStateCollection _graphicsStateCollection;
    private string _collectionName;
    private bool _isCollectingRuntimeVariants;

    private void Awake()
    {
        _isCollectingRuntimeVariants = ShaderVariantToolingConstants.IsCollectingRuntimeVariants;
    }

    private void Start()
    {
        if (!methodEnabled) return;

        // NOTE[Addressables]:
        // Wait for shaders bundle (with GSCs in it) to be downloaded first here before.\
        LoadGsc();
    }

    public void SendGsc(bool endTrace)
    {
        if (!methodEnabled || !_isCollectingRuntimeVariants || !_graphicsStateCollection) return;

        if (endTrace) _graphicsStateCollection.EndTrace();

        Debug.Log("[GraphicsStateCollectionPreCompiler] Sending collection to Editor with "
                  + _graphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
        _graphicsStateCollection.SendToEditor(_collectionName);
    }

    private void LoadGsc()
    {
        // NOTE[Addressables]:
        // Load the GSCs from addressables here.

        foreach (GraphicsStateCollection collection in graphicsStateCollections)
        {
            if (!collection) continue;
            if (collection.runtimePlatform == Application.platform &&
                collection.graphicsDeviceType == SystemInfo.graphicsDeviceType &&
                collection.qualityLevelName ==
                QualitySettings.names[QualitySettings.GetQualityLevel()])
            {
                _graphicsStateCollection = collection;
            }
        }

        if (_isCollectingRuntimeVariants)
        {
            StartTracing();
        }
        else
        {
            if (!_graphicsStateCollection)
            {
                Debug.LogError("[GraphicsStateCollectionPreCompiler] GraphicsStateCollection FAILED to load");
                return;
            }

            WarmUpGsc();
        }
    }

    private void StartTracing()
    {
        if (_graphicsStateCollection)
        {
            _collectionName = graphicsStateCollectionFolderPath + _graphicsStateCollection.name;
        }
        else
        {
            int qualityLevelIndex = QualitySettings.GetQualityLevel();
            string qualityLevelName = QualitySettings.names[qualityLevelIndex];
            qualityLevelName = qualityLevelName.Replace(" ", "");

            _collectionName = string.Concat(graphicsStateCollectionFolderPath, "GfxState_",
                                            Application.platform, "_",
                                            SystemInfo.graphicsDeviceType.ToString(), "_", qualityLevelName);
            _graphicsStateCollection = new GraphicsStateCollection();
        }

        Debug.Log("[GraphicsStateCollectionPreCompiler] Tracing started.");
        _graphicsStateCollection.BeginTrace();
    }

    private void WarmUpGsc()
    {
        Debug.Log("[GraphicsStateCollectionPreCompiler] Starting to warm up GraphicsStateCollection.");

        _graphicsStateCollection.WarmUp();

        Debug.Log(
            $"[GraphicsStateCollectionPreCompiler] GraphicsStateCollection is warmed up: {_graphicsStateCollection.isWarmedUp}, " +
            $"warmed up: {_graphicsStateCollection.completedWarmupCount} variants out of {_graphicsStateCollection.variantCount}");
    }
}