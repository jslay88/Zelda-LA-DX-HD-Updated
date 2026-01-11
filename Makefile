# =============================================================================
# Link's Awakening DX HD - Cross-Platform Makefile
# =============================================================================
# Usage:
#   make build          - Build for current platform
#   make build-windows  - Build Windows binary
#   make build-linux    - Build Linux binary
#   make build-all      - Build both platforms
#   make shaders        - Compile shaders for both platforms
#   make clean          - Clean build artifacts
#   make test-windows   - Build and prepare Windows test environment
#   make test-linux     - Build and prepare Linux test environment
#   make release        - Create release packages (requires shaders)
# =============================================================================

# Configuration
DOTNET := dotnet
PROJECT_DIR := ladxhd_game_source_code
PROJECT := $(PROJECT_DIR)/ProjectZ.csproj
PUBLISH_DIR := publish
CONTENT_DIR := $(PROJECT_DIR)/Content
SHADER_DIR := $(CONTENT_DIR)/Shader
PATCHES_DIR := assets_patches

# Version from csproj
VERSION := $(shell grep -oP '(?<=<Version>)[^<]+' $(PROJECT))

# Wine prefix for Linux shader compilation
WINE_PREFIX := $(HOME)/.winemonogame
export MGFXC_WINE_PATH := $(WINE_PREFIX)
export DOTNET_SYSTEM_GLOBALIZATION_INVARIANT := 1

# Add .NET tools to PATH
DOTNET_TOOLS := $(HOME)/.dotnet/tools
export PATH := $(DOTNET_TOOLS):$(PATH)

# Detect current OS
UNAME_S := $(shell uname -s)
ifeq ($(UNAME_S),Linux)
    CURRENT_OS := Linux
else ifeq ($(UNAME_S),Darwin)
    CURRENT_OS := macOS
else
    CURRENT_OS := Windows
endif

# Colors for output
RED := \033[0;31m
GREEN := \033[0;32m
YELLOW := \033[1;33m
CYAN := \033[0;36m
NC := \033[0m

.PHONY: all build build-windows build-linux build-all shaders shaders-windows shaders-linux \
        clean clean-all test-windows test-linux release help check-deps info

# Default target
all: build

# =============================================================================
# Build Targets
# =============================================================================

build:
ifeq ($(CURRENT_OS),Linux)
	@$(MAKE) build-linux
else
	@$(MAKE) build-windows
endif

build-windows:
	@echo "$(GREEN)Building for Windows...$(NC)"
	cd $(PROJECT_DIR) && $(DOTNET) publish -c Release -r win-x64 \
		-p:TargetPlatformName=Windows \
		-p:SkipContentBuild=true \
		-p:PublishSingleFile=true \
		-p:EnableWindowsTargeting=true \
		--self-contained true \
		-o ../$(PUBLISH_DIR)/Windows
	@echo "$(GREEN)✓ Windows build complete: $(PUBLISH_DIR)/Windows/$(NC)"

build-linux:
	@echo "$(GREEN)Building for Linux...$(NC)"
	cd $(PROJECT_DIR) && $(DOTNET) publish -c Release -r linux-x64 \
		-p:TargetPlatformName=Linux \
		-p:SkipContentBuild=true \
		-p:PublishSingleFile=true \
		--self-contained true \
		-o ../$(PUBLISH_DIR)/Linux
	chmod +x "$(PUBLISH_DIR)/Linux/Link's Awakening DX HD"
	@echo "$(GREEN)✓ Linux build complete: $(PUBLISH_DIR)/Linux/$(NC)"

build-all: build-windows build-linux
	@echo "$(GREEN)✓ All builds complete$(NC)"

# =============================================================================
# Shader Compilation
# =============================================================================

# Install MGCB tool if needed
install-mgcb:
	@command -v mgcb >/dev/null 2>&1 || { \
		echo "$(YELLOW)Installing MGCB...$(NC)"; \
		$(DOTNET) tool install -g dotnet-mgcb; \
	}

# Setup Wine prefix for Linux shader compilation (required for DesktopGL shaders)
setup-wine:
ifeq ($(CURRENT_OS),Linux)
	@if [ ! -f "$(WINE_PREFIX)/drive_c/fxccs.dll" ]; then \
		echo "$(YELLOW)Setting up Wine prefix for shader compilation...$(NC)"; \
		echo "$(YELLOW)This downloads ~350MB and may take several minutes...$(NC)"; \
		wget -qO- https://monogame.net/downloads/net9_mgfxc_wine_setup.sh | bash; \
	else \
		echo "$(GREEN)Wine prefix already configured$(NC)"; \
	fi
endif

# Compile shaders for Windows (DirectX)
shaders-windows: install-mgcb
	@echo "$(GREEN)Compiling shaders for Windows (DirectX)...$(NC)"
	@mkdir -p $(CONTENT_DIR)/bin/Windows/Shader
	@cd $(CONTENT_DIR) && $(DOTNET_TOOLS)/mgcb \
		/platform:Windows \
		/profile:HiDef \
		/outputDir:bin/Windows \
		/intermediateDir:obj/Windows \
		$(foreach fx,$(wildcard $(SHADER_DIR)/*.fx),/importer:EffectImporter /processor:EffectProcessor /build:Shader/$(notdir $(fx)))
	@echo "$(GREEN)✓ Windows shaders compiled to $(CONTENT_DIR)/bin/Windows/Shader/$(NC)"

# Compile shaders for Linux (DesktopGL/OpenGL)
shaders-linux: install-mgcb setup-wine
	@echo "$(GREEN)Compiling shaders for Linux (DesktopGL)...$(NC)"
	@mkdir -p $(CONTENT_DIR)/bin/DesktopGL/Shader
	@cd $(CONTENT_DIR) && \
		MGFXC_WINE_PATH=$(WINE_PREFIX) \
		DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 \
		$(DOTNET_TOOLS)/mgcb \
		/platform:DesktopGL \
		/profile:HiDef \
		/outputDir:bin/DesktopGL \
		/intermediateDir:obj/DesktopGL \
		$(foreach fx,$(wildcard $(SHADER_DIR)/*.fx),/importer:EffectImporter /processor:EffectProcessor /build:Shader/$(notdir $(fx)))
	@echo "$(GREEN)✓ DesktopGL shaders compiled to $(CONTENT_DIR)/bin/DesktopGL/Shader/$(NC)"

# Compile all shaders
shaders: shaders-windows shaders-linux
	@echo "$(GREEN)✓ All shaders compiled$(NC)"

# =============================================================================
# Test Environment Setup
# =============================================================================

# Create a test environment for Windows
# Requires: "Links Awakening DX HD v1.0.0.zip" in project root
test-windows: build-windows
	@echo "$(CYAN)Setting up Windows test environment...$(NC)"
	@if [ ! -f "Links Awakening DX HD v1.0.0.zip" ]; then \
		echo "$(RED)Error: 'Links Awakening DX HD v1.0.0.zip' not found$(NC)"; \
		echo "Please provide the original v1.0.0 release zip file."; \
		exit 1; \
	fi
	@mkdir -p test/Windows
	@echo "  Extracting v1.0.0 assets (Content/ and Data/ only)..."
	@unzip -q -o "Links Awakening DX HD v1.0.0.zip" -d test/Windows/
	@if [ -d "test/Windows/Links Awakening DX HD" ]; then \
		mv test/Windows/Links\ Awakening\ DX\ HD/* test/Windows/; \
		rmdir "test/Windows/Links Awakening DX HD"; \
	fi
	@# Remove files not needed for runtime (source.7z contains copyrighted source assets)
	@rm -f test/Windows/source.7z test/Windows/*.exe test/Windows/*.dll test/Windows/*.config 2>/dev/null || true
	@echo "  Copying Windows binary..."
	@cp "$(PUBLISH_DIR)/Windows/Link's Awakening DX HD.exe" test/Windows/
	@cp $(PUBLISH_DIR)/Windows/*.dll test/Windows/ 2>/dev/null || true
	@echo "$(GREEN)✓ Windows test environment ready: test/Windows/$(NC)"
	@echo "$(YELLOW)Run: cd test/Windows && wine 'Link'\"'\"'s Awakening DX HD.exe'$(NC)"

# Create a test environment for Linux
test-linux: build-linux shaders-linux
	@echo "$(CYAN)Setting up Linux test environment...$(NC)"
	@if [ ! -f "Links Awakening DX HD v1.0.0.zip" ]; then \
		echo "$(RED)Error: 'Links Awakening DX HD v1.0.0.zip' not found$(NC)"; \
		echo "Please provide the original v1.0.0 release zip file."; \
		exit 1; \
	fi
	@mkdir -p test/Linux
	@echo "  Extracting v1.0.0 assets (Content/ and Data/ only)..."
	@unzip -q -o "Links Awakening DX HD v1.0.0.zip" -d test/Linux/
	@if [ -d "test/Linux/Links Awakening DX HD" ]; then \
		mv test/Linux/Links\ Awakening\ DX\ HD/* test/Linux/; \
		rmdir "test/Linux/Links Awakening DX HD"; \
	fi
	@# Remove files not needed for runtime (source.7z contains copyrighted source assets)
	@rm -f test/Linux/source.7z test/Linux/*.exe test/Linux/*.dll test/Linux/*.config 2>/dev/null || true
	@echo "  Copying Linux binary..."
	@cp "$(PUBLISH_DIR)/Linux/Link's Awakening DX HD" test/Linux/
	@cp $(PUBLISH_DIR)/Linux/*.so* test/Linux/ 2>/dev/null || true
	@echo "  Setting up DesktopGL shaders (Shaders-DesktopGL/)..."
	@mkdir -p test/Linux/Shaders-DesktopGL
	@cp $(CONTENT_DIR)/bin/DesktopGL/Shader/*.xnb test/Linux/Shaders-DesktopGL/ 2>/dev/null || true
	@echo "$(GREEN)✓ Linux test environment ready: test/Linux/$(NC)"
	@echo "$(YELLOW)Run: cd test/Linux && ./Link's\\ Awakening\\ DX\\ HD$(NC)"
	@echo "$(YELLOW)Note: Game will auto-install shaders and patch Data on first run$(NC)"

# Full Linux setup (compiles ALL Content from source.7z - requires Wine)
# NOTE: This target keeps source.7z because setup_linux_assets.sh needs it
# to compile PNG/WAV/spritefont files into XNB format for DesktopGL
test-linux-full: build-linux
	@echo "$(CYAN)Setting up Linux test environment with FULL Content compilation...$(NC)"
	@if [ ! -f "Links Awakening DX HD v1.0.0.zip" ]; then \
		echo "$(RED)Error: 'Links Awakening DX HD v1.0.0.zip' not found$(NC)"; \
		echo "Please provide the original v1.0.0 release zip file."; \
		exit 1; \
	fi
	@mkdir -p test/Linux-Full
	@echo "  Extracting v1.0.0 assets..."
	@unzip -q -o "Links Awakening DX HD v1.0.0.zip" -d test/Linux-Full/
	@if [ -d "test/Linux-Full/Links Awakening DX HD" ]; then \
		mv test/Linux-Full/Links\ Awakening\ DX\ HD/* test/Linux-Full/; \
		rmdir "test/Linux-Full/Links Awakening DX HD"; \
	fi
	@echo "  Copying Linux binary..."
	@cp "$(PUBLISH_DIR)/Linux/Link's Awakening DX HD" test/Linux-Full/
	@echo "  Running setup_linux_assets.sh (compiles ALL Content)..."
	@./setup_linux_assets.sh test/Linux-Full
	@echo "$(GREEN)✓ Linux full test environment ready: test/Linux-Full/$(NC)"
	@echo "$(YELLOW)Run: cd test/Linux-Full && ./Link's\\ Awakening\\ DX\\ HD$(NC)"

# Quick test (just copy new binary to existing test dir)
test-quick-linux: build-linux
	@if [ -d "test/Linux" ]; then \
		cp "$(PUBLISH_DIR)/Linux/Link's Awakening DX HD" test/Linux/; \
		echo "$(GREEN)✓ Updated binary in test/Linux/$(NC)"; \
	else \
		echo "$(RED)test/Linux doesn't exist. Run 'make test-linux' first.$(NC)"; \
	fi

test-quick-windows: build-windows
	@if [ -d "test/Windows" ]; then \
		cp "$(PUBLISH_DIR)/Windows/Link's Awakening DX HD.exe" test/Windows/; \
		echo "$(GREEN)✓ Updated binary in test/Windows/$(NC)"; \
	else \
		echo "$(RED)test/Windows doesn't exist. Run 'make test-windows' first.$(NC)"; \
	fi

# =============================================================================
# Release Packaging
# =============================================================================

release: build-all shaders
	@echo "$(CYAN)Creating release packages...$(NC)"
	@mkdir -p release
	
	# Windows release (with Windows shaders in Shaders-Windows/)
	@echo "  Packaging Windows..."
	@mkdir -p $(PUBLISH_DIR)/Windows/Shaders-Windows
	@cp $(CONTENT_DIR)/bin/Windows/Shader/*.xnb $(PUBLISH_DIR)/Windows/Shaders-Windows/ 2>/dev/null || true
	@rm -f release/LADXHD-Windows-x64-v$(VERSION).zip
	@cd $(PUBLISH_DIR)/Windows && zip -r ../../release/LADXHD-Windows-x64-v$(VERSION).zip .
	
	# Linux release (with DesktopGL shaders in Shaders-DesktopGL/)
	@echo "  Packaging Linux..."
	@mkdir -p $(PUBLISH_DIR)/Linux/Shaders-DesktopGL
	@cp $(CONTENT_DIR)/bin/DesktopGL/Shader/*.xnb $(PUBLISH_DIR)/Linux/Shaders-DesktopGL/ 2>/dev/null || true
	@cp setup_linux_assets.sh $(PUBLISH_DIR)/Linux/ 2>/dev/null || true
	@chmod +x $(PUBLISH_DIR)/Linux/setup_linux_assets.sh 2>/dev/null || true
	@rm -f release/LADXHD-Linux-x64-v$(VERSION).tar.gz
	@cd $(PUBLISH_DIR)/Linux && tar -czvf ../../release/LADXHD-Linux-x64-v$(VERSION).tar.gz .
	
	@echo "$(GREEN)✓ Release packages created in release/$(NC)"
	@ls -la release/

# =============================================================================
# Cleanup
# =============================================================================

clean:
	@echo "$(YELLOW)Cleaning build artifacts...$(NC)"
	@rm -rf $(PROJECT_DIR)/bin $(PROJECT_DIR)/obj
	@rm -rf $(PUBLISH_DIR)
	@rm -rf $(CONTENT_DIR)/bin $(CONTENT_DIR)/obj
	@echo "$(GREEN)✓ Clean complete$(NC)"

clean-all: clean
	@echo "$(YELLOW)Cleaning test environments...$(NC)"
	@rm -rf test
	@rm -rf release
	@echo "$(GREEN)✓ Full clean complete$(NC)"

# =============================================================================
# Utilities
# =============================================================================

check-deps:
	@echo "$(CYAN)Checking dependencies...$(NC)"
	@echo -n "  dotnet SDK: "
	@$(DOTNET) --version 2>/dev/null || echo "$(RED)NOT FOUND$(NC)"
	@echo -n "  mgcb: "
	@command -v mgcb >/dev/null 2>&1 && mgcb --version 2>/dev/null | head -1 || echo "$(YELLOW)not installed (run: dotnet tool install -g dotnet-mgcb)$(NC)"
	@echo -n "  xdelta3: "
	@command -v xdelta3 >/dev/null 2>&1 && xdelta3 -V 2>&1 | head -1 || echo "$(YELLOW)not installed$(NC)"
	@echo -n "  wine: "
	@command -v wine >/dev/null 2>&1 && wine --version 2>/dev/null || echo "$(YELLOW)not installed (needed for DesktopGL shaders on Linux)$(NC)"
	@echo -n "  7z: "
	@command -v 7z >/dev/null 2>&1 && echo "installed" || echo "$(YELLOW)not installed$(NC)"
	@echo -n "  unzip: "
	@command -v unzip >/dev/null 2>&1 && echo "installed" || echo "$(YELLOW)not installed$(NC)"

info:
	@echo "$(CYAN)Project Information$(NC)"
	@echo "  Version: $(VERSION)"
	@echo "  Current OS: $(CURRENT_OS)"
	@echo "  Project: $(PROJECT)"
	@echo ""
	@echo "$(CYAN)Directories$(NC)"
	@echo "  Publish: $(PUBLISH_DIR)/"
	@echo "  Content: $(CONTENT_DIR)/"
	@echo "  Patches: $(PATCHES_DIR)/"
	@echo ""
	@echo "$(CYAN)Shader Files$(NC)"
	@ls -1 $(SHADER_DIR)/*.fx 2>/dev/null | wc -l | xargs -I {} echo "  {} shader files found"
	@echo ""
	@echo "$(CYAN)Patch Files$(NC)"
	@ls -1 $(PATCHES_DIR)/*.xdelta 2>/dev/null | wc -l | xargs -I {} echo "  {} xdelta patches found"

help:
	@echo "$(CYAN)Link's Awakening DX HD - Build System$(NC)"
	@echo ""
	@echo "$(GREEN)Build Commands:$(NC)"
	@echo "  make build          Build for current platform"
	@echo "  make build-windows  Build Windows binary"
	@echo "  make build-linux    Build Linux binary"
	@echo "  make build-all      Build both platforms"
	@echo ""
	@echo "$(GREEN)Shader Commands:$(NC)"
	@echo "  make shaders          Compile shaders for both platforms"
	@echo "  make shaders-windows  Compile Windows (DirectX) shaders"
	@echo "  make shaders-linux    Compile Linux (DesktopGL) shaders"
	@echo "  make setup-wine       Setup Wine prefix for shader compilation"
	@echo ""
	@echo "$(GREEN)Test Commands:$(NC)"
	@echo "  make test-windows      Full Windows test env setup"
	@echo "  make test-linux        Full Linux test env setup"
	@echo "  make test-quick-linux  Quick binary update for existing test env"
	@echo ""
	@echo "$(GREEN)Other Commands:$(NC)"
	@echo "  make release     Create release packages"
	@echo "  make clean       Clean build artifacts"
	@echo "  make clean-all   Clean everything including test envs"
	@echo "  make check-deps  Check required dependencies"
	@echo "  make info        Show project information"
	@echo ""
	@echo "$(YELLOW)Prerequisites:$(NC)"
	@echo "  - .NET SDK 6.0+"
	@echo "  - dotnet-mgcb tool (for shader compilation)"
	@echo "  - Wine + d3dcompiler_47 (for DesktopGL shaders on Linux)"
	@echo "  - xdelta3 (for runtime patching)"
	@echo "  - 'Links Awakening DX HD v1.0.0.zip' (for test environments)"
