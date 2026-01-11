using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Auto-patches game assets on first launch using embedded xdelta patches.
    /// Users must provide their own v1.0.0 Content/Data folders.
    /// </summary>
    public static class AssetPatcher
    {
        private const string VersionFile = ".patched_version";
        private const string CurrentVersion = "1.5.2";
        
        // Multi-file patches: some files generate multiple output files from one source
        private static readonly Dictionary<string, string[]> FileTargets = new Dictionary<string, string[]>
        {
            { "eng.lng", new[] { "deu.lng", "esp.lng", "fre.lng", "ind.lng", "ita.lng", "por.lng", "rus.lng" } },
            { "dialog_eng.lng", new[] { "dialog_deu.lng", "dialog_esp.lng", "dialog_fre.lng", "dialog_ind.lng", "dialog_ita.lng", "dialog_por.lng", "dialog_rus.lng" } },
            { "smallFont.xnb", new[] { "smallFont_redux.xnb", "smallFont_vwf.xnb", "smallFont_vwf_redux.xnb" } },
            { "menuBackground.xnb", new[] { "menuBackgroundB.xnb", "menuBackgroundC.xnb", "sgb_border.xnb" } },
            { "link0.png", new[] { "link1.png" } },
            { "npcs.png", new[] { "npcs_redux.png" } },
            { "items.png", new[] { "items_deu.png", "items_esp.png", "items_fre.png", "items_ind.png", "items_ita.png", "items_por.png", "items_rus.png", "items_redux.png", 
                                  "items_redux_deu.png", "items_redux_esp.png", "items_redux_fre.png", "items_redux_ind.png", "items_redux_ita.png", "items_redux_por.png", "items_redux_rus.png" } },
            { "intro.png", new[] { "intro_deu.png", "intro_esp.png", "intro_fre.png", "intro_ind.png", "intro_ita.png", "intro_por.png", "intro_rus.png" } },
            { "minimap.png", new[] { "minimap_deu.png", "minimap_esp.png", "minimap_fre.png", "minimap_ind.png", "minimap_ita.png", "minimap_por.png", "minimap_rus.png" } },
            { "objects.png", new[] { "objects_deu.png", "objects_esp.png", "objects_fre.png", "objects_ind.png", "objects_ita.png", "objects_por.png", "objects_rus.png" } },
            { "photos.png", new[] { "photos_deu.png", "photos_esp.png", "photos_fre.png", "photos_ind.png", "photos_ita.png", "photos_por.png", "photos_rus.png", "photos_redux.png", 
                                   "photos_redux_deu.png", "photos_redux_esp.png", "photos_redux_fre.png", "photos_redux_ind.png", "photos_redux_ita.png", "photos_redux_por.png", "photos_redux_rus.png" } },
            { "ui.png", new[] { "ui_deu.png", "ui_esp.png", "ui_fre.png", "ui_ind.png", "ui_ita.png", "ui_por.png", "ui_rus.png" } },
            { "musicOverworld.data", new[] { "musicOverworldClassic.data" } },
            { "dungeon3_1.map", new[] { "dungeon3.map" } },
            { "dungeon3_1.map.data", new[] { "dungeon3.map.data" } },
            { "BowWow.ani", new[] { "bowwow_water.ani" } }
        };

        private static string _gameDirectory;
        private static string _tempDirectory;
        private static string _xdeltaPath;

        /// <summary>
        /// Check if assets need patching and apply patches if necessary.
        /// Returns true if game can continue, false if critical error.
        /// </summary>
        public static bool CheckAndPatchAssets()
        {
            try
            {
                _gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
                _tempDirectory = Path.Combine(Path.GetTempPath(), "LADXHD_Patch_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                
                string contentPath = Path.Combine(_gameDirectory, "Content");
                string dataPath = Path.Combine(_gameDirectory, "Data");
                string versionPath = Path.Combine(_gameDirectory, VersionFile);

                // Check if Content/Data folders exist
                if (!Directory.Exists(contentPath) || !Directory.Exists(dataPath))
                {
                    ShowError("Missing Assets", 
                        "Content and/or Data folders not found.\n\n" +
                        "Please copy the Content and Data folders from your original\n" +
                        "Link's Awakening DX HD v1.0.0 installation to:\n" +
                        _gameDirectory);
                    return false;
                }

                // Check if already patched to current version
                if (File.Exists(versionPath))
                {
                    string patchedVersion = File.ReadAllText(versionPath).Trim();
                    if (patchedVersion == CurrentVersion)
                    {
                        return true; // Already patched
                    }
                    Console.WriteLine($"Assets version {patchedVersion} -> {CurrentVersion}, patching...");
                }
                else
                {
                    Console.WriteLine("First launch detected, patching assets to v" + CurrentVersion + "...");
                }

                // Check if we have embedded patches
                if (!HasEmbeddedPatches())
                {
                    Console.WriteLine("No embedded patches found, assuming assets are pre-patched.");
                    File.WriteAllText(versionPath, CurrentVersion);
                    return true;
                }

                // Perform patching
                if (!PerformPatching(contentPath, dataPath))
                {
                    ShowError("Patching Failed", 
                        "Failed to patch game assets.\n\n" +
                        "Please ensure you have the original v1.0.0 Content and Data folders.");
                    return false;
                }

                // Write version marker
                File.WriteAllText(versionPath, CurrentVersion);
                Console.WriteLine("Assets successfully patched to v" + CurrentVersion);
                return true;
            }
            catch (Exception ex)
            {
                ShowError("Patching Error", $"An error occurred during patching:\n{ex.Message}");
                return false;
            }
            finally
            {
                Cleanup();
            }
        }

        private static bool HasEmbeddedPatches()
        {
            var assembly = Assembly.GetExecutingAssembly();
            return assembly.GetManifestResourceNames().Any(n => n.EndsWith(".xdelta"));
        }

        private static bool PerformPatching(string contentPath, string dataPath)
        {
            try
            {
                Directory.CreateDirectory(_tempDirectory);
                
                // Extract xdelta3 binary
                if (!ExtractXDelta())
                {
                    Console.WriteLine("Failed to extract xdelta3");
                    return false;
                }

                // Get all embedded patch resources and build a lookup by normalized name
                var assembly = Assembly.GetExecutingAssembly();
                var patchResources = assembly.GetManifestResourceNames()
                    .Where(n => n.EndsWith(".xdelta"))
                    .ToList();

                // Build a lookup: normalized filename -> resource name
                // This handles the space-to-underscore conversion MSBuild does
                var patchLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var resourceName in patchResources)
                {
                    string patchFileName = ExtractPatchFileName(resourceName);
                    // Store both the raw name and a version with underscores replaced by spaces
                    patchLookup[patchFileName] = resourceName;
                    patchLookup[patchFileName.Replace("_", " ")] = resourceName;
                }

                Console.WriteLine($"Found {patchResources.Count} patches available...");

                int patchedCount = 0;
                int skippedCount = 0;

                // Build a list of all files in Content and Data
                var allFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
#if WINDOWS
                // On Windows, patch Content folder (XNB files are Windows format)
                foreach (var file in Directory.GetFiles(contentPath, "*", SearchOption.AllDirectories))
                {
                    allFiles[Path.GetFileName(file)] = file;
                }
#else
                // On Linux, skip Content folder - it requires:
                // 1. Shaders compiled for OpenGL (via mgfxc)
                // 2. XNB platform bytes changed to DesktopGL
                // Use setup_linux_assets.sh for Content preparation instead
                Console.WriteLine("Note: Skipping Content folder on Linux (use setup_linux_assets.sh)");
#endif
                foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
                {
                    allFiles[Path.GetFileName(file)] = file;
                }

                // First pass: Apply patches to existing files
                foreach (var kvp in allFiles)
                {
                    string fileName = kvp.Key;
                    string filePath = kvp.Value;
                    
                    // Look for a patch for this file
                    string patchKey = fileName + ".xdelta";
                    if (!patchLookup.TryGetValue(patchKey, out string resourceName))
                    {
                        // Try with underscores converted to spaces
                        patchKey = fileName.Replace("_", " ") + ".xdelta";
                        patchLookup.TryGetValue(patchKey, out resourceName);
                    }
                    
                    if (resourceName != null)
                    {
                        if (ApplyPatchFromResource(assembly, resourceName, filePath, filePath))
                        {
                            patchedCount++;
                        }
                    }
                }
                
                // Second pass: Create new files from multi-file patches
                foreach (var kvp in FileTargets)
                {
                    string sourceFileName = kvp.Key;
                    string[] derivedFileNames = kvp.Value;
                    
                    if (!allFiles.TryGetValue(sourceFileName, out string sourceFilePath))
                        continue;
                        
                    string sourceDir = Path.GetDirectoryName(sourceFilePath);
                    
                    foreach (var derivedFileName in derivedFileNames)
                    {
                        // Look for a patch for this derived file
                        string patchKey = derivedFileName + ".xdelta";
                        if (!patchLookup.TryGetValue(patchKey, out string resourceName))
                        {
                            patchKey = derivedFileName.Replace("_", " ") + ".xdelta";
                            patchLookup.TryGetValue(patchKey, out resourceName);
                        }
                        
                        if (resourceName != null)
                        {
                            string derivedFilePath = Path.Combine(sourceDir, derivedFileName);
                            if (ApplyPatchFromResource(assembly, resourceName, sourceFilePath, derivedFilePath))
                            {
                                patchedCount++;
                            }
                        }
                    }
                }

                Console.WriteLine($"Patching complete: {patchedCount} files patched");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Patching error: {ex.Message}");
                return false;
            }
        }

        private static string FindSourceFileForTarget(string targetFileName, Dictionary<string, string> allFiles)
        {
            foreach (var kvp in FileTargets)
            {
                if (kvp.Value.Contains(targetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    if (allFiles.ContainsKey(kvp.Key))
                    {
                        return kvp.Key;
                    }
                }
            }
            return null;
        }

        private static string ExtractPatchFileName(string resourceName)
        {
            // Resource names are like "ProjectZ.Patches.filename.xdelta"
            // MSBuild replaces spaces with underscores in resource names
            // We need to extract "filename.xdelta" and restore spaces
            
            const string prefix = "ProjectZ.Patches.";
            if (resourceName.StartsWith(prefix))
            {
                // Return the filename part, converting underscores back to spaces
                // Note: this is a heuristic - original filenames with underscores will be affected
                return resourceName.Substring(prefix.Length);
            }
            
            // Fallback: get the last two parts (filename.xdelta)
            var parts = resourceName.Split('.');
            if (parts.Length >= 2)
            {
                return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            }
            return resourceName;
        }
        
        /// <summary>
        /// Gets the original filename from a resource name, handling space-to-underscore conversion.
        /// </summary>
        private static string GetOriginalFileName(string patchFileName)
        {
            // Remove .xdelta extension
            string baseName = patchFileName.EndsWith(".xdelta", StringComparison.OrdinalIgnoreCase) 
                ? patchFileName.Substring(0, patchFileName.Length - 7) 
                : patchFileName;
            return baseName;
        }

        private static bool ApplyPatchFromResource(Assembly assembly, string resourceName, string sourceFile, string outputFile)
        {
            try
            {
                // Extract patch to temp file
                string patchFile = Path.Combine(_tempDirectory, Path.GetFileName(resourceName));
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return false;
                    using (var fileStream = File.Create(patchFile))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                // Create temp output file
                string tempOutput = Path.Combine(_tempDirectory, "output_" + Path.GetFileName(outputFile));

                // Apply patch using xdelta3
                var result = RunXDelta(sourceFile, patchFile, tempOutput);
                
                if (result && File.Exists(tempOutput))
                {
                    // Replace original file with patched version
                    File.Copy(tempOutput, outputFile, true);
                    File.Delete(tempOutput);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to apply patch {resourceName}: {ex.Message}");
                return false;
            }
        }

        private static bool ExtractXDelta()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                
#if WINDOWS
                _xdeltaPath = Path.Combine(_tempDirectory, "xdelta3.exe");
                string resourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("xdelta3.exe", StringComparison.OrdinalIgnoreCase));
                
                if (resourceName == null)
                {
                    Console.WriteLine("xdelta3.exe not found in embedded resources");
                    return false;
                }
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return false;
                    using (var fileStream = File.Create(_xdeltaPath))
                    {
                        stream.CopyTo(fileStream);
                    }
                }
                return true;
#else
                // On Linux, try to use system xdelta3
                _xdeltaPath = "xdelta3";
                
                // Check if xdelta3 is available
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = "xdelta3",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    };
                    using (var process = Process.Start(psi))
                    {
                        process.WaitForExit();
                        if (process.ExitCode == 0)
                        {
                            return true;
                        }
                    }
                }
                catch { }
                
                Console.WriteLine("xdelta3 not found. Please install it:");
                Console.WriteLine("  Arch Linux: sudo pacman -S xdelta3");
                Console.WriteLine("  Ubuntu/Debian: sudo apt install xdelta3");
                Console.WriteLine("  Fedora: sudo dnf install xdelta3");
                return false;
#endif
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to extract xdelta3: {ex.Message}");
                return false;
            }
        }

        private static bool RunXDelta(string sourceFile, string patchFile, string outputFile)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _xdeltaPath,
                    Arguments = $"-d -f -s \"{sourceFile}\" \"{patchFile}\" \"{outputFile}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"xdelta3 execution failed: {ex.Message}");
                return false;
            }
        }

        private static void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(_tempDirectory) && Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch { }
        }

        private static void ShowError(string title, string message)
        {
#if WINDOWS
            System.Windows.Forms.MessageBox.Show(message, title, 
                System.Windows.Forms.MessageBoxButtons.OK, 
                System.Windows.Forms.MessageBoxIcon.Error);
#else
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] {title}");
            Console.ResetColor();
            Console.WriteLine(message);
            Console.WriteLine("\nPress Enter to exit...");
            Console.ReadLine();
#endif
        }
    }
}
