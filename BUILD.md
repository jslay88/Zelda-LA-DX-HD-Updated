# Build System Documentation

This document explains the build system for Link's Awakening DX HD, covering both local development and CI/CD pipelines.

## Overview

The game is built using MonoGame framework and targets two platforms:
- **Windows** (DirectX/WindowsDX)
- **Linux** (OpenGL/DesktopGL)

### Key Constraints

1. **Copyrighted Content**: The game requires original assets from v1.0.0 release which cannot be committed to the repository
2. **Platform-Specific Shaders**: Shaders must be compiled for each platform's graphics API
3. **XNB Format**: MonoGame content uses XNB format with platform-specific headers
4. **Auto-Patching**: Game binary includes embedded patches to update v1.0.0 assets to latest version

---

## Directory Structure

```
├── Makefile                    # Cross-platform build automation
├── setup_linux_assets.sh       # Linux asset preparation script
├── assets_patches/             # xdelta binary patches (345 files)
├── ladxhd_game_source_code/
│   ├── ProjectZ.csproj         # Multi-platform project file
│   ├── Content/
│   │   ├── Shader/*.fx         # Shader source files (NOT copyrighted)
│   │   ├── *.spritefont        # Font definitions (NOT copyrighted)
│   │   └── Content.mgcb        # MonoGame content build config
│   └── InGame/Things/
│       └── AssetPatcher.cs     # Runtime auto-patching logic
├── publish/
│   ├── Windows/                # Windows build output
│   └── Linux/                  # Linux build output
└── .github/workflows/
    └── release-game.yml        # GitHub Actions CI/CD
```

---

## Local Development

### Prerequisites

| Tool | Windows | Linux (Arch) | Purpose |
|------|---------|--------------|---------|
| .NET SDK 8.0+ | [Download](https://dotnet.microsoft.com/download) | `sudo pacman -S dotnet-sdk` | Build runtime |
| MGCB | `dotnet tool install -g dotnet-mgcb` | Same | Content compilation |
| xdelta3 | Embedded in binary | `sudo pacman -S xdelta3` | Asset patching |
| Wine | N/A | `sudo pacman -S wine` | Shader compilation |
| p7zip | N/A | `sudo pacman -S p7zip` | Asset extraction |

### Using the Makefile

```bash
# Check dependencies
make check-deps

# Show available commands
make help

# Build for current platform
make build

# Build for specific platform
make build-windows
make build-linux
make build-all

# Compile shaders
make shaders          # Both platforms
make shaders-windows  # DirectX only
make shaders-linux    # OpenGL only (requires Wine)

# Set up test environment (requires v1.0.0 zip)
make test-linux
make test-windows

# Quick rebuild (reuse existing test env)
make test-quick-linux
make test-quick-windows

# Create release packages
make release

# Clean build artifacts
make clean
make clean-all  # Also removes test directories
```

### Building Without Make

```bash
# Windows
cd ladxhd_game_source_code
dotnet publish -c Release -p:TargetPlatformName=Windows -p:SkipContentBuild=true

# Linux
cd ladxhd_game_source_code
dotnet publish -c Release -p:TargetPlatformName=Linux -p:SkipContentBuild=true
```

---

## Content Pipeline

### What's Compiled at Build Time

| Content Type | Compiled? | Platform-Specific? | Notes |
|--------------|-----------|-------------------|-------|
| Shaders (`.fx`) | Yes | **Yes** | DirectX vs OpenGL bytecode |
| SpriteFonts (`.spritefont`) | Yes | No* | *References system fonts |
| Textures (`.png`) | Yes | No | XNB header differs |
| Sound Effects (`.wav`) | Yes | No | XNB header differs |

### Shader Compilation

Shaders are the main platform-specific content:

- **Windows**: Compiled to DirectX HLSL bytecode
- **Linux**: Compiled to OpenGL GLSL bytecode

MonoGame's `mgfxc` (Effect Compiler) internally uses `d3dcompiler_47.dll` even for OpenGL targets. On Linux, this requires Wine with the DLL installed.

```bash
# Linux: Setup Wine prefix (one-time, ~350MB download)
wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash

# Compile a shader manually
export MGFXC_WINE_PATH=~/.winemonogame
mgcb /platform:DesktopGL /build:Shader/BlurH.fx
```

### XNB Platform Bytes

XNB files contain a platform identifier:
- `XNBw` (0x77) = Windows
- `XNBd` (0x64) = DesktopGL (Linux)

For non-shader content, you can convert between platforms by changing byte 4:
```bash
# Convert Windows XNB to DesktopGL
printf '\x64' | dd of=file.xnb bs=1 seek=3 count=1 conv=notrunc
```

**Note**: This doesn't work for shaders - they must be recompiled.

---

## Asset Patching System

### How It Works

1. **Embedded Patches**: The binary embeds all 345 xdelta patches from `assets_patches/`
2. **First Launch**: `AssetPatcher.CheckAndPatchAssets()` runs before game starts
3. **Version Check**: Reads `.patched_version` file to check if already patched
4. **Backup/Restore**: Ensures patches always apply from v1.0.0 originals
5. **Apply Patches**: Uses xdelta3 to update v1.0.0 files to latest version
6. **Create Derived Files**: Generates language variants, redux textures, etc.

### Backup System (Upgrade Support)

The patcher maintains backups of original v1.0.0 files in `Data/Backup/`:

```
Data/
├── Backup/           ← v1.0.0 originals stored here
│   ├── eng.lng
│   ├── ui.png
│   ├── items.png
│   └── ...
├── Languages/
│   ├── eng.lng       ← Patched to current version
│   ├── deu.lng       ← Created from eng.lng
│   └── ...
└── ...
```

**Upgrade Flow:**

| Scenario | Backup Exists? | Action |
|----------|----------------|--------|
| First run (v1.0.0 → v1.5.2) | No | Backup v1.0.0 → Apply patch |
| Upgrade (v1.1.4 → v1.5.2) | Yes | Restore v1.0.0 from backup → Apply patch |
| Same version | N/A | Skip (already patched) |

This ensures patches **always work** regardless of the user's current version, because:
- xdelta patches are binary diffs from v1.0.0 specifically
- Applying a v1.0.0→v1.5.2 patch to v1.1.4 would fail
- By restoring v1.0.0 first, patches always succeed

**What Gets Backed Up:**
- Only "base" files that have patches (not derived files)
- Derived files (language variants, redux textures) are regenerated each time

**What Gets Cleaned Up:**
- Derived files are removed from backup if accidentally placed there
- Obsolete files (renamed/removed in newer versions) are deleted

### Platform Differences

| Platform | Content Folder | Data Folder | xdelta3 Source |
|----------|---------------|-------------|----------------|
| Windows | Patched | Patched | Embedded in binary |
| Linux | **Skipped** | Patched | System package |

Linux skips Content folder patching because:
1. v1.0.0 Content has Windows XNB format
2. Shaders are DirectX bytecode
3. Must use `setup_linux_assets.sh` or pre-compiled shaders

### Derived Files

Some patches create new files from existing ones:

| Source File | Derived Files |
|-------------|---------------|
| `eng.lng` | `deu.lng`, `esp.lng`, `fre.lng`, `ind.lng`, `ita.lng`, `por.lng`, `rus.lng` |
| `ui.png` | `ui_deu.png`, `ui_esp.png`, etc. |
| `items.png` | `items_deu.png`, `items_redux.png`, etc. |
| `smallFont.xnb` | `smallFont_redux.xnb`, `smallFont_vwf.xnb`, etc. |

---

## GitHub Actions Workflow

### Workflow: `release-game.yml`

Triggered by:
- Tag push matching `game-v*`
- Manual workflow dispatch

### Jobs

1. **build-content** (Windows runner)
   - Compiles shaders for both DirectX and DesktopGL
   - Uploads as artifacts

2. **build-windows** (Windows runner)
   - Builds Windows binary with `SkipContentBuild=true`
   - Includes Windows shaders in release

3. **build-linux** (Ubuntu runner)
   - Builds Linux binary with `SkipContentBuild=true`
   - Includes DesktopGL shaders in release

4. **release**
   - Creates GitHub release with both platform packages

### Release Contents

| Package | Contents |
|---------|----------|
| `LADXHD-Windows-x64.zip` | Binary + DirectX shaders + embedded patches |
| `LADXHD-Linux-x64.tar.gz` | Binary + DesktopGL shaders + embedded patches |

---

## User Installation Flow

### Windows Users

1. Download `LADXHD-Windows-x64.zip`
2. Extract to game folder
3. Copy `Content/` and `Data/` from v1.0.0 release
4. Run game
   - **ShaderPatcher** automatically installs DirectX shaders from `Shaders-Windows/`
   - **AssetPatcher** automatically patches Content and Data files
   - Creates `.patched_version` marker

### Linux Users

1. Download `LADXHD-Linux-x64.tar.gz`
2. Extract to game folder
3. Copy `Content/` and `Data/` from v1.0.0 release
4. Install `xdelta3`: `sudo pacman -S xdelta3`
5. Run game
   - **ShaderPatcher** automatically installs DesktopGL shaders from `Shaders-DesktopGL/`
   - **AssetPatcher** automatically patches Data files
   - Creates `.patched_version` marker

### Release Package Contents

```
Windows Release (LADXHD-Windows-x64.zip):
├── Link's Awakening DX HD.exe    ← Game binary
├── Shaders-Windows/              ← DirectX shaders (auto-installed)
│   ├── BlurH.xnb
│   ├── BlurV.xnb
│   └── ...
└── (user provides Content/ and Data/)

Linux Release (LADXHD-Linux-x64.tar.gz):
├── Link's Awakening DX HD        ← Game binary
├── Shaders-DesktopGL/            ← OpenGL shaders (auto-installed)
│   ├── BlurH.xnb
│   ├── BlurV.xnb
│   └── ...
├── setup_linux_assets.sh         ← Advanced: Full content compilation
└── (user provides Content/ and Data/)
```

---

## Future Improvements

### Runtime Shader Compilation

Instead of pre-compiling shaders, the game could compile them at runtime:
- Ship `.fx` source files instead of compiled XNB
- Use `Effect.FromStream()` with HLSL-to-GLSL transpilation
- More complex but eliminates platform-specific shader builds

### Alternative: Dual Shader Loading

The game could try loading platform-specific shaders:
1. Try `Content/Shader/{name}.xnb`
2. Fallback to `Content/Shader/DesktopGL/{name}.xnb` on Linux
3. Fallback to `Content/Shader/Windows/{name}.xnb` on Windows

This would allow shipping both shader sets in a single package.

---

## Troubleshooting

### "MGFX effect was built for a different platform"
- Shaders compiled for wrong platform
- Linux: Use DesktopGL shaders, not Windows shaders

### Font compilation errors (Linux)
- `.spritefont` references Windows fonts (Segoe UI)
- `setup_linux_assets.sh` replaces with DejaVu fonts
- Ensure fonts installed: `sudo pacman -S ttf-dejavu`

### xdelta3 not found (Linux)
- Install system package: `sudo pacman -S xdelta3`
- Required for auto-patching Data folder

### Wine errors during shader compilation
- Run MonoGame's Wine setup: `wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash`
- Check Wine prefix exists: `ls ~/.winemonogame/drive_c/fxccs.dll`
