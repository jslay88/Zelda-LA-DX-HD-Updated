# AGENTS.md - AI Agent Build Instructions

This document provides instructions for AI agents to build and set up the Link's Awakening DX HD project for Windows and Linux platforms.

## Project Overview

This is a fan-made HD remake of The Legend of Zelda: Link's Awakening DX using MonoGame. The game requires:
- Original copyrighted assets from v1.0.0 release (not included in repository)
- Asset patches to update v1.0.0 assets to latest version
- Platform-specific shader compilation
- Platform-specific XNB content format

**IMPORTANT**: This repository does NOT include copyrighted game assets. Users must provide their own copy of the v1.0.0 release.

---

## Build Requirements

### Windows Build Requirements

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build runtime |
| Visual Studio 2022 | Latest | IDE (optional) |
| MonoGame | 3.8.1+ | Game framework |

### Linux Build Requirements

| Tool | Version | Purpose | Install Command (Arch) |
|------|---------|---------|------------------------|
| .NET SDK | 8.0+ | Build runtime | `sudo pacman -S dotnet-sdk` |
| Wine | 9.0+ | Shader compilation | `sudo pacman -S wine` |
| xdelta3 | Latest | Asset patching | `sudo pacman -S xdelta3` |
| p7zip | Latest | Archive extraction | `sudo pacman -S p7zip` |
| wget | Latest | Download setup script | `sudo pacman -S wget` |
| mgfxc | 3.8.4+ | Shader compiler | `dotnet tool install -g dotnet-mgfxc` |

---

## Building the Game Binary

### Windows Build

```bash
cd ladxhd_game_source_code
dotnet publish -c Release -p:TargetPlatformName=Windows
```

Output: `publish/Windows/Link's Awakening DX HD.exe`

### Linux Build

```bash
cd ladxhd_game_source_code
dotnet publish -c Release -p:TargetPlatformName=Linux
```

Output: `publish/Linux/Link's Awakening DX HD`

---

## Asset Setup

### Understanding the Asset Pipeline

1. **v1.0.0 Assets**: Original game release (user must provide)
   - `Content/` folder: Compiled XNB files (fonts, shaders, textures, sounds)
   - `Data/` folder: Raw game data (maps, animations, sprites, music)
   - `source.7z`: Source files for Content compilation

2. **Patches**: Binary diffs in `assets_patches/` folder
   - Update v1.0.0 files to latest version
   - Create derived files (language variants, redux textures)

3. **Platform-Specific Content**:
   - **Shaders**: Must be compiled for target platform (DirectX for Windows, OpenGL for Linux)
   - **XNB Files**: Have platform byte in header ('w' for Windows, 'd' for DesktopGL)

### Linux Asset Setup

Run the automated setup script:

```bash
# 1. Extract v1.0.0 release to a directory
mkdir ~/ladxhd-game
cd ~/ladxhd-game
unzip "Links Awakening DX HD v1.0.0.zip"
mv "Links Awakening DX HD"/* .

# 2. Run the Linux asset setup script
/path/to/repo/setup_linux_assets.sh ~/ladxhd-game

# 3. Copy the Linux binary
cp /path/to/repo/publish/Linux/Link's\ Awakening\ DX\ HD ~/ladxhd-game/
chmod +x ~/ladxhd-game/Link's\ Awakening\ DX\ HD

# 4. Run the game
cd ~/ladxhd-game
./Link's\ Awakening\ DX\ HD
```

### What the Setup Script Does

1. **Wine Prefix Setup**: Creates `~/.winemonogame` with d3dcompiler_47 for shader compilation
2. **Content Source Extraction**: Extracts all source files from `source.7z`
3. **Derived Content Files**: Creates derived Content source files (e.g., `smallFont_redux.png` from `smallFont.png`)
4. **Spritefont Fixes**: Replaces Windows fonts with Linux equivalents (Segoe UI → DejaVu Sans)
5. **Full Content Compilation**: Compiles ALL Content (fonts, textures, shaders, etc.) using MGCB for DesktopGL
6. **Derived Data Files**: Creates language variants and redux textures from originals
7. **Data Patch Application**: Updates Data files using xdelta3 binary diffs

---

## Shader Compilation Details

### Why Wine is Required on Linux

MonoGame's shader compiler (`mgfxc`) uses DirectX's `d3dcompiler_47.dll` internally, even when targeting OpenGL. On Linux, this requires Wine with the DLL installed.

### Wine Prefix Setup

MonoGame provides an official setup script that configures Wine with all necessary dependencies:

```bash
# Run MonoGame's official Wine setup script (downloads ~350MB)
wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash

# This creates ~/.winemonogame with:
# - .NET SDK for Wine
# - fxccs.dll (MonoGame shader compiler wrapper)
# - SharpDX.D3DCompiler.dll
# - d3dcompiler_47.dll

# Verify installation
ls ~/.winemonogame/drive_c/fxccs.dll
```

### Compiling Shaders Manually

**Important**: Use `mgcb` (MonoGame Content Builder) to compile shaders, not `mgfxc` directly. MGCB wraps the shader in XNB format which MonoGame's ContentManager expects.

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
export MGFXC_WINE_PATH=~/.winemonogame
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

# Create MGCB response file
cat > build.mgcb << EOF
/outputDir:./output
/intermediateDir:./intermediate
/platform:DesktopGL
/profile:Reach
/compress:False
/importer:EffectImporter
/processor:EffectProcessor
/build:ShaderName.fx
EOF

# Build shader
mgcb /@:build.mgcb
```

Note: Raw `mgfxc` outputs MGFX format; `mgcb` outputs XNB-wrapped format.

### Shader Profiles
- `DirectX_11` - Windows DirectX
- `OpenGL` - Linux/macOS DesktopGL
- `Vulkan` - Vulkan (experimental)

---

## XNB Platform Bytes

XNB files have a 4-byte header: `XNB[platform]`
- `XNBw` (0x584E4277) - Windows
- `XNBd` (0x584E4264) - DesktopGL (Linux/macOS)
- `XNBx` (0x584E4278) - Xbox

To convert Windows XNB to DesktopGL:
```bash
# Change byte 4 from 'w' (0x77) to 'd' (0x64)
printf '\x64' | dd of=file.xnb bs=1 seek=3 count=1 conv=notrunc
```

**Note**: This only works for non-shader XNBs. Shaders must be recompiled.

---

## Derived Files Mapping

Some files are created by applying patches to a base file:

### Content Derived Files (require compilation)

| Base Source File | Derived Source Files |
|------------------|---------------------|
| `Fonts/smallFont.png` | `smallFont_redux.png`, `smallFont_vwf.png`, `smallFont_vwf_redux.png` |
| `Menu/menuBackground.png` | `menuBackgroundB.png`, `menuBackgroundC.png`, `sgb_border.png` |

**Note**: These PNG source files must be created BEFORE running MGCB, which compiles them to XNB.

### Data Derived Files (no compilation needed)

| Base File | Derived Files |
|-----------|---------------|
| `ui.png` | `ui_deu.png`, `ui_esp.png`, `ui_fre.png`, `ui_ind.png` |
| `intro.png` | `intro_deu.png`, `intro_esp.png`, `intro_fre.png`, `intro_ind.png` |
| `items.png` | `items_deu.png`, `items_esp.png`, `items_fre.png`, `items_ind.png`, `items_redux.png`, `items_redux_*.png` |
| `photos.png` | `photos_deu.png`, `photos_esp.png`, `photos_fre.png`, `photos_ind.png`, `photos_redux.png`, `photos_redux_*.png` |
| `objects.png` | `objects_deu.png`, `objects_esp.png`, `objects_fre.png`, `objects_ind.png` |
| `minimap.png` | `minimap_deu.png`, `minimap_esp.png`, `minimap_fre.png`, `minimap_ind.png` |
| `npcs.png` | `npcs_redux.png` |
| `link0.png` | `link1.png` |
| `eng.lng` | `deu.lng`, `esp.lng`, `fre.lng`, `ind.lng`, `ita.lng`, `por.lng`, `rus.lng` |
| `dialog_eng.lng` | `dialog_deu.lng`, `dialog_esp.lng`, etc. |
| `BowWow.ani` | `bowwow_water.ani` |
| `musicOverworld.data` | `musicOverworldClassic.data` |

**Critical**: Derived files must be created BEFORE applying in-place patches to base files.

---

## Common Issues

### "MGFX effect was built for a different platform"
- Shaders need to be compiled for the target platform
- Run `setup_linux_assets.sh` to compile for DesktopGL

### "Asset does not appear to be a valid XNB file"
- XNB files have wrong platform byte
- Run XNB platform byte fix or use `setup_linux_assets.sh`

### "Could not find file 'X.png'" or similar
- Derived files weren't created from base files
- Ensure derived files are created BEFORE patching base files

### Game hangs on screen transition (Linux)
- GBS audio player threading issue (historically fixed in code)
- Ensure using latest game binary

### No music on Linux
- GBS player uses `DynamicSoundEffectInstance` for Linux
- Check that `CDynamicEffectInstance.cs` has proper Linux implementation

### Font compilation errors on Linux
- `.spritefont` files reference Windows-only fonts (Segoe UI, Courier New)
- `setup_linux_assets.sh` replaces these with Linux equivalents (DejaVu Sans, DejaVu Sans Mono)
- Ensure DejaVu fonts are installed: `sudo pacman -S ttf-dejavu`

### Missing Content XNB files (smallFont_redux.xnb, etc.)
- These are **derived Content files** that must be compiled from source
- The v1.0.0 `Content/` folder contains Windows XNBs without derived files
- Must use `setup_linux_assets.sh` to:
  1. Extract source files from `source.7z`
  2. Create derived source files (PNG) from patches
  3. Compile ALL Content with MGCB for DesktopGL

### Case sensitivity crashes on Linux (animation files)
- Linux file systems are case-sensitive; Windows is not
- Some animation files have mixed case (e.g., `spiny Beetle.ani`) but code references lowercase (`spiny beetle`)
- Fix: Rename affected `.ani` files to match code references
- Known issue: `Data/Animations/Enemies/spiny Beetle.ani` → `spiny beetle.ani`

---

## Project Structure

```
├── assets_original/          # Place v1.0.0 Content and Data here
├── assets_patches/           # xdelta3 binary patches
├── ladxhd_game_source_code/  # Main game source
│   ├── ProjectZ.csproj       # Multi-platform project file
│   ├── Game1.cs              # Main game class
│   ├── GbsPlayer/            # Chiptune music player
│   └── InGame/               # Game logic
├── ladxhd_patcher_source_code/  # Windows patcher tool
├── publish/                  # Built binaries
│   ├── Windows/
│   └── Linux/
├── setup_linux_assets.sh     # Linux asset setup script
├── AGENTS.md                 # This file
└── README.md                 # User documentation
```

---

## Conditional Compilation

The codebase uses preprocessor directives for platform-specific code:

```csharp
#if WINDOWS
    // Windows-specific code (Windows Forms, SharpDX, etc.)
#endif

#if LINUX
    // Linux-specific code
#endif

#if DESKTOPGL
    // DesktopGL-specific code (OpenGL rendering)
#endif
```

Key files with platform-specific code:
- `Game1.cs` - Window management, fullscreen toggle
- `Program.cs` - Entry point, error dialogs
- `CDynamicEffectInstance.cs` - Audio backend (SharpDX vs DynamicSoundEffectInstance)
- `Resources.Designer.cs` - Icon loading

---

## GitHub Actions

### Patcher Release (Windows only)
- Triggered by tags matching `patcher-v*`
- Builds Windows patcher tool
- Creates GitHub release with executable

### Game Release (Future)
- Would need to build binaries for both platforms
- Cannot include copyrighted assets
- Users must provide v1.0.0 assets separately

---

## Testing Checklist

After building and setting up assets:

- [ ] Game launches without errors
- [ ] Intro sequence plays
- [ ] Can select/create save file
- [ ] Can navigate menus
- [ ] Music plays (GBS chiptune)
- [ ] Sound effects work
- [ ] Can exit starting area (Marin's house)
- [ ] Screen transitions work without hanging
- [ ] All NPCs render correctly (check BowWow outside)
