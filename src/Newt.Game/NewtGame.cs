using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

/// <summary>MonoGame host responsible only for input, timing, and presentation.</summary>
public sealed class NewtGame : Microsoft.Xna.Framework.Game
{
    private static readonly int[] ZoomLevels = [2, 4, 8, 16, 24];
    private static readonly ToolCategory[] ToolCategoryOrder = [ToolCategory.Terrain];
    private static readonly WorldTool[] TerrainToolOrder = [WorldTool.Elevation, WorldTool.River];
    private static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / SimulationWorld.TicksPerSecond);
    private readonly GraphicsDeviceManager _graphics;
    private SimulationWorld _world = null!;
    private WorldPreset _preset = WorldPreset.Standard;
    private ulong _seed = 20260806;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private TimeSpan _accumulator;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _cameraX;
    private int _cameraY;
    private int _zoomIndex = 2;
    private int _toolCategoryIndex;
    private int _toolIndex;

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
        GenerateWorld();
    }

    private int TileSize => ZoomLevels[_zoomIndex];

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
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
        HandleActiveTool(mouse);
        HandleCamera(keyboard, mouse);
        UpdateWindowTitle(mouse);

        _accumulator += gameTime.ElapsedGameTime;
        while (_accumulator >= SimulationStep)
        {
            _world.AdvanceOneTick();
            _accumulator -= SimulationStep;
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
        var visibleRows = GraphicsDevice.Viewport.Height / TileSize + 2;
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
                    GetTerrainColor(terrain, biome, temperatureBand));
                var water = _world.GetSurfaceWater(position);
                if (water is not SurfaceWaterKind.None)
                {
                    DrawSurfaceWater(screenX, screenY, position, water);
                }
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
    }

    private void HandleActiveTool(MouseState mouse)
    {
        var primaryPressed = mouse.LeftButton is ButtonState.Pressed &&
            _previousMouse.LeftButton is ButtonState.Released;
        var secondaryPressed = mouse.RightButton is ButtonState.Pressed &&
            _previousMouse.RightButton is ButtonState.Released;
        if (!primaryPressed && !secondaryPressed)
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
            case WorldTool.Elevation when primaryPressed:
                Geology.ApplyRadialUplift(_world, position.Value, radius: 7, strength: 0.36f);
                break;
            case WorldTool.Elevation:
                Geology.ApplyRadialLowering(_world, position.Value, radius: 7, strength: 0.36f);
                break;
            case WorldTool.River when primaryPressed:
                Hydrology.StartSpring(_world, position.Value);
                break;
            case WorldTool.River:
                Hydrology.RemoveFreshwaterAt(_world, position.Value);
                break;
        }
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
        var visibleRows = Math.Max(1, GraphicsDevice.Viewport.Height / TileSize);
        _cameraY = Math.Clamp(_cameraY, 0, Math.Max(0, _world.Height - visibleRows));
    }

    private void UpdateWindowTitle(MouseState mouse)
    {
        var position = ScreenToWorld(mouse.X, mouse.Y);
        var inspected = position is null
            ? "outside world"
            : FormatInspection(position.Value);
        var hydrology = _world.ActiveSpringCount > 0
            ? $"{_world.ActiveSpringCount} spring(s) flowing"
            : _world.LastCompletedSpring is { } completed
                ? $"spring {completed.Termination}, {completed.RiverTileCount} tiles"
                : "no spring traced";
        Window.Title = $"Newt | {_preset.Name} {_world.Width}x{_world.Height} | seed {_seed} | " +
            $"tool {CurrentToolCategory} > {CurrentTool} ({GetToolHint(CurrentTool)}) | " +
            $"tick {_world.Tick} | {inspected} | {hydrology}";
    }

    private GridPosition? ScreenToWorld(int screenX, int screenY)
    {
        if (screenX < 0 || screenY < 0)
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

    private string FormatInspection(GridPosition position)
    {
        var elevation = _world.GetElevation(position);
        var terrain = _world.GetTerrain(position);
        var water = _world.GetSurfaceWater(position);
        var depth = water is SurfaceWaterKind.FreshwaterLake
            ? $", depth {_world.GetWaterDepth(position):0.000}"
            : string.Empty;
        var environment = GetEnvironmentLabel(position, terrain);
        var identity = terrain is Terrain.Plains or Terrain.Hills or Terrain.Mountain
            ? environment
            : $"{terrain}, {environment}";
        return $"{position}: {identity}, {water}, " +
            $"elevation {elevation:+0.000;-0.000;0.000} ({GetElevationLabel(terrain, elevation)}), " +
            $"temperature {_world.GetTemperature(position):0.000} ({_world.GetTemperatureBand(position)}), " +
            $"moisture {_world.GetMoisture(position):0.000} ({_world.GetMoistureBand(position)}){depth}";
    }

    private static string GetElevationLabel(Terrain terrain, float elevation)
    {
        if (terrain is Terrain.DeepOcean)
        {
            return "Deep Ocean";
        }

        if (terrain is Terrain.Shallows)
        {
            return "Shallows";
        }

        if (terrain is Terrain.Ocean or Terrain.Ice)
        {
            return "Ocean";
        }

        if (elevation > 0.58f)
        {
            return "Mountain";
        }

        return elevation > 0.34f ? "Hills" : "Plains";
    }

    private string GetEnvironmentLabel(GridPosition position, Terrain terrain)
    {
        if (terrain is Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows or Terrain.Ice)
        {
            return _world.GetTemperatureBand(position).ToString();
        }

        return _world.GetBiome(position).ToString();
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
                    _world.AddCritter(species, position);
                    return;
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

    private static Color GetTerrainColor(
        Terrain terrain,
        Biome biome,
        TemperatureBand temperatureBand) => terrain switch
    {
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows => GetOceanColor(terrain, temperatureBand),
        Terrain.Beach => new Color(215, 195, 135),
        Terrain.Plains => biome switch
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
        },
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

        var depthShade = Math.Clamp(depth / 0.15f, 0f, 1f);
        return ScaleColor(new Color(50, 135, 205), 1f - 0.15f * depthShade);
    }

    private static string GetToolHint(WorldTool tool) => tool switch
    {
        WorldTool.Elevation => "left raise, right lower",
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
        River,
    }
}
