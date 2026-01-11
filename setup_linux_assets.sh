#!/bin/bash
# =============================================================================
# LADXHD Linux Asset Setup Script
# =============================================================================
# This script prepares game assets for Linux by:
# 1. Setting up a Wine prefix with d3dcompiler_47 for shader compilation
# 2. Extracting Content sources from source.7z
# 3. Creating derived Content files (fonts, textures)
# 4. Fixing spritefont Linux font compatibility
# 5. Compiling ALL Content for DesktopGL (OpenGL)
# 6. Creating derived Data files (sprites, languages)
# 7. Applying xdelta patches to update files
# 8. Fixing case sensitivity issues (Linux filesystems are case-sensitive)
# =============================================================================

GAME_DIR="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PATCHES_DIR="$SCRIPT_DIR/assets_patches"
WINE_PREFIX="$HOME/.winemonogame"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

log_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
log_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# =============================================================================
# Validation
# =============================================================================
if [ -z "$GAME_DIR" ] || [ ! -d "$GAME_DIR" ]; then
    echo "Usage: $0 <game_directory>"
    echo ""
    echo "  game_directory: Path containing Content/, Data/, and source.7z from v1.0.0 zip"
    echo ""
    echo "Prerequisites:"
    echo "  - xdelta3"
    echo "  - p7zip (7z command)"
    echo "  - wine"
    echo "  - .NET SDK 8.0+"
    exit 1
fi

# Check required tools
check_dependency() {
    if ! command -v "$1" >/dev/null 2>&1; then
        log_error "$1 is not installed. Please install it first."
        exit 1
    fi
}

log_info "Checking dependencies..."
check_dependency xdelta3
check_dependency 7z
check_dependency wine
check_dependency wget
check_dependency dotnet

FIXED=0
FAILED=0
declare -A DERIVED_FILES

# =============================================================================
# Step 0: Setup Wine prefix for shader compilation
# =============================================================================
setup_wine_prefix() {
    log_info "Setting up Wine prefix for shader compilation..."
    
    # Check if MonoGame Wine prefix already exists (fxccs.dll is the shader compiler)
    if [ -d "$WINE_PREFIX" ] && [ -f "$WINE_PREFIX/drive_c/fxccs.dll" ]; then
        log_info "Wine prefix already configured at $WINE_PREFIX"
        return 0
    fi
    
    # Use MonoGame's official setup script
    log_info "Running MonoGame's official Wine setup script..."
    log_info "This downloads ~350MB and may take several minutes..."
    
    if wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash; then
        if [ -f "$WINE_PREFIX/drive_c/fxccs.dll" ]; then
            log_info "Wine prefix ready!"
            return 0
        else
            log_error "Wine prefix setup completed but fxccs.dll not found"
            return 1
        fi
    else
        log_error "Failed to run MonoGame Wine setup script"
        return 1
    fi
}

# =============================================================================
# Step 1: Extract and prepare Content sources
# =============================================================================
extract_content_sources() {
    log_info "Extracting Content sources from source.7z..."
    
    if [ ! -f "$GAME_DIR/source.7z" ]; then
        log_error "source.7z not found in game directory"
        return 1
    fi
    
    cd "$GAME_DIR"
    
    # Extract all Content source files
    7z x -y source.7z "ProjectZ/Content/*" >/dev/null 2>&1
    
    if [ ! -d "$GAME_DIR/ProjectZ/Content" ]; then
        log_error "Failed to extract Content sources from source.7z"
        return 1
    fi
    
    # Move to working directory
    CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    log_info "Content sources extracted to $CONTENT_SRC"
    
    return 0
}

# =============================================================================
# Step 2: Create derived Content source files
# =============================================================================
apply_content_patch() {
    local source_file="$1"
    local patch_file="$2"
    local output_file="$3"
    
    [ ! -f "$source_file" ] && { echo "  SKIP: Source not found $(basename "$source_file")"; return 1; }
    [ ! -f "$patch_file" ] && return 1
    [ -f "$output_file" ] && return 0
    
    if xdelta3 -d -f -s "$source_file" "$patch_file" "$output_file" 2>/dev/null; then
        echo "  CREATED: $(basename "$output_file")"
        DERIVED_FILES["$output_file"]=1
        FIXED=$((FIXED + 1))
        return 0
    else
        echo "  FAILED: $(basename "$output_file")"
        FAILED=$((FAILED + 1))
        return 1
    fi
}

create_derived_content() {
    log_info "Creating derived Content source files..."
    
    local CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    
    # Font derived files (smallFont -> smallFont_redux, smallFont_vwf, etc.)
    apply_content_patch "$CONTENT_SRC/Fonts/smallFont.png" "$PATCHES_DIR/smallFont_redux.png.xdelta" "$CONTENT_SRC/Fonts/smallFont_redux.png"
    apply_content_patch "$CONTENT_SRC/Fonts/smallFont.png" "$PATCHES_DIR/smallFont_vwf.png.xdelta" "$CONTENT_SRC/Fonts/smallFont_vwf.png"
    apply_content_patch "$CONTENT_SRC/Fonts/smallFont.png" "$PATCHES_DIR/smallFont_vwf_redux.png.xdelta" "$CONTENT_SRC/Fonts/smallFont_vwf_redux.png"
    
    # Menu derived files (menuBackground -> menuBackgroundB, menuBackgroundC)
    apply_content_patch "$CONTENT_SRC/Menu/menuBackground.png" "$PATCHES_DIR/menuBackgroundB.png.xdelta" "$CONTENT_SRC/Menu/menuBackgroundB.png"
    apply_content_patch "$CONTENT_SRC/Menu/menuBackground.png" "$PATCHES_DIR/menuBackgroundC.png.xdelta" "$CONTENT_SRC/Menu/menuBackgroundC.png"
    
    # SGB border (derived from itself or new - check if patch creates it)
    if [ -f "$PATCHES_DIR/sgb_border.png.xdelta" ]; then
        # Create empty base if needed, some patches work differently
        if [ -f "$CONTENT_SRC/Menu/menuBackground.png" ]; then
            apply_content_patch "$CONTENT_SRC/Menu/menuBackground.png" "$PATCHES_DIR/sgb_border.png.xdelta" "$CONTENT_SRC/Menu/sgb_border.png"
        fi
    fi
}

# =============================================================================
# Step 3: Apply in-place patches to Content sources
# =============================================================================
patch_content_sources() {
    log_info "Applying patches to Content source files..."
    
    local CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    
    # Patch font source files
    for font_file in "$CONTENT_SRC/Fonts"/*.png; do
        [ ! -f "$font_file" ] && continue
        local name=$(basename "$font_file")
        local patch="$PATCHES_DIR/$name.xdelta"
        
        # Skip derived files we just created
        [ -n "${DERIVED_FILES[$font_file]}" ] && continue
        
        if [ -f "$patch" ]; then
            local backup="${font_file}.orig"
            cp "$font_file" "$backup"
            if xdelta3 -d -f -s "$backup" "$patch" "$font_file" 2>/dev/null; then
                rm "$backup"
                echo "  PATCHED: $name"
                FIXED=$((FIXED + 1))
            else
                mv "$backup" "$font_file"
            fi
        fi
    done
    
    # Patch menu source files
    for menu_file in "$CONTENT_SRC/Menu"/*.png; do
        [ ! -f "$menu_file" ] && continue
        local name=$(basename "$menu_file")
        local patch="$PATCHES_DIR/$name.xdelta"
        
        [ -n "${DERIVED_FILES[$menu_file]}" ] && continue
        
        if [ -f "$patch" ]; then
            local backup="${menu_file}.orig"
            cp "$menu_file" "$backup"
            if xdelta3 -d -f -s "$backup" "$patch" "$menu_file" 2>/dev/null; then
                rm "$backup"
                echo "  PATCHED: $name"
                FIXED=$((FIXED + 1))
            else
                mv "$backup" "$menu_file"
            fi
        fi
    done
}

# =============================================================================
# Step 4: Fix .spritefont files for Linux fonts
# =============================================================================
fix_spritefonts() {
    log_info "Fixing spritefont files for Linux..."
    
    local CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    
    for sf in "$CONTENT_SRC/Fonts"/*.spritefont; do
        [ ! -f "$sf" ] && continue
        
        # Replace Windows fonts with Linux equivalents
        sed -i 's/<FontName>Segoe UI</<FontName>DejaVu Sans</g' "$sf"
        sed -i 's/<FontName>Courier New</<FontName>DejaVu Sans Mono</g' "$sf"
        
        echo "  FIXED: $(basename "$sf")"
    done
}

# =============================================================================
# Step 5: Generate extended Content.mgcb with derived files
# =============================================================================
generate_content_mgcb() {
    log_info "Generating extended Content.mgcb..."
    
    local CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    local MGCB="$CONTENT_SRC/Content.mgcb"
    
    # Append derived font entries to Content.mgcb
    cat >> "$MGCB" << 'EOF'

#-------------------------------- Derived Files --------------------------------#

#begin Fonts/smallFont_redux.png
/importer:TextureImporter
/processor:FontTextureProcessor
/processorParam:FirstCharacter= 
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Fonts/smallFont_redux.png

#begin Fonts/smallFont_vwf.png
/importer:TextureImporter
/processor:FontTextureProcessor
/processorParam:FirstCharacter= 
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Fonts/smallFont_vwf.png

#begin Fonts/smallFont_vwf_redux.png
/importer:TextureImporter
/processor:FontTextureProcessor
/processorParam:FirstCharacter= 
/processorParam:PremultiplyAlpha=True
/processorParam:TextureFormat=Color
/build:Fonts/smallFont_vwf_redux.png

#begin Menu/menuBackgroundB.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=True
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:Menu/menuBackgroundB.png

#begin Menu/menuBackgroundC.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=True
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:Menu/menuBackgroundC.png

#begin Menu/sgb_border.png
/importer:TextureImporter
/processor:TextureProcessor
/processorParam:ColorKeyColor=255,0,255,255
/processorParam:ColorKeyEnabled=True
/processorParam:GenerateMipmaps=False
/processorParam:PremultiplyAlpha=True
/processorParam:ResizeToPowerOfTwo=False
/processorParam:MakeSquare=False
/processorParam:TextureFormat=Color
/build:Menu/sgb_border.png
EOF

    # Change platform from MacOSX to DesktopGL
    sed -i 's|/platform:MacOSX|/platform:DesktopGL|g' "$MGCB"
    
    log_info "Content.mgcb updated with derived files and DesktopGL platform"
}

# =============================================================================
# Step 6: Compile ALL Content with MGCB
# =============================================================================
compile_all_content() {
    log_info "Compiling ALL Content for DesktopGL..."
    
    local CONTENT_SRC="$GAME_DIR/ProjectZ/Content"
    
    # Install mgcb if needed
    export PATH="$PATH:$HOME/.dotnet/tools"
    if ! command -v mgcb >/dev/null 2>&1; then
        log_info "Installing mgcb content builder..."
        dotnet tool install -g dotnet-mgcb >/dev/null 2>&1 || true
    fi
    
    if ! command -v mgcb >/dev/null 2>&1; then
        log_error "Failed to install mgcb"
        return 1
    fi
    
    # Set environment for Wine-based shader compilation
    export MGFXC_WINE_PATH="$WINE_PREFIX"
    export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1
    
    cd "$CONTENT_SRC"
    
    log_info "Running MGCB (this may take a while)..."
    
    # Run MGCB - it will compile everything listed in Content.mgcb
    if mgcb /@:Content.mgcb 2>&1 | tee /tmp/mgcb_output.log | grep -E "^(Build|Skip|Error|Warning|Processor)" | head -50; then
        local built_count=$(find bin/DesktopGL -name "*.xnb" 2>/dev/null | wc -l)
        log_info "MGCB built $built_count XNB files"
    else
        log_warn "MGCB completed with warnings/errors - check /tmp/mgcb_output.log"
    fi
    
    # Copy compiled content to game's Content folder
    if [ -d "bin/DesktopGL" ]; then
        log_info "Copying compiled Content to game directory..."
        cp -r bin/DesktopGL/* "$GAME_DIR/Content/"
        
        local copied=$(find "$GAME_DIR/Content" -name "*.xnb" | wc -l)
        log_info "Content folder now has $copied XNB files"
        FIXED=$((FIXED + copied))
    else
        log_error "MGCB output directory not found"
        return 1
    fi
    
    return 0
}

# =============================================================================
# Step 7: Create derived Data files
# =============================================================================
apply_derived_patch() {
    local source_file="$1"
    local patch_file="$2"
    local output_file="$3"
    
    [ ! -f "$source_file" ] && return
    [ ! -f "$patch_file" ] && return
    [ -f "$output_file" ] && return
    
    if xdelta3 -d -f -s "$source_file" "$patch_file" "$output_file" 2>/dev/null; then
        echo "  CREATED: $(basename "$output_file")"
        DERIVED_FILES["$output_file"]=1
        FIXED=$((FIXED + 1))
    else
        echo "  FAILED: $(basename "$output_file")"
        FAILED=$((FAILED + 1))
    fi
}

create_derived_data_files() {
    log_info "Creating derived Data files from original v1.0.0..."
    
    # UI/Intro/Objects variants for supported languages
    for lang in deu esp fre ind; do
        apply_derived_patch "$GAME_DIR/Data/ui.png" "$PATCHES_DIR/ui_${lang}.png.xdelta" "$GAME_DIR/Data/ui_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Intro/intro.png" "$PATCHES_DIR/intro_${lang}.png.xdelta" "$GAME_DIR/Data/Intro/intro_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Map Objects/objects.png" "$PATCHES_DIR/objects_${lang}.png.xdelta" "$GAME_DIR/Data/Map Objects/objects_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Map Objects/items.png" "$PATCHES_DIR/items_${lang}.png.xdelta" "$GAME_DIR/Data/Map Objects/items_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Map Objects/items.png" "$PATCHES_DIR/items_redux_${lang}.png.xdelta" "$GAME_DIR/Data/Map Objects/items_redux_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Photo Mode/photos.png" "$PATCHES_DIR/photos_${lang}.png.xdelta" "$GAME_DIR/Data/Photo Mode/photos_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Photo Mode/photos.png" "$PATCHES_DIR/photos_redux_${lang}.png.xdelta" "$GAME_DIR/Data/Photo Mode/photos_redux_${lang}.png"
        apply_derived_patch "$GAME_DIR/Data/Map Objects/minimap.png" "$PATCHES_DIR/minimap_${lang}.png.xdelta" "$GAME_DIR/Data/Map Objects/minimap_${lang}.png"
    done
    
    # Language files for all languages
    for lang in deu esp fre ind ita por rus; do
        apply_derived_patch "$GAME_DIR/Data/Languages/eng.lng" "$PATCHES_DIR/${lang}.lng.xdelta" "$GAME_DIR/Data/Languages/${lang}.lng"
        apply_derived_patch "$GAME_DIR/Data/Languages/dialog_eng.lng" "$PATCHES_DIR/dialog_${lang}.lng.xdelta" "$GAME_DIR/Data/Languages/dialog_${lang}.lng"
    done
    
    # Redux/special variants
    apply_derived_patch "$GAME_DIR/Data/Map Objects/items.png" "$PATCHES_DIR/items_redux.png.xdelta" "$GAME_DIR/Data/Map Objects/items_redux.png"
    apply_derived_patch "$GAME_DIR/Data/Photo Mode/photos.png" "$PATCHES_DIR/photos_redux.png.xdelta" "$GAME_DIR/Data/Photo Mode/photos_redux.png"
    apply_derived_patch "$GAME_DIR/Data/Map Objects/npcs.png" "$PATCHES_DIR/npcs_redux.png.xdelta" "$GAME_DIR/Data/Map Objects/npcs_redux.png"
    apply_derived_patch "$GAME_DIR/Data/Map Objects/link0.png" "$PATCHES_DIR/link1.png.xdelta" "$GAME_DIR/Data/Map Objects/link1.png"
    apply_derived_patch "$GAME_DIR/Data/musicOverworld.data" "$PATCHES_DIR/musicOverworldClassic.data.xdelta" "$GAME_DIR/Data/musicOverworldClassic.data"
    apply_derived_patch "$GAME_DIR/Data/Animations/NPCs/BowWow.ani" "$PATCHES_DIR/bowwow_water.ani.xdelta" "$GAME_DIR/Data/Animations/NPCs/bowwow_water.ani"
}

# =============================================================================
# Step 8: Apply in-place patches to Data files
# =============================================================================
apply_inplace_patch() {
    local target_file="$1"
    local patch_file="$2"
    
    [ -n "${DERIVED_FILES[$target_file]}" ] && return
    [ ! -f "$target_file" ] && return
    [ ! -f "$patch_file" ] && return
    
    local backup="${target_file}.orig"
    cp "$target_file" "$backup"
    
    if xdelta3 -d -f -s "$backup" "$patch_file" "$target_file" 2>/dev/null; then
        rm "$backup"
        echo "  PATCHED: $(basename "$target_file")"
        FIXED=$((FIXED + 1))
    else
        mv "$backup" "$target_file"
    fi
}

apply_data_patches() {
    log_info "Applying xdelta patches to Data files..."
    
    while IFS= read -r file; do
        patch_name="$(basename "$file").xdelta"
        patch_file="$PATCHES_DIR/$patch_name"
        apply_inplace_patch "$file" "$patch_file"
    done < <(find "$GAME_DIR/Data" -type f \( -name "*.png" -o -name "*.atlas" -o -name "*.lng" -o -name "*.data" -o -name "*.ani" -o -name "*.zScript" \) 2>/dev/null)
}

# =============================================================================
# Step 9: Fix case sensitivity issues for Linux
# =============================================================================
fix_case_sensitivity() {
    log_info "Fixing case sensitivity issues for Linux..."
    
    # Known case mismatches between code and files
    # Code uses lowercase, files may have mixed case
    local fixes=0
    
    # spiny Beetle.ani -> spiny beetle.ani
    if [ -f "$GAME_DIR/Data/Animations/Enemies/spiny Beetle.ani" ]; then
        mv "$GAME_DIR/Data/Animations/Enemies/spiny Beetle.ani" "$GAME_DIR/Data/Animations/Enemies/spiny beetle.ani"
        echo "  FIXED: spiny Beetle.ani -> spiny beetle.ani"
        fixes=$((fixes + 1))
    fi
    
    log_info "Fixed $fixes case sensitivity issues"
    FIXED=$((FIXED + fixes))
}

# =============================================================================
# Step 10: Cleanup
# =============================================================================
cleanup() {
    log_info "Cleaning up temporary files..."
    rm -rf "$GAME_DIR/ProjectZ"
}

# =============================================================================
# Main
# =============================================================================
echo "=============================================="
echo "  LADXHD Linux Asset Setup"
echo "=============================================="
echo ""
echo "Game directory: $GAME_DIR"
echo ""

# Run all steps
setup_wine_prefix || exit 1
echo ""

extract_content_sources || exit 1
echo ""

create_derived_content
echo ""

patch_content_sources
echo ""

fix_spritefonts
echo ""

generate_content_mgcb
echo ""

compile_all_content || exit 1
echo ""

create_derived_data_files
echo ""

apply_data_patches
echo ""

fix_case_sensitivity
echo ""

cleanup
echo ""

# Summary
echo "=============================================="
echo "  Summary"
echo "=============================================="
echo "  Items processed: $FIXED"
echo "  Failures: $FAILED"
echo ""

if [ $FAILED -eq 0 ]; then
    log_info "✓ Linux assets ready!"
    exit 0
else
    log_warn "✗ Some items failed - check output above"
    exit 1
fi
