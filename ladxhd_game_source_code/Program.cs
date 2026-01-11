using System;
using ProjectZ.InGame.Things;

#if WINDOWS
using System.Windows.Forms;
#endif

namespace ProjectZ
{
    public static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            var editorMode = false;
            var loadSave = false;
            var saveSlot = 0;

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.Equals("editor", StringComparison.OrdinalIgnoreCase))
                {
                    editorMode = true;
                }
                else if (arg.Equals("loadSave", StringComparison.OrdinalIgnoreCase))
                {
                    loadSave = true;

                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int parsedSlot))
                    {
                        saveSlot = parsedSlot;
                        i++;
                    }
                }
                else if (arg.Equals("exclusive", StringComparison.OrdinalIgnoreCase))
                {
                    GameSettings.ExFullscreen = true;
                }
            }

            try
            {
                // Show splash screen during startup operations
                SplashScreen.Show("Checking game assets...");

                // Step 1: Check if user has provided the v1.0.0 zip file and extract Content/Data if so
                // This allows users to simply drop the zip file next to the game binary
                if (!ZipExtractor.CheckAndExtract())
                {
                    SplashScreen.Close();
                    SplashScreen.ShowError(
                        "Game assets not found.\n\n" +
                        "Please provide the original 'Links Awakening DX HD v1.0.0.zip' file " +
                        "or copy the Content and Data folders to the game directory.",
                        "Missing Assets");
                    return;
                }

                // Step 2: Check and install platform-correct shaders if needed
                SplashScreen.SetStatus("Installing shaders...");
                SplashScreen.SetProgress(40);
                ShaderPatcher.EnsureCorrectShaders();
                
                // Step 3: Check and auto-patch assets if needed
                SplashScreen.SetStatus("Checking for asset updates...");
                SplashScreen.SetProgress(50);
                if (!AssetPatcher.CheckAndPatchAssets())
                {
                    SplashScreen.Close();
                    SplashScreen.ShowError(
                        "Asset patching failed or was cancelled.",
                        "Patching Error");
                    return;
                }

                // Close splash and start the game
                SplashScreen.SetStatus("Starting game...");
                SplashScreen.SetProgress(100);
                SplashScreen.Close();

                using (var game = new Game1(editorMode, loadSave, saveSlot))
                    game.Run();
            }

            catch (Exception exception)
            {
#if WINDOWS
                MessageBox.Show(exception.StackTrace, exception.Message, MessageBoxButtons.OK, MessageBoxIcon.Error);
#else
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {exception.Message}");
                Console.ResetColor();
                Console.WriteLine(exception.StackTrace);
#endif
                throw;
            }
        }
    }
}
