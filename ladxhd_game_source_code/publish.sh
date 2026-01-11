#!/bin/bash
# Linux build script for Link's Awakening DX HD
# Usage: ./publish.sh [windows|linux|all]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

echo_info() { echo -e "${GREEN}[INFO]${NC} $1"; }
echo_warn() { echo -e "${YELLOW}[WARN]${NC} $1"; }
echo_error() { echo -e "${RED}[ERROR]${NC} $1"; }

check_dotnet() {
    if ! command -v dotnet &> /dev/null; then
        echo_error "dotnet SDK is not installed!"
        echo "Please install .NET 6.0 SDK:"
        echo "  https://dotnet.microsoft.com/download/dotnet/6.0"
        exit 1
    fi
    
    # Check for .NET 6.0
    if ! dotnet --list-sdks | grep -q "^6\."; then
        echo_warn ".NET 6.0 SDK not found. Build may fail."
    fi
}

build_windows() {
    echo_info "Building for Windows (win-x64)..."
    dotnet publish -c Release -r win-x64 -p:TargetPlatformName=Windows -p:PublishSingleFile=true --self-contained true -o Publish/Windows
    echo_info "Windows build complete: Publish/Windows/"
}

build_linux() {
    echo_info "Building for Linux (linux-x64)..."
    dotnet publish -c Release -r linux-x64 -p:TargetPlatformName=Linux -p:PublishSingleFile=true --self-contained true -o Publish/Linux
    
    # Make the executable... executable
    chmod +x "Publish/Linux/Link's Awakening DX HD"
    
    echo_info "Linux build complete: Publish/Linux/"
}

show_help() {
    echo "Link's Awakening DX HD - Build Script"
    echo ""
    echo "Usage: $0 [OPTION]"
    echo ""
    echo "Options:"
    echo "  windows    Build for Windows only"
    echo "  linux      Build for Linux only"
    echo "  all        Build for both platforms (default)"
    echo "  help       Show this help message"
    echo ""
}

# Main
check_dotnet

case "${1:-all}" in
    windows)
        build_windows
        ;;
    linux)
        build_linux
        ;;
    all)
        build_windows
        build_linux
        ;;
    help|--help|-h)
        show_help
        ;;
    *)
        echo_error "Unknown option: $1"
        show_help
        exit 1
        ;;
esac

echo ""
echo_info "Build process complete!"
