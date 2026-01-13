# The Legend of Zelda: Links Awakening DX HD - PC Port

### $${\color{red}THIS \space REPOSITORY \space DOES \space NOT \space INCLUDE \space COPYRIGHTED \space GAME \space ASSETS!}$$

To use this fork, whether to play the game or build upon, it requires the user to provide the assets from the original v1.0.0 release.   
Some assets have been updated, but I have created tooling to make migration easier.

- This is a continuation of my [previous fork](https://github.com/BigheadSMZ/Links-Awakening-DX-HD) and here's a link to the [commits](https://github.com/BigheadSMZ/Links-Awakening-DX-HD/commits/master/).
- See the [manual](https://github.com/BigheadSMZ/Zelda-LA-DX-HD-Updated/blob/main/MANUAL.md) to learn more about the game (WIP).
- See the [changelog](https://github.com/BigheadSMZ/Zelda-LA-DX-HD-Updated/blob/main/CHANGELOG.md) for a list of changes from v1.0.0.
- As of v1.1.0, the game is in a really good state and the "feel" is really close to the original game.  
- As of v1.2.0, the vast majority of bugs have been fixed and features from the [Redux romhack](https://github.com/ShadowOne333/Links-Awakening-Redux) were implemented.
- As of v1.3.0, I consider the work that I've done to be "feature complete" and everything from this point is gravy.
- As of v1.4.0, the gravy train never stopped and much work has been done to make this port more accurate.
- As of v1.5.0, it has evolved into something I never dreamed of. Hundreds of issues fixed with tons of features.

## Getting Started (Recommended)

The easiest way to play is using the **auto-patching game binary** from the [Releases](https://github.com/BigheadSMZ/Zelda-LA-DX-HD-Updated/releases) page:

### Windows

1. **Install [.NET Desktop Runtime 6.0](https://dotnet.microsoft.com/download/dotnet/6.0/runtime)** if not already installed
2. **Download** `LADXHD-Windows-x64.zip` from the releases page
3. **Extract** the archive
4. **Copy your v1.0.0 assets**: Copy the `Content` and `Data` folders from your original **Link's Awakening DX HD v1.0.0** installation
5. **Run the game** - everything is automatic!
   - ✅ Shaders auto-installed from `Shaders-Windows/`
   - ✅ Assets auto-patched on first launch

### Linux

1. **Download** `LADXHD-Linux-x64.tar.gz` from the releases page
2. **Extract** the archive
3. **Install xdelta3** (for auto-patching):
   - Arch Linux: `sudo pacman -S xdelta3`
   - Ubuntu/Debian: `sudo apt install xdelta3`
   - Fedora: `sudo dnf install xdelta3`
4. **Copy your v1.0.0 assets**: Copy the `Content` and `Data` folders from your original **Link's Awakening DX HD v1.0.0** installation
5. **Run the game** - everything is automatic!
   - ✅ DesktopGL shaders auto-installed from `Shaders-DesktopGL/`
   - ✅ Data files auto-patched on first launch

> 💡 **Note**: The v1.0.0 release ships with Windows/DirectX shaders which don't work on Linux. The Linux release includes pre-compiled DesktopGL/OpenGL shaders that are automatically installed on first run.

## Alternative: Using the Standalone Patcher

If you prefer to patch an existing v1.0.0/v1.1.4+ installation in place, there is a Windows patcher tool on the [Releases](https://github.com/BigheadSMZ/Zelda-LA-DX-HD-Updated/releases) page (`patcher-v*` releases).

- Find the v1.0.0 release originally from itch.io.
- If you can not find it, you can search for an "archive" of it.
- It's a good idea to keep a <ins>backup</ins> of v1.0.0.
- Download the patcher from the releases page.
- Drop it into the same folder as v1.0.0/v1.1.4+.
- Open the patcher. Press the "Patch" button.
- When it is done, the patcher can be deleted.

### Silent Mode (Automated Patching)

The patcher supports silent mode for automated installations and scripts:

```
LADXHD.Patcher.exe --silent
```

| Option | Description |
|--------|-------------|
| `--silent`, `-s` | Run without GUI prompts |
| `--help`, `-h` | Show help message |

| Exit Code | Meaning |
|-----------|---------|
| 0 | Success |
| 1 | Game executable not found |
| 2 | Patching failed |
## Running on Linux

This project now supports **native Linux builds** using MonoGame's DesktopGL backend!

See the [Linux guide](./LINUX.md) for:
- **Native Linux builds** - Build and run without Wine/Proton
- **Proton setup** - Run the Windows build through Steam/Proton

Quick Start with Proton - Use the automated installer:
[zladxhd-installer](https://github.com/jslay88/zladxhd-installer)

## About This Repository

A few years back, an anonymous user posted a PC Port of Link's Awakening on itch.io built with MonoGame. It wasn't long before the game was taken down, fortunately the release contained the source code. This is a continuation of that PC Port but with the assets stripped away to avoid copyright issues. 

> ⚠️ **Important**: This repository contains only source code and xdelta patches. The GitHub Releases page provides:
> - **Game binaries** (`game-v*` tags) - Self-patching executables for Windows and Linux (no copyrighted assets included)
> - **Patcher tool** (`patcher-v*` tags) - Windows-only tool to patch an existing v1.0.0 installation
> 
> You must provide your own copy of the original v1.0.0 `Content` and `Data` folders to play the game.

This section explains the files and folders found in the base of this respository.
- **assets_original**: This is where the **"Content"** and **"Data"** folders from v1.0.0 should go.
- **assets_patches**: Contains xdelta3 patches that are the difference of assets from v1.0.0 to the latest updates.
- **ladxhd_game_source_code**: Source code for The Legend of Zelda: Link's Awakening DX HD.
- **ladxhd_migrate_source_code**: Source code for the migration tool which can apply/create assets patches.
- **ladxhd_patcher_source_code**: Source code for the patcher to update the game to v1.5.2b.
- **LADXHD_Migrater.exe**: This is the migration tool used to apply or create patches to the assets.
- **Unblock-All-Files.ps1**: This script can be used to unblock all files automatically for Visual Studio.

## Updating Source Code Assets

The latest source code can be downloaded from this repository. But, you will need to provide the assets from the original v1.0.0 release. It is very important to follow the instructions carefully as many assets have been updated.
- You will notice there is a folder in the base of this repository named **"assets_original"**.
- This is where the **"Content"** and **"Data"** folders go from the v1.0.0 release.
- Note that there is two versions of these folders, and you must provide the correct ones.
- Inside the original release folder are two folders: **"Content"** and **"Data"**.
- Copy the **"Data"** folder from the original v1.0.0 <ins>game folder</ins> to the **"assets_original"** folder.
- This is NOT the correct **"Content"** folder. You need the one from the source code.
- There should also be a 7-Zip of the v1.0.0 source code included with the game: **"source.7z"**.
- Unzip the **"source.7z"** file from the original v1.0.0 release.
- Copy the **"Content"** folder from the original v1.0.0 <ins>source code folder</ins> to the **"assets_original"** folder.
- After both folders are copied, run the migration tool:
  - **Windows**: Open the **"LADXHD_Migrater.exe"** tool and click **"Migrate Assets From v1.0.0"**.
  - **Linux**: Run `./migrate_assets.sh` (requires `xdelta3` installed).
- This will create new **"Content"** and **"Data"** folders in the **"ladxhd_game_source_code"** folder.
- And you are done. From here you can build the game or work on the code.
- The original **"Content/Data"** folders should be kept in **"assets_original"** for future patches.

Again, make sure you are grabbing the correct Content and Data folders. The "Data" folder should come from the <ins>game folder</ins>, and the "Content" folder should come from the <ins>source .7z file</ins>. While it is possible the original assets would work, there have been bugs fixed and issues addressed in some of them. The patches in **"assets_patches"** never need to be interacted with directly, as the migration tool can handle both directions: updating 1.0.0 assets, and creating new patches for asset updates.

## Contributing Prerequisites

If you wish to work on the code in this repository.
- Basic knowledge of C# .NET and Visual Studio is required.
- [Visual Studio 2022](https://visualstudio.microsoft.com/downloads/)
    - Make sure to select `.NET desktop development` components in the visual studio installer.
- The "base" game should remain as close to the original DX version as possible.
- Modifications are okay in the form of options, but should default to **<ins>false</ins>**.
- Some exceptions may be okay depending on their intent.

## Contributing Assets

Do not make pull requests providing assets directly. This includes ALL files within the **Content** and **Data** folders. Instead, use the **LADXHD Migrater** tool provided to create xdelta patches. These patches can then be applied to the original assets to update them to the latest versions also using the migration tool.

See the wiki page on [contributing to this project](https://github.com/BigheadSMZ/Zelda-LA-DX-HD-Updated/wiki/Contributing-to-this-project) for more information.

## Build Instructions

If you wish to build the code in this repository.
- Clone or Download this repository: green `Code` Button > `Download ZIP`
- The game's source code is in **"ladxhd_game_source_code"** folder
- Follow the steps in **Updating Source Code Assets**
- Run the PowerShell script "Unblock-All-Files.ps1".
  - -OR- Go to the folder `ladxhd_game_source_code\.config` you will see `dotnet-tools.json`.
  - -AND- Right click, go to properties, check `Unblock`.
- Open ProjectZ.sln
- Build/run like any normal C# program

## Personal Build / Publishing

To create a personal build, follow the steps below:
- Download and install [.NET 6.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/6.0).
- Clone or Download this repository: green `Code` Button > `Download ZIP`
- Unzip the repository and open up the unzipped folder.
- Follow the steps in **Updating Source Code Assets**
- **Windows**:
  - Run the PowerShell script "Unblock-All-Files.ps1".
    - -OR- Go to the folder `ladxhd_game_source_code\.config` you will see `dotnet-tools.json`.
    - -AND- Right click, go to properties, check `Unblock`.
  - Run `ladxhd_game_source_code\publish.bat` to build.
  - Alternatively, the **"LADXHD_Migrater.exe"** tool can now build the game.
- **Linux**:
  - Run `ladxhd_game_source_code/publish.sh linux` to build.
  - Note: Shader compilation requires Wine with d3dcompiler_47, or use pre-compiled Content from a Windows build.
- When done, the build will be in the `Publish` folder.

## Build Troubleshooting

If you experience the error **The command “dotnet tool restore” exited with code 1** then make sure the file **.config\dotnet-tools.json** isn't blocked. 

- To unblock all files in one go, run the included PowerShell script **"Unblock-All-Files.ps1"**.
- To unblock a single file: Right click, go to Properties, check Unblock, and click OK.

## About This Fork

I am a terrible programmer, but I have a love for this game. A ton of forks popped up, some with fixes, but nowhere were they all centralized. This fork attempted to find and implement all the various fixes and improvements spread across the other various forks. Once that was done, I started tackling the issues from the repository this was cloned from. And after that was done, I worked on anything else I could find that would make the game feel more like the original game.

Feel free to commit any potential fixes as a PR. There are no coding guidelines and any style is welcome as long as the code either fixes something broken or makes the game behave closer to the original. But do try to at least keep it neat.
