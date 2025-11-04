# Shader-Otimization
Generates and maintains the ShaderVariantCollection to pre-warm and strips unused variants at build time

## Overview
This helps optimize shaders by:
1. Parsing shader compilation logs to identify used variants
2. Processing and filtering relevant variants to be pre-compiled
3. Collecting keyword combinations (global and local) from runtime logs and project materials
4. Automatically generating an updated ShaderVariantCollection at build time
5. Stripping unused shader variants during build to reduce build size and compilation time
4. Pre-compiling the ShaderVariantCollection

## Setup or 1st time run
1. Configure `ShaderPreCompilerSettings` asset if needed (default settings should work for most cases)
2. Run the Maintenance phase to get a shaders log and populate the ShaderVariantCollection at build time
3. Add the `ShaderPreCompiler` component to a GameObject

## How It Works
1. **Maintenance**:
   - Enable `DEBUG_SHADER_PRECOMPILER` and make a development build
   - Run the game to log shader compilations without prewarming (this will avoid adding variants that are not needed anymore)
   - Up-to-date variants are logged to player log file
   - Copy the content into the shaders log file (it will automatically clean and analyze the file)
2. **Processing Phase**:
   - Analyzes and re-generate the updated ShaderVariantCollection on the next build (manual option to update the svc if needed: `Tools/Shader Optimization/Shader Variants Processor`)
      - Parses the log file:
         - Extracts shader names, pass types, keywords, and upload times
         - Identifies and collects global keywords used at runtime
      - Filters variants based on settings
         - Minimum upload time threshold (`minUploadTime`)
         - Upload count (skips variants uploaded multiple times if `skipMultipleUploads` is enabled)
      - Updates the ShaderVariantCollection
3. **Stripping Phase (Build Time)**:
   - Strips unused shader variants based on keyword combinations:
      - Compares each variant's local keywords against collected keyword combinations
      - Keeps variants that match any collected keyword combination for that shader
   - Generates compilation report: Logs all compiled variants to `Artifact/ShaderVariantsCompiled.txt`
   - Can be disabled: Set `strippingEnabled = false` in settings to disable stripping
3. **Runtime Pre-compilation**:
   - ShaderPreCompiler loads and warms up variants at startup

## Debug Options
- DEBUG_SHADER_PRECOMPILER - Disables prewarming to identify currently needed variants
   - Use this during Maintenance phase
   - All variants will be compiled and logged
   - No variants will be stripped

## Tools Menu
- `Tools/Shader Optimization/Shader Variants Processor`: Manually process shader variants and update collections
- `Tools/Shader Optimization/Add EnabledGlobalKeywords From Scenes`: Collect global keywords from currently loaded scenes if necessary

## Notes
- **Important**: All shaders in the SVC must be loaded before loading the SVC itself. Otherwise this will cause Unity to duplicate the shaders in the build and pre-warm incorrect versions.
- Repeat maintenance phase when adding new shaders/materials or changing rendering features.