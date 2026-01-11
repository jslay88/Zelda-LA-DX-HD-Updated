using System;
using System.IO;
using ProjectZ.InGame.SaveLoad;
using Microsoft.Xna.Framework;

namespace ProjectZ.InGame.Things
{
    public partial class Values
    {
        public static string VersionString = "v1.5.2b";

        public static Color ColorBackgroundLight = Color.Black * 0.8f;
        public static Color ColorBackgroundDark = Color.Black * 0.85f;
        public static Color ColorUiEditor = new Color(41, 57, 85) * 0.85f;

        public static Color MenuButtonColor = new Color(40, 64, 128);
        public static Color MenuButtonColorSelected = new Color(90, 110, 170);
        public static Color MenuButtonColorSlider = new Color(40, 64, 128);

        public static Color MenuButtonColorDisabled = new Color(100, 100, 100);
        public static Color MenuButtonColorSelectedDisabled = new Color(150, 150, 150);
        public static Color MenuButtonColorSliderDisabled = new Color(100, 100, 100);

        public static Color InventoryBackgroundColorTop = new Color(255, 255, 230) * 0.85f;
        public static Color InventoryBackgroundColor = new Color(255, 255, 230) * 0.75f;

        public static Color GameMenuBackgroundColor = new Color(255, 255, 255, 255);

        public static Color TextboxBackgroundColor = new Color(0, 0, 0) * 0.85f;
        public static Color TextboxBackgroundSideColor = new Color(248, 248, 136) * 0.65f;
        public static Color TextboxBlurColor = new Color(255, 255, 255, 255);
        public static Color TextboxFontColor = new Color(248, 248, 136);

        public static Color MapTransitionColor = new Color(0, 0, 0, 255);
        public static Color MapFirstTransitionColor = new Color(0, 0, 0, 255);

        public static Color OverlayBackgroundColor = new Color(255, 255, 190) * 0.55f;
        public static Color OverlayBackgroundBlurColor = new Color(255, 255, 255, 255);

        public static Color[] SkirtColors = { new Color(16, 168, 64), new Color(0, 38, 255), new Color(255, 0, 0) };

        public static string WorkingDirectory = AppContext.BaseDirectory;
        public static string AppDataFolder = GetAppDataFolder();
        public static string PathSaveFolder = SaveManager.GetSaveFilePath();

        /// <summary>
        /// Gets the appropriate application data folder for the current platform.
        /// Windows: %LocalAppData% (e.g., C:\Users\X\AppData\Local)
        /// Linux/macOS: $XDG_DATA_HOME or ~/.local/share
        /// </summary>
        private static string GetAppDataFolder()
        {
#if WINDOWS
            return Environment.ExpandEnvironmentVariables("%LocalAppData%");
#else
            // Use XDG Base Directory Specification for Linux/macOS
            // XDG_DATA_HOME defaults to ~/.local/share
            string xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrEmpty(xdgDataHome))
                return xdgDataHome;
            
            string home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrEmpty(home))
                return Path.Combine(home, ".local", "share");
            
            // Fallback to current directory if nothing else works
            return AppContext.BaseDirectory;
#endif
        }

        public static string PathContentFolder = "Data";
        public static string PathLanguageFolder => Path.Combine(PathContentFolder, "Languages");
        public static string PathMapsFolder => Path.Combine(PathContentFolder, "Maps");
        public static string PathTilesetFolder => Path.Combine(PathContentFolder, "Maps", "Tilesets");
        public static string PathMapObjectFolder => Path.Combine(PathContentFolder, "Map Objects");
        public static string PathLightsFolder => Path.Combine(PathContentFolder, "Lights");
        public static string PathAnimationFolder => Path.Combine(PathContentFolder, "Animations");
        public static string PathMinimapFolder => Path.Combine(PathContentFolder, "Dungeon");
        public static string PathModFolder => Path.Combine(PathContentFolder, "Mods");

        public const string EditorUiObjectEditor = "objectEditor";
        public const string EditorUiObjectSelection = "objectSelection";
        public const string EditorUiTileEditor = "tileEditor";
        public const string EditorUiTileSelection = "tileSelection";
        public const string EditorUiDigTileEditor = "digTileEditor";
        public const string EditorUiMusicTileEditor = "musicTileEditor";
        public const string EditorUiTileExtractor = "tileExtractor";
        public const string EditorUiTilesetEditor = "tilesetEditor";
        public const string EditorUiAnimation = "animationEditor";
        public const string EditorUiSpriteAtlas = "spriteAtlasEditor";

        public const string ScreenNameIntro = "INTRO";
        public const string ScreenNameMenu = "MENU";
        public const string ScreenNameGame = "GAME";
        public const string ScreenGameOver = "GAMEOVER";
        public const string ScreenNameMap = "MAP";
        public const string ScreenNameSettings = "SETTINGS";
        public const string ScreenEnding = "ENDING";

        public const string ScreenNameEditor = "MAP_EDITOR";
        public const string ScreenNameEditorTileset = "TILESET_EDITOR";
        public const string ScreenNameEditorTilesetExtractor = "TILESET_EXTRACTOR";
        public const string ScreenNameEditorAnimation = "ANIMATION_EDITOR";
        public const string ScreenNameSpriteAtlasEditor = "SPRITE_ATLAS_EDITOR";

        public const float UiBackgroundRadius = 2.0f;
        public const float UiTextboxRadius = 3.0f;

        public static int TileSize = 16;
        public static int FieldWidth = 160;
        public static int FieldHeight = 128;

        public static int ToolBarHeight = 40;

        public static int LayerBackground = 0;  // layer behind tileset
        public static int LayerBottom = 1;      // layer under the player (grass, water, flowers, etc.)
        public static int LayerPlayer = 2;      // same layer as the player
        public static int LayerTop = 3;         // on top of the player

        public static int LightLayer0 = 0;  // lamp
        public static int LightLayer1 = 1;  // teleporter light
        public static int LightLayer2 = 2;  // dark room
        public static int LightLayer3 = 3;

        public static int HandItemSlots = 6;

        // The original game field size was 160x128. The minimum resolution for this port is 380x256 due to the
        // fact the menus were designed around this size. This means the minimum real scale is at least 2x.
        public static int MinWidth = 380;
        public static int MinHeight = 256;

        public static double MenuHeaderSize = 0.2;
        public static double MenuContentSize = 0.7;
        public static double MenuFooterSize = 0.2;

        public static int LetterWidth = 8;
        public static int LetterHeight = 8;

        public static int GameSaveBlackScreen = 250;
        public static int GameRespawnBlackScreen = 250;

        public static float ShadowHeightDefault = 0.75f;
        public static float ShadowRotationDefault = 0.0f;

        public static int SaveRetries = 10;
        public static int LoadRetries = 10;

        public const float SoundEffectVolumeMult = 0.85f;
    }
}
