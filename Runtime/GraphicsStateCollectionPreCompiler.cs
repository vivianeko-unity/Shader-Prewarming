using UnityEngine;
using UnityEngine.Experimental.Rendering;

/// <summary>
/// Trace and warm up pipeline state objects (PSOs) in a GraphicsStateCollection object.
/// The recommended best practice is to trace a separate collection per graphics API and build platform, because GPU states can vary per API.
/// </summary>
public class GraphicsStateCollectionPreCompiler : MonoBehaviour
{
    public string graphicsStateCollectionFolderPath;
    public GraphicsStateCollection[] graphicsStateCollections;
    [SerializeField] private GraphicsStateCollection graphicsStateCollection;
    private string _collectionName;
    private bool _isCollectingRuntimeVariants;
    
    private void Awake()
    {
        _isCollectingRuntimeVariants = ShaderVariantToolingConstants.IsCollectingRuntimeVariants;
    }
    
    private void Start()
    {
        // NOTE[Addressables]:
        // Wait for shaders bundle (with GSCs in it) to be downloaded first here before.\
        LoadGsc();
    }
    
    public void SendGsc(bool endTrace)
    {
        if (!_isCollectingRuntimeVariants || !graphicsStateCollection) return;
        
        if (endTrace) graphicsStateCollection.EndTrace();
        
        Debug.Log("[GraphicsStateCollectionPreCompiler] Sending collection to Editor with "
                  + graphicsStateCollection.totalGraphicsStateCount + " GraphicsState entries.");
        graphicsStateCollection.SendToEditor(_collectionName);
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
                graphicsStateCollection = collection;
            }
        }

        if (_isCollectingRuntimeVariants)
        {
            StartTracing();
        }
        else
        {
            if (!graphicsStateCollection)
            {
                Debug.LogError("[GraphicsStateCollectionPreCompiler] GraphicsStateCollection FAILED to load");
                return;
            }

            WarmUpGsc();
        }
    }

    private void StartTracing()
    {
        if (graphicsStateCollection)
        {
            _collectionName = graphicsStateCollectionFolderPath + graphicsStateCollection.name;
        }
        else
        {
            int qualityLevelIndex = QualitySettings.GetQualityLevel();
            string qualityLevelName = QualitySettings.names[qualityLevelIndex];
            qualityLevelName = qualityLevelName.Replace(" ", "");

            _collectionName = string.Concat(graphicsStateCollectionFolderPath, "GfxState_",
                                            Application.platform, "_",
                                            SystemInfo.graphicsDeviceType.ToString(), "_", qualityLevelName);
            graphicsStateCollection = new GraphicsStateCollection();
        }

        Debug.Log("[GraphicsStateCollectionPreCompiler] Tracing started.");
        graphicsStateCollection.BeginTrace();
    }

    private void WarmUpGsc()
    {
        Debug.Log("[GraphicsStateCollectionPreCompiler] Starting to warm up GraphicsStateCollection.");

        graphicsStateCollection.WarmUp();

        Debug.Log(
            $"[GraphicsStateCollectionPreCompiler] GraphicsStateCollection is warmed up: {graphicsStateCollection.isWarmedUp}, " +
            $"warmed up: {graphicsStateCollection.completedWarmupCount} variants out of {graphicsStateCollection.variantCount}");
    }
}