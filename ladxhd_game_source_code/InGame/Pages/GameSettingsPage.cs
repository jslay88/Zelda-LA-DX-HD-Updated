using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Interface;
using ProjectZ.InGame.Things;

namespace ProjectZ.InGame.Pages
{
    class GameSettingsPage : InterfacePage
    {
        private readonly InterfaceListLayout _gameSettingsList;
        private readonly InterfaceListLayout _contentLayout;
        private readonly InterfaceListLayout _bottomBar;
        private readonly InterfaceButton _controllerType;
        private readonly InterfaceSlider _subLangSlider;
        private readonly ContentManager _content;
        private bool _showTooltip;

        public GameSettingsPage(int width, int height, ContentManager content)
        {
            EnableTooltips = true;
            _content = content;

            // Game Settings Layout
            _gameSettingsList = new InterfaceListLayout { Size = new Point(width, height - 12), Selectable = true };

            var buttonWidth = 320;
            var buttonHeight = 14;

            _gameSettingsList.AddElement(new InterfaceLabel(Resources.GameHeaderFont, "settings_game_header",
                new Point(buttonWidth, (int)(height * Values.MenuHeaderSize)), new Point(0, 0)));

            _contentLayout = new InterfaceListLayout { Size = new Point(width, (int)(height * Values.MenuContentSize) - 12), Selectable = true, ContentAlignment = InterfaceElement.Gravities.Top };

            // Button: Language
            _contentLayout.AddElement(new InterfaceButton(new Point(buttonWidth, buttonHeight), new Point(0, 2), "settings_game_language", PressButtonLanguageChange));

            // Slider: Sub-Language
            _subLangSlider = new InterfaceSlider(Resources.GameFont, "settings_game_sublanguage",
                buttonWidth, 11, new Point(1, 2), 0, 2, 1, Game1.LanguageManager.CurrentSubLanguageIndex,
                number => { Game1.LanguageManager.CurrentSubLanguageIndex = number; })
                { SetString = number => LangSliderAdjustment(number) };
            _contentLayout.AddElement(_subLangSlider);

            // Menu Border Slider:
            var menuBricksSlider = new InterfaceSlider(Resources.GameFont, "settings_redux_menubricks",
                buttonWidth, 11, new Point(1, 2), 0, 2, 1, GameSettings.MenuBorder,
                number => { GameSettings.MenuBorder = number; })
                { SetString = number => MenuBorderScaleSliderAdjustment(number) };
            _contentLayout.AddElement(menuBricksSlider);

            // Button: Classic Sword
            var toggleClassicSword = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_classicsword", GameSettings.ClassicSword, 
                newState => { GameSettings.ClassicSword = newState; });
            _contentLayout.AddElement(toggleClassicSword);

            // Button: Save Position
            var toggleSavePosition = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_saveposition", GameSettings.StoreSavePos, 
                newState => { GameSettings.StoreSavePos = newState; });
            _contentLayout.AddElement(toggleSavePosition);

            // Button: AutoSave
            var toggleAutosave = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_autosave", GameSettings.Autosave, 
                newState => { GameSettings.Autosave = newState; });
            _contentLayout.AddElement(toggleAutosave);

            // Button: Items on Right
            var toggleItemSlotSide = InterfaceToggle.GetToggleButton(new Point(buttonWidth, buttonHeight), new Point(5, 2),
                "settings_game_items_on_right", GameSettings.ItemsOnRight, 
                newState => { GameSettings.ItemsOnRight = newState; });
            _contentLayout.AddElement(toggleItemSlotSide);

            // Bottom Bar / Back Button:
            _bottomBar = new InterfaceListLayout() { Size = new Point(width, (int)(height * Values.MenuFooterSize)), Selectable = true, HorizontalMode = true };
            _bottomBar.AddElement(new InterfaceButton(new Point(100, 18), new Point(2, 4), "settings_menu_back", element => { Game1.UiPageManager.PopPage(); }));
            _gameSettingsList.AddElement(_contentLayout);
            _gameSettingsList.AddElement(_bottomBar);
            PageLayout = _gameSettingsList;
        }

        public override void Update(CButtons pressedButtons, GameTime gameTime)
        {
            base.Update(pressedButtons, gameTime);

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

        public override void OnLoad(Dictionary<string, object> intent)
        {
            // the left button is always the first one selected
            _bottomBar.Deselect(false);
            _bottomBar.Select(InterfaceElement.Directions.Left, false);
            _bottomBar.Deselect(false);

            PageLayout.Deselect(false);
            PageLayout.Select(InterfaceElement.Directions.Top, false);
        }

        private string LangSliderAdjustment(int number)
        {
            string langKey = number switch
            {
                1 => "settings_game_sublanguage_02",
                2 => "settings_game_sublanguage_03",
                _ => "settings_game_sublanguage_01"
            };
            return " " + Game1.LanguageManager.GetString(langKey, "error");
        }

        public void PressButtonLanguageChange(InterfaceElement element)
        {
            // Update the selected language.
            Game1.LanguageManager.ToggleLanguage();

            // Refresh any textures that need refreshed.
            Resources.RefreshDynamicResources();
        }

        private string MenuBorderScaleSliderAdjustment(int number)
        {
            // Swap out the menu border with it's replacement.
            Resources.RefreshMenuBorderTexture(_content, number);

            return ": " + number switch
            {
                0 => Game1.LanguageManager.GetString("settings_redux_menubricksA", "error"),
                1 => Game1.LanguageManager.GetString("settings_redux_menubricksB", "error"),
                2 => Game1.LanguageManager.GetString("settings_redux_menubricksC", "error")
            };
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
            if (_gameSettingsList.SelectionIndex == 2)
                return  Game1.LanguageManager.GetString("tooltip_default", "error");

            // Detect the chosen button by checking the content InterfaceListLayout.
            int index = _contentLayout.SelectionIndex;
            string tooltip = "Select an option to view its tooltip.";

            // Use the selected index to determine which tooltip to show.
            switch (index) 
            {
                case 0: { tooltip = Game1.LanguageManager.GetString("tooltip_game_language", "error"); break; }
                case 1: { tooltip = Game1.LanguageManager.GetString("tooltip_game_sublanguage", "error"); break; }
                case 2: { tooltip = Game1.LanguageManager.GetString("tooltip_game_menubricks", "error"); break; }
                case 3: { tooltip = Game1.LanguageManager.GetString("tooltip_game_classicsword", "error"); break; }
                case 4: { tooltip = Game1.LanguageManager.GetString("tooltip_game_saveposition", "error"); break; }
                case 5: { tooltip = Game1.LanguageManager.GetString("tooltip_game_autosave", "error"); break; }
                case 6: { tooltip = Game1.LanguageManager.GetString("tooltip_game_itemsonright", "error"); break; }
            }
            // Display the tooltip in the tooltip window.
            return tooltip;
        }
    }
}
