# Shader-Prewarming
Generates and maintains the ShaderVariantCollection to pre-warm.

## Overview
This helps optimize shader compilation by:
1. Parsing shader compilation logs
2. Processing and filtering relevant variants to be pre-compiled
3. Automatically generating an updated ShaderVariantCollection at build time
4. Pre-compiling the ShaderVariantCollection at startup

## Setup or 1st time run
1. Configure `ShaderPreCompilerSettings` settings if needed (default settings should work for most cases)
2. Run the Maintenance phase to get a shaders log and populate the ShaderVariantCollection at build time
3. Place the `ShaderPreCompiler` component on a GameObject or use the `ShaderPreCompiler` prefab in startup scene

## How It Works
1. **Logging Phase**:
   - Enable `DEBUG_SHADER_VARIANTS` and make a development build
   - Run the game to log shader compilations
2. **Maintenance**:
   - Enable `DEBUG_SHADER_PRECOMPILER` and make a development build
   - Run the game to log shader compilations without prewarming (this will avoid adding variants that are not needed anymore)
   - Up-to-date variants are logged to player log file
   - Copy the content into the shaders log file (it will automatically clean and analyze the file)
3. **Processing Phase**:
   - Analyzes and re-generate the updated ShaderVariantCollection on the next build (manual option to update the svc if needed: Tools/Shader Variants Processor)
      - Parses the log file
      - Filters variants based on settings
      - Updates the ShaderVariantCollection
4. **Runtime Pre-compilation**:
   - ShaderPreCompiler loads and warms up variants at startup

## Notes
- **Important**: All shaders in the SVC must be loaded before loading the SVC itself. Otherwise this will cause Unity to duplicate the shaders in the build and pre-warm incorrect versions.
- Example ShadersLog file should not be used, instead follow the Maintenance phase steps to updated with correct information from the project

## Debug Options
- DEBUG_SHADER_VARIANTS - Enables logging of shader compilations during runtime
- DEBUG_SHADER_PRECOMPILER - Disables prewarming to identify currently needed variants
