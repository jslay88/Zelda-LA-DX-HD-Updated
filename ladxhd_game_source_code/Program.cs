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
                // Check and install platform-correct shaders if needed
                // This handles the case where v1.0.0 ships Windows shaders but we need DesktopGL on Linux
                ShaderPatcher.EnsureCorrectShaders();
                
                // Check and auto-patch assets if needed
                if (!AssetPatcher.CheckAndPatchAssets())
                {
                    Console.WriteLine("Asset patching failed or was cancelled. Exiting.");
                    return;
                }

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
