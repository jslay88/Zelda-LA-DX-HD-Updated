#!/bin/bash
# Linux Asset Migration Script for Zelda: Link's Awakening DX HD
# This script replicates the functionality of LADXHD_Migrater.exe
# Requires: xdelta3

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_ORIGINAL="$SCRIPT_DIR/assets_original"
ASSETS_PATCHES="$SCRIPT_DIR/assets_patches"
GAME_SOURCE="$SCRIPT_DIR/ladxhd_game_source_code"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

echo_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
echo_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
echo_error() { echo -e "${RED}[ERROR]${NC} $1"; }
echo_note() { echo -e "${CYAN}[NOTE]${NC} $1"; }

check_dependencies() {
    if ! command -v xdelta3 &> /dev/null; then
        echo_error "xdelta3 is not installed!"
        echo "Please install xdelta3:"
        echo "  Arch Linux: sudo pacman -S xdelta3"
        echo "  Debian/Ubuntu: sudo apt install xdelta3"
        echo "  Fedora: sudo dnf install xdelta3"
        exit 1
    fi
}

check_assets() {
    if [[ ! -d "$ASSETS_ORIGINAL/Content" ]]; then
        echo_error "Content folder not found in assets_original/"
        echo "Please follow the README instructions to set up the Content folder from source.7z"
        exit 1
    fi
    if [[ ! -d "$ASSETS_ORIGINAL/Data" ]]; then
        echo_error "Data folder not found in assets_original/"
        echo "Please follow the README instructions to set up the Data folder"
        exit 1
    fi
}

# Apply xdelta patch if it exists, otherwise copy the file
process_file() {
    local src_file="$1"
    local dest_file="$2"
    local filename=$(basename "$src_file")
    local patch_file="$ASSETS_PATCHES/${filename}.xdelta"
    
    mkdir -p "$(dirname "$dest_file")"
    
    if [[ -f "$patch_file" ]]; then
        if ! xdelta3 -d -f -s "$src_file" "$patch_file" "$dest_file" 2>/dev/null; then
            echo_warn "Failed to apply patch for $filename, copying original"
            cp "$src_file" "$dest_file"
        fi
    else
        cp "$src_file" "$dest_file"
    fi
}

# Handle files that generate multiple output files from one source
handle_multi_file_patches() {
    local src_file="$1"
    local dest_dir="$2"
    local filename=$(basename "$src_file")
    local targets=""
    
    case "$filename" in
        "eng.lng")
            targets="deu.lng esp.lng fre.lng ind.lng ita.lng por.lng rus.lng"
            ;;
        "dialog_eng.lng")
            targets="dialog_deu.lng dialog_esp.lng dialog_fre.lng dialog_ind.lng dialog_ita.lng dialog_por.lng dialog_rus.lng"
            ;;
        "smallFont.png")
            targets="smallFont_redux.png smallFont_vwf.png smallFont_vwf_redux.png"
            ;;
        "menuBackground.png")
            targets="menuBackgroundB.png menuBackgroundC.png sgb_border.png"
            ;;
        "link0.png")
            targets="link1.png"
            ;;
        "npcs.png")
            targets="npcs_redux.png"
            ;;
        "items.png")
            targets="items_deu.png items_esp.png items_fre.png items_ind.png items_ita.png items_por.png items_rus.png items_redux.png items_redux_deu.png items_redux_esp.png items_redux_fre.png items_redux_ind.png items_redux_ita.png items_redux_por.png items_redux_rus.png"
            ;;
        "intro.png")
            targets="intro_deu.png intro_esp.png intro_fre.png intro_ind.png intro_ita.png intro_por.png intro_rus.png"
            ;;
        "minimap.png")
            targets="minimap_deu.png minimap_esp.png minimap_fre.png minimap_ind.png minimap_ita.png minimap_por.png minimap_rus.png"
            ;;
        "objects.png")
            targets="objects_deu.png objects_esp.png objects_fre.png objects_ind.png objects_ita.png objects_por.png objects_rus.png"
            ;;
        "photos.png")
            targets="photos_deu.png photos_esp.png photos_fre.png photos_ind.png photos_ita.png photos_por.png photos_rus.png photos_redux.png photos_redux_deu.png photos_redux_esp.png photos_redux_fre.png photos_redux_ind.png photos_redux_ita.png photos_redux_por.png photos_redux_rus.png"
            ;;
        "ui.png")
            targets="ui_deu.png ui_esp.png ui_fre.png ui_ind.png ui_ita.png ui_por.png ui_rus.png"
            ;;
        "musicOverworld.data")
            targets="musicOverworldClassic.data"
            ;;
        "dungeon3_1.map")
            targets="dungeon3.map"
            ;;
        "dungeon3_1.map.data")
            targets="dungeon3.map.data"
            ;;
        "BowWow.ani")
            targets="bowwow_water.ani"
            ;;
    esac
    
    if [[ -n "$targets" ]]; then
        for target_file in $targets; do
            local patch_file="$ASSETS_PATCHES/${target_file}.xdelta"
            local output_file="$dest_dir/$target_file"
            
            if [[ -f "$patch_file" ]]; then
                if ! xdelta3 -d -f -s "$src_file" "$patch_file" "$output_file" 2>/dev/null; then
                    echo_warn "Failed to create $target_file from $filename"
                fi
            fi
        done
    fi
}

# Fix fonts for Linux compatibility
fix_linux_fonts() {
    local fonts_dir="$GAME_SOURCE/Content/Fonts"
    
    if [[ -d "$fonts_dir" ]]; then
        echo_info "Fixing fonts for Linux compatibility..."
        find "$fonts_dir" -name "*.spritefont" -exec sed -i 's/Segoe UI/Noto Sans/g' {} \;
        find "$fonts_dir" -name "*.spritefont" -exec sed -i 's/Courier New/Liberation Mono/g' {} \;
    fi
}

migrate_folder() {
    local src_folder="$1"
    local dest_folder="$2"
    local folder_name=$(basename "$src_folder")
    
    echo_info "Migrating $folder_name folder..."
    
    if [[ -d "$dest_folder" ]]; then
        rm -rf "$dest_folder"
    fi
    
    mkdir -p "$dest_folder"
    
    local file_count=0
    while IFS= read -r -d '' file; do
        if [[ "$file" == *"/bin/"* ]] || [[ "$file" == *"/obj/"* ]]; then
            continue
        fi
        
        local rel_path="${file#$src_folder/}"
        local dest_file="$dest_folder/$rel_path"
        local dest_dir=$(dirname "$dest_file")
        
        process_file "$file" "$dest_file"
        handle_multi_file_patches "$file" "$dest_dir"
        
        file_count=$((file_count + 1))
    done < <(find "$src_folder" -type f -print0)
    
    echo_info "Processed $file_count files from $folder_name"
}

show_help() {
    echo "Zelda LA DX HD - Linux Asset Migration Script"
    echo ""
    echo "Usage: $0 [OPTIONS]"
    echo ""
    echo "Options:"
    echo "  -y, --yes     Skip confirmation prompts"
    echo "  -h, --help    Show this help message"
    echo ""
    echo "This script migrates assets from v1.0.0 to the latest version."
    echo ""
    echo "Requirements:"
    echo "  - xdelta3 installed"
    echo "  - Content folder from source.7z in assets_original/"
    echo "  - Data folder from game folder in assets_original/"
    echo ""
    echo "Note: Building on Linux requires Wine with d3dcompiler_47 for shader"
    echo "compilation, OR you can copy pre-compiled Content from a Windows build."
}

main() {
    echo "============================================"
    echo "  Zelda LA DX HD - Linux Asset Migration"
    echo "============================================"
    echo
    
    check_dependencies
    check_assets
    
    echo_info "Source: $ASSETS_ORIGINAL"
    echo_info "Destination: $GAME_SOURCE"
    echo
    
    read -p "This will overwrite existing Content/Data folders. Continue? [y/N] " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo_info "Migration cancelled."
        exit 0
    fi
    
    migrate_folder "$ASSETS_ORIGINAL/Content" "$GAME_SOURCE/Content"
    migrate_folder "$ASSETS_ORIGINAL/Data" "$GAME_SOURCE/Data"
    fix_linux_fonts
    
    echo
    echo_info "Migration complete!"
    echo
    echo_note "To build on Linux, you have two options:"
    echo "  1. Set up Wine with d3dcompiler_47 for shader compilation"
    echo "     (See MonoGame docs: https://docs.monogame.net/articles/getting_started/1_setting_up_your_development_environment_linux.html)"
    echo "  2. Copy pre-compiled Content folder from a Windows build"
    echo "     (The .xnb files are cross-platform compatible)"
}

# Parse arguments
AUTO_YES=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        -y|--yes)
            AUTO_YES=true
            shift
            ;;
        -h|--help)
            show_help
            exit 0
            ;;
        *)
            echo_error "Unknown option: $1"
            show_help
            exit 1
            ;;
    esac
done

if [[ "$AUTO_YES" == "true" ]]; then
    check_dependencies
    check_assets
    migrate_folder "$ASSETS_ORIGINAL/Content" "$GAME_SOURCE/Content"
    migrate_folder "$ASSETS_ORIGINAL/Data" "$GAME_SOURCE/Data"
    fix_linux_fonts
    echo "Migration complete!"
else
    main
fi
