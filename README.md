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
1. A `ShaderVariantToolingSettings` asset is auto-created in `Assets/Editor/` on first use
2. A `ShaderVariantToolingPrecompiler` prefab is auto-created and kept in sync with settings — drop it in your scene
3. Run the Maintenance phase to collect shader variant data

The prefab contains three components managed automatically by settings:
- `ShaderVariantToolingManager` — owns the singleton, `DontDestroyOnLoad`, and all data sending via `PlayerConnection`
- `ShaderVariantCollectionPreCompiler` — handles SVC warmup and shader variant log collection
- `GraphicsStateCollectionPreCompiler` — handles GSC tracing and warmup

## How It Works

### ShaderVariantCollection (SVC) Workflow
1. **Maintenance (Collecting)**:
   - Enable `COLLECT_SHADER_VARIANTS` scripting define and make a development build
   - Run the game on device — shader variant upload lines are captured from the platform log
   - `ShaderVariantCollectionPreCompiler` reads the log and filters for upload lines after the recording marker
   - `ShaderVariantToolingManager` sends all collected data to the Editor via `PlayerConnection` on focus loss or quit
   - `ShaderVariantToolingReceiver` (Editor) appends received lines to the shaders log file and updates the prefab
   - The log parser automatically cleans and deduplicates entries, preserving older data
2. **Processing Phase**:
   - Analyzes and re-generates the updated ShaderVariantCollection on the next build
   - Manual option: `Tools/Shader Variants Tools/Shader Variants Processor`
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
   - Can be disabled: Set `strippingEnabled = false` in settings
4. **Runtime Pre-warming**:
   - `ShaderVariantCollectionPreCompiler` loads and warms up the SVC at startup

### GraphicsStateCollection (GSC / PSO Caching) Workflow
1. **Tracing (Collecting)**:
   - Enable `COLLECT_SHADER_VARIANTS` scripting define and make a development build
   - `GraphicsStateCollectionPreCompiler` starts a trace at runtime to record pipeline state objects
   - On focus loss or quit, `ShaderVariantToolingManager` triggers the collection to be sent to the Editor
   - The Editor saves the collection file and automatically updates the prefab's collection list
2. **Warmup**:
   - `GraphicsStateCollectionPreCompiler` finds the matching collection for the current platform, graphics API, and quality level, then warms it up

## Define Symbols
- `COLLECT_SHADER_VARIANTS` — Disables stripping and prewarming; enables shader variant log collection and GSC tracing
  - Use this during the Maintenance/Tracing phase
  - All variants will be compiled and logged
  - No variants will be stripped

## Tools Menu
- `Tools/Shader Variants Tools/Shader Variants Processor`: Manually process shader variants and update collections

## Notes
- **Important**: All shaders in the SVC must be loaded before loading the SVC itself. Otherwise Unity will duplicate the shaders in the build and pre-warm incorrect versions.
- Comments marked `NOTE[Addressables]` indicate where to add addressables loading logic
- Repeat the maintenance phase when adding new shaders or materials.
- The prefab is automatically kept in sync with settings — no manual scene saves or component configuration required
