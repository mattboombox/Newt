using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private sealed class WarChartHistory
    {
        public readonly List<int> Attacking = [];
        public readonly List<int> Defending = [];
        public long NextSampleTick;
    }

    private const int WarChartHeight = 104;
    private const int WarChartGap = 8;
    private readonly Dictionary<long, WarChartHistory> _warCharts = [];
    private IReadOnlyList<ActiveApeWar> _activeWars = [];
    private int _warChartScroll;
    private readonly List<(Rectangle Bounds, long WarId, bool Attacking)> _warChartLinks = [];

    private void HandleWarChartClick(MouseState mouse)
    {
        foreach (var link in _warChartLinks)
            if (link.Bounds.Contains(mouse.Position))
            {
                var war = _activeWars.FirstOrDefault(war => war.Id == link.WarId);
                var destination = link.Attacking ? war?.AttackerPosition : war?.DefenderPosition;
                if (destination is { } tile) TeleportCameraToTile(tile);
                break;
            }
    }

    private int VisibleWarChartCount => Math.Max(1, (MapViewportHeight - 42) / (WarChartHeight + WarChartGap));
    private int WarChartWidth => Math.Max(1, Math.Min(380, GraphicsDevice.Viewport.Width - 24));
    private int WarChartX => GraphicsDevice.Viewport.Width - WarChartWidth - 12;

    private bool IsPointerOverWarCharts(MouseState mouse) => _activeWars.Count > 0 &&
        new Rectangle(WarChartX, 12, WarChartWidth,
            Math.Min(_activeWars.Count, VisibleWarChartCount) * (WarChartHeight + WarChartGap) + 18)
            .Contains(mouse.X, mouse.Y);

    private void ScrollWarCharts(MouseState mouse)
    {
        var steps = (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120;
        _warChartScroll = Math.Clamp(_warChartScroll - steps, 0,
            Math.Max(0, _activeWars.Count - VisibleWarChartCount));
    }

    private void UpdateWarCharts()
    {
        _activeWars = _world.GetActiveApeWars();
        var activeIds = _activeWars.Select(war => war.Id).ToHashSet();
        foreach (var id in _warCharts.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            _warCharts.Remove(id);
        foreach (var war in _activeWars)
        {
            if (!_warCharts.TryGetValue(war.Id, out var history))
            {
                history = new WarChartHistory { NextSampleTick = _world.Tick + PopulationSampleIntervalTicks };
                history.Attacking.Add(war.Armies.Attacking.StartingFighters);
                history.Defending.Add(war.Armies.Defending.StartingFighters);
                _warCharts.Add(war.Id, history);
            }
            if (_world.Tick < history.NextSampleTick) continue;
            AppendPopulationSample(history.Attacking, war.Armies.Attacking.Fighters);
            AppendPopulationSample(history.Defending, war.Armies.Defending.Fighters);
            history.NextSampleTick = _world.Tick + PopulationSampleIntervalTicks;
        }
        _warChartScroll = Math.Clamp(_warChartScroll, 0, Math.Max(0, _activeWars.Count - VisibleWarChartCount));
    }

    private static Color WarArmyColor(ApeWarArmyStatus army, bool attacking) =>
        army.Undead ? new Color(113, 136, 81) : army.Culture switch
        {
            ApeCulture.Monarchy => new Color(130, 90, 200),
            ApeCulture.Luddite => new Color(65, 145, 75),
            _ => attacking ? new Color(195, 65, 65) : new Color(65, 110, 200),
        };

    private void DrawWarCharts()
    {
        _warChartLinks.Clear();
        if (_spriteBatch is null || _pixel is null || _hudFont is null) return;
        var shown = Math.Min(_activeWars.Count - _warChartScroll, VisibleWarChartCount);
        for (var row = 0; row < shown; row++)
        {
            var war = _activeWars[row + _warChartScroll];
            var history = _warCharts[war.Id];
            var bounds = new Rectangle(WarChartX, 12 + row * (WarChartHeight + WarChartGap), WarChartWidth, WarChartHeight);
            _spriteBatch.Draw(_pixel, bounds, new Color(76, 91, 102));
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4),
                MenuBackgroundColor);
            _spriteBatch.DrawString(_hudFont, $"{war.Attacker} / {war.Defender} / {war.WarType}",
                new Vector2(bounds.X + 12, bounds.Y + 8), new Color(126, 190, 213));
            var attacking = war.Armies.Attacking;
            var defending = war.Armies.Defending;
            var red = WarArmyColor(attacking, true);
            var blue = WarArmyColor(defending, false);
            DrawLegend(attacking, "Attack", red, bounds.X + 12, true);
            DrawLegend(defending, "Defend", blue, bounds.X + bounds.Width / 2, false);

            var graph = new Rectangle(bounds.X + 12, bounds.Y + 46, bounds.Width - 24, 46);
            var maximum = Math.Max(1, new[] { attacking.StartingFighters, defending.StartingFighters,
                attacking.Fighters, defending.Fighters, history.Attacking.Max(), history.Defending.Max() }.Max());
            _spriteBatch.Draw(_pixel, graph, new Color(31, 39, 45));
            _spriteBatch.Draw(_pixel, new Rectangle(graph.X, graph.Bottom - 1, graph.Width, 1), new Color(65, 76, 84));
            if (attacking.StopAt == defending.StopAt)
                DrawMinimum(attacking.StopAt, new Color(190, 198, 204));
            else
            {
                DrawMinimum(attacking.StopAt, red);
                DrawMinimum(defending.StopAt, blue);
            }
            DrawSeries(history.Attacking, attacking.Fighters, red);
            DrawSeries(history.Defending, defending.Fighters, blue);

            void DrawLegend(ApeWarArmyStatus army, string name, Color color, int x, bool attackingSide)
            {
                var text = $"{name} {army.Fighters:N0}{(army.PopulationTarget ? " pop" : "")}";
                var textSize = _hudFont.MeasureString(text);
                var linkBounds = Rectangle.Intersect(bounds,
                    new Rectangle(x, bounds.Y + 25, 16 + (int)MathF.Ceiling(textSize.X),
                        Math.Max(16, (int)MathF.Ceiling(textSize.Y))));
                _warChartLinks.Add((linkBounds, war.Id, attackingSide));
                _spriteBatch.Draw(_pixel, new Rectangle(x, bounds.Y + 29, 10, 10), color);
                _spriteBatch.DrawString(_hudFont, text,
                    new Vector2(x + 16, bounds.Y + 26),
                    linkBounds.Contains(Mouse.GetState().Position) ? Color.White : new Color(220, 225, 228));
            }

            int GraphY(int value) => graph.Bottom - 2 - (int)MathF.Round(value / (float)maximum * (graph.Height - 3));

            void DrawMinimum(int value, Color color)
            {
                var y = GraphY(value);
                for (var x = graph.X; x < graph.Right; x += 7)
                    _spriteBatch.Draw(_pixel, new Rectangle(x, y, Math.Min(4, graph.Right - x), 1), color);
            }

            void DrawSeries(IReadOnlyList<int> samples, int current, Color color)
            {
                var previous = new Point(graph.X, GraphY(samples[0]));
                for (var i = 1; i <= samples.Count; i++)
                {
                    var value = i == samples.Count ? current : samples[i];
                    var next = new Point(graph.X + i * (graph.Width - 1) / samples.Count, GraphY(value));
                    DrawPixelLine(previous, next, color);
                    previous = next;
                }
            }
        }
        if (_activeWars.Count > VisibleWarChartCount)
            _spriteBatch.DrawString(_hudFont, $"Wars {_warChartScroll + 1}-{_warChartScroll + shown}/{_activeWars.Count}  Scroll for more",
                new Vector2(WarChartX + 12, 12 + shown * (WarChartHeight + WarChartGap)), new Color(220, 225, 228));
    }
}
