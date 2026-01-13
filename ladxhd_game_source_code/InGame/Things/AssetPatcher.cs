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
    /// 
    /// BACKUP SYSTEM:
    /// - On first run, v1.0.0 files are backed up to Data/Backup/
    /// - On subsequent runs (upgrades), v1.0.0 is restored from backup before patching
    /// - This ensures patches always work regardless of current version
    /// </summary>
    public static class AssetPatcher
    {
        private const string VersionFile = ".patched_version";
        private const string CurrentVersion = "1.6.5";
        
        // Backup folder stores v1.0.0 originals for future upgrades
        private const string BackupFolderName = "Backup";
        
        // Multi-file patches: some files generate multiple output files from one source
        // Key format: "filename" or "subdir/filename" for disambiguation
        private static readonly Dictionary<string, string[]> FileTargets = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
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
            { "NPCs/BowWow.ani", new[] { "bowwow_water.ani" } }  // Use path to disambiguate from Sequences/bowWow.ani
        };

        /// <summary>
        /// Progress callback for UI updates. Parameters: (status message, percent 0-100)
        /// </summary>
        public static Action<string, int> OnProgress;

        private static void ReportProgress(string status, int percent)
        {
            Console.WriteLine(status);
            OnProgress?.Invoke(status, percent);
            SplashScreen.SetStatus(status);
            SplashScreen.SetProgress(50 + percent / 2); // Patching is 50-100% range
        }

        // Files that are derived and should NOT be backed up (they're created from base files)
        private static readonly HashSet<string> DerivedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Obsolete files that should be removed (cause problems if they exist)
        private static readonly string[] ObsoleteFiles = new[]
        {
            "cave bird.map.data", "dungeon_end.map.data", "dungeon3_1.map", "dungeon3_1.map.data",
            "dungeon3_2.map", "dungeon3_2.map.data", "dungeon3_3.map", "dungeon3_3.map.data",
            "dungeon3_4.map", "dungeon3_4.map.data", "dungeon 7_2d.map.data",
            "three_1.txt", "three_2.txt", "three_3.txt"
        };
        
        // Case sensitivity fixes for Linux - files that need to be renamed
        // Format: "current name" -> "expected name" (lowercase)
        private static readonly Dictionary<string, string> CaseSensitivityFixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "spiny Beetle.ani", "spiny beetle.ani" }
        };

        private static string _gameDirectory;
        private static string _tempDirectory;
        private static string _backupDirectory;
        private static string _xdeltaPath;

        static AssetPatcher()
        {
            // Build set of all derived file names
            foreach (var targets in FileTargets.Values)
            {
                foreach (var target in targets)
                {
                    DerivedFiles.Add(target);
                }
            }
        }

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
                _backupDirectory = Path.Combine(dataPath, BackupFolderName);

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
                        // Verify critical derived files exist - if not, we need to re-patch
                        if (VerifyDerivedFilesExist(dataPath))
                        {
                            return true; // Already patched and all files present
                        }
                        Console.WriteLine("Some derived files are missing, re-patching...");
                        // Delete version file to force re-patch
                        try { File.Delete(versionPath); } catch { }
                    }
                    else
                    {
                        Console.WriteLine($"Assets version {patchedVersion} -> {CurrentVersion}, upgrading...");
                    }
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

        /// <summary>
        /// Verifies that critical derived files exist.
        /// Returns false if any are missing, indicating re-patching is needed.
        /// </summary>
        private static bool VerifyDerivedFilesExist(string dataPath)
        {
            // Check a few critical derived files that the game needs
            // These paths must match the actual game data structure
            string[] criticalFiles = new[]
            {
                Path.Combine(dataPath, "Photo Mode", "photos_redux.png"),
                Path.Combine(dataPath, "Map Objects", "npcs_redux.png"),
                Path.Combine(dataPath, "Animations", "NPCs", "bowwow_water.ani"),
                Path.Combine(dataPath, "Languages", "deu.lng"),
            };

            foreach (var file in criticalFiles)
            {
                if (!File.Exists(file))
                {
                    Console.WriteLine($"Missing derived file: {Path.GetFileName(file)}");
                    return false;
                }
            }

            return true;
        }

        private static bool PerformPatching(string contentPath, string dataPath)
        {
            try
            {
                Directory.CreateDirectory(_tempDirectory);
                Directory.CreateDirectory(_backupDirectory);
                
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
                var patchLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var resourceName in patchResources)
                {
                    string patchFileName = ExtractPatchFileName(resourceName);
                    patchLookup[patchFileName] = resourceName;
                    patchLookup[patchFileName.Replace("_", " ")] = resourceName;
                }

                Console.WriteLine($"Found {patchResources.Count} patches available...");
                SplashScreen.SetStatus($"Found {patchResources.Count} patches to apply...");

                // Clean up bad backup files (derived files shouldn't be in backup)
                RemoveBadBackupFiles();

                int patchedCount = 0;
                int processedCount = 0;

                // Build a list of all files to process
                var filesToProcess = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                // Patch Content folder (fonts, textures, etc. - but NOT shaders on Linux)
                foreach (var file in Directory.GetFiles(contentPath, "*", SearchOption.AllDirectories))
                {
                    // Skip files in backup folder
                    if (file.IndexOf(Path.DirectorySeparatorChar + BackupFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    
#if !WINDOWS
                    // On Linux, skip shader XNBs - they need to be DesktopGL compiled
                    // (handled separately by ShaderPatcher)
                    if (file.IndexOf(Path.DirectorySeparatorChar + "Shader" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
#endif
                    // Use parent/filename as key to avoid collisions (e.g., NPCs/BowWow.ani vs Sequences/bowWow.ani)
                    string parentDir = Path.GetFileName(Path.GetDirectoryName(file));
                    string key = string.IsNullOrEmpty(parentDir) ? Path.GetFileName(file) : parentDir + "/" + Path.GetFileName(file);
                    filesToProcess[key] = file;
                }
                foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
                {
                    // Skip files in backup folder
                    if (file.IndexOf(Path.DirectorySeparatorChar + BackupFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;
                    // Use parent/filename as key to avoid collisions
                    string parentDir = Path.GetFileName(Path.GetDirectoryName(file));
                    string key = string.IsNullOrEmpty(parentDir) ? Path.GetFileName(file) : parentDir + "/" + Path.GetFileName(file);
                    filesToProcess[key] = file;
                }

                // First pass: Backup/restore and patch existing files
                int totalFiles = filesToProcess.Count;
                foreach (var kvp in filesToProcess)
                {
                    string fileKey = kvp.Key;      // May be "parent/filename" or just "filename"
                    string filePath = kvp.Value;
                    processedCount++;
                    
                    // Update progress (50-95% range for patching)
                    int progressPercent = 50 + (int)((processedCount / (float)totalFiles) * 45);
                    SplashScreen.SetProgress(progressPercent);
                    
                    // Extract just the filename (patches use filename only)
                    string fileName = fileKey.Contains("/") ? fileKey.Substring(fileKey.LastIndexOf('/') + 1) : fileKey;
                    
                    // Update splash status periodically
                    if (processedCount % 50 == 0 || processedCount == totalFiles)
                    {
                        SplashScreen.SetStatus($"Patching assets... {processedCount}/{totalFiles}");
                    }
                    
                    
                    // Skip derived files (they're created fresh from base files)
                    if (DerivedFiles.Contains(fileName))
                        continue;
                    
                    // Look for a patch for this file
                    string patchKey = fileName + ".xdelta";
                    if (!patchLookup.TryGetValue(patchKey, out string resourceName))
                    {
                        patchKey = fileName.Replace("_", " ") + ".xdelta";
                        patchLookup.TryGetValue(patchKey, out resourceName);
                    }
                    
                    if (resourceName != null)
                    {
                        string backupPath = Path.Combine(_backupDirectory, fileName);
                        
                        // BACKUP/RESTORE LOGIC:
                        // - If no backup exists: This is v1.0.0, back it up before patching
                        // - If backup exists: This is an upgrade, restore v1.0.0 then patch
                        if (!File.Exists(backupPath))
                        {
                            // First time patching this file - backup the v1.0.0 original
                            try
                            {
                                File.Copy(filePath, backupPath, false);
                                Console.WriteLine($"  BACKUP: {fileName}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  WARN: Could not backup {fileName}: {ex.Message}");
                            }
                        }
                        else
                        {
                            // Upgrade scenario - restore v1.0.0 from backup before patching
                            try
                            {
                                File.Copy(backupPath, filePath, true);
                                Console.WriteLine($"  RESTORE: {fileName}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  WARN: Could not restore {fileName}: {ex.Message}");
                            }
                        }
                        
                        // Now apply the patch (source is always v1.0.0)
                        if (ApplyPatchFromResource(assembly, resourceName, filePath, filePath))
                        {
                            patchedCount++;
                        }
                    }
                    
                    // Handle multi-file patches (create derived files from this source)
                    // IMPORTANT: Use the v1.0.0 backup as the source, NOT the patched file!
                    // Derived file patches are created from v1.0.0 originals.
                    // FileTargets can use either just filename or "subdir/filename" for disambiguation
                    string[] derivedFileNames = null;
                    
                    // Try with the full key first (e.g., "NPCs/BowWow.ani")
                    if (!FileTargets.TryGetValue(fileKey, out derivedFileNames))
                    {
                        // Fall back to just filename
                        FileTargets.TryGetValue(fileName, out derivedFileNames);
                    }
                    
                    if (derivedFileNames != null)
                    {
                        string sourceDir = Path.GetDirectoryName(filePath);
                        string backupPath = Path.Combine(_backupDirectory, fileName);
                        
                        // Use backup (v1.0.0) as source if available, otherwise use the current file
                        // (e.g., BowWow.ani has no patch but bowwow_water.ani is derived from it)
                        string sourceForDerived = File.Exists(backupPath) ? backupPath : filePath;
                        
                        
                        foreach (var derivedFileName in derivedFileNames)
                        {
                            patchKey = derivedFileName + ".xdelta";
                            if (!patchLookup.TryGetValue(patchKey, out resourceName))
                            {
                                patchKey = derivedFileName.Replace("_", " ") + ".xdelta";
                                patchLookup.TryGetValue(patchKey, out resourceName);
                            }
                            
                            if (resourceName != null)
                            {
                                string derivedFilePath = Path.Combine(sourceDir, derivedFileName);
                                if (ApplyPatchFromResource(assembly, resourceName, sourceForDerived, derivedFilePath))
                                {
                                    patchedCount++;
                                }
                            }
                            else
                            {
                                Console.WriteLine($"    WARN: No patch found for derived file {derivedFileName}");
                            }
                        }
                    }
                }

                // Special fix for dungeon3 (historical issue - dungeon3_1.map was renamed)
                Dungeon3PatchFix(patchLookup, assembly);

                // Clean up derived files from backup (they shouldn't be there)
                RemoveBadBackupFiles();
                
                // Remove obsolete files that may cause problems
                RemoveObsoleteFiles(dataPath);
                
#if !WINDOWS
                // Fix case sensitivity issues on Linux
                FixCaseSensitivity(dataPath);
#endif

                SplashScreen.SetStatus($"Patched {patchedCount} files");
                SplashScreen.SetProgress(98);
                Console.WriteLine($"Patching complete: {patchedCount} files patched");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Patching error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Remove derived files from backup folder - they shouldn't be there
        /// as they're created fresh from the base v1.0.0 files each time.
        /// </summary>
        private static void RemoveBadBackupFiles()
        {
            if (!Directory.Exists(_backupDirectory))
                return;
                
            foreach (var file in Directory.GetFiles(_backupDirectory))
            {
                string fileName = Path.GetFileName(file);
                if (DerivedFiles.Contains(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        Console.WriteLine($"  CLEANUP: Removed {fileName} from backup");
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Remove obsolete files that may cause problems if they exist.
        /// </summary>
        private static void RemoveObsoleteFiles(string dataPath)
        {
            var obsoleteSet = new HashSet<string>(ObsoleteFiles, StringComparer.OrdinalIgnoreCase);
            
            foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                // Skip backup folder
                if (file.IndexOf(Path.DirectorySeparatorChar + BackupFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                    
                string fileName = Path.GetFileName(file);
                if (obsoleteSet.Contains(fileName))
                {
                    try
                    {
                        File.Delete(file);
                        Console.WriteLine($"  REMOVED: {fileName} (obsolete)");
                    }
                    catch { }
                }
            }
        }

#if !WINDOWS
        /// <summary>
        /// Fix case sensitivity issues on Linux. Some files have mixed case names
        /// but the code expects lowercase (e.g., "spiny Beetle.ani" -> "spiny beetle.ani").
        /// </summary>
        private static void FixCaseSensitivity(string dataPath)
        {
            foreach (var file in Directory.GetFiles(dataPath, "*", SearchOption.AllDirectories))
            {
                // Skip backup folder
                if (file.IndexOf(Path.DirectorySeparatorChar + BackupFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                    
                string fileName = Path.GetFileName(file);
                if (CaseSensitivityFixes.TryGetValue(fileName, out string correctName))
                {
                    string correctPath = Path.Combine(Path.GetDirectoryName(file), correctName);
                    
                    // Only rename if the correct name doesn't already exist
                    if (!File.Exists(correctPath))
                    {
                        try
                        {
                            File.Move(file, correctPath);
                            Console.WriteLine($"  RENAMED: {fileName} -> {correctName} (case fix)");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  WARN: Could not rename {fileName}: {ex.Message}");
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Special fix for dungeon3 - the file was renamed from dungeon3_1.map to dungeon3.map
        /// but we need to patch from the backup of the original name.
        /// </summary>
        private static void Dungeon3PatchFix(Dictionary<string, string> patchLookup, Assembly assembly)
        {
            string d3backup = Path.Combine(_backupDirectory, "dungeon3_1.map");
            
            if (File.Exists(d3backup))
            {
                // Patch dungeon3.map from dungeon3_1.map backup
                if (patchLookup.TryGetValue("dungeon3.map.xdelta", out string resourceName))
                {
                    string targetPath = Path.Combine(_gameDirectory, "Data", "Maps", "dungeon3.map");
                    ApplyPatchFromResource(assembly, resourceName, d3backup, targetPath);
                    Console.WriteLine("  SPECIAL: Created dungeon3.map from dungeon3_1.map backup");
                }
            }
        }

        private static string ExtractPatchFileName(string resourceName)
        {
            const string prefix = "ProjectZ.Patches.";
            if (resourceName.StartsWith(prefix))
            {
                return resourceName.Substring(prefix.Length);
            }
            
            var parts = resourceName.Split('.');
            if (parts.Length >= 2)
            {
                return parts[parts.Length - 2] + "." + parts[parts.Length - 1];
            }
            return resourceName;
        }

        private static bool ApplyPatchFromResource(Assembly assembly, string resourceName, string sourceFile, string outputFile)
        {
            try
            {
                // Extract patch to temp file
                string patchFile = Path.Combine(_tempDirectory, Path.GetFileName(resourceName));
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) 
                    {
                        Console.WriteLine($"  FAILED: {Path.GetFileName(outputFile)} - resource stream is null for {resourceName}");
                        return false;
                    }
                    using (var fileStream = File.Create(patchFile))
                    {
                        stream.CopyTo(fileStream);
                    }
                }

                // Create temp output file
                string tempOutput = Path.Combine(_tempDirectory, "output_" + Path.GetFileName(outputFile));

                // Verify source file exists
                if (!File.Exists(sourceFile))
                {
                    Console.WriteLine($"  FAILED: {Path.GetFileName(outputFile)} - source file not found: {sourceFile}");
                    return false;
                }

                // Apply patch using xdelta3
                var result = RunXDelta(sourceFile, patchFile, tempOutput);
                
                if (result && File.Exists(tempOutput))
                {
                    // Ensure output directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    
                    // Replace original file with patched version
                    File.Copy(tempOutput, outputFile, true);
                    File.Delete(tempOutput);
                    Console.WriteLine($"  PATCHED: {Path.GetFileName(outputFile)}");
                    return true;
                }
                
                Console.WriteLine($"  FAILED: {Path.GetFileName(outputFile)} - xdelta3 failed or output missing");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED: {Path.GetFileName(outputFile)} - {ex.Message}");
                return false;
            }
        }

        private static bool ExtractXDelta()
        {
            try
            {
                Directory.CreateDirectory(_tempDirectory);
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
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    string stderr = process.StandardError.ReadToEnd();
                    string stdout = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode != 0)
                    {
                        Console.WriteLine($"    xdelta3 exit={process.ExitCode}: {stderr}");
                    }
                    
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
