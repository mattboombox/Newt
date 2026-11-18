using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private Texture2D? _sandwormHead;
    private Texture2D? _sandwormBody;
    private Texture2D? _sandwormTail;

    private void LoadSandwormSprites()
    {
        _sandwormHead = LoadCritterTexture("sand-worm-head.png");
        _sandwormBody = LoadCritterTexture("sand-worm-body.png");
        _sandwormTail = LoadCritterTexture("sand-worm-tail.png");
    }

    private void DrawSandwormSegment(int screenX, int screenY, GridPosition tile)
    {
        if (_spriteBatch is null || !_world.TryGetSandwormSegment(tile, out var segment))
            return;

        var sprite = segment.Index == 0 ? _sandwormHead
            : segment.Index == segment.Length - 1 ? _sandwormTail : _sandwormBody;
        if (sprite is null) return;

        var head = SandwormDirection(tile, segment.TowardHead);
        var tail = SandwormDirection(tile, segment.TowardTail);
        var center = new Vector2(MapOffsetX + (screenX + 0.5f) * TileSize, MapOffsetY + (screenY + 0.5f) * TileSize);
        var origin = new Vector2(sprite.Width / 2f, sprite.Height / 2f);
        var scale = new Vector2((float)TileSize / sprite.Width, (float)TileSize / sprite.Height);

        if (head != Vector2.Zero && tail != Vector2.Zero && head != -tail)
        {
            // Use the upper half of the straight body for each leg of the turn.
            var half = new Rectangle(0, 0, sprite.Width, (sprite.Height + 1) / 2);
            Draw(head, half);
            Draw(tail, half);
        }
        else
        {
            Draw(segment.Index == 0 ? -tail : head, null);
        }

        void Draw(Vector2 direction, Rectangle? source) =>
            _spriteBatch.Draw(sprite, center, source, Color.White, MathF.Atan2(direction.X, -direction.Y),
                origin, scale, SpriteEffects.None, 0f);
    }

    private Vector2 SandwormDirection(GridPosition tile, GridPosition? neighbor)
    {
        if (neighbor is null) return Vector2.Zero;
        foreach (var (_, dx, dy) in ThreeDGeometry.ConnectionDirections)
        {
            if (dx != 0 && dy != 0) continue;
            var adjacent = _world.Body is WorldBody.Nexus ? new GridPosition(tile.X + dx, tile.Y + dy)
                : RiverNeighbor(tile, dx, dy);
            if (adjacent == neighbor)
                return new Vector2(dx, dy);
        }
        return Vector2.Zero;
    }
}
