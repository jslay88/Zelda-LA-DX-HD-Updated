﻿using System;
﻿using System.IO;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.Base.UI;
using ProjectZ.Editor;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.GameObjects;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Pages;
using ProjectZ.InGame.SaveLoad;
using ProjectZ.InGame.Screens;
using ProjectZ.InGame.Things;
using GBSPlayer;

#if WINDOWS
using Forms = System.Windows.Forms;
#endif

namespace ProjectZ
{
    public class Game1 : Game
    {
        public static GraphicsDeviceManager Graphics;
        public static SpriteBatch SpriteBatch;
        public static UiManager UiManager = new UiManager();
        public static ScreenManager ScreenManager = new ScreenManager();
        public static PageManager UiPageManager = new PageManager();
        public static Language LanguageManager = new Language();
        public static GameManager GameManager = new GameManager();
        public static GbsPlayer GbsPlayer = new GbsPlayer();
        public static Random RandomNumber = new Random();
        public static CameraField ClassicCamera = new CameraField();
        public static EditorManager EditorManager;
  
        public static int WindowWidth;
        public static int WindowHeight;
        public static int WindowWidthEnd;
        public static int WindowHeightEnd;
        public static int RenderWidth;
        public static int RenderHeight;

        public static bool FullScreen;
        public static bool WasExclusive;

        private bool _firstFrameDrawn;
        private bool _fullscreenWasSet;

        private static int _lastWindowWidth;
        private static int _lastWindowHeight;

#if WINDOWS
        private static System.Drawing.Rectangle _lastWindowBounds;
#endif

        public static bool FpsSettingChanged;
        private readonly SimpleFps _fpsCounter = new SimpleFps();

        public static double FreezeTime;
        public static float TimeMultiplier;
        public static float DeltaTime;
        public static double TotalTime;
        public static double TotalGameTime;
        public static double TotalGameTimeLast;

        private static DoubleAverage _avgTotalMs = new DoubleAverage(30);
        private static DoubleAverage _avgTimeMult = new DoubleAverage(30);

        public static RenderTarget2D MainRenderTarget;
        private static RenderTarget2D _renderTarget1;
        private static RenderTarget2D _renderTarget2;
        private static bool _initRenderTargets;

        public static int UiScale;
        public static bool ScaleChanged;
        public static int MaxGameScale;

        public static bool WasActive;
        public static bool UpdateGame;
        public static bool ForceDialogUpdate;
        public static bool EditorMode;
        public static bool SaveAndExitGame;
        public static bool AutoLoadSave;
        public static int AutoLoadSlot;

        private static bool _finishedLoading;

        public static string DebugText;
        public static float DebugTimeScale = 1.0f;
        public static bool DebugStepper;
        public static int DebugLightMode;
        public static int DebugBoxMode;
        public static bool DebugMode;
        public static bool ShowDebugText;
        private Vector2 _debugTextSize;

        public static Keys DebugToggleDebugText = Keys.F1;
        public static Keys DebugToggleDebugModeKey = Keys.F2;
        public static Keys DebugBox = Keys.F3;
        public static Keys DebugSaveKey = Keys.F5;
        public static Keys DebugLoadKey = Keys.F6;
        public static Keys DebugShadowKey = Keys.F9;

        // True when in-game after selecting save file. False at main menu and intro.
        public static bool InProgress;
        
        // Stores classic cam setting for ending.
        static public bool StoredClassicCam;

        public static bool FinishedLoading => _finishedLoading;

        public static Matrix GetMatrix => Matrix.CreateScale(new Vector3(
            (float)Graphics.PreferredBackBufferWidth / WindowWidth,
            (float)Graphics.PreferredBackBufferHeight / WindowHeight, 0));

#if WINDOWS
        private static Forms.Form _windowForm;
        private static Forms.FormWindowState _lastWindowState;
#endif

        // lahdmod values
        private int  max_game_scale = 20;
        private bool editor_mode = false;

        public Game1(bool editorMode, bool loadSave, int loadSlot)
        {
            // If a mod file exists load the values from it.
            string modFile = Path.Combine(Values.PathModFolder, "Game1.lahdmod");

            if (File.Exists(modFile))
                ModFile.Parse(modFile, this);

            // Use max game scale set by lahdmod file or default value set above.
            MaxGameScale = max_game_scale;

            // Enable editor via lahdmod file or through the command line option.
            EditorMode = editorMode || editor_mode;

#if WINDOWS
            // Get the form handle and set the icon of the window.
            _windowForm = (Forms.Form)Forms.Control.FromHandle(Window.Handle);
            _windowForm.Icon = Properties.Resources.Icon;

            // Calculate the extra pixels taken up by the title bar and window border.
            var deltaWidth = _windowForm.Width - _windowForm.ClientSize.Width;
            var deltaHeight = _windowForm.Height - _windowForm.ClientSize.Height;

            // Set the minimum window size including the extra pixels.
            _windowForm.MinimumSize = new System.Drawing.Size(Values.MinWidth + deltaWidth, Values.MinHeight + deltaHeight);
#endif

            // Create the graphics device and set the back buffer width/height.
            Graphics = new GraphicsDeviceManager(this);
            Graphics.GraphicsProfile = GraphicsProfile.HiDef;
            Graphics.PreferredBackBufferWidth = Values.MinWidth * 3;
            Graphics.PreferredBackBufferHeight = Values.MinHeight * 3;

            // Allow the user to resize the window.
            Window.AllowUserResizing = true;

            // Store any command line parameters if available.
            IsMouseVisible = EditorMode;
            AutoLoadSave = loadSave;
            AutoLoadSlot = loadSlot;

            // Set the content directory.
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            // Initialize the editor.
            EditorManager = new EditorManager(this);
            base.Initialize();
        }

        protected override void OnExiting(object sender, EventArgs args)
        {
            // Close out the GBS Player before exiting.
            GbsPlayer.OnExit();
            base.OnExiting(sender, args);
        }

        private void OnDeviceReset(object sender, EventArgs e)
        {
            // Update render targets when device resets.
            GameManager?.UpdateRenderTargets();
            UpdateRenderTargetSizes(WindowWidth, WindowHeight);
        }

        protected override void LoadContent()
        {
            // Hook device reset function & create a new SpriteBatch to draw textures.
            GraphicsDevice.DeviceReset += OnDeviceReset;
            SpriteBatch = new SpriteBatch(GraphicsDevice);

            // Start loading the resources that are needed after the intro.
            ThreadPool.QueueUserWorkItem(LoadContentThreaded);

            // Initialize controller and input handler.
            ControlHandler.Initialize();
            Components.Add(new InputHandler(this));

            // Load the users saved settings.
            SettingsSaveLoad.LoadSettings();

            // Load the Intro Screen and its resources.
            GameManager.UpdateSoundEffects();
            Resources.LoadIntro(Graphics.GraphicsDevice, Content);
            ScreenManager.LoadIntro(Content);

            // Initialize the GBS Player and load in the Link's Awakening GBS file.
            GbsPlayer.LoadFile(Path.Combine(Values.PathContentFolder, "Music", "awakening.gbs"));
            GbsPlayer.StartThread();

            // set the fps settings of the game
            UpdateFpsSettings();

            // Initialize extra monster hit points set by the user.
            ObjLives.Initialize();

            // If borderless fullscreen is selected we can do it now.
            if (GameSettings.IsFullscreen && !GameSettings.ExFullscreen)
            {
                GameSettings.IsFullscreen = false;
                ToggleFullscreen();
            }
        }

        private void LoadContentThreaded(Object obj)
        {
            // Load all of the game's resources.
            Resources.LoadTextures(Graphics.GraphicsDevice, Content);
            Resources.LoadSounds(Content);
            GameManager.Load(Content);

            // Set up all of the GameObject templates.
            GameObjectTemplates.SetUpGameObjects();

            // Finish loading in resources.
            ScreenManager.Load(Content);
            LanguageManager.Load();
            UiPageManager.Load(Content);

            // Set up the editor if enabled.
            if (EditorMode)
                EditorManager.SetUpEditorUi();

            // Flag that the thread has finished loading in content.
            _finishedLoading = true;

            // Now that everything has been loaded in, make sure the proper language textures are reloaded.
            Resources.RefreshDynamicResources();
        }

        protected override void Update(GameTime gameTime)
        {
            // If exclusive fullscreen mode is enabled.
            if (_firstFrameDrawn && !_fullscreenWasSet)
            {
                // We need to delay it until the graphics device has been fully set up.
                if (GameSettings.IsFullscreen && GameSettings.ExFullscreen)
                {
                    GameSettings.IsFullscreen = false;
                    ToggleFullscreen();
                }
                _fullscreenWasSet = true;
            }
            // Prevent input when window is in background (do we even want this?).
            WasActive = IsActive;

            // Mute music and sound effects if user disabled on inactive window.
            GameManager.HandleInactiveWindow(IsActive);

            // Updates the FPS counter.
            _fpsCounter.Update(gameTime);

            // Toggles fullscreen mode.
            if ((InputHandler.KeyDown(Keys.LeftAlt) || InputHandler.KeyDown(Keys.RightAlt)) && InputHandler.KeyPressed(Keys.Enter))
            {
                ToggleFullscreen();
                InputHandler.ResetInputState();
                SettingsSaveLoad.SaveSettings();
            }
            // Initialize render targets if thread is finished loading resources and they have not been initialized yet. 
            if (_finishedLoading && !_initRenderTargets)
            {
                _initRenderTargets = true;
                WindowWidth = 0;
                WindowHeightEnd = 0;
            }
            // If the window size has changed then trigger a resize event.
            if ((WindowWidth != Window.ClientBounds.Width) || (WindowHeight != Window.ClientBounds.Height))
                OnResize();

            // Update the scale if it has been changed.
            if (ScaleChanged)
                UpdateScale();

            // If the FPS settings has changed then update them.
            if (FpsSettingChanged)
            {
                UpdateFpsSettings();
                FpsSettingChanged = false;
            }
            // Update all render targets.
            UpdateRenderTargets();

            // Update input from any input devices (controller/keyboard).
            ControlHandler.Update();

            // When the content thread is finished loading.
            if (_finishedLoading)
            {
                // Update the editor UI.
                if (EditorMode)
                {
                    UiManager.Update();
                    EditorManager.EditorUpdate(gameTime);
                }
                // Update the UI.
                UiManager.CurrentScreen = "";
                UiPageManager.Update(gameTime);
            }
            // If editor is enabled and F1 key is pressed.
            if (EditorMode && InputHandler.KeyPressed(DebugToggleDebugText))
                ShowDebugText = !ShowDebugText;

            // Debug Stepper (N Key) is not active.
            if (!DebugStepper)
            {
                TimeMultiplier = gameTime.ElapsedGameTime.Ticks / 166667f * DebugTimeScale;
                TotalGameTimeLast = TotalGameTime;

                // limit the game time so that it slows down if the steps are bigger than they would be for 30fps
                // if the timesteps get too big it would be hard (wast of time) to make the logic still function 100% correctly
                if (TimeMultiplier > 2.0f)
                {
                    TimeMultiplier = 2.0f;
                    DeltaTime = (TimeMultiplier * 1000.0f) / 60.0f;
                    TotalTime += (TimeMultiplier * 1000.0) / 60.0;
                    DebugText += "\nLow Framerate";

                    if (UpdateGame)
                        TotalGameTime += (TimeMultiplier * 1000.0) / 60.0;
                }
                else
                {
                    DeltaTime = (float)gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                    TotalTime += gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                    if (UpdateGame)
                        TotalGameTime += gameTime.ElapsedGameTime.TotalMilliseconds * DebugTimeScale;
                }
            }
            // update the screen manager
            UpdateGame = true;

            if (!DebugStepper || InputHandler.KeyPressed(Keys.M))
                ScreenManager.Update(gameTime);

            if (_finishedLoading)
            {
                DebugText += _fpsCounter.Msg;

                _avgTotalMs.AddValue(gameTime.ElapsedGameTime.TotalMilliseconds);
                _avgTimeMult.AddValue(TimeMultiplier);
                DebugText += $"\ntotal ms:      {_avgTotalMs.Average,6:N3}" +
                             $"\ntime mult:     {_avgTimeMult.Average,6:N3}" +
                             $"\ntime scale:    {DebugTimeScale}" +
                             $"\ntime:          {TotalGameTime}";

                DebugText += "\nHistory Enabled: " + GameManager.SaveManager.HistoryEnabled + "\n";
            }
            base.Update(gameTime);
        }

        private void EnsureRenderTargets()
        {
            if (MainRenderTarget == null || _renderTarget1 == null || _renderTarget2 == null)
            {
                UpdateRenderTargetSizes(Math.Max(1, WindowWidth), Math.Max(1, WindowHeight));
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            EnsureRenderTargets();

            if (!_finishedLoading)
            {
                ScreenManager.Draw(SpriteBatch);
                return;
            }
            _firstFrameDrawn = true;

            _fpsCounter.CountDraw();

            ScreenManager.DrawRT(SpriteBatch);

            if (MainRenderTarget == null)
            {
                GraphicsDevice.Clear(Color.CadetBlue);
                ScreenManager.Draw(SpriteBatch);
                return;
            }
            Graphics.GraphicsDevice.SetRenderTarget(MainRenderTarget);
            GraphicsDevice.Clear(Color.CadetBlue);

            ScreenManager.Draw(SpriteBatch);

            BlurImage();
            {
                Graphics.GraphicsDevice.SetRenderTarget(null);

                SpriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap);

                SpriteBatch.Draw(MainRenderTarget, new Rectangle(0, 0, MainRenderTarget.Width, MainRenderTarget.Height), Color.White);

                SpriteBatch.End();
            }

            {
                if (_renderTarget2 != null)
                {
                    Resources.BlurEffect.Parameters["sprBlur"].SetValue(_renderTarget2);
                    Resources.RoundedCornerBlurEffect.Parameters["sprBlur"].SetValue(_renderTarget2);
                }
                SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.RoundedCornerBlurEffect, GetMatrix);

                // blurred ui parts
                UiManager.DrawBlur(SpriteBatch);

                // blured stuff
                GameManager.InGameOverlay.InGameHud.DrawBlur(SpriteBatch);

                // background for the debug text
                DebugTextBackground();

                SpriteBatch.End();
            }

            {
                // draw the top part
                SpriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, GetMatrix);

                // draw the ui part
                UiManager.Draw(SpriteBatch);

                // draw the game ui
                UiPageManager.Draw(SpriteBatch);

                // draw the screen tops
                ScreenManager.DrawTop(SpriteBatch);

                // draw the debug text
                DrawDebugText();
                DebugText = "";

                #if DEBUG
                    if (GameManager.SaveManager.HistoryEnabled)
                        SpriteBatch.Draw(Resources.SprWhite, new Rectangle(0, WindowHeight - 6, WindowWidth, 6), Color.Red);
                #endif

                SpriteBatch.End();
            }
            base.Draw(gameTime);
        }

        private void BlurImage()
        {
            if (MainRenderTarget == null || _renderTarget1 == null || _renderTarget2 == null)
                return;

            var blurValue = 0.2f;

            Resources.BlurEffectH.Parameters["pixelX"].SetValue(1.0f / _renderTarget1.Width);
            Resources.BlurEffectV.Parameters["pixelY"].SetValue(1.0f / _renderTarget1.Height);

            var mult0 = blurValue;
            var mult1 = (1 - blurValue * 2) / 2;
            Resources.BlurEffectH.Parameters["mult0"].SetValue(mult0);
            Resources.BlurEffectH.Parameters["mult1"].SetValue(mult1);
            Resources.BlurEffectV.Parameters["mult0"].SetValue(mult0);
            Resources.BlurEffectV.Parameters["mult1"].SetValue(mult1);

            Graphics.GraphicsDevice.SetRenderTarget(_renderTarget2);
            SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, null, null);
            SpriteBatch.Draw(MainRenderTarget, new Rectangle(0, 0, _renderTarget2.Width, _renderTarget2.Height), Color.White);
            SpriteBatch.End();

            for (var i = 0; i < 2; i++)
            {
                // v blur
                Graphics.GraphicsDevice.SetRenderTarget(_renderTarget1);
                SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BlurEffectV, null);
                SpriteBatch.Draw(_renderTarget2, Vector2.Zero, Color.White);
                SpriteBatch.End();

                // h blur
                Graphics.GraphicsDevice.SetRenderTarget(_renderTarget2);
                SpriteBatch.Begin(SpriteSortMode.Immediate, null, SamplerState.AnisotropicClamp, null, null, Resources.BlurEffectH, null);
                SpriteBatch.Draw(_renderTarget1, Vector2.Zero, Color.White);
                SpriteBatch.End();
            }
        }

        public void TriggerFpsSettings()
        {
            if (!IsFixedTimeStep)
            {
                IsFixedTimeStep = true;
                Graphics.SynchronizeWithVerticalRetrace = false;
            }
            else
            {
                IsFixedTimeStep = false;
                Graphics.SynchronizeWithVerticalRetrace = true;
            }
            Graphics.ApplyChanges();
        }

        public static void ToggleFullscreen()
        {
#if WINDOWS
            ToggleFullscreenWindows();
#else
            ToggleFullscreenLinux();
#endif
        }

#if WINDOWS
        private static void ToggleFullscreenWindows()
        {
            // Switch to fullscreen mode.
            if (!GameSettings.IsFullscreen)
            {
                // Set fullscreen mode to true.
                FullScreen = GameSettings.IsFullscreen = true;
                var screenBounds = Forms.Screen.GetBounds(_windowForm);

                // Save current window state for restoration.
                _lastWindowState = _windowForm.WindowState;
                _lastWindowBounds = _windowForm.Bounds;
                _lastWindowWidth = Graphics.PreferredBackBufferWidth;
                _lastWindowHeight = Graphics.PreferredBackBufferHeight;

                // Exclusive fullscreen mode.
                if (GameSettings.ExFullscreen)
                {
                    Graphics.PreferredBackBufferWidth = screenBounds.Width;
                    Graphics.PreferredBackBufferHeight = screenBounds.Height;
                    Graphics.ToggleFullScreen();
                    WasExclusive = true;
                }
                // Borderless fullscreen mode.
                else
                {
                    _windowForm.FormBorderStyle = Forms.FormBorderStyle.None;
                    _windowForm.WindowState = Forms.FormWindowState.Normal;
                    _windowForm.Bounds = screenBounds;
                }
            }
            // Switch to windowed mode.
            else
            {
                // Set fullscreen mode to false.
                FullScreen = GameSettings.IsFullscreen = false;

                // Restore the backbuffer to the previous window state if exclusive.
                if ((GameSettings.ExFullscreen || WasExclusive) && Graphics.IsFullScreen)
                {
                    Graphics.ToggleFullScreen();
                    Graphics.PreferredBackBufferWidth = _lastWindowWidth;
                    Graphics.PreferredBackBufferHeight = _lastWindowHeight;
                }
                // Restore the windowed settings.
                _windowForm.FormBorderStyle = Forms.FormBorderStyle.Sizable;
                _windowForm.WindowState = _lastWindowState;
                _windowForm.Bounds = _lastWindowBounds;

                // Apply the graphics changes.
                Graphics.ApplyChanges();
                WasExclusive = false;
            }
            // Update the render targets.
            GameManager?.UpdateRenderTargets();
        }
#else
        private static void ToggleFullscreenLinux()
        {
            // Switch to fullscreen mode.
            if (!GameSettings.IsFullscreen)
            {
                // Set fullscreen mode to true.
                FullScreen = GameSettings.IsFullscreen = true;
                
                // Save current window state for restoration.
                _lastWindowWidth = Graphics.PreferredBackBufferWidth;
                _lastWindowHeight = Graphics.PreferredBackBufferHeight;

                // On Linux, we use MonoGame's built-in fullscreen toggle
                // which works with SDL and supports both exclusive and borderless modes
                if (GameSettings.ExFullscreen)
                {
                    // Exclusive fullscreen - let MonoGame/SDL handle the resolution
                    Graphics.HardwareModeSwitch = true;
                    Graphics.IsFullScreen = true;
                    Graphics.ApplyChanges();
                    WasExclusive = true;
                }
                else
                {
                    // Borderless fullscreen
                    Graphics.HardwareModeSwitch = false;
                    Graphics.IsFullScreen = true;
                    Graphics.ApplyChanges();
                }
            }
            // Switch to windowed mode.
            else
            {
                // Set fullscreen mode to false.
                FullScreen = GameSettings.IsFullscreen = false;

                // Restore windowed mode
                Graphics.IsFullScreen = false;
                Graphics.PreferredBackBufferWidth = _lastWindowWidth;
                Graphics.PreferredBackBufferHeight = _lastWindowHeight;
                Graphics.ApplyChanges();
                WasExclusive = false;
            }
            // Update the render targets.
            GameManager?.UpdateRenderTargets();
        }
#endif

        public void DebugTextBackground()
        {
            if (!ShowDebugText)
                return;

            _debugTextSize = Resources.GameFont.MeasureString(DebugText);

            SpriteBatch.Draw(_renderTarget2, new Rectangle(0, 0,
                (int)(_debugTextSize.X * 2) + 20, (int)(_debugTextSize.Y * 2) + 20), Color.White);
        }

        public void DrawDebugText()
        {
            if (!ShowDebugText)
                return;

            SpriteBatch.Draw(Resources.SprWhite, new Rectangle(0, 0,
                    (int)(_debugTextSize.X * 2) + 20, (int)(_debugTextSize.Y * 2) + 20), Color.Black * 0.75f);

            SpriteBatch.DrawString(Resources.GameFont, DebugText, new Vector2(10), Color.White,
                0, Vector2.Zero, new Vector2(2f), SpriteEffects.None, 0);
        }

        public void UpdateFpsSettings()
        {
            IsFixedTimeStep = false;
            Graphics.SynchronizeWithVerticalRetrace = GameSettings.VerticalSync;
            Graphics.ApplyChanges();
        }

        private void OnResize()
        {
            // if minimized window bounds may be zero; ignore those
            if (Window.ClientBounds.Width <= 0 || Window.ClientBounds.Height <= 0)
                return;

            // Update the window resolution and update the scale.
            WindowWidth = Window.ClientBounds.Width;
            WindowHeight = Window.ClientBounds.Height;
            ScaleChanged = true;
        }

        private void UpdateScale()
        {
            if (Camera.ClassicMode)
            {
                // Force integer scale or the field rect will be thrown off. The scaling value is calculated using the original dimensions of the
                // Game Boy version of Link's Awakening, minus the 16 pixels HUD ( 160x144 >> 160x128 ) so higher scaling values can be achieved.
                int gameScale = Math.Max(1, Math.Min(WindowWidth / 160, WindowHeight / 128));

                // Super Game Boy border is enabled. Calculate from the base resolution of the border instead.
                if (GameSettings.ClassicBorders == 2)
                    gameScale = Math.Max(1, Math.Min(WindowWidth / 256, WindowHeight / 224));

                // Send the game scale to the proper places it needs to go.
                MapManager.Camera.Scale = gameScale;
                GameManager.SetGameScale(gameScale);
            }
            else
            {
                // Get the maximum scale and add 1 for auto-scale.
                int maxScale = MaxGameScale + 1;

                // Calculate the game scale that is used for auto scaling.
                float gameScale = MathHelper.Clamp(Math.Min(WindowWidth / 160, WindowHeight / 128), 1, maxScale);
                float usedScale = gameScale;

                if (GameSettings.GameScale == maxScale)
                    usedScale = gameScale / 2;

                // If set to autoscale (Game1.MaxGameScale + 1) used the calculated value; otherwise use the value set by the user.
                MapManager.Camera.Scale = GameSettings.GameScale == maxScale
                    ? MathF.Ceiling(usedScale) 
                    : GameSettings.GameScale;

                // The camera scale uses a float value and can use a fractional scaling value when drawing the world.
                if (MapManager.Camera.Scale < 1)
                {
                    MapManager.Camera.Scale = 1 / (2 - MapManager.Camera.Scale);
                    GameManager.SetGameScale(1);
                }
                // If it's 1x or greater. We use "gameScale" directly here as a float as it allows fractional 
                // values while manually setting the scale only allows upscaling using integer values.
                else
                {
                    float newGameScale = GameSettings.GameScale == maxScale
                        ? MathF.Ceiling(usedScale)
                        : GameSettings.GameScale;
                    GameManager.SetGameScale(newGameScale);
                }
            }
            // Scale of the user interface.
            int interfaceScale = MathHelper.Clamp(Math.Min(WindowWidth / Values.MinWidth, WindowHeight / Values.MinHeight), 1, 11);

            if (GameSettings.UiScale > interfaceScale)
                UiScale = interfaceScale;
            else
                UiScale = GameSettings.UiScale == 0 
                    ? interfaceScale 
                    : MathHelper.Clamp(GameSettings.UiScale, 1, interfaceScale);

            UiManager.SizeChanged();
            ScreenManager.OnResize(WindowWidth, WindowHeight);
            UiPageManager.OnResize(WindowWidth, WindowHeight);

            // This needs to go false or it will run every loop.
            ScaleChanged = false;
        }

        private void UpdateRenderTargets()
        {
            if (WindowWidthEnd == WindowWidth && WindowHeightEnd == WindowHeight)
                return;

            WindowWidthEnd = WindowWidth;
            WindowHeightEnd = WindowHeight;

            UpdateRenderTargetSizes(WindowWidth, WindowHeight);

            ScreenManager.OnResizeEnd(WindowWidth, WindowHeight);
        }

        private void UpdateRenderTargetSizes(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            Resources.BlurEffect.Parameters["width"].SetValue(width);
            Resources.BlurEffect.Parameters["height"].SetValue(height);
            Resources.RoundedCornerBlurEffect.Parameters["textureWidth"].SetValue(width);
            Resources.RoundedCornerBlurEffect.Parameters["textureHeight"].SetValue(height);

            var blurScale = MathHelper.Clamp(MapManager.Camera.Scale / 2, 1, 10);
            var blurRtWidth = Math.Max(1, (int)(width / blurScale));
            var blurRtHeight = Math.Max(1, (int)(height / blurScale));

            RenderTarget2D newMain = null;
            RenderTarget2D newRt1 = null;
            RenderTarget2D newRt2 = null;

            try
            {
                newMain = new RenderTarget2D(Graphics.GraphicsDevice, width, height);
                newRt1 = new RenderTarget2D(Graphics.GraphicsDevice, blurRtWidth, blurRtHeight);
                newRt2 = new RenderTarget2D(Graphics.GraphicsDevice, blurRtWidth, blurRtHeight);
            }
            catch (Exception ex)
            {
                newMain?.Dispose();
                newRt1?.Dispose();
                newRt2?.Dispose();
                return;
            }
            MainRenderTarget?.Dispose();
            _renderTarget1?.Dispose();
            _renderTarget2?.Dispose();

            MainRenderTarget = newMain;
            _renderTarget1 = newRt1;
            _renderTarget2 = newRt2;
        }
    }
}
