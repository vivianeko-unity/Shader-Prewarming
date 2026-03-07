# Shader Variants Tooling
Generates and maintains ShaderVariantCollections and GraphicsStateCollections to pre-warm, and strips unused variants at build time.

## Overview
This helps optimize shaders by:
1. Parsing shader compilation logs to identify used variants
2. Processing and filtering relevant variants to be pre-compiled
3. Collecting keyword combinations (global and local) from runtime logs and project materials
4. Automatically generating an updated ShaderVariantCollection at build time
5. Stripping unused shader variants during build to reduce build size and compilation time
6. Pre-warming the ShaderVariantCollection at runtime
7. Tracing and pre-warming GraphicsStateCollections (PSO caching) at runtime

## Setup
1. **Run `Tools/Shader Variants Tools/Open Settings`** - This creates and opens the settings asset
   - Creates `ShaderVariantToolingSettings` asset in `Assets/Editor/`
   - Creates `ShaderVariantToolingPrecompiler` prefab and keeps it in sync with settings
   - Creates `ShaderVariantCollection` asset in `Assets/Shaders/ShaderVariantsToPreCompile`
2. Drop the `ShaderVariantToolingPrecompiler` prefab in your scene
3. Choose which method to use: WarmupMethod dropdown (SVC Only, GSC Only, or Both)

The prefab contains three components auto-managed by settings (all fields hidden):
- `ShaderVariantToolingManager` — All data sending via `PlayerConnection`
- `ShaderVariantCollectionPreCompiler` — handles SVC warmup and shader variant log collection
- `GraphicsStateCollectionPreCompiler` — handles GSC tracing and warmup

## How It Works

### ShaderVariantCollection (SVC) Workflow
1. **Collection Phase**:
   - Add `COLLECT_SHADER_VARIANTS` to Player Settings → Scripting Define Symbols
   - Make a development build
   - Run the game on device — shader variant upload lines are captured from the platform log
   - `ShaderVariantCollectionPreCompiler` reads the log and filters for upload lines after the recording marker
   - `ShaderVariantToolingManager` sends all collected data to the Editor via `PlayerConnection` on focus loss or quit
   - `ShaderVariantToolingReceiver` (Editor) appends received lines to the shaders log file and updates the prefab
   - The log parser automatically cleans and deduplicates entries, preserving older data
   - Shader variants are automatically processed after receiving data to regenerate the ShaderVariantCollection
2. **Processing Phase**:
   - Automatically runs when data is received from device and at build time
   - Manual option: Right-click settings asset → "Process Shader Variants"
   - Parses the log file:
      - Extracts shader names, pass types, keywords, and upload times
      - Identifies and collects global keywords used at runtime
   - Filters variants based on settings:
      - Minimum upload time threshold (`minUploadTime`)
      - Upload count (skips variants uploaded multiple times if `skipMultipleUploads` is enabled)
   - Updates the ShaderVariantCollection
3. **Stripping Phase (Build Time)**:
   - Strips unused shader variants based on keyword combinations:
      - Compares each variant's local keywords against collected keyword combinations
      - Keeps variants that match any collected keyword combination for that shader
   - Generates compilation report: `Artifact/ShaderVariantsCompiled.txt`
   - Can be disabled: Set `strippingEnabled = false` in the settings inspector
4. **Runtime Pre-warming**:
   - Remove `COLLECT_SHADER_VARIANTS` from Player Settings
   - `ShaderVariantCollectionPreCompiler` loads and warms up the SVC at startup

### GraphicsStateCollection (GSC / PSO Caching) Workflow
1. **Collection Phase**:
   - Add `COLLECT_SHADER_VARIANTS` to Player Settings → Scripting Define Symbols
   - Make a development build
   - `GraphicsStateCollectionPreCompiler` starts a trace at runtime to record pipeline state objects
   - On focus loss or quit, `ShaderVariantToolingManager` triggers the collection to be sent to the Editor
   - The Editor saves the collection file and automatically updates the prefab's collection list
2. **Warmup**:
   - Remove `COLLECT_SHADER_VARIANTS` from Player Settings
   - `GraphicsStateCollectionPreCompiler` finds the matching collection for the current platform, graphics API, and quality level, then warms it up

## WarmupMethod
- Dropdown in the settings inspector: `ShaderVariantsOnly`, `GraphicsStatesOnly`, or `Both`
- Controls which method are active for both collection and warmup
- **ShaderVariantsOnly**: Only SVC collects/warms up
- **GraphicsStatesOnly**: Only GSC collects/warms up
- **Both**: Both systems collect/warmup (useful for testing)
- Disabled systems skip all operations (collection, warmup, and data sending)

## Collection Mode
Add `COLLECT_SHADER_VARIANTS` to Player Settings → Scripting Define Symbols to enable collection mode.
- When enabled:
    - Disables stripping and prewarming for active systems
    - Enables data collection for active systems
    - All variants will be compiled and logged
- When disabled: Active systems will warmup instead of collect
- The define can also be set manually via Player Settings or CI/build scripts to override

## Tools
**Menu:**
- `Tools/Shader Variants Tools/Open Settings`: Creates/opens the settings asset and generates all required files

**Settings Context Menu (Right-click settings asset):**
- `Collect Global Keywords From Editor`: Manually update global keywords list from current Editor state
- `Process Shader Variants`: Manually regenerate ShaderVariantCollection from log file

Note: Processing automatically runs when data is received from device and at build time. Manual options are for edge cases only.

## Notes
- **Important**: All shaders in the SVC must be loaded before loading the SVC itself. Otherwise Unity will duplicate the shaders in the build and pre-warm incorrect versions.
- Comments marked `NOTE[Addressables]` indicate where to add addressables loading logic
- Repeat the collection phase when adding new shaders or materials.
- The prefab is automatically kept in sync with settings — no manual scene saves or component configuration required
