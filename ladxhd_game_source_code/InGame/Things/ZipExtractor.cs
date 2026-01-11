using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

#if WINDOWS
using System.Windows.Forms;
#endif

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Handles extraction of the v1.0.0 game assets from the original release zip file.
    /// Users can drop the zip file next to the game binary and it will be automatically extracted.
    /// </summary>
    public static class ZipExtractor
    {
        // SHA256 checksum of the official v1.0.0 release zip
        private const string ExpectedChecksum = "118a4adfa782b4c0097867609cb79474abaf9a95b3f684b04715a46d424beb1c";
        
        // Possible zip file names to look for
        private static readonly string[] ZipFileNames = new[]
        {
            "Links Awakening DX HD v1.0.0.zip",
            "Link's Awakening DX HD v1.0.0.zip",
            "LADXHD-v1.0.0.zip",
            "v1.0.0.zip"
        };

        // Files to delete after extraction (not needed for runtime)
        private static readonly string[] FilesToDelete = new[]
        {
            "source.7z",           // Contains copyrighted source assets (PNG, WAV)
            "Link's Awakening DX HD.exe",  // Windows executable from v1.0.0
            "Link's Awakening DX HD.dll.config",
            "MonoGame.Framework.dll.config"
        };

        /// <summary>
        /// Checks for and extracts the v1.0.0 zip file if present.
        /// Returns true if Content and Data folders exist (either already present or newly extracted).
        /// </summary>
        public static bool CheckAndExtract()
        {
            string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string contentPath = Path.Combine(gameDirectory, "Content");
            string dataPath = Path.Combine(gameDirectory, "Data");

            // If Content and Data already exist, nothing to do
            if (Directory.Exists(contentPath) && Directory.Exists(dataPath))
            {
                return true;
            }

            // Look for the v1.0.0 zip file
            string zipPath = FindZipFile(gameDirectory);
            if (zipPath == null)
            {
                Console.WriteLine("No v1.0.0 zip file found in game directory.");
                Console.WriteLine("");
                
                // Prompt user to select the zip file
                zipPath = PromptForZipFile(gameDirectory);
                
                if (zipPath == null)
                {
                    Console.WriteLine("");
                    Console.WriteLine("No zip file selected.");
                    Console.WriteLine("");
                    Console.WriteLine("To play this game, you need the original v1.0.0 release assets.");
                    Console.WriteLine("Please do ONE of the following:");
                    Console.WriteLine("");
                    Console.WriteLine("  Option 1: Drop the zip file here");
                    Console.WriteLine("    - Place 'Links Awakening DX HD v1.0.0.zip' in:");
                    Console.WriteLine($"      {gameDirectory}");
                    Console.WriteLine("    - Run the game again");
                    Console.WriteLine("");
                    Console.WriteLine("  Option 2: Manually copy folders");
                    Console.WriteLine("    - Extract the v1.0.0 zip yourself");
                    Console.WriteLine("    - Copy the 'Content' and 'Data' folders to:");
                    Console.WriteLine($"      {gameDirectory}");
                    Console.WriteLine("");
                    return false;
                }
            }

            Console.WriteLine($"Found zip file: {Path.GetFileName(zipPath)}");
            Console.WriteLine("Verifying checksum...");

            // Verify the checksum
            string actualChecksum = ComputeChecksum(zipPath);
            if (!string.Equals(actualChecksum, ExpectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("");
                Console.WriteLine("ERROR: Checksum mismatch!");
                Console.WriteLine($"  Expected: {ExpectedChecksum}");
                Console.WriteLine($"  Got:      {actualChecksum}");
                Console.WriteLine("");
                Console.WriteLine("This doesn't appear to be the official v1.0.0 release.");
                Console.WriteLine("Please provide the original 'Links Awakening DX HD v1.0.0.zip' file.");
                return false;
            }

            Console.WriteLine("Checksum verified!");
            Console.WriteLine("Extracting Content and Data folders...");

            // Extract the zip
            if (!ExtractZip(zipPath, gameDirectory))
            {
                Console.WriteLine("ERROR: Failed to extract zip file.");
                return false;
            }

            // Clean up unnecessary files
            CleanupExtractedFiles(gameDirectory);

            // Optionally delete or rename the zip file after successful extraction
            try
            {
                string extractedMarker = zipPath + ".extracted";
                if (!File.Exists(extractedMarker))
                {
                    File.WriteAllText(extractedMarker, $"Extracted on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine("Created extraction marker file.");
                }
            }
            catch { /* Ignore errors creating marker */ }

            Console.WriteLine("Extraction complete!");
            Console.WriteLine("");

            return Directory.Exists(contentPath) && Directory.Exists(dataPath);
        }

        private static string FindZipFile(string directory)
        {
            foreach (var fileName in ZipFileNames)
            {
                string path = Path.Combine(directory, fileName);
                if (File.Exists(path))
                    return path;
            }

            // Also look for any .zip file that might be the v1.0.0 release
            try
            {
                var zipFiles = Directory.GetFiles(directory, "*.zip", SearchOption.TopDirectoryOnly);
                foreach (var zipFile in zipFiles)
                {
                    string name = Path.GetFileName(zipFile).ToLowerInvariant();
                    if (name.Contains("awakening") || name.Contains("ladx") || name.Contains("1.0.0"))
                        return zipFile;
                }
            }
            catch { /* Ignore search errors */ }

            return null;
        }

        /// <summary>
        /// Prompts the user to select the v1.0.0 zip file using a GUI file picker.
        /// If selected, copies it to the game directory and returns the new path.
        /// </summary>
        private static string PromptForZipFile(string gameDirectory)
        {
            Console.WriteLine("Please select the 'Links Awakening DX HD v1.0.0.zip' file...");
            Console.WriteLine("");

            string selectedPath = null;

#if WINDOWS
            selectedPath = ShowWindowsFileDialog();
#else
            selectedPath = ShowLinuxFileDialog();
#endif

            if (string.IsNullOrEmpty(selectedPath) || !File.Exists(selectedPath))
                return null;

            // If the selected file is already in the game directory, use it directly
            string selectedDir = Path.GetDirectoryName(selectedPath);
            if (string.Equals(selectedDir, gameDirectory.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return selectedPath;
            }

            // Copy the zip file to the game directory
            string destPath = Path.Combine(gameDirectory, Path.GetFileName(selectedPath));
            Console.WriteLine($"Copying zip file to game directory...");
            Console.WriteLine($"  From: {selectedPath}");
            Console.WriteLine($"  To:   {destPath}");

            try
            {
                File.Copy(selectedPath, destPath, overwrite: true);
                Console.WriteLine("Copy complete!");
                return destPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Failed to copy file: {ex.Message}");
                Console.WriteLine("You can manually copy the zip file to the game directory and try again.");
                return null;
            }
        }

#if WINDOWS
        private static string ShowWindowsFileDialog()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Select Link's Awakening DX HD v1.0.0 zip file";
                dialog.Filter = "ZIP files (*.zip)|*.zip|All files (*.*)|*.*";
                dialog.FilterIndex = 1;
                dialog.Multiselect = false;
                dialog.CheckFileExists = true;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.FileName;
                }
            }
            return null;
        }
#else
        private static string ShowLinuxFileDialog()
        {
            // Try zenity first (most common on Linux)
            string result = TryZenity();
            if (result != null)
                return result;

            // Try kdialog (KDE)
            result = TryKdialog();
            if (result != null)
                return result;

            // Fall back to console input
            return PromptConsoleInput();
        }

        private static string TryZenity()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "zenity",
                        Arguments = "--file-selection --title=\"Select Link's Awakening DX HD v1.0.0 zip file\" --file-filter=\"ZIP files (*.zip)|*.zip\" --file-filter=\"All files|*\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    return output;
                }
            }
            catch { /* zenity not available */ }
            return null;
        }

        private static string TryKdialog()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "kdialog",
                        Arguments = "--getopenfilename . \"*.zip|ZIP files\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    return output;
                }
            }
            catch { /* kdialog not available */ }
            return null;
        }

        private static string PromptConsoleInput()
        {
            Console.WriteLine("No GUI file picker available (install zenity or kdialog for GUI support).");
            Console.WriteLine("");
            Console.WriteLine("Enter the full path to 'Links Awakening DX HD v1.0.0.zip':");
            Console.WriteLine("(or press Enter to cancel)");
            Console.Write("> ");

            string input = Console.ReadLine()?.Trim();

            // Remove quotes if present
            if (!string.IsNullOrEmpty(input))
            {
                input = input.Trim('"', '\'');
            }

            if (!string.IsNullOrEmpty(input) && File.Exists(input))
            {
                return input;
            }

            if (!string.IsNullOrEmpty(input))
            {
                Console.WriteLine($"File not found: {input}");
            }

            return null;
        }
#endif

        private static string ComputeChecksum(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static bool ExtractZip(string zipPath, string destinationDirectory)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    // Find the root folder inside the zip (e.g., "Links Awakening DX HD/")
                    string rootFolder = null;
                    var firstEntry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.Name));
                    if (firstEntry != null)
                    {
                        var parts = firstEntry.FullName.Split('/');
                        if (parts.Length > 1)
                            rootFolder = parts[0] + "/";
                    }

                    int extracted = 0;
                    foreach (var entry in archive.Entries)
                    {
                        // Get the path relative to the root folder
                        string entryPath = entry.FullName;
                        if (rootFolder != null && entryPath.StartsWith(rootFolder))
                            entryPath = entryPath.Substring(rootFolder.Length);

                        // Only extract Content/ and Data/ folders
                        if (!entryPath.StartsWith("Content/") && !entryPath.StartsWith("Data/"))
                            continue;

                        // Skip directories (we'll create them when extracting files)
                        if (string.IsNullOrEmpty(entry.Name))
                            continue;

                        string destPath = Path.Combine(destinationDirectory, entryPath);
                        string destDir = Path.GetDirectoryName(destPath);

                        // Create directory if needed
                        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                            Directory.CreateDirectory(destDir);

                        // Extract the file
                        entry.ExtractToFile(destPath, overwrite: true);
                        extracted++;

                        // Progress indicator
                        if (extracted % 100 == 0)
                            Console.WriteLine($"  Extracted {extracted} files...");
                    }

                    Console.WriteLine($"  Extracted {extracted} files total.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Extraction error: {ex.Message}");
                return false;
            }
        }

        private static void CleanupExtractedFiles(string directory)
        {
            Console.WriteLine("Cleaning up unnecessary files...");
            
            foreach (var fileName in FilesToDelete)
            {
                string filePath = Path.Combine(directory, fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"  Deleted: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Could not delete {fileName}: {ex.Message}");
                    }
                }

                // Also check in Content/ folder
                filePath = Path.Combine(directory, "Content", fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"  Deleted: Content/{fileName}");
                    }
                    catch { }
                }

                // Also check in Data/ folder
                filePath = Path.Combine(directory, "Data", fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                        Console.WriteLine($"  Deleted: Data/{fileName}");
                    }
                    catch { }
                }
            }

            // Delete source.7z which is at the root of extracted content
            string source7z = Path.Combine(directory, "source.7z");
            if (File.Exists(source7z))
            {
                try
                {
                    File.Delete(source7z);
                    Console.WriteLine("  Deleted: source.7z");
                }
                catch { }
            }
        }

        /// <summary>
        /// Gets information about the current asset state.
        /// </summary>
        public static string GetAssetStatus()
        {
            string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string contentPath = Path.Combine(gameDirectory, "Content");
            string dataPath = Path.Combine(gameDirectory, "Data");

            bool hasContent = Directory.Exists(contentPath);
            bool hasData = Directory.Exists(dataPath);
            string zipPath = FindZipFile(gameDirectory);

            if (hasContent && hasData)
                return "Assets ready";
            else if (zipPath != null)
                return $"Zip found: {Path.GetFileName(zipPath)} (will extract on next run)";
            else
                return "Missing assets - need v1.0.0 zip or Content/Data folders";
        }
    }
}
