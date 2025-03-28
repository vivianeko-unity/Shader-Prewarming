# Shader-Prewarming
Creates and maintain the list of variants to prewarm

## Overview
This helps optimize shader compilation by:
1. Tracking shader variants compiled during runtime
2. Processing and filtering relevant variants to be pre-compiled
3. Pre-compiling these variants during game startup

## Setup
1. Configure `ShaderPreCompilerSettings` settings:
   - Set path to shaders log file
   - Adjust minimum upload time threshold
   - Configure filtering options
   - Add manual shader variants if needed
   - Assign a ShaderVariantCollection asset
3. Place the `ShaderPreCompiler` component on a GameObject in startup scene

## How It Works
1. **Logging Phase**:
   - Enable `DEBUG_SHADER_VARIANTS` and make a development build
   - Run the game to log shader compilations
   - Variants are logged to player log file
2. **Maintenance**:
   - Enable `DEBUG_SHADER_PRECOMPILER` and make a development build
   - Run the game to log shader compilations without prewarming (to avoid adding variants that are not needed anymore)
   - Up To data Variants are logged to player log file 
3. **Processing Phase**:
   - Use "Tools/Shader Variants Processor" in Unity Editor
   - Or let it run automatically during build
   - Parses the log file
   - Filters variants based on settings
   - Updates the ShaderVariantCollection
4. **Runtime Pre-compilation**:
   - ShaderPreCompiler loads and warms up variants at startup

## Debug Options
- DEBUG_SHADER_VARIANTS - Enables logging of shader compilations during runtime
- DEBUG_SHADER_PRECOMPILER - Disables prewarming to identify currently needed variants
