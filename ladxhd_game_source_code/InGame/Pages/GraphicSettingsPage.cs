using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class GraphicSettingsPage : InterfacePage
    {
        private readonly InterfaceSlider _uiScaleSlider;
        private readonly InterfaceSlider _gameScaleSlider;
        private readonly InterfaceListLayout _graphicSettingsLayout;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly InterfaceListLayout _toggleFullscreen;
        private readonly InterfaceListLayout _toggleExFullscreen;
        private bool _showTooltip;

        public GraphicSettingsPage(int width, int height)
        {
            EnableTooltips = true;

            // Graphics Settings Layout
            _graphicSettingsLayout = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };

            var buttonWidth = 320;
            var sliderHeight = 10;
            var buttonHeight = 14;

            _graphicSettingsLayout.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_graphics_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));
            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) + 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top };

            // Slider: Game Scale
            _gameScaleSlider = new InterfaceSlider(Resources.GameFont, "settings_graphics_game_scale",
                buttonWidth, sliderHeight, new Point(1, 2), -3, Game1.MaxGameScale + 1, 1, GameSettings.GameScale, 
                number => { GameSettings.GameScale = number; Game1.ScaleChanged = true; })
                { SetString = number => GameScaleSliderAdjustmentString(number) };
            _contentLayout.AddElement(_gameScaleSlider);

            // Slider: UI Scale
            _uiScaleSlider = new InterfaceSlider(Resources.GameFont, "settings_graphics_ui_scale",
                buttonWidth, sliderHeight, new Point(1, 2), 1, 11, 1, GameSettings.UiScale-1,
                number => { GameSettings.UiScale = number; Game1.ScaleChanged = true; })
                { SetString = number => UIScaleSliderAdjustmentString(number) };
            _contentLayout.AddElement(_uiScaleSlider);

            // Button: Fullscreen
            _toggleFullscreen = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_fullscreen", GameSettings.IsFullscreen,
                newState => { Game1.ToggleFullscreen(); Game1.ScaleChanged = true; });
            _contentLayout.AddElement(_toggleFullscreen);

            // Button: Exclusive Fullscreen
            _toggleExFullscreen = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_exfullscreen", GameSettings.ExFullscreen,
                newState => { GameSettings.ExFullscreen = newState; });
            _contentLayout.AddElement(_toggleExFullscreen);

            // Button: Disable Global Lighting
            var disableGlobalLightingToggle = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
               "settings_graphics_globallights", GameSettings.GlobalLighting,
               newState => GameSettings.GlobalLighting = newState);
             _contentLayout.AddElement(disableGlobalLightingToggle);

            // Button: Disable Object Lighting
            var disableObjectLightingToggle = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
               "settings_graphics_objectlights", GameSettings.ObjectLighting,
               newState => GameSettings.ObjectLighting = newState);
             _contentLayout.AddElement(disableObjectLightingToggle);

            // Button: Dynamic Shadows
            var shadowToggle = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
               "settings_graphics_shadow", GameSettings.EnableShadows,
               newState => GameSettings.EnableShadows = newState);
             _contentLayout.AddElement(shadowToggle);

            // Button: Vertical Sync
            var toggleFpsLock = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_fps_lock", GameSettings.VerticalSync,
                newState => { GameSettings.VerticalSync = newState; Game1.FpsSettingChanged = true; });
            _contentLayout.AddElement(toggleFpsLock);

            // Button: Epilepsy Safe
            var toggleEpilepsySafe = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_graphics_epilepsysafe", GameSettings.EpilepsySafe,
                newState => { GameSettings.EpilepsySafe = newState; });
            _contentLayout.AddElement(toggleEpilepsySafe);

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _graphicSettingsLayout.AddElement(_contentLayout);
            _graphicSettingsLayout.AddElement(_bottomBar);
            PageLayout = _graphicSettingsLayout;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

            UpdateFullscreenState();
            UpdateGameScaleSlider();

            // The back button was pressed.
            if (ControlHandler.ButtonPressed(ControlHandler.CancelButton))
                Game1.UiPageManager.PopPage();

            // The tooltip button was pressed.
            if (ControlHandler.ButtonPressed(CButtons.Y))
            {
                _showTooltip = !_showTooltip;
                if (_showTooltip)
                    Game1.GameManager.PlaySoundEffect("D360-21-15");
            }
            // Hide the tooltip when pressing anything.
            else if (ControlHandler.AnyButtonPressed())
                _showTooltip = false;
        }

        private string GameScaleSliderAdjustmentString(int number)
        {
            // Get the maximum scale and add 1 for auto-scale.
            int maxScale = Game1.MaxGameScale + 1;

            // Translate values below 1x and when autoscale is set.
            return number == maxScale
                ? " " + Game1.LanguageManager.GetString("settings_graphics_autodetect", "error")
                : number switch
                {
                     0 => " 50%",
                    -1 => " 33%",
                    -2 => " 25%",
                    -3 => " 20%",
                    _  => " " + number + "x"
                };
        }

        private string UIScaleSliderAdjustmentString(int number)
        {
            if (number == 11)
                return " " + Game1.LanguageManager.GetString("settings_graphics_autodetect", "error");
            return " " + number + "x";
        }

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // the left button is always the first one selected
            _bottomBar.Deselect(false);
            _bottomBar.Select(InterfaceElement.Directions.Left, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        public override void OnResize(int newWidth, int newHeight)
        {

        }

        private void UpdateFullscreenState()
        {
            var toggle = (InterfaceToggle)_toggleFullscreen.Elements[1];
            if (toggle.ToggleState != GameSettings.IsFullscreen)
                toggle.SetToggle(GameSettings.IsFullscreen);
        }

        private void UpdateGameScaleSlider()
        {
            // The step starts at 0 and ends at max. Add the amount it goes negative.
            _gameScaleSlider.CurrentStep = GameSettings.GameScale + 3;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, int height, float alpha)
        {
            // Always draw the menu even when not showing tooltips.
            base.Draw(spriteBatch, position, height, alpha);

            // If the user pressed the top most face button, show the tooltip window.
            if (_showTooltip)
            {
                string tooltipText = GetOptionToolip();
                PageTooltip.Draw(spriteBatch, tooltipText);
            }
        }

        private string GetOptionToolip()
        {
            // Detect back button press by checking the index of the main InterfaceListLayout.
            if (_graphicSettingsLayout.SelectionIndex == 2)
                return Game1.LanguageManager.GetString("tooltip_default", "error");

            // Detect the chosen button by checking the content InterfaceListLayout.
            int index = _contentLayout.SelectionIndex;
            string tooltip = "Select an option to view its tooltip.";

            // Use the selected index to determine which tooltip to show.
            switch (index) 
            {
                case 0: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_game_scale", "error"); break; }
                case 1: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_ui_scale", "error"); break; }
                case 2: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_fullscreen", "error"); break; }
                case 3: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_exfullscreen", "error"); break; }
                case 4: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_nogloballights", "error"); break; }
                case 5: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_noobjectlights", "error"); break; }
                case 6: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_shadow", "error"); break; }
                case 7: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_fps_lock", "error"); break; }
                case 8: { tooltip = Game1.LanguageManager.GetString("tooltip_graphics_epilepsysafe", "error"); break; }
            }
            // Display the tooltip in the tooltip window.
            return tooltip;
        }
    }
}
