using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private void DrawBuildingInspection(int x, int y, GridPosition? pointerPosition)
    {
        if (pointerPosition is not { } pointer)
        {
            DrawHudLines(x, y, "BUILDING", "None");
            return;
        }
        var position = GetBuildingInspectionPosition(pointer);
        var lines = GetBuildingInspectionLines(position);
        if (lines[1] == "None")
        {
            DrawHudLines(x, y, lines);
            return;
        }
        DrawBuildingInspectionIcon(position, new Rectangle(x, y + 17, 16, 16));
        DrawHudLines(x, y, lines[0]);
        for (var i = 1; i < lines.Length; i++)
            _spriteBatch!.DrawString(_hudFont!, lines[i],
                new Vector2(x + (i == 1 ? 22 : 0), y + i * 17), new Color(220, 225, 228));
    }

    private void DrawBuildingInspectionIcon(GridPosition position, Rectangle bounds)
    {
        var x = bounds.X;
        var y = bounds.Y;
        var size = Math.Min(bounds.Width, bounds.Height);
        if (_world.GetWhaleFallNutrition(position) > 0)
            DrawRemainsInspectionIcon(_whaleFallSprite, bounds);
        else if (_world.GetShipwreck(position) is { } wreck)
            DrawRemainsInspectionIcon(wreck is ShipwreckKind.Pirate ? _pirateShipwreckSprite : _shipwreckSprite, bounds);
        else if (_world.HasNecropolis(position))
            DrawNecropolisSprite(x, y, size);
        else if (_world.IsCustodianFabricator(position))
        {
            var edge = Math.Max(1, size / 4);
            _spriteBatch!.Draw(_pixel!, bounds, new Color(65, 82, 94));
            _spriteBatch.Draw(_pixel!, new Rectangle(x + edge, y + edge, size - edge * 2, size - edge * 2), new Color(170, 200, 210));
            _spriteBatch.Draw(_pixel!, new Rectangle(x + edge, y + size - edge, size - edge * 2, edge), new Color(100, 245, 240));
        }
        else if (_world.HasVampireLair(position))
        {
            _spriteBatch!.Draw(_pixel!, new Rectangle(x, y + size / 3, size, size * 2 / 3), new Color(150, 150, 150));
            _spriteBatch.Draw(_pixel!, new Rectangle(x + size / 4, y, size / 2, size / 2), new Color(85, 85, 85));
        }
        else if (_world.GetVolcanoState(position) is { } state)
        {
            _spriteBatch!.Draw(_pixel!, new Rectangle(x + size / 4, y + size / 4, size / 2, size / 2),
                state == VolcanoState.Active ? new Color(255, 208, 45) : new Color(92, 70, 66));
        }
        else if (_world.GetWolfDenCharges(position) is { } charges)
            DrawWolfDenSprite(x, y, size, charges);
        else if (_world.GetMegaSpiderWebFood(position) is { } food)
        {
            var center = new Point(x + size / 2, y + size / 2);
            var silk = food > 0 ? new Color(245, 225, 175) : new Color(220, 220, 210);
            DrawPixelLine(new Point(x + 1, y + 1), new Point(bounds.Right - 2, bounds.Bottom - 2), silk);
            DrawPixelLine(new Point(x + 1, bounds.Bottom - 2), new Point(bounds.Right - 2, y + 1), silk);
            DrawPixelLine(new Point(x + 1, center.Y), new Point(bounds.Right - 2, center.Y), silk);
            DrawPixelLine(new Point(center.X, y + 1), new Point(center.X, bounds.Bottom - 2), silk);
            if (food > 0)
                _spriteBatch!.Draw(_pixel!, new Rectangle(center.X - 1, center.Y - 1, 3, 3), new Color(170, 85, 65));
        }
        else if (_world.HasTeleporter(position))
            DrawTeleporterSprite(x, y, size);
        else if (_world.GetApeStructure(position) is { } structure)
            DrawApeStructureSprite(structure, x, y, size, _world.IsBarbarianVillage(position),
                _world.GetBiome(position) == Biome.Arid);
    }

    private void DrawRemainsInspectionIcon(Texture2D? sprite, Rectangle bounds)
    {
        if (sprite is null || _spriteBatch is null) return;
        var scale = Math.Min((float)bounds.Width / sprite.Width, (float)bounds.Height / sprite.Height);
        var width = Math.Max(1, (int)(sprite.Width * scale));
        var height = Math.Max(1, (int)(sprite.Height * scale));
        _spriteBatch.Draw(sprite, new Rectangle(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height), Color.White);
    }
}
