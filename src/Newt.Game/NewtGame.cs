using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

/// <summary>MonoGame host responsible only for input, timing, and presentation.</summary>
public sealed class NewtGame : Microsoft.Xna.Framework.Game
{
    private const int HudHeight = 156;
    private const int HudPadding = 12;
    private const double ToolRepeatDelaySeconds = 0.25;
    private const double ToolRepeatIntervalSeconds = 0.075;
    private static readonly int[] ZoomLevels = [2, 4, 8, 16, 24];
    private static readonly double[] SimulationRates = [0.25, 0.5, 1, 2, 4, 8, 16];
    private static readonly ToolCategory[] ToolCategoryOrder = [ToolCategory.Terrain];
    private static readonly WorldTool[] TerrainToolOrder =
        [WorldTool.Elevation, WorldTool.SeaLevel, WorldTool.OceanSeed,
            WorldTool.Temperature, WorldTool.Moisture, WorldTool.Seasons, WorldTool.Volcano, WorldTool.Meteor,
            WorldTool.River];
    private static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / SimulationWorld.TicksPerSecond);
    private readonly GraphicsDeviceManager _graphics;
    private SimulationWorld _world = null!;
    private WorldPreset _preset = WorldPreset.Standard;
    private ulong _seed = 20260806;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private SpriteFont? _hudFont;
    private TimeSpan _accumulator;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _cameraX;
    private int _cameraY;
    private int _zoomIndex = 2;
    private int _toolCategoryIndex;
    private int _toolIndex;
    private WorldTool? _repeatingTool;
    private int _repeatingButton;
    private double _toolHoldSeconds;
    private double _nextToolRepeatSeconds;
    private int _meteorMagnitudeIndex = 3;
    private int _simulationRateIndex = 2;
    private bool _paused;

    public NewtGame()
    {
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 960,
            PreferredBackBufferHeight = 640,
        };
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = "Newt";
        GenerateWorld();
    }

    private int TileSize => ZoomLevels[_zoomIndex];

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _hudFont = Content.Load<SpriteFont>("HudFont");
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }

        HandleWorldShortcuts(keyboard, mouse);
        HandleActiveTool(mouse, gameTime.ElapsedGameTime.TotalSeconds);
        HandleCamera(keyboard, mouse);

        if (!_paused)
        {
            _accumulator += gameTime.ElapsedGameTime * SimulationRates[_simulationRateIndex];
            while (_accumulator >= SimulationStep)
            {
                _world.AdvanceOneTick();
                _accumulator -= SimulationStep;
            }
        }

        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var visibleColumns = GraphicsDevice.Viewport.Width / TileSize + 2;
        var visibleRows = MapViewportHeight / TileSize + 2;
        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        for (var screenY = 0; screenY < visibleRows; screenY++)
        {
            var worldY = _cameraY + screenY;
            if (worldY >= _world.Height)
            {
                break;
            }

            for (var screenX = 0; screenX < visibleColumns; screenX++)
            {
                var worldX = (_cameraX + screenX) % _world.Width;
                var position = new GridPosition(worldX, worldY);
                var terrain = _world.GetTerrain(position);
                var biome = _world.GetBiome(position);
                var temperatureBand = _world.GetTemperatureBand(position);
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(screenX * TileSize, screenY * TileSize, TileSize, TileSize),
                    GetTileColor(position, terrain, biome, temperatureBand));
                var water = _world.GetSurfaceWater(position);
                if (water is not SurfaceWaterKind.None)
                {
                    DrawSurfaceWater(screenX, screenY, position, water);
                }
                DrawVolcanoVent(screenX, screenY, position);
                DrawImpactWave(screenX, screenY, position);
            }
        }

        for (var index = 0; index < _world.CritterCount; index++)
        {
            var critter = _world.GetCritter(index);
            var screenX = WrappedScreenX(critter.Position.X);
            var screenY = critter.Position.Y - _cameraY;
            if (screenX < 0 || screenX >= visibleColumns || screenY < 0 || screenY >= visibleRows)
            {
                continue;
            }

            _spriteBatch.Draw(
                _pixel,
                new Rectangle(screenX * TileSize, screenY * TileSize, TileSize, TileSize),
                GetCritterColor(critter.Species));
        }

        DrawHud();

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void HandleWorldShortcuts(KeyboardState keyboard, MouseState mouse)
    {
        if (WasPressed(keyboard, Keys.D1))
        {
            _preset = WorldPreset.Micro;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D2))
        {
            _preset = WorldPreset.Standard;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D3))
        {
            _preset = WorldPreset.Large;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D4))
        {
            _preset = WorldPreset.Ring;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D5))
        {
            _preset = WorldPreset.Earth;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D6))
        {
            _preset = WorldPreset.Moon;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.D7))
        {
            _preset = WorldPreset.Mars;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.Q))
        {
            CycleTool(-1);
        }
        else if (WasPressed(keyboard, Keys.E))
        {
            CycleTool(1);
        }
        else if (WasPressed(keyboard, Keys.R))
        {
            CycleToolCategory(1);
        }
        else if (WasPressed(keyboard, Keys.N))
        {
            _seed++;
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.F))
        {
            var position = ScreenToWorld(mouse.X, mouse.Y);
            if (position is not null)
            {
                Hydrology.StartSpring(_world, position.Value);
            }
        }
        else if (WasPressed(keyboard, Keys.OemComma))
        {
            _simulationRateIndex = Math.Max(0, _simulationRateIndex - 1);
        }
        else if (WasPressed(keyboard, Keys.OemPeriod))
        {
            _simulationRateIndex = Math.Min(SimulationRates.Length - 1, _simulationRateIndex + 1);
        }
        else if (WasPressed(keyboard, Keys.P))
        {
            _paused = !_paused;
        }
    }

    private void HandleActiveTool(MouseState mouse, double elapsedSeconds)
    {
        var primaryPressed = mouse.LeftButton is ButtonState.Pressed &&
            _previousMouse.LeftButton is ButtonState.Released;
        var secondaryPressed = mouse.RightButton is ButtonState.Pressed &&
            _previousMouse.RightButton is ButtonState.Released;
        var primaryActivated = primaryPressed;
        var secondaryActivated = secondaryPressed;
        if (IsContinuousTool(CurrentTool))
        {
            var heldButton = mouse.LeftButton is ButtonState.Pressed &&
                mouse.RightButton is ButtonState.Released
                    ? 1
                    : mouse.RightButton is ButtonState.Pressed &&
                        mouse.LeftButton is ButtonState.Released
                        ? -1
                        : 0;
            if (heldButton == 0)
            {
                ResetToolRepeat();
            }
            else if (_repeatingTool != CurrentTool || _repeatingButton != heldButton)
            {
                _repeatingTool = CurrentTool;
                _repeatingButton = heldButton;
                _toolHoldSeconds = 0;
                _nextToolRepeatSeconds = ToolRepeatDelaySeconds;
            }
            else
            {
                _toolHoldSeconds += elapsedSeconds;
                if (_toolHoldSeconds >= _nextToolRepeatSeconds)
                {
                    primaryActivated |= heldButton > 0;
                    secondaryActivated |= heldButton < 0;
                    _nextToolRepeatSeconds += ToolRepeatIntervalSeconds;
                }
            }
        }
        else
        {
            ResetToolRepeat();
        }

        if (!primaryActivated && !secondaryActivated)
        {
            return;
        }

        var position = ScreenToWorld(mouse.X, mouse.Y);
        if (position is null)
        {
            return;
        }

        switch (CurrentTool)
        {
            case WorldTool.Elevation when primaryActivated:
                Geology.ApplyRadialUplift(_world, position.Value, radius: 7, strength: 0.36f);
                break;
            case WorldTool.Elevation:
                Geology.ApplyRadialLowering(_world, position.Value, radius: 7, strength: 0.36f);
                break;
            case WorldTool.SeaLevel when !_world.HasOceans:
                break;
            case WorldTool.SeaLevel when primaryActivated:
                Geology.ChangeSeaLevel(_world, Geology.SeaLevelEditStep);
                break;
            case WorldTool.SeaLevel:
                Geology.ChangeSeaLevel(_world, -Geology.SeaLevelEditStep);
                break;
            case WorldTool.OceanSeed:
                Geology.MoveOceanSeed(_world, position.Value);
                break;
            case WorldTool.Temperature when primaryActivated:
                ClimateSystem.AdjustGlobalTemperature(_world, ClimateSystem.GlobalClimateEditStep);
                break;
            case WorldTool.Temperature:
                ClimateSystem.AdjustGlobalTemperature(_world, -ClimateSystem.GlobalClimateEditStep);
                break;
            case WorldTool.Moisture when primaryActivated:
                ClimateSystem.AdjustGlobalMoisture(_world, ClimateSystem.GlobalClimateEditStep);
                break;
            case WorldTool.Moisture:
                ClimateSystem.AdjustGlobalMoisture(_world, -ClimateSystem.GlobalClimateEditStep);
                break;
            case WorldTool.Seasons when primaryActivated:
                SeasonSystem.SetEnabled(_world, true);
                break;
            case WorldTool.Seasons:
                SeasonSystem.SetEnabled(_world, false);
                break;
            case WorldTool.Volcano when primaryActivated:
                Volcanism.SpawnVolcano(_world, position.Value);
                break;
            case WorldTool.Meteor when primaryActivated:
                Impacts.CreateMeteorImpact(_world, position.Value, _meteorMagnitudeIndex / 10f);
                break;
            case WorldTool.Meteor:
                _meteorMagnitudeIndex = (_meteorMagnitudeIndex + 1) % 11;
                break;
            case WorldTool.River when primaryActivated:
                Hydrology.StartSpring(_world, position.Value);
                break;
            case WorldTool.River:
                Hydrology.RemoveFreshwaterAt(_world, position.Value);
                break;
        }
    }

    private static bool IsContinuousTool(WorldTool tool) => tool is
        WorldTool.Elevation or WorldTool.SeaLevel or WorldTool.Temperature or WorldTool.Moisture;

    private void ResetToolRepeat()
    {
        _repeatingTool = null;
        _repeatingButton = 0;
        _toolHoldSeconds = 0;
        _nextToolRepeatSeconds = 0;
    }

    private ToolCategory CurrentToolCategory => ToolCategoryOrder[_toolCategoryIndex];

    private WorldTool CurrentTool => GetTools(CurrentToolCategory)[_toolIndex];

    private void CycleToolCategory(int step)
    {
        _toolCategoryIndex = Mod(_toolCategoryIndex + step, ToolCategoryOrder.Length);
        _toolIndex = 0;
    }

    private void CycleTool(int step)
    {
        var tools = GetTools(CurrentToolCategory);
        _toolIndex = Mod(_toolIndex + step, tools.Length);
    }

    private static WorldTool[] GetTools(ToolCategory category) => category switch
    {
        ToolCategory.Terrain => TerrainToolOrder,
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    private void HandleCamera(KeyboardState keyboard, MouseState mouse)
    {
        var panStep = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 4 : 1;
        if (keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left))
        {
            _cameraX -= panStep;
        }
        if (keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right))
        {
            _cameraX += panStep;
        }
        if (keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up))
        {
            _cameraY -= panStep;
        }
        if (keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down))
        {
            _cameraY += panStep;
        }

        if (mouse.ScrollWheelValue > _previousMouse.ScrollWheelValue)
        {
            _zoomIndex = Math.Min(_zoomIndex + 1, ZoomLevels.Length - 1);
        }
        else if (mouse.ScrollWheelValue < _previousMouse.ScrollWheelValue)
        {
            _zoomIndex = Math.Max(_zoomIndex - 1, 0);
        }

        _cameraX = Mod(_cameraX, _world.Width);
        var visibleRows = Math.Max(1, MapViewportHeight / TileSize);
        _cameraY = Math.Clamp(_cameraY, 0, Math.Max(0, _world.Height - visibleRows));
    }

    private int MapViewportHeight => Math.Max(1, GraphicsDevice.Viewport.Height - HudHeight);

    private void DrawHud()
    {
        if (_spriteBatch is null || _pixel is null || _hudFont is null)
        {
            return;
        }

        var mouse = Mouse.GetState();
        var hudY = MapViewportHeight;
        var width = GraphicsDevice.Viewport.Width;
        _spriteBatch.Draw(_pixel, new Rectangle(0, hudY, width, HudHeight), new Color(16, 19, 22));
        _spriteBatch.Draw(_pixel, new Rectangle(0, hudY, width, 2), new Color(76, 91, 102));

        var worldWidth = Math.Max(220, width / 4);
        var toolWidth = Math.Max(190, width / 5);
        var toolX = worldWidth;
        var tileX = Math.Min(width - 1, toolX + toolWidth);
        DrawHudDivider(toolX, hudY);
        DrawHudDivider(tileX, hudY);

        DrawHudLines(HudPadding, hudY + 8,
            "WORLD",
            $"{_preset.Name}  {_world.Width} x {_world.Height}",
            $"Seed {_seed}   Tick {_world.Tick}   Year {_world.Year}   {GetSimulationRateText()}",
            _world.HasOceans
                ? $"Sea {_world.SeaLevel:+0.00;-0.00;0.00}   Seed POS ({_world.OceanSeed.X}, {_world.OceanSeed.Y})"
                : $"Datum {_world.SeaLevel:+0.00;-0.00;0.00}   No oceans",
            $"Temperature {_world.GlobalTemperatureOffset:+0.00;-0.00;0.00}",
            $"Moisture {_world.GlobalMoistureOffset:+0.00;-0.00;0.00}",
            GetWorldSeasonLine());

        DrawHudLines(toolX + HudPadding, hudY + 8,
            "ACTIVE TOOL",
            CurrentTool.ToString(),
            GetToolHint(CurrentTool),
            "Q / E  cycle tools",
            "R  cycle categories",
            "< / >  speed   P  pause",
            "Wheel  zoom",
            "WASD / arrows  pan");

        var position = ScreenToWorld(mouse.X, mouse.Y);
        DrawHudLines(tileX + HudPadding, hudY + 8,
            position is null ? ["TILE", "Move the pointer over the map"] : GetInspectionLines(position.Value));
    }

    private void DrawHudDivider(int x, int hudY)
    {
        if (_spriteBatch is not null && _pixel is not null && x > 0 && x < GraphicsDevice.Viewport.Width)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x, hudY + 8, 1, HudHeight - 16), new Color(54, 63, 70));
        }
    }

    private string GetWorldSeasonLine() => _world.SeasonsEnabled
        ? $"Seasons N {SeasonSystem.GetSeason(_world, new GridPosition(0, 0))} / S {SeasonSystem.GetSeason(_world, new GridPosition(0, _world.Height - 1))}"
        : "Seasons disabled";

    private string GetSimulationRateText() => _paused
        ? $"Paused ({SimulationRates[_simulationRateIndex]:0.##}x)"
        : $"{SimulationRates[_simulationRateIndex]:0.##}x";

    private void DrawHudLines(int x, int y, params string[] lines)
    {
        if (_spriteBatch is null || _hudFont is null)
        {
            return;
        }

        for (var index = 0; index < lines.Length; index++)
        {
            var color = index == 0 ? new Color(126, 190, 213) : new Color(220, 225, 228);
            _spriteBatch.DrawString(_hudFont, lines[index], new Vector2(x, y + index * 17), color);
        }
    }

    private string[] GetInspectionLines(GridPosition position)
    {
        var elevation = _world.GetElevation(position);
        var terrain = _world.GetTerrain(position);
        var water = _world.GetSurfaceWater(position);
        var waterText = water is SurfaceWaterKind.FreshwaterLake
            ? _world.GetBiome(position) is Biome.Arctic
                ? $"Frozen Lake   Depth {_world.GetWaterDepth(position):0.000}"
                : $"Freshwater Lake   Depth {_world.GetWaterDepth(position):0.000}"
            : water is SurfaceWaterKind.River
                ? $"River   Connections {_world.GetRiverConnections(position)}"
                : water.ToString();
        return
        [
            $"TILE {position}",
            GetTileIdentity(position, terrain),
            $"Elevation {elevation:+0.000;-0.000;0.000}",
            _world.SeasonsEnabled
                ? $"Season {SeasonSystem.GetSeason(_world, position)}"
                : "Season disabled",
            $"Temperature {_world.GetTemperature(position):0.000} ({SeasonSystem.GetTemperatureChange(_world, position):+0.000;-0.000;0.000} season)   {_world.GetTemperatureBand(position)}",
            $"Moisture {_world.GetMoisture(position):0.000} ({SeasonSystem.GetMoistureChange(_world, position):+0.000;-0.000;0.000} season)   {_world.GetMoistureBand(position)}",
            $"Water {waterText}",
            GetEntityInspection(position),
        ];
    }

    private string GetTileIdentity(GridPosition position, Terrain terrain)
    {
        var biome = _world.GetBiome(position);
        var temperature = _world.GetTemperatureBand(position);
        var cover = _world.GetSurfaceCover(position);
        if (cover is SurfaceCover.Lava)
        {
            return terrain is Terrain.Mountain ? "Lava Mountain" : "Lava";
        }
        if (cover is SurfaceCover.Stone && terrain is not Terrain.Mountain &&
            !IsSaltwaterTerrain(terrain))
        {
            return $"Stone {GetTerrainDisplayName(terrain)}";
        }
        return terrain switch
        {
            Terrain.Ice => "Ice Sheet",
            Terrain.Mountain => biome is Biome.Arctic ? "Snowy Mountain" : "Mountain",
            Terrain.DeepOcean => $"{temperature} Deep Ocean",
            Terrain.Ocean => $"{temperature} Ocean",
            Terrain.Shallows => $"{temperature} Shallows",
            Terrain.Beach => $"{temperature} Beach",
            _ when biome is not Biome.None => $"{biome} {GetTerrainDisplayName(terrain)}",
            _ => GetTerrainDisplayName(terrain),
        };
    }

    private static string GetTerrainDisplayName(Terrain terrain) => terrain switch
    {
        Terrain.DeepOcean => "Deep Ocean",
        Terrain.Ice => "Ice Sheet",
        _ => terrain.ToString(),
    };

    private string GetEntityInspection(GridPosition position)
    {
        var entities = new List<string>();
        if (_world.GetVolcanoState(position) is { } volcanoState)
        {
            entities.Add($"{volcanoState} Volcano");
        }
        for (var index = 0; index < _world.CritterCount; index++)
        {
            var critter = _world.GetCritter(index);
            if (critter.Position == position)
            {
                entities.Add(critter.Species.ToString());
                break;
            }
        }

        return entities.Count == 0 ? "Entities None" : $"Entities {string.Join(", ", entities)}";
    }

    private GridPosition? ScreenToWorld(int screenX, int screenY)
    {
        if (screenX < 0 || screenY < 0 || screenY >= MapViewportHeight)
        {
            return null;
        }

        var y = _cameraY + screenY / TileSize;
        if (y >= _world.Height)
        {
            return null;
        }

        return new GridPosition(Mod(_cameraX + screenX / TileSize, _world.Width), y);
    }

    private void GenerateWorld()
    {
        _world = WorldGenerator.Generate(new WorldGenerationOptions(_preset, _seed));
        _cameraX = 0;
        _cameraY = 0;
        _accumulator = TimeSpan.Zero;
        AddFirstCritter(CritterSpecies.Plankton, Terrain.Ocean, Terrain.DeepOcean);
        AddFirstCritter(CritterSpecies.Crab, Terrain.Shallows, Terrain.Beach);
        AddFirstCritter(CritterSpecies.Ape, Terrain.Plains, Terrain.Beach);
    }

    private void AddFirstCritter(CritterSpecies species, params Terrain[] allowedTerrains)
    {
        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                var position = new GridPosition(x, y);
                if (!_world.IsOccupied(position) && allowedTerrains.Contains(_world.GetTerrain(position)))
                {
                    if (_world.GetSurfaceCover(position) is SurfaceCover.None)
                    {
                        _world.AddCritter(species, position);
                        return;
                    }
                }
            }
        }
    }

    private bool WasPressed(KeyboardState current, Keys key) => current.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

    private int WrappedScreenX(int worldX)
    {
        var difference = Mod(worldX - _cameraX, _world.Width);
        return difference;
    }

    private void DrawSurfaceWater(
        int screenX,
        int screenY,
        GridPosition worldPosition,
        SurfaceWaterKind water)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var color = water is SurfaceWaterKind.FreshwaterLake
            ? GetLakeColor(
                _world.GetWaterDepth(worldPosition),
                _world.GetBiome(worldPosition) is Biome.Arctic)
            : GetSurfaceWaterColor(water);
        var originX = screenX * TileSize;
        var originY = screenY * TileSize;
        if (water is SurfaceWaterKind.FreshwaterLake)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(originX, originY, TileSize, TileSize), color);
            return;
        }

        var width = Math.Max(1, TileSize / 3);
        var centerX = originX + TileSize / 2;
        var centerY = originY + TileSize / 2;
        _spriteBatch.Draw(_pixel, new Rectangle(centerX - width / 2, centerY - width / 2, width, width), color);

        var connections = _world.GetRiverConnections(worldPosition);
        DrawRiverConnections(connections, originX, originY, centerX, centerY, width, color);
    }

    private void DrawVolcanoVent(int screenX, int screenY, GridPosition position)
    {
        if (_spriteBatch is null || _pixel is null ||
            _world.GetVolcanoState(position) is not { } state ||
            state is VolcanoState.Extinct)
        {
            return;
        }

        var size = Math.Max(1, TileSize / 2);
        var inset = (TileSize - size) / 2;
        var color = state is VolcanoState.Active
            ? new Color(255, 208, 45)
            : new Color(92, 70, 66);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(screenX * TileSize + inset, screenY * TileSize + inset, size, size),
            color);
    }

    private void DrawImpactWave(int screenX, int screenY, GridPosition position)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        for (var index = 0; index < _world.ActiveImpactWaveCount; index++)
        {
            var wave = _world.GetImpactWave(index);
            if (!Impacts.IsOnShockFront(_world, position, wave))
            {
                continue;
            }

            var inset = Math.Max(0, TileSize / 5);
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(
                    screenX * TileSize + inset,
                    screenY * TileSize + inset,
                    Math.Max(1, TileSize - inset * 2),
                    Math.Max(1, TileSize - inset * 2)),
                new Color(255, 222, 145, 185));
            return;
        }
    }

    private void DrawRiverConnections(
        RiverConnection connections,
        int originX,
        int originY,
        int centerX,
        int centerY,
        int width,
        Color color)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        if ((connections & RiverConnection.North) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(centerX - width / 2, originY, width, centerY - originY + 1), color);
        }
        if ((connections & RiverConnection.East) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(centerX, centerY - width / 2, originX + TileSize - centerX, width), color);
        }
        if ((connections & RiverConnection.South) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(centerX - width / 2, centerY, width, originY + TileSize - centerY), color);
        }
        if ((connections & RiverConnection.West) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(originX, centerY - width / 2, centerX - originX + 1, width), color);
        }
        if ((connections & RiverConnection.NorthEast) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(centerX, centerY - width / 2, originX + TileSize - centerX, width), color);
            _spriteBatch.Draw(_pixel, new Rectangle(originX + TileSize - width, originY, width, centerY - originY + 1), color);
        }
        if ((connections & RiverConnection.SouthEast) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(centerX, centerY - width / 2, originX + TileSize - centerX, width), color);
            _spriteBatch.Draw(_pixel, new Rectangle(originX + TileSize - width, centerY, width, originY + TileSize - centerY), color);
        }
        if ((connections & RiverConnection.SouthWest) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(originX, centerY - width / 2, centerX - originX + 1, width), color);
            _spriteBatch.Draw(_pixel, new Rectangle(originX, centerY, width, originY + TileSize - centerY), color);
        }
        if ((connections & RiverConnection.NorthWest) != 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(originX, centerY - width / 2, centerX - originX + 1, width), color);
            _spriteBatch.Draw(_pixel, new Rectangle(originX, originY, width, centerY - originY + 1), color);
        }
    }

    private static int Mod(int value, int modulus)
    {
        var result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private Color GetTileColor(
        GridPosition position,
        Terrain terrain,
        Biome biome,
        TemperatureBand temperatureBand)
    {
        var cover = _world.GetSurfaceCover(position);
        if (cover is SurfaceCover.Lava)
        {
            var glow = ((_world.Tick + position.X * 3L + position.Y * 5L) / 5) % 2 == 0;
            return glow ? new Color(245, 72, 20) : new Color(190, 35, 15);
        }
        if (cover is SurfaceCover.Stone && terrain is not Terrain.Mountain &&
            !IsSaltwaterTerrain(terrain))
        {
            return GetStoneColor(terrain);
        }
        if (_world.Body is WorldBody.Mars)
        {
            return GetMarsColor(position, temperatureBand);
        }
        if (_world.Body is WorldBody.Moon)
        {
            return GetMoonColor(position);
        }
        return GetTerrainColor(terrain, biome, temperatureBand);
    }

    private Color GetMoonColor(GridPosition position) => _world.GetElevation(position) switch
    {
        < -0.70f => new Color(43, 44, 47),
        < -0.40f => new Color(65, 66, 70),
        < -0.15f => new Color(88, 89, 92),
        < 0.10f => new Color(112, 112, 113),
        < 0.34f => new Color(137, 136, 133),
        < 0.58f => new Color(161, 159, 154),
        < 1.0f => new Color(187, 184, 176),
        _ => new Color(216, 212, 202),
    };

    private Color GetMarsColor(GridPosition position, TemperatureBand temperatureBand)
    {
        var latitude = Math.Abs((position.Y + 0.5f) / _world.Height * 2 - 1);
        if (latitude > 0.86f && temperatureBand is TemperatureBand.Freezing)
        {
            return new Color(224, 211, 185);
        }

        return _world.GetElevation(position) switch
        {
            < -0.55f => new Color(72, 31, 27),
            < -0.25f => new Color(104, 43, 31),
            < 0f => new Color(137, 57, 36),
            < 0.34f => new Color(168, 77, 43),
            < 0.58f => new Color(188, 101, 58),
            < 1.0f => new Color(205, 130, 78),
            _ => new Color(224, 164, 108),
        };
    }

    private static Color GetTerrainColor(
        Terrain terrain,
        Biome biome,
        TemperatureBand temperatureBand) => terrain switch
    {
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows => GetOceanColor(terrain, temperatureBand),
        Terrain.Beach => GetBeachColor(temperatureBand),
        Terrain.Plains => GetBiomeLandColor(biome),
        Terrain.Lowlands => ScaleColor(GetBiomeLandColor(biome), 0.82f),
        Terrain.Canyon => ScaleColor(GetBiomeLandColor(biome), 0.65f),
        Terrain.Trench => ScaleColor(GetBiomeLandColor(biome), 0.50f),
        Terrain.Hills => biome switch
        {
            Biome.Arctic => new Color(205, 218, 218),
            Biome.Tundra => new Color(112, 121, 105),
            Biome.Taiga => new Color(45, 76, 60),
            Biome.Bog => new Color(57, 70, 62),
            Biome.Grassland => new Color(91, 117, 65),
            Biome.Forest => new Color(45, 91, 56),
            Biome.Swamp => new Color(42, 78, 62),
            Biome.Desert => new Color(148, 122, 77),
            Biome.Arid => new Color(129, 112, 67),
            Biome.Jungle => new Color(35, 82, 48),
            _ => new Color(115, 115, 105),
        },
        Terrain.Mountain => biome is Biome.Arctic
            ? new Color(210, 222, 225)
            : new Color(78, 72, 68),
        Terrain.Ice => new Color(165, 220, 235),
        _ => Color.Magenta,
    };

    private static Color GetStoneColor(Terrain terrain) => terrain switch
    {
        Terrain.Beach => new Color(132, 129, 122),
        Terrain.Plains => new Color(122, 119, 113),
        Terrain.Lowlands => new Color(108, 106, 101),
        Terrain.Hills => new Color(101, 98, 93),
        Terrain.Canyon => new Color(82, 80, 77),
        Terrain.Trench => new Color(62, 61, 59),
        _ => new Color(104, 101, 96),
    };

    private static bool IsSaltwaterTerrain(Terrain terrain) => terrain is
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice;

    private static Color GetOceanColor(Terrain terrain, TemperatureBand temperatureBand)
    {
        var baseColor = terrain switch
        {
            Terrain.DeepOcean => new Color(10, 37, 83),
            Terrain.Ocean => new Color(24, 68, 128),
            Terrain.Shallows => new Color(66, 145, 180),
            _ => Color.Magenta,
        };

        var brightness = temperatureBand switch
        {
            TemperatureBand.Freezing => 0.90f,
            TemperatureBand.Cold => 0.95f,
            TemperatureBand.Hot => 1.06f,
            _ => 1f,
        };
        return ScaleColor(baseColor, brightness);
    }

    private static Color GetBeachColor(TemperatureBand temperatureBand) => temperatureBand switch
    {
        TemperatureBand.Freezing => new Color(220, 224, 214),
        TemperatureBand.Cold => new Color(204, 198, 169),
        TemperatureBand.Hot => new Color(226, 181, 103),
        _ => new Color(215, 195, 135),
    };

    private static Color GetBiomeLandColor(Biome biome) => biome switch
    {
        Biome.Arctic => new Color(224, 233, 232),
        Biome.Tundra => new Color(137, 147, 124),
        Biome.Taiga => new Color(51, 92, 68),
        Biome.Bog => new Color(65, 82, 70),
        Biome.Grassland => new Color(112, 145, 68),
        Biome.Forest => new Color(42, 112, 62),
        Biome.Swamp => new Color(35, 91, 67),
        Biome.Desert => new Color(205, 172, 94),
        Biome.Arid => new Color(168, 145, 74),
        Biome.Jungle => new Color(25, 105, 53),
        _ => new Color(110, 110, 100),
    };

    private static Color ScaleColor(Color color, float brightness) => new(
        Math.Clamp((int)MathF.Round(color.R * brightness), 0, 255),
        Math.Clamp((int)MathF.Round(color.G * brightness), 0, 255),
        Math.Clamp((int)MathF.Round(color.B * brightness), 0, 255));

    private static Color GetCritterColor(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => new Color(160, 255, 180),
        CritterSpecies.Crab => new Color(255, 80, 80),
        CritterSpecies.Ape => new Color(145, 105, 70),
        _ => Color.Magenta,
    };

    private static Color GetSurfaceWaterColor(SurfaceWaterKind water) => water switch
    {
        SurfaceWaterKind.River => new Color(65, 175, 235),
        SurfaceWaterKind.FreshwaterLake => new Color(50, 135, 205),
        _ => Color.Magenta,
    };

    private static Color GetLakeColor(float depth, bool frozen)
    {
        if (frozen)
        {
            return new Color(165, 220, 235);
        }

        var depthShade = Math.Clamp(depth / 0.20f, 0f, 1f);
        return ScaleColor(new Color(50, 135, 205), 1f - 0.45f * depthShade);
    }

    private string GetToolHint(WorldTool tool) => tool switch
    {
        WorldTool.Elevation => "left raise, right lower",
        WorldTool.SeaLevel => _world.HasOceans ? "left raise, right lower" : "no oceans on this world",
        WorldTool.OceanSeed => "click move seed",
        WorldTool.Temperature => "left warmer, right cooler",
        WorldTool.Moisture => "left wetter, right drier",
        WorldTool.Seasons => "left enable, right disable",
        WorldTool.Volcano => "left spawn active vent",
        WorldTool.Meteor => $"left impact, right magnitude {_meteorMagnitudeIndex / 10f:0.0}",
        WorldTool.River => "left spawn, right remove",
        _ => string.Empty,
    };

    private enum ToolCategory
    {
        Terrain,
    }

    private enum WorldTool
    {
        Elevation,
        SeaLevel,
        OceanSeed,
        Temperature,
        Moisture,
        Seasons,
        Volcano,
        Meteor,
        River,
    }
}
