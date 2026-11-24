using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

public sealed partial class NewtGame
{
    private readonly Dictionary<(int Village, CritterSpecies? Species), List<int>> _villagePopulationHistory = [];
    private readonly List<(Rectangle Bounds, int VillageId, GridPosition Position, CritterSpecies? Species)> _villagePopulationLinks = [];
    private readonly List<(Rectangle Bounds, CritterSpecies Species, PopulationCounts.Selection Selection)> _populationLinks = [];
    private CritterId _lastPopulationTarget;
    private int _villagePopulationScroll;
    private int _villagePopulationMaximumScroll;
    private const int VillagePopulationRowHeight = 24;

    private void RecordVillagePopulationSamples()
    {
        var villages = _world.GetVillagePopulations();
        var active = villages.Select(village => village.Id).ToHashSet();
        foreach (var key in _villagePopulationHistory.Keys.Where(key => !active.Contains(key.Village)).ToArray())
            _villagePopulationHistory.Remove(key);
        foreach (var village in villages)
        {
            Sample((village.Id, null), village.ApeCount);
            var rows = PopulationCounts.VillageRows(village);
            foreach (var species in Enum.GetValues<CritterSpecies>())
                if (rows.ContainsKey(species) || _villagePopulationHistory.ContainsKey((village.Id, species)))
                    Sample((village.Id, species), rows.GetValueOrDefault(species));
        }

        void Sample((int Village, CritterSpecies? Species) key, int value)
        {
            if (!_villagePopulationHistory.TryGetValue(key, out var history))
                _villagePopulationHistory[key] = history = [];
            AppendPopulationSample(history, value);
        }
    }

    private void HandleVillagePopulationClick(MouseState mouse)
    {
        foreach (var link in _villagePopulationLinks)
            if (link.Bounds.Contains(mouse.Position) && _world.GetApeVillageId(link.Position) == link.VillageId)
            {
                var target = link.Species is { } species
                    ? PopulationCounts.FindCritter(_world, species, village: link.Position, previous: _lastPopulationTarget) : null;
                if (target is { } resident) _lastPopulationTarget = resident.Id;
                TeleportCameraToTile(target?.Position ?? link.Position);
                break;
            }
    }

    private void HandlePopulationClick(MouseState mouse)
    {
        foreach (var link in _populationLinks)
            if (link.Bounds.Contains(mouse.Position))
            {
                if (PopulationCounts.FindCritter(_world, link.Species, link.Selection, previous: _lastPopulationTarget) is { } target)
                {
                    _lastPopulationTarget = target.Id;
                    TeleportCameraToTile(target.Position);
                }
                break;
            }
    }
    private void ScrollVillagePopulation(MouseState mouse)
    {
        var steps = (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120;
        _villagePopulationScroll = Math.Clamp(_villagePopulationScroll - steps * 3, 0, _villagePopulationMaximumScroll);
    }

    private void DrawVillagePopulationWindow()
    {
        _villagePopulationLinks.Clear();
        var villages = _world.GetVillagePopulations();
        var rows = new List<(VillagePopulationSnapshot Village, CritterSpecies? Species, int Count)>();
        foreach (var village in villages)
        {
            rows.Add((village, null, village.ApeCount));
            foreach (var entry in PopulationCounts.VillageRows(village)
                .OrderBy(entry => GetCritterDisplayName(entry.Key), StringComparer.OrdinalIgnoreCase))
                rows.Add((village, entry.Key, entry.Value));
        }
        const int padding = 16;
        const int headerHeight = 78;
        const int footerHeight = 30;
        var maximumRows = Math.Max(1, (MapViewportHeight - 24 - headerHeight - footerHeight) / VillagePopulationRowHeight);
        var visibleRows = Math.Min(maximumRows, Math.Max(1, rows.Count));
        _villagePopulationMaximumScroll = Math.Max(0, rows.Count - visibleRows);
        _villagePopulationScroll = Math.Clamp(_villagePopulationScroll, 0, _villagePopulationMaximumScroll);
        var bounds = new Rectangle(12, 12, Math.Min(GraphicsDevice.Viewport.Width - 24, 760),
            headerHeight + visibleRows * VillagePopulationRowHeight + footerHeight);
        var mouse = Mouse.GetState();
        var overOverlay = bounds.Contains(mouse.Position) ||
            (_controlsWindowOpen && _controlsUiBounds.Contains(mouse.Position)) || IsPointerOverWarCharts(mouse) ||
            (_toolMenuOpen && (ToolCategoryMenuBounds.Contains(mouse.Position) || ToolChoiceMenuBounds.Contains(mouse.Position)));
        var hoveredTile = overOverlay ? null : ScreenToWorld(mouse.X, mouse.Y);
        var hoveredVillageId = hoveredTile is { } tile ? _world.GetApeVillageId(tile) : null;
        if (hoveredVillageId is { } villageId)
        {
            var heading = rows.FindIndex(row => row.Species is null && row.Village.Id == villageId);
            if (heading >= 0 && (heading < _villagePopulationScroll || heading >= _villagePopulationScroll + visibleRows))
                _villagePopulationScroll = Math.Clamp(heading, 0, _villagePopulationMaximumScroll);
        }
        _populationUiBounds = bounds;
        _spriteBatch!.Draw(_pixel!, bounds, new Color(76, 91, 102));
        _spriteBatch.Draw(_pixel!, new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4), MenuBackgroundColor);
        var textColor = new Color(220, 225, 228);
        var titleColor = new Color(126, 190, 213);
        _spriteBatch.DrawString(_hudFont!, "POPULATION BY VILLAGE", new Vector2(bounds.X + padding, bounds.Y + 11), titleColor);
        _spriteBatch.DrawString(_hudFont!, $"{villages.Count} settlements   {villages.Sum(village => village.ApeCount):N0} resident apes",
            new Vector2(bounds.X + padding, bounds.Y + 31), textColor);
        if (rows.Count == 0)
            _spriteBatch.DrawString(_hudFont!, "No villages exist.", new Vector2(bounds.X + padding, bounds.Y + headerHeight), textColor);
        for (var i = 0; i < visibleRows && i + _villagePopulationScroll < rows.Count; i++)
        {
            var row = rows[i + _villagePopulationScroll];
            var x = bounds.X + padding;
            var y = bounds.Y + headerHeight + i * VillagePopulationRowHeight;
            var graph = new Rectangle(bounds.Right - padding - 96, y + 3, 96, 16);
            var header = row.Species is null;
            var linkBounds = new Rectangle(x, y, bounds.Width - padding * 2, VillagePopulationRowHeight);
            _villagePopulationLinks.Add((linkBounds, row.Village.Id, row.Village.Position, row.Species));
            if (!header && linkBounds.Contains(mouse.Position))
                _spriteBatch.Draw(_pixel!, linkBounds, new Color(48, 83, 102));
            string label;
            var color = titleColor;
            if (header)
            {
                var hovered = linkBounds.Contains(mouse.Position) || row.Village.Id == hoveredVillageId;
                _spriteBatch.Draw(_pixel!, linkBounds, hovered ? new Color(65, 105, 126) : new Color(48, 83, 102));
                DrawBuildingMenuIcon(row.Village.Barbarian ? WorldTool.BarbarianApeVillage : WorldTool.ApeVillage,
                    new Rectangle(x + 3, y + 3, 18, 18));
                label = $"{(row.Village.Barbarian ? "Barbarian Camp" : "Village")} #{row.Village.Id}  {row.Village.Position}  {row.Village.Culture}";
            }
            else
            {
                var species = row.Species!.Value;
                Texture2D? icon = null;
                label = GetCritterDisplayName(species);
                if (row.Village.Barbarian)
                    (label, icon) = species switch
                    {
                        CritterSpecies.Dog => ("Dog", _barbarianDogSprite),
                        CritterSpecies.ApeChieftain => ("Ape Barbarian Chief", _apeBarbarianChiefSprite),
                        CritterSpecies.ApeSailor => ("Ape Barbarian", _apeBarbarianSprite),
                        CritterSpecies.Ape or CritterSpecies.ApeWarrior => ("Ape Barbarian", _apeBarbarianSprite),
                        _ => (label, null),
                    };
                DrawCritterIcon(species, new Rectangle(x + 15, y + 3, 18, 18), icon);
                color = GetCritterColor(species);
            }
            var count = header ? $"{row.Count:N0}/{row.Village.Capacity:N0}" : row.Count.ToString("N0");
            var countWidth = _hudFont!.MeasureString(count).X;
            var countX = graph.X - countWidth - 12;
            var labelX = x + (header ? 27 : 40);
            var scale = Math.Min(1f, Math.Max(1, countX - labelX - 10) / Math.Max(1f, _hudFont.MeasureString(label).X));
            _spriteBatch.DrawString(_hudFont, label, new Vector2(labelX, y + 4), header ? Color.White : textColor,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
            _spriteBatch.DrawString(_hudFont, count, new Vector2(countX, y + 4), new Color(245, 210, 120));
            var history = _villagePopulationHistory.GetValueOrDefault((row.Village.Id, row.Species));
            DrawPopulationSparkline(history ?? [], color, row.Count, graph);
        }
        _spriteBatch.DrawString(_hudFont!, _villagePopulationMaximumScroll > 0
                ? $"Wheel to scroll ({_villagePopulationScroll + 1}-{Math.Min(rows.Count, _villagePopulationScroll + visibleRows)} / {rows.Count})"
                : "",
            new Vector2(bounds.X + padding, bounds.Bottom - 23), new Color(175, 184, 190));
    }
}
