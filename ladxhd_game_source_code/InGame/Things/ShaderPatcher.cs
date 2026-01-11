using System;
using System.IO;
using System.Linq;

namespace ProjectZ.InGame.Things
{
    /// <summary>
    /// Handles installation of platform-correct shaders.
    /// 
    /// The v1.0.0 release ships with Windows/DirectX shaders in Content/Shader/.
    /// On Linux, we need DesktopGL/OpenGL shaders instead.
    /// 
    /// This class checks for bundled platform-specific shaders and installs them
    /// to the Content/Shader/ folder, replacing any incompatible shaders.
    /// </summary>
    public static class ShaderPatcher
    {
        private const string ShaderMarkerFile = ".shader_platform";
        
#if WINDOWS
        private const string TargetPlatform = "Windows";
        private const string BundledShaderFolder = "Shaders-Windows";
#else
        private const string TargetPlatform = "DesktopGL";
        private const string BundledShaderFolder = "Shaders-DesktopGL";
#endif

        /// <summary>
        /// Check if shaders need to be installed and install them if necessary.
        /// Returns true if shaders are ready (installed or already correct), false on error.
        /// </summary>
        public static bool EnsureCorrectShaders()
        {
            try
            {
                string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string contentShaderPath = Path.Combine(gameDirectory, "Content", "Shader");
                string bundledShaderPath = Path.Combine(gameDirectory, BundledShaderFolder);
                string markerPath = Path.Combine(contentShaderPath, ShaderMarkerFile);

                // Check if Content/Shader exists
                if (!Directory.Exists(contentShaderPath))
                {
                    Console.WriteLine("Content/Shader folder not found - will be created when user provides v1.0.0 assets");
                    return true; // Let AssetPatcher handle the missing Content error
                }

                // Check if shaders are already for the correct platform
                if (File.Exists(markerPath))
                {
                    string currentPlatform = File.ReadAllText(markerPath).Trim();
                    if (currentPlatform == TargetPlatform)
                    {
                        // Verify at least one shader file actually exists and is correct
                        if (VerifyShaderPlatform(contentShaderPath))
                        {
                            Console.WriteLine($"Shaders already configured for {TargetPlatform}");
                            return true;
                        }
                        // Marker exists but shaders are wrong - delete marker and reinstall
                        Console.WriteLine($"Shader marker says {TargetPlatform} but shaders appear incorrect, reinstalling...");
                        try { File.Delete(markerPath); } catch { }
                    }
                    else
                    {
                        Console.WriteLine($"Shaders are for {currentPlatform}, need {TargetPlatform}");
                    }
                }

                // Check if we have bundled shaders for this platform
                if (Directory.Exists(bundledShaderPath))
                {
                    Console.WriteLine($"Installing {TargetPlatform} shaders from {BundledShaderFolder}/...");
                    return InstallShadersFromFolder(bundledShaderPath, contentShaderPath, markerPath);
                }

                // On Linux, v1.0.0 ships with Windows shaders - this is a problem
#if !WINDOWS
                // Check if the shaders are Windows format (no marker = assume Windows from v1.0.0)
                if (!File.Exists(markerPath))
                {
                    Console.WriteLine("WARNING: Content/Shader contains Windows shaders from v1.0.0");
                    Console.WriteLine("         DesktopGL shaders are required for Linux.");
                    Console.WriteLine("");
                    Console.WriteLine("To fix this:");
                    Console.WriteLine("  1. If you downloaded from GitHub releases, copy the");
                    Console.WriteLine("     'Shaders-DesktopGL' folder contents to 'Content/Shader/'");
                    Console.WriteLine("  2. Or run: setup_linux_assets.sh to compile shaders");
                    Console.WriteLine("");
                    
                    // Try to continue anyway - the game will crash with a clearer error if shaders are wrong
                    return true;
                }
#endif

                // On Windows, v1.0.0 shaders should work fine
#if WINDOWS
                if (!File.Exists(markerPath))
                {
                    // Mark as Windows (v1.0.0 ships with Windows shaders)
                    File.WriteAllText(markerPath, "Windows");
                    Console.WriteLine("Marked existing shaders as Windows platform");
                }
#endif

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shader check error: {ex.Message}");
                return true; // Continue anyway, let the game fail with a clearer error if needed
            }
        }

        /// <summary>
        /// Install shaders from a bundled folder to Content/Shader/.
        /// </summary>
        private static bool InstallShadersFromFolder(string sourceFolder, string destFolder, string markerPath)
        {
            try
            {
                // Get all XNB files from the source
                var shaderFiles = Directory.GetFiles(sourceFolder, "*.xnb", SearchOption.AllDirectories);
                
                if (shaderFiles.Length == 0)
                {
                    Console.WriteLine($"No shader files found in {sourceFolder}");
                    return false;
                }

                int installed = 0;
                foreach (var shaderFile in shaderFiles)
                {
                    string fileName = Path.GetFileName(shaderFile);
                    string destFile = Path.Combine(destFolder, fileName);
                    
                    try
                    {
                        // Create Shader directory if it doesn't exist
                        Directory.CreateDirectory(destFolder);
                        
                        File.Copy(shaderFile, destFile, overwrite: true);
                        installed++;
                        Console.WriteLine($"  Installed: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  Failed to install {fileName}: {ex.Message}");
                    }
                }

                // Only write marker if we actually installed shaders
                if (installed > 0)
                {
                    File.WriteAllText(markerPath, TargetPlatform);
                    Console.WriteLine($"Installed {installed} {TargetPlatform} shaders");
                    return true;
                }
                else
                {
                    Console.WriteLine("WARNING: No shaders were installed!");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to install shaders: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verify that shader XNB files are for the correct platform.
        /// Checks the XNB header byte (offset 3): 'w' = Windows, 'd' = DesktopGL.
        /// </summary>
        private static bool VerifyShaderPlatform(string shaderPath)
        {
            try
            {
                var xnbFiles = Directory.GetFiles(shaderPath, "*.xnb");
                if (xnbFiles.Length == 0)
                    return false;

                // Check the first XNB file's platform byte
                string testFile = xnbFiles[0];
                byte[] header = new byte[4];
                using (var fs = File.OpenRead(testFile))
                {
                    if (fs.Read(header, 0, 4) < 4)
                        return false;
                }

                // XNB header: 'X', 'N', 'B', platform_byte
                if (header[0] != 'X' || header[1] != 'N' || header[2] != 'B')
                    return false;

                char platformByte = (char)header[3];
#if WINDOWS
                // Windows expects 'w' (0x77)
                return platformByte == 'w';
#else
                // DesktopGL expects 'd' (0x64)
                return platformByte == 'd';
#endif
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if Content/Shader exists and has XNB files.
        /// </summary>
        public static bool HasShaders()
        {
            string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string shaderPath = Path.Combine(gameDirectory, "Content", "Shader");
            
            if (!Directory.Exists(shaderPath))
                return false;
                
            return Directory.GetFiles(shaderPath, "*.xnb").Length > 0;
        }

        /// <summary>
        /// Get the current shader platform (if known).
        /// </summary>
        public static string GetCurrentPlatform()
        {
            string gameDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string markerPath = Path.Combine(gameDirectory, "Content", "Shader", ShaderMarkerFile);
            
            if (File.Exists(markerPath))
                return File.ReadAllText(markerPath).Trim();
                
            return "Unknown (probably Windows from v1.0.0)";
        }
    }
}
