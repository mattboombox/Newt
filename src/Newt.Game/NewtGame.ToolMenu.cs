using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private bool _toolMenuOpen;
    private bool _toolMenuKeyboardSelection;

    private static readonly Keys[] ToolNavigationKeys = [Keys.Q, Keys.E, Keys.R, Keys.T];
    private Keys _repeatingToolMenuKey = Keys.None;
    private double _toolMenuKeyRepeatSeconds;
    private const double ToolMenuKeyRepeatDelay = 0.35;
    private const double ToolMenuKeyRepeatInterval = 0.10;

    private void ResetToolMenuKeyRepeat()
    {
        _repeatingToolMenuKey = Keys.None;
        _toolMenuKeyRepeatSeconds = 0;
    }

    private bool HandleToolMenuKeyboard(KeyboardState keyboard, double elapsedSeconds)
    {
        var key = Keys.None;
        foreach (var candidate in ToolNavigationKeys)
            if (WasPressed(keyboard, candidate))
            {
                key = candidate;
                break;
            }
        if (key == Keys.None && keyboard.IsKeyDown(_repeatingToolMenuKey))
            key = _repeatingToolMenuKey;
        if (key == Keys.None)
            foreach (var candidate in ToolNavigationKeys)
                if (keyboard.IsKeyDown(candidate))
                {
                    key = candidate;
                    break;
                }
        if (key == Keys.None)
        {
            ResetToolMenuKeyRepeat();
            return false;
        }

        var advance = key != _repeatingToolMenuKey || WasPressed(keyboard, key);
        if (advance)
        {
            _repeatingToolMenuKey = key;
            _toolMenuKeyRepeatSeconds = ToolMenuKeyRepeatDelay;
        }
        else
        {
            _toolMenuKeyRepeatSeconds -= elapsedSeconds;
            if (_toolMenuKeyRepeatSeconds <= 0)
            {
                advance = true;
                // Avoid skipping several entries after a slow frame.
                _toolMenuKeyRepeatSeconds = ToolMenuKeyRepeatInterval;
            }
        }
        if (advance)
        {
            if (key is Keys.Q or Keys.E) CycleTool(key == Keys.Q ? -1 : 1);
            else CycleToolCategory(key == Keys.R ? -1 : 1);
        }
        return true;
    }
    private void SyncToolMenuKeyboardSelection()
    {
        _hudHidden = false;
        _toolMenuOpen = true;
        _menuCategoryIndex = _toolCategoryIndex;
        _toolMenuKeyboardSelection = true;
    }
    private int _menuCategoryIndex = -1;
    private bool _uiMouseCaptured;
    private const int ToolMenuRowHeight = 26;
    private Rectangle _populationUiBounds, _controlsUiBounds;

    private Rectangle ToolCategoryMenuBounds => new(0,
        Math.Max(0, MapViewportHeight + 1 - ToolCategoryOrder.Length * ToolMenuRowHeight),
        Math.Min(220, GraphicsDevice.Viewport.Width / 3), ToolCategoryOrder.Length * ToolMenuRowHeight);

    private int ToolMenuRows => Math.Max(1, MapViewportHeight / ToolMenuRowHeight);

    private Rectangle ToolChoiceMenuBounds
    {
        get
        {
            var category = _menuCategoryIndex < 0 ? _toolCategoryIndex : _menuCategoryIndex;
            var count = GetTools(ToolCategoryOrder[category]).Length;
            var columns = (count + ToolMenuRows - 1) / ToolMenuRows;
            var width = Math.Min(240, (GraphicsDevice.Viewport.Width - ToolCategoryMenuBounds.Width) / columns);
            var height = Math.Min(count, ToolMenuRows) * ToolMenuRowHeight;
            return new Rectangle(ToolCategoryMenuBounds.Right, Math.Max(0, MapViewportHeight + 1 - height), width * columns, height);
        }
    }

    private Rectangle ToolChoiceBounds(int index)
    {
        var bounds = ToolChoiceMenuBounds;
        var count = GetTools(ToolCategoryOrder[_menuCategoryIndex]).Length;
        var columns = (count + ToolMenuRows - 1) / ToolMenuRows;
        var width = bounds.Width / columns;
        return new Rectangle(bounds.X + index / ToolMenuRows * width,
            bounds.Y + index % ToolMenuRows * ToolMenuRowHeight, width, ToolMenuRowHeight);
    }

    private bool HandleToolMenuInput(MouseState mouse)
    {
        if (_hudHidden) _toolMenuOpen = false;
        var clicked = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if ((mouse.Position != _previousMouse.Position && mouse.MiddleButton == ButtonState.Released) || clicked)
            _toolMenuKeyboardSelection = false;
        var down = mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed || mouse.MiddleButton == ButtonState.Pressed;
        var overHud = !_hudHidden && mouse.Y >= MapViewportHeight;
        var wasOpen = _toolMenuOpen;
        if (_populationWindowMode != 0 && _populationUiBounds.Contains(mouse.Position))
        {
            var coveredByControls = _controlsWindowOpen && _controlsUiBounds.Contains(mouse.Position);
            var coveredByToolMenu = _toolMenuOpen && (ToolCategoryMenuBounds.Contains(mouse.Position) ||
                ToolChoiceMenuBounds.Contains(mouse.Position));
            if (!coveredByControls && !coveredByToolMenu)
            {
                if (clicked && mouse.ScrollWheelValue == _previousMouse.ScrollWheelValue)
                {
                    if (_populationWindowMode == 3) HandleVillagePopulationClick(mouse);
                    else HandlePopulationClick(mouse);
                }
                if (_populationWindowMode == 3) ScrollVillagePopulation(mouse);
            }
        }
        if ((_controlsWindowOpen && _controlsUiBounds.Contains(mouse.Position)) ||
            (_populationWindowMode != 0 && _populationUiBounds.Contains(mouse.Position)))
        {
            if (down) _uiMouseCaptured = true;
            else _uiMouseCaptured = false;
            return true;
        }
        if (clicked && overHud && mouse.X < GraphicsDevice.Viewport.Width / 5)
        {
            _toolMenuOpen = !_toolMenuOpen;
            _menuCategoryIndex = _toolCategoryIndex;
        }
        else if (_toolMenuOpen)
        {
            var categories = ToolCategoryMenuBounds;
            if (!_toolMenuKeyboardSelection && mouse.MiddleButton == ButtonState.Released && categories.Contains(mouse.Position))
                _menuCategoryIndex = Math.Clamp((mouse.Y - categories.Y) / ToolMenuRowHeight, 0, ToolCategoryOrder.Length - 1);
            else if (clicked && ToolChoiceMenuBounds.Contains(mouse.Position))
            {
                var choices = GetTools(ToolCategoryOrder[_menuCategoryIndex]);
                for (var i = 0; i < choices.Length; i++)
                    if (ToolChoiceBounds(i).Contains(mouse.Position))
                    {
                        ResetRoadToolSelection();
                        ResetToolRepeat();
                        _toolCategoryIndex = _menuCategoryIndex;
                        _toolIndex = i;
                        _toolMenuOpen = false;
                        break;
                    }
            }
            else if (clicked) _toolMenuOpen = false;
        }
        var overUi = overHud || wasOpen || _toolMenuOpen || IsPointerOverWarCharts(mouse) ||
            (_populationWindowMode != 0 && _populationUiBounds.Contains(mouse.Position)) ||
            (_controlsWindowOpen && _controlsUiBounds.Contains(mouse.Position));
        if (overUi && down) _uiMouseCaptured = true;
        var blocked = overUi || _uiMouseCaptured;
        if (!down) _uiMouseCaptured = false;
        if (IsPointerOverWarCharts(mouse))
        {
            if (clicked && !wasOpen && !_toolMenuOpen && mouse.ScrollWheelValue == _previousMouse.ScrollWheelValue)
                HandleWarChartClick(mouse);
            ScrollWarCharts(mouse);
        }
        return blocked;
    }

    private void DrawToolMenu()
    {
        var mouse = Mouse.GetState();
        var categories = ToolCategoryMenuBounds;
        for (var i = 0; i < ToolCategoryOrder.Length; i++)
            DrawToolMenuRow(new Rectangle(categories.X, categories.Y + i * ToolMenuRowHeight, categories.Width, ToolMenuRowHeight),
                GetToolCategoryName(ToolCategoryOrder[i]) + " >", i == _menuCategoryIndex);
        if (_menuCategoryIndex < 0) return;
        var category = ToolCategoryOrder[_menuCategoryIndex];
        var tools = GetTools(category);
        for (var i = 0; i < tools.Length; i++)
        {
            var row = ToolChoiceBounds(i);
            DrawToolMenuRow(row, GetToolName(tools[i]), (!_toolMenuKeyboardSelection && row.Contains(mouse.Position)) ||
                (_toolCategoryIndex == _menuCategoryIndex && _toolIndex == i), category == ToolCategory.CritterTools ? GetCritterSpecies(tools[i]) : null,
                category == ToolCategory.BuildingTools ? tools[i] : null);
        }
    }

    private void DrawToolMenuRow(Rectangle bounds, string text, bool highlighted, CritterSpecies? species = null, WorldTool? building = null)
    {
        _spriteBatch!.Draw(_pixel!, bounds, highlighted ? new Color(48, 83, 102) : MenuBackgroundColor);
        _spriteBatch.Draw(_pixel!, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(76, 91, 102));
        var inset = species.HasValue || building.HasValue ? 30 : 10;
        if (species is { } critter) DrawCritterIcon(critter, new Rectangle(bounds.X + 5, bounds.Y + 3, 20, 20));
        if (building is { } tool) DrawBuildingMenuIcon(tool, new Rectangle(bounds.X + 5, bounds.Y + 3, 20, 20));
        var scale = Math.Min(1f, (bounds.Width - inset - 6) / Math.Max(1f, _hudFont!.MeasureString(text).X));
        _spriteBatch.DrawString(_hudFont, text, new Vector2(bounds.X + inset, bounds.Y + 5), Color.White,
            0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    private void DrawBuildingMenuIcon(WorldTool tool, Rectangle bounds)
    {
        var size = Math.Min(bounds.Width, bounds.Height);
        var x = bounds.X;
        var y = bounds.Y;
        if (tool == WorldTool.WolfDen)
        {
            DrawWolfDenSprite(x, y, size, 1);
            return;
        }
        if (tool == WorldTool.Teleporter)
        {
            DrawTeleporterSprite(x, y, size);
            return;
        }
        if (tool == WorldTool.Road)
        {
            _spriteBatch!.Draw(_pixel!, new Rectangle(x, y + size / 3, size, size / 3), new Color(131, 109, 78));
            _spriteBatch.Draw(_pixel!, new Rectangle(x, y + size / 2, size, Math.Max(1, size / 10)), new Color(174, 150, 108));
            return;
        }
        var structure = tool switch
        {
            WorldTool.ApeVillage or WorldTool.BarbarianApeVillage => ApeStructureKind.Village,
            WorldTool.Farm => ApeStructureKind.Farm,
            WorldTool.LumberCamp => ApeStructureKind.LumberCamp,
            WorldTool.Harbor => ApeStructureKind.NavalDistrict,
            WorldTool.ResidentialDistrict => ApeStructureKind.ResidentialDistrict,
            WorldTool.MilitaryDistrict => ApeStructureKind.MilitaryDistrict,
            WorldTool.Library => ApeStructureKind.Library,
            WorldTool.Market => ApeStructureKind.Market,
            _ => throw new ArgumentOutOfRangeException(nameof(tool)),
        };
        DrawApeStructureSprite(structure, x, y, size, tool == WorldTool.BarbarianApeVillage);
    }
    private void DrawCritterIcon(CritterSpecies species, Rectangle bounds, Texture2D? icon = null)
    {
        var sprite = icon ?? (species == CritterSpecies.Sandworm ? _sandwormHead : _critterSprites.GetValueOrDefault(species));
        if (sprite is null) return;
        var scale = Math.Min((float)bounds.Width / sprite.Width, (float)bounds.Height / sprite.Height);
        var width = Math.Max(1, (int)(sprite.Width * scale));
        var height = Math.Max(1, (int)(sprite.Height * scale));
        _spriteBatch!.Draw(sprite, new Rectangle(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height), Color.White);
    }
}
