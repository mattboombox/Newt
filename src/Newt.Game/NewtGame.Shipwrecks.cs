using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private Texture2D? _shipwreckSprite, _pirateShipwreckSprite;

    private float ShipwreckSinkProgress(GridPosition tile) =>
        (float)_world.GetShipwreckSinkProgress(tile, _accumulator.TotalSeconds / SimulationStep.TotalSeconds);

    private void LoadShipwreckSprites()
    {
        _shipwreckSprite = Load("ship-wreck.png");
        _pirateShipwreckSprite = Load("ship-wreck-pirate.png");
        Texture2D? Load(string filename)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "Critters", filename);
            if (!File.Exists(path)) return null;
            using var stream = File.OpenRead(path);
            return Texture2D.FromStream(GraphicsDevice, stream);
        }
    }

    private void DrawShipwreck(int screenX, int screenY, GridPosition tile)
    {
        var kind = _world.GetShipwreck(tile);
        var sprite = kind is ShipwreckKind.Pirate ? _pirateShipwreckSprite : _shipwreckSprite;
        if (kind is null || sprite is null) return;
        DrawRemainsSprite(sprite, screenX, screenY, tile, shipwreck: true);
    }

    private void DrawRemainsSprite(Texture2D sprite, int screenX, int screenY, GridPosition tile, bool shipwreck)
    {
        var center = new Vector2(MapOffsetX + (screenX + 0.5f) * TileSize,
            MapOffsetY + (screenY + 0.5f) * TileSize);
        if (shipwreck && !_view3D)
            center.Y -= (1 - ShipwreckSinkProgress(tile)) * TileSize * 0.35f;
        var rotation = RemainsOrientation.QuarterTurns(_world.Seed, tile, shipwreck) * MathHelper.PiOver2;
        _spriteBatch!.Draw(sprite, center, null, Color.White, rotation,
            new Vector2(sprite.Width, sprite.Height) * 0.5f,
            new Vector2(TileSize / (float)sprite.Width, TileSize / (float)sprite.Height), SpriteEffects.None, 0);
    }
}
