using System;

public static class ShaderVariantToolingConstants
{
    // Marker line in log to indicate the start of shader variant recording.
    // Lines before this are ignored during parsing.
    public const string LogRecordingMarker = "ShaderPreCompiler: Disabled, debugging variants to pre-compile.";
    
    // Prefix of log lines that contain uploaded shader variant data.
    public const string ShaderUploadLinePrefix = "Uploaded shader variant to the GPU driver:";
    
    // GUID used for PlayerConnection messaging between device and Editor.
    public static readonly Guid PlayerConnectionMessageId = new("e2f8f8e0-1234-4b5a-9c3d-abcdef123456");
    
    // Path for the ShaderVariantCollection used for pre-warming.
    public const string ShaderVariantCollectionPath = "Assets/Shaders/ShaderVariantsToPreCompile.shadervariants";
    
    // Path for the shader log file.
    public const string LogFilePath = "Assets/Editor/ShadersLog.txt";
    
    // Path in project to store generated graphics state collections.
    public const string GraphicsStateCollectionFolderPath = "Shaders/GraphicsStateCollections/";

    // Toggle for collecting shader variants at runtime. Should be disabled for release builds.
    public static bool IsCollectingRuntimeVariants =>
#if COLLECT_SHADER_VARIANTS
        true;
#else
        false;
#endif
}