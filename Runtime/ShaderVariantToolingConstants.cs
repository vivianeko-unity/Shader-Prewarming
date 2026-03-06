using System;

public static class ShaderVariantToolingConstants
{
    // Marker line in log to indicate the start of shader variant recording.
    // Lines before this are ignored during parsing.
    public const string LogRecordingMarker = "ShaderVariantCollectionPreCompiler: Disabled, debugging variants to pre-compile.";
    
    // Prefix of log lines that contain uploaded shader variant data.
    public const string ShaderUploadLinePrefix = "Uploaded shader variant to the GPU driver:";
    
    // GUID used for PlayerConnection messaging between device and Editor.
    public static readonly Guid DataSentMessageId = new("f3a9b7c2-4e1d-4a8f-b2e5-9d7c3f1a6b8e");
    
    // Toggle for collecting shader variants at runtime. Should be disabled for release builds.
    public static bool IsCollectingRuntimeVariants =>
#if COLLECT_SHADER_VARIANTS
        true;
#else
        false;
#endif
}