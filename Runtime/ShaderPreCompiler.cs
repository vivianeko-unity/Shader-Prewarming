using UnityEngine;

/// <summary>
/// Example Shader Pre warming implementation.
/// If shaders referenced in the SVC are not loaded first, Unity will duplicate them in the build and pre-warm incorrect versions.
/// Ensure all referenced shaders are loaded before attempting to load and warmup the SVC.
/// If using addressables or asset bundles, make sure to set the SVC as addressable, and load all referenced shaders first
/// </summary>
public class ShaderPreCompiler : MonoBehaviour
{
    [SerializeField] private ShaderVariantCollection svc;
    
    private void Start()
    {
#if DEBUG_SHADER_PRECOMPILER
        Debug.Log("ShaderPreCompiler: Disabled, debugging variants to pre-compile.");
#else
        // if addressables wait for shaders bundle to be downloaded first
        LoadSvc();
#endif
    }

    private void LoadSvc()
    {
        // if addressables load the svc here
        
        if (!svc)
        {
            Debug.LogError("ShaderVariantsToPreCompile FAILED to load");
            return;
        }
        WarmupSvc();
    }
    
    private void WarmupSvc()
    {
        Debug.Log("Starting to warm up ShaderVariantCollection");
        
        svc.WarmUp();
        
        Debug.Log($"ShaderVariantCollection is warmed up: {svc.isWarmedUp}, " +
                  $"warmed up: {svc.warmedUpVariantCount} variants out of {svc.variantCount}");
    }
}
