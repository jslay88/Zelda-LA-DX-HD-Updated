namespace ProjectZ.InGame.Things
{
    class GameSettings
    {
        // Game Settings
        public static int     MenuBorder      =  0;
        public static bool    ClassicSword    =  false;
        public static bool    StoreSavePos    =  false;
        public static int     LastSavePos     =  0;
        public static bool    Autosave        =  true;
        public static bool    ItemsOnRight    =  false;
        public static bool    EpilepsySafe    =  false;

        // Redux Settings
        public static bool    VarWidthFont    =  false;
        public static bool    NoHelperText    =  false;
        public static bool    DialogSkip      =  false;
        public static bool    Uncensored      =  false;
        public static bool    Unmissables     =  false;
        public static bool    PhotosColor     =  false;
        public static bool    NoAnimalDamage  =  false;

        // Camera Settings
        public static bool    ClassicCamera   =  false;
        public static bool    ClassicDungeon  =  false;
        public static int     ClassicBorders  =  0;
        public static float   ClassicAlpha    =  1.00f;
        public static bool    CameraLock      =  true;
        public static bool    SmoothCamera    =  true;
        public static bool    ScreenShake     =  true;

        // Video Settings
        public static int     GameScale       =  Game1.MaxGameScale + 1;
        public static int     UiScale         =  11;
        public static bool    IsFullscreen    =  false;
        public static bool    ExFullscreen    =  false;
        public static bool    GlobalLighting  =  true;
        public static bool    ObjectLighting  =  true;
        public static bool    EnableShadows   =  true;
        public static bool    VerticalSync    =  true;

        // Audio Settings
        private static int    _musicVolume    =  100;
        private static int    _effectVolume   =  100;
        public static bool    ClassicMusic    =  false;
        public static bool    MuteInactive    =  true;
        public static bool    HeartBeep       =  true;
        public static bool    MutePowerups    =  false;

        // Control Settings
        public static float   DeadZone        =  0.10f;
        public static string  Controller      =  "XBox";
        public static bool    SwapButtons     =  false;
        public static bool    OldMovement     =  false;
        public static bool    DigitalAnalog   =  false;

        // Modifiers Settings
        public static int     EnemyBonusHP    =  0;
        public static int     DamageFactor    =  4;
        public static int     DmgCooldown     =  16;
        public static float   MoveSpeedAdded  =  0;
        public static bool    NoHeartDrops    =  false;
        public static bool    NoDamageLaunch  =  false;

        // Sword Collection
        public static bool    SwGrabNormal    =  true;
        public static bool    SwGrabWorldItem =  false;
        public static bool    SwGrabFairy     =  false;
        public static bool    SwGrabSmallKey  =  false;
        public static bool    SwBoomerang     =  false;
        public static bool    SwSmackBombs    =  false;
        public static bool    SwMissileBlock  =  false;
        public static bool    SwBreakPots     =  false;
        public static bool    SwBeamShrubs    =  false;

        public static int MusicVolume
        {
            get => _musicVolume;
            set { _musicVolume = value; Game1.GbsPlayer.SetVolume(value / 100.0f); }
        }

        public static int EffectVolume
        {
            get => _effectVolume;
            set { _effectVolume = value; }
        }

    }
}
