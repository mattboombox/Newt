using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Newt.Game;

/// <summary>MonoGame host responsible only for input, timing, and presentation.</summary>
public sealed class NewtGame : Microsoft.Xna.Framework.Game
{
    private const int HudHeight = 156;
    private const int HudPadding = 12;
    private const double ToolRepeatDelaySeconds = 0.25;
    private const double ToolRepeatIntervalSeconds = 0.075;
    private const int PopulationSampleIntervalTicks = 5 * SimulationWorld.TicksPerSecond;
    private const int PopulationHistoryLength = 90;
    private static readonly int[] ZoomLevels = [2, 4, 8, 16, 24, 32, 48];
    private static readonly WorldPreset[] MenuSizes =
        [WorldPreset.Micro, WorldPreset.Small, WorldPreset.Standard, WorldPreset.Large, WorldPreset.Huge];
    private static readonly WorldMapType[] MenuMapTypes =
        [WorldMapType.Continents, WorldMapType.Pangaea, WorldMapType.Archipelago, WorldMapType.AllOcean,
            WorldMapType.RingWorld, WorldMapType.Earth];
    private static readonly double[] SimulationRates = [0.25, 0.5, 1, 2, 4, 8, 16, 32];
    private static readonly ToolCategory[] ToolCategoryOrder =
        [ToolCategory.WorldTools, ToolCategory.TerrainTools, ToolCategory.CritterTools,
            ToolCategory.BuildingTools, ToolCategory.Events, ToolCategory.Other];
    private static readonly WorldTool[] WorldToolOrder =
        [WorldTool.SeaLevel, WorldTool.OceanSeed, WorldTool.Temperature,
            WorldTool.Moisture, WorldTool.EvolutionChance, WorldTool.Seasons, WorldTool.Life];
    private static readonly WorldTool[] EventToolOrder =
        [WorldTool.Meteor, WorldTool.Tsunami, WorldTool.WatershedShift,
            WorldTool.Evolve, WorldTool.NaturalEvents, WorldTool.Colonist,
            WorldTool.Plague, WorldTool.ZombiePlague];
    private static readonly WorldTool[] CritterToolOrder =
        [WorldTool.Plankton, WorldTool.Jellyfish, WorldTool.Worm, WorldTool.Trilobite,
            WorldTool.SeaScorpion, WorldTool.Nautilus, WorldTool.Squid, WorldTool.SquidEgg,
            WorldTool.Fish, WorldTool.Newt, WorldTool.MegaToad, WorldTool.Therapsid,
            WorldTool.Monkey, WorldTool.Ape, WorldTool.Deer, WorldTool.Elk,
            WorldTool.Gazelle, WorldTool.Wolf, WorldTool.Crab, WorldTool.ToothedWhale,
            WorldTool.BaleenWhale];
    private static readonly WorldTool[] BuildingToolOrder = [WorldTool.WolfDen, WorldTool.Teleporter];
    private static readonly WorldTool[] OtherToolOrder =
        [WorldTool.JumpStart, WorldTool.Population, WorldTool.Inspect, WorldTool.Controls];
    private static readonly string[] ControlLines =
    [
        "?  open or close controls",
        "Esc  quit",
        "H  hide or show HUD",
        "M  open world menu",
        "N  generate next seed",
        "Q / E  previous / next tool",
        "R  next tool category",
        ", / .  slower / faster simulation",
        "- / =  decrease / increase magnitude",
        "P  pause or resume",
        "G  toggle automatic slowdown",
        "WASD / arrows  pan camera",
        "Shift + pan  move faster",
        "Mouse wheel  zoom toward pointer",
        "F  create spring on hovered land",
        "Left click  use selected tool",
        "Right click  alternate tool action",
        "World menu: arrows / WASD navigate",
        "World menu: digits / Backspace edit seed",
        "World menu: Enter / Space generate",
    ];
    private static readonly WorldTool[] TerrainToolOrder =
        [WorldTool.Elevation, WorldTool.Smoothing, WorldTool.River, WorldTool.Volcano, WorldTool.Stone, WorldTool.Lava];
    private static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / SimulationWorld.TicksPerSecond);
    private readonly GraphicsDeviceManager _graphics;
    private SimulationWorld _world = null!;
    private WorldPreset _preset = WorldPreset.Standard;
    private WorldMapType _mapType = WorldMapType.Continents;
    private ulong _seed = CreateRandomSeed();
    private string _seedText;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private SpriteFont? _hudFont;
    private readonly Dictionary<CritterSpecies, Texture2D> _critterSprites = [];
    private readonly Dictionary<CritterSpecies, Texture2D> _critterFlashSprites = [];
    private TimeSpan _accumulator;
    private KeyboardState _previousKeyboard;
    private MouseState _previousMouse;
    private int _cameraX;
    private int _cameraY;
    private int _zoomIndex = 2;
    private int _toolCategoryIndex = Array.IndexOf(ToolCategoryOrder, ToolCategory.CritterTools);
    private int _toolIndex;
    private WorldTool? _repeatingTool;
    private int _repeatingButton;
    private double _toolHoldSeconds;
    private double _nextToolRepeatSeconds;
    private int _eventMagnitudeIndex = 3;
    private int _terrainMagnitudeIndex = 3;
    private int TerrainBrushRadius => 1 + 2 * _terrainMagnitudeIndex;
    private float ElevationBrushStrength => 0.12f + 0.08f * _terrainMagnitudeIndex;
    private float SmoothingBrushStrength => 0.10f + 0.08f * _terrainMagnitudeIndex;
    private int _simulationRateIndex = 2;
    private readonly SimulationSpeedGuard _simulationSpeedGuard = new();
    private bool _speedAutomaticallyReduced;
    private bool _lifeEnabled = true;
    private bool _paused;
    private bool _hudHidden;
    private bool _setupMenuOpen;
    private bool _seedTypingActive;
    private int _setupRow;
    private int _menuSizeIndex = 2;
    private int _menuMapTypeIndex;
    private CritterId _inspectedCritterId;
    private bool _populationWindowOpen;
    private bool _controlsWindowOpen;
    private readonly Dictionary<CritterSpecies, List<int>> _populationHistory =
        Enum.GetValues<CritterSpecies>().ToDictionary(species => species, _ => new List<int>());
    private readonly List<int> _sickApeHistory = [];
    private long _nextPopulationSampleTick;

    public NewtGame()
    {
        _seedText = _seed.ToString(CultureInfo.InvariantCulture);
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

    private int MapOffsetX => Math.Max(0,
        (GraphicsDevice.Viewport.Width - _world.Width * TileSize) / 2);

    private int MapOffsetY => Math.Max(0,
        (MapViewportHeight - _world.Height * TileSize) / 2);

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _hudFont = Content.Load<SpriteFont>("HudFont");
        LoadCritterSprite(CritterSpecies.Plankton, "plankton.png");
        LoadCritterSprite(CritterSpecies.Newt, "newt.png");
        LoadCritterSprite(CritterSpecies.MegaToad, "mega-toad.png");
        LoadCritterSprite(CritterSpecies.Ape, "ape.png");
        LoadCritterSprite(CritterSpecies.ApeSailor, "ape-sailor.png");
        LoadCritterSprite(CritterSpecies.Deer, "deer.png");
        LoadCritterSprite(CritterSpecies.Wolf, "wolf.png");
        LoadCritterSprite(CritterSpecies.ToothedWhale, "toothed-whale.png");
        LoadCritterSprite(CritterSpecies.BaleenWhale, "baleen-whale.png");
        SetWindowIconFromNewtSprite();
    }

    protected override void Update(GameTime gameTime)
    {
        var updateStarted = Stopwatch.GetTimestamp();
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        if (_setupMenuOpen)
        {
            _simulationSpeedGuard.Reset();
            HandleSetupMenu(keyboard);
            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            base.Update(gameTime);
            return;
        }

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
                if (_world.Tick >= _nextPopulationSampleTick)
                {
                    RecordPopulationSample();
                }
                _accumulator -= SimulationStep;
            }
        }

        UpdateInspectedCritterFollow();

        if (_paused || !IsActive)
        {
            _simulationSpeedGuard.Reset();
        }
        else if (_simulationSpeedGuard.ShouldReduce(
            SimulationRates[_simulationRateIndex], _world.CritterCount,
            Stopwatch.GetElapsedTime(updateStarted),
            TargetElapsedTime, gameTime.IsRunningSlowly))
        {
            _simulationRateIndex = Array.IndexOf(SimulationRates, SimulationSpeedGuard.FallbackRate);
            _speedAutomaticallyReduced = true;
            // Discard catch-up time, not world ticks, so old overload cannot delay recovery.
            _accumulator = TimeSpan.Zero;
            ResetElapsedTime();
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

        var visibleColumns = MapOffsetX > 0
            ? _world.Width
            : GraphicsDevice.Viewport.Width / TileSize + 2;
        var visibleRows = MapOffsetY > 0
            ? _world.Height
            : MapViewportHeight / TileSize + 2;
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
                    new Rectangle(
                        MapOffsetX + screenX * TileSize,
                        MapOffsetY + screenY * TileSize,
                        TileSize,
                        TileSize),
                    GetTileColor(position, terrain, biome, temperatureBand));
                var water = _world.GetSurfaceWater(position);
                if (water is not SurfaceWaterKind.None)
                {
                    DrawSurfaceWater(screenX, screenY, position, water);
                }
                DrawApeStructure(screenX, screenY, position);
                DrawWolfDen(screenX, screenY, position);
                DrawTeleporter(screenX, screenY, position);
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

            DrawCritter(critter, screenX, screenY);
        }

        if (!_hudHidden)
        {
            DrawHud();
        }
        if (_populationWindowOpen)
        {
            DrawPopulationWindow();
        }
        if (_controlsWindowOpen)
        {
            DrawControlsWindow();
        }
        if (_setupMenuOpen)
        {
            DrawSetupMenu();
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void LoadCritterSprite(CritterSpecies species, string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "Critters", fileName);
        if (!File.Exists(path))
        {
            return;
        }

        using var stream = File.OpenRead(path);
        var sprite = Texture2D.FromStream(GraphicsDevice, stream);
        _critterSprites.Add(species, sprite);

        var flashPixels = new Color[sprite.Width * sprite.Height];
        sprite.GetData(flashPixels);
        for (var index = 0; index < flashPixels.Length; index++)
        {
            flashPixels[index] = new Color(255, 255, 255, (int)flashPixels[index].A);
        }

        var flashSprite = new Texture2D(GraphicsDevice, sprite.Width, sprite.Height);
        flashSprite.SetData(flashPixels);
        _critterFlashSprites.Add(species, flashSprite);
    }

    private void SetWindowIconFromNewtSprite()
    {
        if (!_critterSprites.TryGetValue(CritterSpecies.Newt, out var sprite))
        {
            return;
        }

        var colors = new Color[sprite.Width * sprite.Height];
        sprite.GetData(colors);
        var pixels = new byte[colors.Length * 4];
        for (var index = 0; index < colors.Length; index++)
        {
            var pixelOffset = index * 4;
            pixels[pixelOffset] = colors[index].R;
            pixels[pixelOffset + 1] = colors[index].G;
            pixels[pixelOffset + 2] = colors[index].B;
            pixels[pixelOffset + 3] = colors[index].A;
        }

        var pinnedPixels = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            var surface = SdlCreateRgbSurfaceFrom(
                pinnedPixels.AddrOfPinnedObject(),
                sprite.Width,
                sprite.Height,
                32,
                sprite.Width * 4,
                0x000000ff,
                0x0000ff00,
                0x00ff0000,
                0xff000000);
            if (surface == IntPtr.Zero)
            {
                return;
            }

            try
            {
                SdlSetWindowIcon(Window.Handle, surface);
            }
            finally
            {
                SdlFreeSurface(surface);
            }
        }
        finally
        {
            pinnedPixels.Free();
        }
    }

    [DllImport("SDL2", EntryPoint = "SDL_CreateRGBSurfaceFrom", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SdlCreateRgbSurfaceFrom(
        IntPtr pixels,
        int width,
        int height,
        int depth,
        int pitch,
        uint redMask,
        uint greenMask,
        uint blueMask,
        uint alphaMask);

    [DllImport("SDL2", EntryPoint = "SDL_SetWindowIcon", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SdlSetWindowIcon(IntPtr window, IntPtr icon);

    [DllImport("SDL2", EntryPoint = "SDL_FreeSurface", CallingConvention = CallingConvention.Cdecl)]
    private static extern void SdlFreeSurface(IntPtr surface);

    private void DrawCritter(CritterSnapshot critter, int screenX, int screenY)
    {
        if (_critterSprites.TryGetValue(critter.Species, out var sprite))
        {
            if (critter.IsDamageFlashing &&
                _critterFlashSprites.TryGetValue(critter.Species, out var flashSprite))
            {
                sprite = flashSprite;
            }

            _spriteBatch!.Draw(
                sprite,
                new Rectangle(
                    MapOffsetX + screenX * TileSize,
                    MapOffsetY + screenY * TileSize,
                    TileSize,
                    TileSize),
                Color.White);
            return;
        }

        _spriteBatch!.Draw(
            _pixel,
            new Rectangle(
                MapOffsetX + screenX * TileSize + GetCritterMarkerInset(),
                MapOffsetY + screenY * TileSize + GetCritterMarkerInset(),
                GetCritterMarkerSize(),
                GetCritterMarkerSize()),
            GetCritterColor(critter));
    }

    private int GetCritterMarkerSize() => TileSize <= 2
        ? 1
        : TileSize - 2 * Math.Max(1, TileSize / 5);

    private int GetCritterMarkerInset() => (TileSize - GetCritterMarkerSize()) / 2;

    private void HandleWorldShortcuts(KeyboardState keyboard, MouseState mouse)
    {
        if (WasPressed(keyboard, Keys.OemQuestion))
        {
            _controlsWindowOpen = !_controlsWindowOpen;
        }
        else if (WasPressed(keyboard, Keys.OemMinus))
        {
            AdjustActiveToolMagnitude(-1);
        }
        else if (WasPressed(keyboard, Keys.OemPlus))
        {
            AdjustActiveToolMagnitude(1);
        }
        else if (WasPressed(keyboard, Keys.H))
        {
            _hudHidden = !_hudHidden;
        }
        else if (WasPressed(keyboard, Keys.M))
        {
            _menuMapTypeIndex = Array.IndexOf(MenuMapTypes, _mapType);
            if (_menuMapTypeIndex < 0)
            {
                _menuMapTypeIndex = 0;
            }
            var currentSize = _preset == WorldPreset.Earth ? WorldPreset.Huge : _preset;
            var sizeIndex = Array.IndexOf(MenuSizes, currentSize);
            if (sizeIndex >= 0)
            {
                _menuSizeIndex = sizeIndex;
            }
            _setupMenuOpen = true;
            _setupRow = 0;
            _seedTypingActive = false;
            _seedText = _seed.ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (WasPressed(keyboard, Keys.Q))
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
            _seedText = _seed.ToString(CultureInfo.InvariantCulture);
            GenerateWorld();
        }
        else if (WasPressed(keyboard, Keys.F))
        {
            var position = ScreenToWorld(mouse.X, mouse.Y);
            if (position is not null)
            {
                Hydrology.StartSnowmeltSpring(_world, position.Value, SpringOrigin.Player);
            }
        }
        else if (WasPressed(keyboard, Keys.G))
        {
            _simulationSpeedGuard.Enabled = !_simulationSpeedGuard.Enabled;
            _speedAutomaticallyReduced = false;
        }
        else if (WasPressed(keyboard, Keys.OemComma))
        {
            _simulationRateIndex = Math.Max(0, _simulationRateIndex - 1);
            ResetSpeedGuard();
        }
        else if (WasPressed(keyboard, Keys.OemPeriod))
        {
            _simulationRateIndex = Math.Min(SimulationRates.Length - 1, _simulationRateIndex + 1);
            ResetSpeedGuard();
        }
        else if (WasPressed(keyboard, Keys.P))
        {
            _paused = !_paused;
            _simulationSpeedGuard.Reset();
        }
    }

    private void AdjustActiveToolMagnitude(int change)
    {
        switch (CurrentTool)
        {
            case WorldTool.Elevation or WorldTool.Smoothing:
                _terrainMagnitudeIndex = Math.Clamp(_terrainMagnitudeIndex + change, 0, 10);
                break;
            case WorldTool.Meteor or WorldTool.Tsunami:
                _eventMagnitudeIndex = Math.Clamp(_eventMagnitudeIndex + change, 0, 10);
                break;
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

        if (CurrentTool is WorldTool.JumpStart && primaryActivated)
        {
            _lifeEnabled = true;
            _world.JumpStartPlankton();
            return;
        }

        if (CurrentTool is WorldTool.Population)
        {
            if (primaryActivated)
            {
                _populationWindowOpen = true;
            }
            else if (secondaryActivated)
            {
                _populationWindowOpen = false;
            }
            return;
        }

        if (CurrentTool is WorldTool.Controls)
        {
            if (primaryActivated)
            {
                _controlsWindowOpen = true;
            }
            else if (secondaryActivated)
            {
                _controlsWindowOpen = false;
            }
            return;
        }

        if (CurrentTool is WorldTool.Inspect && secondaryActivated)
        {
            _inspectedCritterId = default;
            return;
        }

        var position = ScreenToWorld(mouse.X, mouse.Y);
        if (position is null)
        {
            return;
        }

        switch (CurrentTool)
        {
            case WorldTool.Plague when primaryActivated:
                _world.TryInfectApeAt(position.Value, PlagueKind.Plague);
                break;
            case WorldTool.ZombiePlague when primaryActivated:
                _world.TryInfectApeAt(position.Value, PlagueKind.Zombie);
                break;
            case WorldTool.Elevation when primaryActivated:
                Geology.ApplyRadialUplift(_world, position.Value, TerrainBrushRadius, ElevationBrushStrength);
                break;
            case WorldTool.Elevation:
                Geology.ApplyRadialLowering(_world, position.Value, TerrainBrushRadius, ElevationBrushStrength);
                break;
            case WorldTool.Smoothing when primaryActivated:
                Geology.ApplyRadialSmoothing(_world, position.Value, TerrainBrushRadius, SmoothingBrushStrength);
                break;
            case WorldTool.Smoothing:
                break;
            case WorldTool.SeaLevel when !_world.HasOceans:
                break;
            case WorldTool.SeaLevel when primaryActivated:
                Geology.ChangeSeaLevel(_world, Geology.SeaLevelEditStep);
                break;
            case WorldTool.SeaLevel:
                Geology.ChangeSeaLevel(_world, -Geology.SeaLevelEditStep);
                break;
            case WorldTool.OceanSeed when primaryActivated:
                Geology.MoveOceanSeed(_world, position.Value);
                break;
            case WorldTool.OceanSeed:
                if (!Hydrology.TryConvertOversizedLakeToOcean(_world, position.Value))
                {
                    Geology.TryAddOceanSeed(_world, position.Value);
                }
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
            case WorldTool.EvolutionChance when primaryActivated:
                _world.AdjustEvolutionChance(1);
                break;
            case WorldTool.EvolutionChance:
                _world.AdjustEvolutionChance(-1);
                break;
            case WorldTool.Seasons when primaryActivated:
                SeasonSystem.SetEnabled(_world, true);
                break;
            case WorldTool.Seasons:
                SeasonSystem.SetEnabled(_world, false);
                break;
            case WorldTool.Life when primaryActivated:
                _lifeEnabled = true;
                LifeSystem.SetEnabled(_world, true);
                break;
            case WorldTool.Life:
                _lifeEnabled = false;
                LifeSystem.SetEnabled(_world, false);
                break;
            case WorldTool.Volcano when primaryActivated:
                Volcanism.SpawnVolcano(_world, position.Value);
                break;
            case WorldTool.Meteor when primaryActivated:
                Impacts.CreateMeteorImpact(_world, position.Value, _eventMagnitudeIndex / 10f);
                break;
            case WorldTool.Tsunami when primaryActivated:
                Tsunamis.Create(_world, position.Value, _eventMagnitudeIndex / 10f);
                break;
            case WorldTool.WatershedShift when primaryActivated:
                Hydrology.ShiftNaturalWatershed(_world);
                break;
            case WorldTool.WatershedShift:
                break;
            case WorldTool.Evolve when primaryActivated:
                _world.TryEvolveCritterAt(position.Value);
                break;
            case WorldTool.Evolve:
                _world.TryDevolveCritterAt(position.Value);
                break;
            case WorldTool.NaturalEvents when primaryActivated:
                NaturalEvents.SetEnabled(_world, true);
                break;
            case WorldTool.NaturalEvents:
                NaturalEvents.SetEnabled(_world, false);
                break;
            case WorldTool.River when primaryActivated:
                Hydrology.StartSnowmeltSpring(_world, position.Value, SpringOrigin.Player);
                break;
            case WorldTool.River:
                Hydrology.RemoveFreshwaterAt(_world, position.Value);
                break;
            case WorldTool.Plankton or WorldTool.Jellyfish or WorldTool.Worm or
                WorldTool.Trilobite or WorldTool.SeaScorpion or WorldTool.Nautilus or
                WorldTool.Squid or WorldTool.SquidEgg or
                WorldTool.Fish or WorldTool.Newt or WorldTool.MegaToad or
                WorldTool.Therapsid or WorldTool.Monkey or WorldTool.Deer or
                WorldTool.Ape or WorldTool.Elk or WorldTool.Gazelle or
                WorldTool.Wolf or WorldTool.Crab or WorldTool.ToothedWhale or WorldTool.BaleenWhale
                when primaryActivated:
                var species = GetCritterSpecies(CurrentTool);
                _world.TrySpawnCritter(species, position.Value);
                break;
            case WorldTool.WolfDen when primaryActivated:
                _world.TryPlaceWolfDen(position.Value);
                break;
            case WorldTool.WolfDen:
                _world.RemoveWolfDenAt(position.Value);
                break;
            case WorldTool.Teleporter when primaryActivated:
                _world.TryPlaceTeleporter(position.Value);
                break;
            case WorldTool.Teleporter:
                _world.RemoveTeleporterAt(position.Value);
                break;
            case WorldTool.Stone when primaryActivated:
                Volcanism.PlaceStone(_world, position.Value);
                break;
            case WorldTool.Stone:
                Volcanism.ClearGeologicalCover(_world, position.Value);
                break;
            case WorldTool.Lava when primaryActivated:
                Volcanism.PlaceLava(_world, position.Value);
                break;
            case WorldTool.Lava:
                Volcanism.ClearGeologicalCover(_world, position.Value);
                break;
            case WorldTool.Colonist when primaryActivated:
                _world.TrySendApeColonist(position.Value);
                break;
            case WorldTool.Inspect when primaryActivated:
                if (_world.TryGetCritterAt(position.Value, out var inspected))
                {
                    _inspectedCritterId = inspected.Id;
                }
                break;
            case WorldTool.Inspect:
                break;
        }
    }

    private static bool IsContinuousTool(WorldTool tool) => tool is
        WorldTool.Elevation or WorldTool.Smoothing or WorldTool.SeaLevel or WorldTool.Temperature or
        WorldTool.Moisture or WorldTool.EvolutionChance;

    private static CritterSpecies GetCritterSpecies(WorldTool tool) => tool switch
    {
        WorldTool.Plankton => CritterSpecies.Plankton,
        WorldTool.Jellyfish => CritterSpecies.Jellyfish,
        WorldTool.Worm => CritterSpecies.Worm,
        WorldTool.Trilobite => CritterSpecies.Trilobite,
        WorldTool.SeaScorpion => CritterSpecies.SeaScorpion,
        WorldTool.Nautilus => CritterSpecies.Nautilus,
        WorldTool.Squid => CritterSpecies.Squid,
        WorldTool.SquidEgg => CritterSpecies.SquidEgg,
        WorldTool.Fish => CritterSpecies.Fish,
        WorldTool.Newt => CritterSpecies.Newt,
        WorldTool.MegaToad => CritterSpecies.MegaToad,
        WorldTool.Therapsid => CritterSpecies.Therapsid,
        WorldTool.Monkey => CritterSpecies.Monkey,
        WorldTool.Ape => CritterSpecies.Ape,
        WorldTool.Deer => CritterSpecies.Deer,
        WorldTool.Elk => CritterSpecies.Elk,
        WorldTool.Gazelle => CritterSpecies.Gazelle,
        WorldTool.Wolf => CritterSpecies.Wolf,
        WorldTool.Crab => CritterSpecies.Crab,
        WorldTool.ToothedWhale => CritterSpecies.ToothedWhale,
        WorldTool.BaleenWhale => CritterSpecies.BaleenWhale,
        _ => throw new ArgumentOutOfRangeException(nameof(tool)),
    };

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
        ToolCategory.WorldTools => WorldToolOrder,
        ToolCategory.TerrainTools => TerrainToolOrder,
        ToolCategory.CritterTools => CritterToolOrder,
        ToolCategory.BuildingTools => BuildingToolOrder,
        ToolCategory.Events => EventToolOrder,
        ToolCategory.Other => OtherToolOrder,
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

        var zoomDirection = Math.Sign(mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue);
        var anchor = zoomDirection == 0 ? null : ScreenToWorld(mouse.X, mouse.Y);
        var previousZoomIndex = _zoomIndex;
        if (zoomDirection > 0)
        {
            _zoomIndex = Math.Min(_zoomIndex + 1, ZoomLevels.Length - 1);
        }
        else if (zoomDirection < 0)
        {
            _zoomIndex = Math.Max(_zoomIndex - 1, 0);
        }

        if (_zoomIndex != previousZoomIndex && anchor is not null)
        {
            // Keep the world tile under the pointer in the same screen region.
            _cameraX = anchor.Value.X - (mouse.X - MapOffsetX) / TileSize;
            _cameraY = anchor.Value.Y - (mouse.Y - MapOffsetY) / TileSize;
        }

        _cameraX = Mod(_cameraX, _world.Width);
        var visibleRows = Math.Max(1, MapViewportHeight / TileSize);
        _cameraY = Math.Clamp(_cameraY, 0, Math.Max(0, _world.Height - visibleRows));
    }

    private void UpdateInspectedCritterFollow()
    {
        if (!_inspectedCritterId.IsValid)
        {
            return;
        }
        if (!_world.TryGetCritter(_inspectedCritterId, out var critter))
        {
            _inspectedCritterId = default;
            return;
        }

        var visibleColumns = Math.Max(1, GraphicsDevice.Viewport.Width / TileSize);
        var visibleRows = Math.Max(1, MapViewportHeight / TileSize);
        _cameraX = Mod(critter.Position.X - visibleColumns / 2, _world.Width);
        _cameraY = Math.Clamp(
            critter.Position.Y - visibleRows / 2,
            0,
            Math.Max(0, _world.Height - visibleRows));
    }

    private int MapViewportHeight => _hudHidden
        ? GraphicsDevice.Viewport.Height
        : Math.Max(1, GraphicsDevice.Viewport.Height - HudHeight);

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
        var entityWidth = Math.Clamp(width / 3, 320, 460);
        var entityX = Math.Max(tileX, width - entityWidth);
        DrawHudDivider(toolX, hudY);
        DrawHudDivider(tileX, hudY);
        DrawHudDivider(entityX, hudY);

        DrawHudLines(HudPadding, hudY + 8,
            "WORLD",
            $"{GetMapTypeName(_mapType)} / {_preset.Name}  {_world.Width} x {_world.Height}",
            $"Seed {_seed}   Tick {_world.Tick}   Year {_world.Year}   {GetSimulationRateText()}",
            _world.HasOceans
                ? $"Sea {_world.SeaLevel:+0.00;-0.00;0.00}   Seeds {1 + _world.AdditionalOceanSeeds.Count}   Primary ({_world.OceanSeed.X}, {_world.OceanSeed.Y})"
                : $"Datum {_world.SeaLevel:+0.00;-0.00;0.00}   No oceans",
            $"Temperature {_world.GlobalTemperatureOffset:+0.00;-0.00;0.00}",
            $"Moisture {_world.GlobalMoistureOffset:+0.00;-0.00;0.00}",
            $"Life {(_world.LifeEnabled ? "enabled" : "disabled")}   Critters {_world.CritterCount}",
            GetWorldSeasonLine());

        DrawHudLines(toolX + HudPadding, hudY + 8,
            $"TOOL: {GetToolCategoryName(CurrentToolCategory)}",
            GetToolName(CurrentTool),
            GetToolHint(CurrentTool),
            "Q / E  cycle tools",
            "R  cycle categories",
            "< / >  speed   P  pause",
            $"M menu   G auto-slow {(_simulationSpeedGuard.Enabled ? "ON" : "OFF")}");

        var position = ScreenToWorld(mouse.X, mouse.Y);
        DrawHudLines(tileX + HudPadding, hudY + 8,
            position is null
                ? ["TILE", "Move the pointer over the map"]
                : GetTileInspectionLines(position.Value));
        DrawHudLines(entityX + HudPadding, hudY + 8,
            position is null
                ? ["ENTITY", "None"]
                : GetEntityInspectionLines(position.Value));
    }

    private void DrawPopulationWindow()
    {
        if (_spriteBatch is null || _pixel is null || _hudFont is null)
        {
            return;
        }

        var populations = Enum.GetValues<CritterSpecies>()
            .Select(species => (Name: GetCritterDisplayName(species),
                Count: _world.GetCritterCount(species), Color: GetCritterColor(species),
                History: _populationHistory[species]))
            .Where(entry => entry.Count > 0)
            .ToList();
        if (_world.SickApeCount > 0)
        {
            populations.Add(("Sick Apes", _world.SickApeCount,
                new Color(205, 195, 55), _sickApeHistory));
        }
        const int padding = 16;
        const int headerHeight = 74;
        const int rowHeight = 20;
        const int preferredColumnWidth = 300;
        var maximumRows = Math.Max(1, (MapViewportHeight - 24 - headerHeight - padding) / rowHeight);
        var columns = Math.Max(1, (populations.Count + maximumRows - 1) / maximumRows);
        var rows = populations.Count == 0
            ? 1
            : Math.Min(maximumRows, (populations.Count + columns - 1) / columns);
        var windowWidth = Math.Min(
            GraphicsDevice.Viewport.Width - 24,
            padding * 2 + columns * preferredColumnWidth);
        var windowHeight = headerHeight + rows * rowHeight + padding;
        var windowX = 12;
        var windowY = 12;
        var bounds = new Rectangle(windowX, windowY, windowWidth, windowHeight);
        _spriteBatch.Draw(_pixel, bounds, new Color(76, 91, 102));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4),
            new Color(18, 23, 27, 244));

        _spriteBatch.DrawString(
            _hudFont,
            "CRITTER POPULATION",
            new Vector2(bounds.X + padding, bounds.Y + 11),
            new Color(126, 190, 213));
        _spriteBatch.DrawString(
            _hudFont,
            $"Total {_world.CritterCount:N0}",
            new Vector2(bounds.X + padding, bounds.Y + 30),
            new Color(175, 184, 190));

        if (populations.Count == 0)
        {
            _spriteBatch.DrawString(
                _hudFont,
                "No critters exist.",
                new Vector2(bounds.X + padding, bounds.Y + headerHeight),
                new Color(220, 225, 228));
            return;
        }

        var columnWidth = (bounds.Width - padding * 2) / columns;
        for (var index = 0; index < populations.Count; index++)
        {
            var column = index / rows;
            var row = index % rows;
            var x = bounds.X + padding + column * columnWidth;
            var y = bounds.Y + headerHeight + row * rowHeight;
            var entry = populations[index];
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(x, y + 3, 10, 10),
                entry.Color);
            _spriteBatch.DrawString(
                _hudFont,
                entry.Name,
                new Vector2(x + 16, y),
                new Color(220, 225, 228));
            var countText = entry.Count.ToString("N0");
            var countWidth = _hudFont.MeasureString(countText).X;
            var graphWidth = Math.Clamp(columnWidth / 3, 48, 96);
            var graphBounds = new Rectangle(
                x + columnWidth - graphWidth - 8,
                y + 2,
                graphWidth,
                15);
            _spriteBatch.DrawString(
                _hudFont,
                countText,
                new Vector2(graphBounds.X - countWidth - 8, y),
                new Color(245, 210, 120));
            DrawPopulationSparkline(entry.History, entry.Color, entry.Count, graphBounds);
        }
    }

    private void DrawControlsWindow()
    {
        if (_spriteBatch is null || _pixel is null || _hudFont is null)
        {
            return;
        }

        const int padding = 16;
        const int headerHeight = 50;
        const int rowHeight = 18;
        const int columns = 2;
        var rows = (ControlLines.Length + columns - 1) / columns;
        var windowWidth = Math.Min(GraphicsDevice.Viewport.Width - 24, 720);
        var windowHeight = headerHeight + rows * rowHeight + padding;
        var windowX = Math.Max(0, (GraphicsDevice.Viewport.Width - windowWidth) / 2);
        var windowY = Math.Max(0, (MapViewportHeight - windowHeight) / 2);
        var bounds = new Rectangle(windowX, windowY, windowWidth, windowHeight);
        _spriteBatch.Draw(_pixel, bounds, new Color(76, 91, 102));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4),
            new Color(18, 23, 27, 244));
        _spriteBatch.DrawString(
            _hudFont,
            "CONTROLS",
            new Vector2(bounds.X + padding, bounds.Y + 11),
            new Color(126, 190, 213));
        _spriteBatch.DrawString(
            _hudFont,
            "Press ? or right-click the Controls tool to close",
            new Vector2(bounds.X + padding, bounds.Y + 29),
            new Color(175, 184, 190));

        var columnWidth = (bounds.Width - padding * 2) / columns;
        for (var index = 0; index < ControlLines.Length; index++)
        {
            var column = index / rows;
            var row = index % rows;
            _spriteBatch.DrawString(
                _hudFont,
                ControlLines[index],
                new Vector2(bounds.X + padding + column * columnWidth, bounds.Y + headerHeight + row * rowHeight),
                new Color(220, 225, 228));
        }
    }

    private void RecordPopulationSample()
    {
        foreach (var species in Enum.GetValues<CritterSpecies>())
        {
            AppendPopulationSample(_populationHistory[species], _world.GetCritterCount(species));
        }
        AppendPopulationSample(_sickApeHistory, _world.SickApeCount);
        _nextPopulationSampleTick = _world.Tick + PopulationSampleIntervalTicks;
    }

    private static void AppendPopulationSample(List<int> history, int count)
    {
        history.Add(count);
        if (history.Count > PopulationHistoryLength)
        {
            history.RemoveAt(0);
        }
    }

    private void DrawPopulationSparkline(
        IReadOnlyList<int> history,
        Color color,
        int currentCount,
        Rectangle bounds)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        _spriteBatch.Draw(_pixel, bounds, new Color(31, 39, 45));
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1),
            new Color(65, 76, 84));

        var pointCount = history.Count + 1;
        var maximum = Math.Max(1, Math.Max(currentCount, history.Count == 0 ? 0 : history.Max()));
        var previous = GetPoint(0);
        for (var index = 1; index < pointCount; index++)
        {
            var current = GetPoint(index);
            DrawPixelLine(previous, current, color);
            previous = current;
        }

        Point GetPoint(int index)
        {
            var value = index < history.Count ? history[index] : currentCount;
            var x = pointCount <= 1
                ? bounds.Right - 1
                : bounds.X + index * (bounds.Width - 1) / (pointCount - 1);
            var y = bounds.Bottom - 2 -
                (int)MathF.Round(value / (float)maximum * (bounds.Height - 3));
            return new Point(x, y);
        }
    }

    private void DrawPixelLine(Point start, Point end, Color color)
    {
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        var steps = Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y));
        if (steps == 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(start.X, start.Y, 1, 1), color);
            return;
        }
        for (var step = 0; step <= steps; step++)
        {
            var x = start.X + (end.X - start.X) * step / steps;
            var y = start.Y + (end.Y - start.Y) * step / steps;
            _spriteBatch.Draw(_pixel, new Rectangle(x, y, 1, 1), color);
        }
    }

    private static string GetCritterDisplayName(CritterSpecies species) => species switch
    {
        CritterSpecies.SeaScorpion => "Sea Scorpion",
        CritterSpecies.SquidEgg => "Squid Egg",
        CritterSpecies.MegaToad => "Mega Toad",
        CritterSpecies.ApeSailor => "Ape Sailor",
        CritterSpecies.UndeadApe => "Undead Ape",
        CritterSpecies.ToothedWhale => "Toothed Whale",
        CritterSpecies.BaleenWhale => "Baleen Whale",
        _ => species.ToString(),
    };

    private void DrawHudDivider(int x, int hudY)
    {
        if (_spriteBatch is not null && _pixel is not null && x > 0 && x < GraphicsDevice.Viewport.Width)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(x, hudY + 8, 1, HudHeight - 16), new Color(54, 63, 70));
        }
    }

    private string GetWorldSeasonLine() => _world.Body is WorldBody.RingWorld
        ? "Engineered climate zones"
        : _world.SeasonsEnabled
        ? $"Seasons N {SeasonSystem.GetSeason(_world, new GridPosition(0, 0))} / S {SeasonSystem.GetSeason(_world, new GridPosition(0, _world.Height - 1))}"
        : "Seasons disabled";

    private string GetSimulationRateText() => _paused
        ? $"Paused ({SimulationRates[_simulationRateIndex]:0.##}x)"
        : $"{SimulationRates[_simulationRateIndex]:0.##}x{(_speedAutomaticallyReduced ? " (auto)" : "")}";

    private void ResetSpeedGuard()
    {
        _simulationSpeedGuard.Reset();
        _speedAutomaticallyReduced = false;
    }

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

    private string[] GetTileInspectionLines(GridPosition position)
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
            $"Temperature {_world.GetTemperature(position):0.000} ({SeasonSystem.GetTemperatureChange(_world, position):+0.000;-0.000;0.000} season)   {_world.GetTemperatureBand(position)}",
            $"Moisture {_world.GetMoisture(position):0.000} ({SeasonSystem.GetMoistureChange(_world, position):+0.000;-0.000;0.000} season)   {_world.GetMoistureBand(position)}",
            $"Fresh Water {waterText}",
            .. water is SurfaceWaterKind.FreshwaterLake
                ? new[] { GetLakeSizeLine(position) } : Array.Empty<string>(),
            $"Nutrition {_world.GetTileNutrition(position)} / {_world.GetTileNutritionCapacity(position)}",
        ];
    }

    private string GetLakeSizeLine(GridPosition position)
    {
        var tiles = _world.GetLakeTileCount(position);
        var mapPercent = 100d * tiles / (_world.Width * _world.Height);
        return $"Lake size {tiles:N0} tiles ({mapPercent:0.00}% of map)";
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
        if (LifeSystem.IsStoneBiome(_world, position))
        {
            return $"Stone {GetTerrainDisplayName(terrain)}";
        }
        return terrain switch
        {
            Terrain.Ice => "Ice Sheet",
            Terrain.RingWorldWall => "Ring World Wall",
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

    private string[] GetEntityInspectionLines(GridPosition position)
    {
        if (_inspectedCritterId.IsValid &&
            _world.TryGetCritter(_inspectedCritterId, out var followedCritter))
        {
            return GetCritterInspectionLines(followedCritter, following: true);
        }

        for (var index = 0; index < _world.CritterCount; index++)
        {
            var critter = _world.GetCritter(index);
            if (critter.Position == position)
            {
                return GetCritterInspectionLines(critter, following: false);
            }
        }

        if (_world.GetVolcanoState(position) is { } volcanoState)
        {
            return ["ENTITY", "Volcano", $"Behavior {volcanoState}", "Energy None"];
        }

        if (_world.GetWolfDenCharges(position) is { } charges)
        {
            return ["ENTITY", "Wolf Den", $"Charges {charges}", "Behavior Ambush nursery"];
        }

        if (_world.HasTeleporter(position))
        {
            var behavior = _world.TeleporterCount switch
            {
                1 => "Random valid destination",
                2 => "Linked portal pair",
                _ => "Random linked portal",
            };
            return ["ENTITY", "Teleporter", $"Network {_world.TeleporterCount}", $"Behavior {behavior}"];
        }

        if (_world.GetApeStructure(position) is { } apeStructure)
        {
            var village = _world.GetApeStructureVillage(position);
            var villageId = _world.GetApeVillageId(position);
            if (apeStructure is ApeStructureKind.Village)
            {
                return [
                    "ENTITY",
                    villageId.HasValue ? $"Ape Village #{villageId.Value}" : "Ape Village",
                    $"Residents {_world.GetApeVillageResidentCount(position)} / {_world.GetApeVillagePopulationCapacity(position)}",
                    $"Civilians {_world.GetApeVillageCivilianCount(position)}   Sailors {_world.GetApeVillageSailorCount(position)}",
                    $"Food {_world.GetApeVillageFood(position)} / {_world.GetApeVillageFoodCapacity(position)}",
                    $"Wood {_world.GetApeVillageWood(position)} / {_world.GetApeVillageWoodCapacity(position)}",
                    "Behavior Settlement",
                ];
            }

            return village is { } owner
                ? [
                    "ENTITY",
                    GetApeStructureDisplayName(apeStructure),
                    villageId.HasValue ? $"Village #{villageId.Value}" : "Village",
                    $"Residents {_world.GetApeVillageResidentCount(owner)} / {_world.GetApeVillagePopulationCapacity(owner)}",
                    .. GetApeStructureProductionLines(position, apeStructure),
                    _world.IsApeStructureOperational(position)
                        ? "Behavior Village district"
                        : "Behavior Inactive (out of season)",
                ]
                : ["ENTITY", GetApeStructureDisplayName(apeStructure)];
        }

        return ["ENTITY", "None"];
    }

    private string[] GetApeStructureProductionLines(
        GridPosition position,
        ApeStructureKind structure)
    {
        if (_world.GetApeStructureProductionPerMinute(position) is not { } rate)
        {
            return [];
        }

        var resource = structure is ApeStructureKind.LumberCamp ? "wood" : "food";
        var production = $"{rate:0.#} {resource}/simulated min";
        return _world.IsApeStructureOperational(position)
            ? [$"Production {production}"]
            : [$"Production inactive ({production} when active)"];
    }

    private static string[] GetCritterInspectionLines(CritterSnapshot critter, bool following) =>
    [
        following ? "ENTITY (FOLLOWING)" : "ENTITY",
        $"{GetCritterDisplayName(critter)} #{critter.Id.Value}   {critter.Position}",
        $"Behavior {GetCritterBehavior(critter)}",
        critter.MaximumEnergy > 0
            ? $"Energy {critter.Energy} / {critter.MaximumEnergy}"
            : "Energy None",
        $"Hungry {(critter.IsHungry ? "yes" : "no")}   Reproduce {(critter.CanReproduce ? "ready" : "no")}",
        $"Habitat {CritterHabitats.GetHabitat(critter.Species)}",
        $"Diet {GetCritterDiet(critter.Species)}",
        .. critter.Species is CritterSpecies.Ape or CritterSpecies.ApeSailor or CritterSpecies.UndeadApe
            ? new[] { GetPlagueDescription(critter) } : Array.Empty<string>(),
    ];

    private static string GetCritterDisplayName(CritterSnapshot critter) =>
        critter.IsColonist ? "Colonist Ape" : GetCritterDisplayName(critter.Species);

    private static string GetPlagueDescription(CritterSnapshot critter) =>
        critter.Species is CritterSpecies.UndeadApe ? "Undead: contagious, cannot reproduce" :
        critter.IsPlagueImmune ? "Plague immune: ID divisible by 5" :
        critter.Plague switch
        {
            PlagueKind.Plague => "Plague: -1 energy / 10 seconds",
            PlagueKind.Zombie => "Zombie plague: -1 energy / 10s, rises on death",
            _ => "Plague: susceptible",
        };

    private static string GetCritterDiet(CritterSpecies species) => species switch
    {
        CritterSpecies.UndeadApe => "living apes and ape sailors",
        CritterSpecies.Plankton => "ambient nutrients",
        CritterSpecies.Worm => "deep-ocean, shallow, and beach detritus",
        CritterSpecies.Trilobite => "deep-sea detritus",
        CritterSpecies.SeaScorpion => "broad aquatic and shoreline prey; adjacent worms, trilobites, and nautiluses",
        CritterSpecies.Nautilus => "plankton plus ocean and deep-ocean forage",
        CritterSpecies.Squid => "aquatic prey and grazers entering shallows; adjacent worms and nautiluses",
        CritterSpecies.SquidEgg => "hatches when squid prey approaches",
        CritterSpecies.Jellyfish => "plankton only",
        CritterSpecies.Fish => "shallow/freshwater forage and plankton",
        CritterSpecies.Newt => "freshwater food; feeds and breeds in swamps and jungles",
        CritterSpecies.MegaToad => "broad prey; adjacent worms/cannibalism; fights sea scorpions and squid",
        CritterSpecies.Therapsid => "prefers wetland forage; fish as fallback",
        CritterSpecies.Monkey => "swamp and jungle foliage only",
        CritterSpecies.Ape => "prey except plankton, worms, and its civilization; plus wetland foliage",
        CritterSpecies.ApeSailor => "sea life except plankton and worms",
        CritterSpecies.Deer => "grassland and forest foliage",
        CritterSpecies.Elk => "grassland, tundra, and taiga foliage",
        CritterSpecies.Gazelle => "arid, forest, and grassland foliage",
        CritterSpecies.Wolf => "broad terrestrial prey; therapsids last; never toads",
        CritterSpecies.Crab => "beach and shallow detritus",
        CritterSpecies.ToothedWhale => "marine predators; land prey in shallows; adjacent nautiluses/crabs",
        CritterSpecies.BaleenWhale => "plankton only",
        _ => "not implemented",
    };

    private static string GetCritterBehavior(CritterSnapshot critter)
    {
        if (critter.IsColonist)
        {
            return critter.ColonistDestination is { } destination
                ? $"Traveling to X {destination.X}, Y {destination.Y} to found a village"
                : "Traveling to found a village";
        }
        if (critter.Species is CritterSpecies.UndeadApe)
        {
            return "Hunting living apes and spreading zombie plague";
        }
        if (critter.CanReproduce)
        {
            return critter.Species is CritterSpecies.Ape or CritterSpecies.ApeSailor
                ? "Returning to found or reproduce at a village"
                : "Seeking reproductive space";
        }
        if (critter.IsHungry)
        {
            return critter.Species switch
            {
                CritterSpecies.Plankton => "Absorbing nutrients",
                CritterSpecies.Worm => "Seeking detritus or fleeing predators",
                CritterSpecies.Trilobite => "Seeking detritus or fleeing predators",
                CritterSpecies.SeaScorpion => "Hunting aquatic prey",
                CritterSpecies.Nautilus => "Roaming deep water, hunting plankton, or fleeing",
                CritterSpecies.Squid => "Hunting aquatic prey",
                CritterSpecies.SquidEgg => "Waiting for nearby prey",
                CritterSpecies.Jellyfish => "Seeking plankton",
                CritterSpecies.Fish => "Seeking shallow or freshwater forage, hunting, or fleeing",
                CritterSpecies.Newt => "Seeking freshwater or wetland foliage",
                CritterSpecies.MegaToad => "Ambushing aquatic prey",
                CritterSpecies.Therapsid => "Hunting or foraging in lush wetlands",
                CritterSpecies.Monkey => "Seeking wetland foliage or fleeing predators",
                CritterSpecies.Ape => "Hunting below 10 village food, wetland foraging, or returning",
                CritterSpecies.ApeSailor => "Hunting at sea or returning food to harbor",
                CritterSpecies.Deer => "Grazing or fleeing predators",
                CritterSpecies.Elk => "Grazing or fleeing predators",
                CritterSpecies.Gazelle => "Grazing or fleeing predators",
                CritterSpecies.Wolf => "Hunting broad terrestrial prey",
                CritterSpecies.Crab => "Seeking coastal detritus",
                CritterSpecies.ToothedWhale => "Hunting ocean predators",
                CritterSpecies.BaleenWhale => "Filtering plankton",
                _ => "Seeking food",
            };
        }

        return critter.Species switch
        {
            CritterSpecies.Plankton => "Drifting and feeding",
            CritterSpecies.Worm => "Scavenging while watching for predators",
            CritterSpecies.Trilobite => "Scavenging while watching for predators",
            CritterSpecies.SeaScorpion => "Patrolling coastal waters",
            CritterSpecies.Nautilus => "Roaming deep water for forage and plankton",
            CritterSpecies.Squid => "Patrolling for aquatic prey",
            CritterSpecies.SquidEgg => "Drifting with the currents",
            CritterSpecies.Jellyfish => "Drifting and hunting",
            CritterSpecies.Fish => "Foraging in freshwater and shallows",
            CritterSpecies.Newt => "Foraging in wetlands and freshwater",
            CritterSpecies.MegaToad => "Patrolling the water's edge",
            CritterSpecies.Therapsid => "Patrolling terrestrial hunting grounds",
            CritterSpecies.Monkey => "Foraging while watching for predators",
            CritterSpecies.Ape => "Hunting below 10 village food or wetland foraging",
            CritterSpecies.ApeSailor => "Patrolling village waters",
            CritterSpecies.Deer => "Roaming while watching for predators",
            CritterSpecies.Elk => "Slowly roaming while watching for predators",
            CritterSpecies.Gazelle => "Roaming while watching for predators",
            CritterSpecies.Wolf => "Quickly patrolling its hunting grounds",
            CritterSpecies.Crab => "Foraging along the coast",
            CritterSpecies.ToothedWhale => "Patrolling for ocean predators",
            CritterSpecies.BaleenWhale => "Searching for plankton",
            _ => "Wandering",
        };
    }

    private GridPosition? ScreenToWorld(int screenX, int screenY)
    {
        if (screenX < 0 || screenY < 0 || screenY >= MapViewportHeight)
        {
            return null;
        }

        var mapX = screenX - MapOffsetX;
        var mapY = screenY - MapOffsetY;
        if (mapY < 0 || mapY >= _world.Height * TileSize ||
            (MapOffsetX > 0 && (mapX < 0 || mapX >= _world.Width * TileSize)))
        {
            return null;
        }

        var y = _cameraY + mapY / TileSize;
        if (y >= _world.Height)
        {
            return null;
        }

        return new GridPosition(Mod(_cameraX + FloorDiv(mapX, TileSize), _world.Width), y);
    }

    private void GenerateWorld()
    {
        ResetSpeedGuard();
        _world = WorldGenerator.Generate(new WorldGenerationOptions(_preset, _seed, MapType: _mapType));
        _cameraX = 0;
        _cameraY = 0;
        _inspectedCritterId = default;
        _accumulator = TimeSpan.Zero;
        LifeSystem.SetEnabled(_world, _lifeEnabled);
        foreach (var history in _populationHistory.Values)
        {
            history.Clear();
        }
        _sickApeHistory.Clear();
        RecordPopulationSample();
    }

    private void HandleSetupMenu(KeyboardState keyboard)
    {
        if (WasPressed(keyboard, Keys.M) || WasPressed(keyboard, Keys.Escape))
        {
            _setupMenuOpen = false;
            return;
        }
        if (WasPressed(keyboard, Keys.Up) || WasPressed(keyboard, Keys.W))
        {
            _setupRow = Mod(_setupRow - 1, 4);
            FinishSeedTyping();
        }
        else if (WasPressed(keyboard, Keys.Down) || WasPressed(keyboard, Keys.S))
        {
            _setupRow = Mod(_setupRow + 1, 4);
            FinishSeedTyping();
        }

        var direction = WasPressed(keyboard, Keys.Left) || WasPressed(keyboard, Keys.A) ? -1 :
            WasPressed(keyboard, Keys.Right) || WasPressed(keyboard, Keys.D) ? 1 : 0;
        if (direction != 0)
        {
            if (_setupRow == 0)
            {
                _menuMapTypeIndex = Mod(_menuMapTypeIndex + direction, MenuMapTypes.Length);
            }
            else if (_setupRow == 1)
            {
                _menuSizeIndex = Mod(_menuSizeIndex + direction, MenuSizes.Length);
            }
            else if (_setupRow == 2)
            {
                _seed = direction > 0 ? _seed + 1 : _seed == 0 ? ulong.MaxValue : _seed - 1;
                FinishSeedTyping();
            }
        }

        if (_setupRow == 2)
        {
            HandleSeedTyping(keyboard);
        }

        if (WasPressed(keyboard, Keys.Enter) || (_setupRow == 3 && WasPressed(keyboard, Keys.Space)))
        {
            _mapType = MenuMapTypes[_menuMapTypeIndex];
            _preset = _mapType is WorldMapType.RingWorld ? WorldPreset.Ring : MenuSizes[_menuSizeIndex];
            GenerateWorld();
            _setupMenuOpen = false;
        }
    }

    private void DrawSetupMenu()
    {
        if (_spriteBatch is null || _pixel is null || _hudFont is null)
        {
            return;
        }

        var panelWidth = 520;
        var panelHeight = 250;
        var left = (GraphicsDevice.Viewport.Width - panelWidth) / 2;
        var top = (MapViewportHeight - panelHeight) / 2;
        _spriteBatch.Draw(_pixel, new Rectangle(left, top, panelWidth, panelHeight), new Color(12, 16, 20, 245));
        _spriteBatch.Draw(_pixel, new Rectangle(left, top, panelWidth, 2), new Color(126, 190, 213));
        _spriteBatch.DrawString(_hudFont, "NEW WORLD", new Vector2(left + 24, top + 20), new Color(126, 190, 213));

        var type = MenuMapTypes[_menuMapTypeIndex];
        var sizeText = type is WorldMapType.RingWorld
            ? $"Fixed  {WorldPreset.Ring.Width} x {WorldPreset.Ring.Height}"
            : $"{MenuSizes[_menuSizeIndex].Name}  {MenuSizes[_menuSizeIndex].Width} x {MenuSizes[_menuSizeIndex].Height}";
        string[] rows =
        [
            $"Map Shape       <  {GetMapTypeName(type)}  >",
            $"World Size      <  {sizeText}  >",
            $"Seed            <  {(_seedText.Length == 0 ? "_" : _seedText)}  >",
            "Generate World",
        ];
        for (var row = 0; row < rows.Length; row++)
        {
            var color = row == _setupRow ? Color.White : new Color(160, 168, 174);
            var prefix = row == _setupRow ? "> " : "  ";
            _spriteBatch.DrawString(_hudFont, prefix + rows[row], new Vector2(left + 24, top + 62 + row * 34), color);
        }
        var help = _setupRow == 2
            ? "Type digits to replace seed   Backspace edits   Enter generate"
            : "Arrows or WASD navigate   Enter generate   M/Esc close";
        _spriteBatch.DrawString(_hudFont, help,
            new Vector2(left + 24, top + panelHeight - 32), new Color(130, 140, 146));
    }

    private void HandleSeedTyping(KeyboardState keyboard)
    {
        var digit = GetPressedDigit(keyboard);
        if (digit >= 0)
        {
            var digitText = digit.ToString(CultureInfo.InvariantCulture);
            var candidate = _seedTypingActive ? _seedText + digitText : digitText;
            if (ulong.TryParse(candidate, NumberStyles.None, CultureInfo.InvariantCulture, out var seed))
            {
                _seedTypingActive = true;
                _seedText = candidate;
                _seed = seed;
            }
        }
        else if (WasPressed(keyboard, Keys.Back))
        {
            _seedTypingActive = true;
            _seedText = _seedText.Length > 0 ? _seedText[..^1] : string.Empty;
            _seed = _seedText.Length == 0
                ? 0
                : ulong.Parse(_seedText, CultureInfo.InvariantCulture);
        }
    }

    private void FinishSeedTyping()
    {
        _seedTypingActive = false;
        _seedText = _seed.ToString(CultureInfo.InvariantCulture);
    }

    private int GetPressedDigit(KeyboardState keyboard)
    {
        ReadOnlySpan<Keys> digitKeys =
        [
            Keys.D0, Keys.D1, Keys.D2, Keys.D3, Keys.D4,
            Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9,
        ];
        ReadOnlySpan<Keys> numberPadKeys =
        [
            Keys.NumPad0, Keys.NumPad1, Keys.NumPad2, Keys.NumPad3, Keys.NumPad4,
            Keys.NumPad5, Keys.NumPad6, Keys.NumPad7, Keys.NumPad8, Keys.NumPad9,
        ];
        for (var digit = 0; digit <= 9; digit++)
        {
            if (WasPressed(keyboard, digitKeys[digit]) || WasPressed(keyboard, numberPadKeys[digit]))
            {
                return digit;
            }
        }

        return -1;
    }

    private static ulong CreateRandomSeed()
    {
        ulong seed;
        do
        {
            seed = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(sizeof(ulong)));
        }
        while (seed == 0);

        return seed;
    }

    private static string GetMapTypeName(WorldMapType type) => type switch
    {
        WorldMapType.RingWorld => "Ring World",
        WorldMapType.AllOcean => "Water World",
        _ => type.ToString(),
    };

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
        var originX = MapOffsetX + screenX * TileSize;
        var originY = MapOffsetY + screenY * TileSize;
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
            new Rectangle(
                MapOffsetX + screenX * TileSize + inset,
                MapOffsetY + screenY * TileSize + inset,
                size,
                size),
            color);
    }

    private void DrawWolfDen(int screenX, int screenY, GridPosition position)
    {
        if (_spriteBatch is null || _pixel is null ||
            _world.GetWolfDenCharges(position) is not { } charges)
        {
            return;
        }

        var size = Math.Max(2, TileSize * 2 / 3);
        var inset = (TileSize - size) / 2;
        var originX = MapOffsetX + screenX * TileSize + inset;
        var originY = MapOffsetY + screenY * TileSize + inset;
        _spriteBatch.Draw(_pixel, new Rectangle(originX, originY, size, size), new Color(68, 45, 30));
        var opening = Math.Max(1, size / 2);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(originX + (size - opening) / 2, originY + size - opening, opening, opening),
            new Color(25, 20, 18));
        if (charges > 0)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(originX, originY, Math.Max(1, size / 4), Math.Max(1, size / 4)), new Color(210, 190, 145));
        }
    }

    private void DrawTeleporter(int screenX, int screenY, GridPosition position)
    {
        if (_spriteBatch is null || _pixel is null || !_world.HasTeleporter(position))
        {
            return;
        }

        var size = Math.Max(3, TileSize * 3 / 4);
        var inset = (TileSize - size) / 2;
        var x = MapOffsetX + screenX * TileSize + inset;
        var y = MapOffsetY + screenY * TileSize + inset;
        var capHeight = Math.Max(1, size / 4);
        var beamWidth = Math.Max(1, size / 3);
        var wallGray = new Color(112, 126, 136);
        var portalGreen = new Color(145, 255, 115);
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, capHeight), wallGray);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(
                x + (size - beamWidth) / 2,
                y + capHeight,
                beamWidth,
                Math.Max(1, size - capHeight * 2)),
            portalGreen);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(x, y + size - capHeight, size, capHeight),
            wallGray);
    }

    private void DrawApeStructure(int screenX, int screenY, GridPosition position)
    {
        if (_spriteBatch is null || _pixel is null ||
            _world.GetApeStructure(position) is not { } structure)
        {
            return;
        }

        var size = Math.Max(2, TileSize * 3 / 4);
        var inset = (TileSize - size) / 2;
        var x = MapOffsetX + screenX * TileSize + inset;
        var y = MapOffsetY + screenY * TileSize + inset;
        switch (structure)
        {
            case ApeStructureKind.Village:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size / 3, size, Math.Max(1, size * 2 / 3)), new Color(181, 139, 88));
                _spriteBatch.Draw(_pixel, new Rectangle(x + size / 4, y, Math.Max(1, size / 2), Math.Max(1, size / 2)), new Color(112, 69, 45));
                break;
            case ApeStructureKind.Farm:
                var aridFarm = _world.GetBiome(position) is Biome.Arid;
                var fieldColor = aridFarm
                    ? new Color(178, 126, 52)
                    : new Color(173, 151, 66);
                var cropColor = aridFarm
                    ? new Color(128, 88, 38)
                    : new Color(71, 112, 48);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), fieldColor);
                var rowWidth = Math.Max(1, size / 5);
                for (var row = 1; row < size; row += Math.Max(2, rowWidth * 2))
                {
                    _spriteBatch.Draw(
                        _pixel,
                        new Rectangle(x, y + row, size, rowWidth),
                        cropColor);
                }
                break;
            case ApeStructureKind.RicePaddy:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), new Color(80, 139, 112));
                var paddyRowWidth = Math.Max(1, size / 6);
                for (var row = 1; row < size; row += Math.Max(2, paddyRowWidth * 2))
                {
                    _spriteBatch.Draw(_pixel, new Rectangle(x, y + row, size, paddyRowWidth), new Color(170, 190, 91));
                }
                break;
            case ApeStructureKind.Orchard:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), new Color(103, 75, 45));
                var treeSize = Math.Max(1, size / 3);
                _spriteBatch.Draw(_pixel, new Rectangle(x, y, treeSize, treeSize), new Color(61, 125, 55));
                _spriteBatch.Draw(_pixel, new Rectangle(x + size - treeSize, y, treeSize, treeSize), new Color(61, 125, 55));
                _spriteBatch.Draw(_pixel, new Rectangle(x + (size - treeSize) / 2, y + size - treeSize, treeSize, treeSize), new Color(61, 125, 55));
                break;
            case ApeStructureKind.Aquaculture:
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x, y, size, size),
                    new Color(48, 128, 145));
                var penWidth = Math.Max(1, size / 6);
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x + size / 4, y, penWidth, size),
                    new Color(142, 205, 177));
                _spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x + size * 3 / 4, y, penWidth, size),
                    new Color(142, 205, 177));
                break;
            case ApeStructureKind.LumberCamp:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size / 2, size, Math.Max(1, size / 2)), new Color(111, 72, 42));
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size / 4, size, Math.Max(1, size / 5)), new Color(177, 126, 70));
                _spriteBatch.Draw(_pixel, new Rectangle(x + size / 5, y, Math.Max(1, size / 6), size), new Color(78, 53, 35));
                break;
            case ApeStructureKind.NavalDistrict:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size / 3, size, Math.Max(1, size / 3)), new Color(117, 77, 46));
                _spriteBatch.Draw(_pixel, new Rectangle(x + size / 4, y, Math.Max(1, size / 5), size), new Color(190, 155, 98));
                break;
            case ApeStructureKind.ResidentialDistrict:
                _spriteBatch.Draw(_pixel, new Rectangle(x, y + size / 3, size, Math.Max(1, size * 2 / 3)), new Color(151, 105, 72));
                _spriteBatch.Draw(_pixel, new Rectangle(x + size / 5, y, Math.Max(1, size * 3 / 5), Math.Max(1, size / 2)), new Color(92, 61, 45));
                break;
        }
    }

    private static string GetApeStructureDisplayName(ApeStructureKind structure) => structure switch
    {
        ApeStructureKind.Village => "Ape Village",
        ApeStructureKind.Farm => "Farm",
        ApeStructureKind.RicePaddy => "Rice Paddy",
        ApeStructureKind.Orchard => "Orchard",
        ApeStructureKind.Aquaculture => "Aquaculture",
        ApeStructureKind.LumberCamp => "Lumber Camp",
        ApeStructureKind.NavalDistrict => "Harbor",
        ApeStructureKind.ResidentialDistrict => "Residential District",
        _ => structure.ToString(),
    };

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
                    MapOffsetX + screenX * TileSize + inset,
                    MapOffsetY + screenY * TileSize + inset,
                    Math.Max(1, TileSize - inset * 2),
                    Math.Max(1, TileSize - inset * 2)),
                wave.Kind is WaveKind.Tsunami
                    ? new Color(45, 180, 255, 210)
                    : new Color(255, 222, 145, 185));
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

    private static int FloorDiv(int value, int divisor) =>
        value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

    private Color GetTileColor(
        GridPosition position,
        Terrain terrain,
        Biome biome,
        TemperatureBand temperatureBand)
    {
        if (terrain is Terrain.RingWorldWall)
        {
            return new Color(112, 126, 136);
        }

        var cover = _world.GetSurfaceCover(position);
        if (cover is SurfaceCover.Lava)
        {
            var glow = ((_world.Tick + position.X * 3L + position.Y * 5L) / 5) % 2 == 0;
            return glow ? new Color(245, 72, 20) : new Color(190, 35, 15);
        }
        var elevation = _world.GetElevation(position);
        var color = LifeSystem.IsStoneBiome(_world, position)
            ? GetStoneColor(terrain)
            : GetTerrainColor(terrain, biome, temperatureBand, elevation);
        return ScaleColor(color, TerrainShading.GetBrightness(terrain, elevation, _world.SeaLevel));
    }

    private static Color GetTerrainColor(
        Terrain terrain,
        Biome biome,
        TemperatureBand temperatureBand,
        float elevation) => terrain switch
    {
        Terrain.DeepOcean or Terrain.Ocean or Terrain.Shallows => GetOceanColor(terrain, temperatureBand),
        Terrain.Beach => GetBeachColor(temperatureBand),
        Terrain.Plains => GetBiomeLandColor(biome),
        Terrain.Lowlands => ScaleColor(GetBiomeLandColor(biome), 0.92f),
        Terrain.Canyon => ScaleColor(GetBiomeLandColor(biome), 0.84f),
        Terrain.Trench => ScaleColor(GetBiomeLandColor(biome), 0.76f),
        Terrain.Hills => ScaleColor(GetBiomeLandColor(biome), 0.92f),
        Terrain.Mountain => GetMountainColor(elevation, biome is Biome.Arctic),
        Terrain.Ice => new Color(165, 220, 235),
        Terrain.RingWorldWall => new Color(112, 126, 136),
        _ => Color.Magenta,
    };

    private static Color GetStoneColor(Terrain terrain) => terrain switch
    {
        Terrain.Beach => new Color(132, 129, 122),
        Terrain.Plains => new Color(122, 119, 113),
        Terrain.Lowlands or Terrain.Hills => ScaleColor(new Color(122, 119, 113), 0.92f),
        Terrain.Canyon => ScaleColor(new Color(122, 119, 113), 0.84f),
        Terrain.Trench => ScaleColor(new Color(122, 119, 113), 0.76f),
        _ => new Color(104, 101, 96),
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

    private static Color GetBeachColor(TemperatureBand temperatureBand) => temperatureBand switch
    {
        TemperatureBand.Freezing => new Color(220, 224, 214),
        TemperatureBand.Cold => new Color(204, 198, 169),
        TemperatureBand.Hot => new Color(226, 181, 103),
        _ => new Color(215, 195, 135),
    };

    private static Color GetMountainColor(float elevation, bool snowy)
    {
        var elevationShade = Math.Clamp(
            (elevation - TerrainClassifier.MountainElevationThreshold) /
                (SimulationWorld.MaximumGroundElevation -
                    TerrainClassifier.MountainElevationThreshold),
            0f,
            1f);
        var baseColor = snowy
            ? new Color(210, 222, 225)
            : new Color(78, 72, 68);
        var brightness = snowy
            ? 0.88f + 0.25f * elevationShade
            : 0.90f + 0.75f * elevationShade;
        return ScaleColor(baseColor, brightness);
    }

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

    private static Color GetCritterColor(CritterSnapshot critter) =>
        critter.IsDamageFlashing ? Color.White :
        critter.Species is CritterSpecies.UndeadApe ? GetCritterColor(critter.Species) :
        critter.Plague switch
        {
            PlagueKind.Plague => new Color(205, 195, 55),
            PlagueKind.Zombie => new Color(170, 85, 190),
            _ when critter.IsColonist => new Color(215, 180, 65),
            _ => GetCritterColor(critter.Species),
        };

    private static Color GetCritterColor(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => new Color(160, 255, 180),
        CritterSpecies.Jellyfish => new Color(180, 160, 240),
        CritterSpecies.Worm => new Color(210, 105, 145),
        CritterSpecies.Trilobite => new Color(135, 92, 55),
        CritterSpecies.SeaScorpion => new Color(190, 145, 105),
        CritterSpecies.Nautilus => new Color(205, 185, 140),
        CritterSpecies.Squid => new Color(165, 70, 145),
        CritterSpecies.SquidEgg => new Color(225, 185, 230),
        CritterSpecies.Fish => new Color(80, 220, 235),
        CritterSpecies.Newt => new Color(245, 145, 55),
        CritterSpecies.MegaToad => new Color(90, 155, 65),
        CritterSpecies.Therapsid => new Color(214, 242, 162),
        CritterSpecies.Monkey => new Color(190, 135, 85),
        CritterSpecies.Ape => new Color(125, 95, 70),
        CritterSpecies.ApeSailor => new Color(75, 115, 155),
        CritterSpecies.UndeadApe => new Color(95, 190, 100),
        CritterSpecies.Deer => new Color(181, 133, 82),
        CritterSpecies.Elk => new Color(112, 78, 48),
        CritterSpecies.Gazelle => new Color(220, 175, 95),
        CritterSpecies.Wolf => new Color(125, 130, 135),
        CritterSpecies.Crab => new Color(255, 80, 80),
        CritterSpecies.ToothedWhale => new Color(58, 92, 120),
        CritterSpecies.BaleenWhale => new Color(105, 135, 155),
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
        WorldTool.Elevation => $"L raise; R lower; -/= {_terrainMagnitudeIndex / 10f:0.0}",
        WorldTool.Smoothing => $"L smooth; -/= {_terrainMagnitudeIndex / 10f:0.0}",
        WorldTool.SeaLevel => _world.HasOceans ? "L raise; R lower" : "No oceans",
        WorldTool.OceanSeed => "L move seed; R add seed",
        WorldTool.Temperature => "L warmer; R cooler",
        WorldTool.Moisture => "L wetter; R drier",
        WorldTool.EvolutionChance =>
            $"L +0.5%; R -0.5%   {_world.EvolutionChancePercent:0.0}%",
        WorldTool.Seasons => _world.Body is WorldBody.RingWorld
            ? "Fixed climate"
            : "L on; R off",
        WorldTool.Life => _world.LifeEnabled
            ? "L on; R barren"
            : "L restore; R off",
        WorldTool.Volcano => "L spawn vent",
        WorldTool.Meteor => $"L strike; -/= {_eventMagnitudeIndex / 10f:0.0}",
        WorldTool.Tsunami => $"L ocean; -/= {_eventMagnitudeIndex / 10f:0.0}",
        WorldTool.WatershedShift => "L reroute river",
        WorldTool.Evolve => "L evolve; R devolve",
        WorldTool.Plague => "L infect ape",
        WorldTool.ZombiePlague => "L infect ape",
        WorldTool.NaturalEvents => "L on; R off",
        WorldTool.River => "L add; R remove",
        WorldTool.Plankton => "L spawn: deep ocean",
        WorldTool.Jellyfish => "L spawn: water",
        WorldTool.Fish => "L spawn: water",
        WorldTool.Worm => "L spawn: ocean / shallows",
        WorldTool.Trilobite => "L spawn: ocean / shallows",
        WorldTool.SeaScorpion => "L spawn: water / beach",
        WorldTool.Nautilus => "L spawn: water",
        WorldTool.Squid => "L spawn: water",
        WorldTool.SquidEgg => "L spawn egg",
        WorldTool.Newt => "L spawn: amphibious",
        WorldTool.MegaToad => "L spawn: land / shallows",
        WorldTool.Therapsid => "L spawn: land",
        WorldTool.Monkey => "L spawn: land / jungle",
        WorldTool.Ape => "L spawn: land",
        WorldTool.Deer => "L spawn: grass / forest",
        WorldTool.Elk => "L spawn: grass / tundra / taiga",
        WorldTool.Gazelle => "L spawn: grass / arid",
        WorldTool.Wolf => "L spawn: land",
        WorldTool.Crab => "L spawn: coast / land",
        WorldTool.ToothedWhale => "L spawn: ocean",
        WorldTool.BaleenWhale => "L spawn: ocean",
        WorldTool.WolfDen => "L place; R remove",
        WorldTool.Teleporter => "L place; R remove",
        WorldTool.Stone => "L place; R clear",
        WorldTool.Lava => "L add; R clear",
        WorldTool.JumpStart => "L seed plankton",
        WorldTool.Colonist => "L village: auto; tile: target",
        WorldTool.Population => _populationWindowOpen
            ? "Open; R close"
            : "L open counts",
        WorldTool.Controls => _controlsWindowOpen
            ? "Open; R close or ?"
            : "L open or ?",
        WorldTool.Inspect => _inspectedCritterId.IsValid
            ? "Following; R clear"
            : "L follow; R clear",
        _ => string.Empty,
    };

    private static string GetToolName(WorldTool tool) => tool switch
    {
        WorldTool.JumpStart => "Jump Start",
        WorldTool.ZombiePlague => "Zombie Plague",
        _ => tool.ToString(),
    };

    private static string GetToolCategoryName(ToolCategory category) => category switch
    {
        ToolCategory.WorldTools => "WORLD TOOLS",
        ToolCategory.TerrainTools => "TERRAIN TOOLS",
        ToolCategory.CritterTools => "CRITTER TOOLS",
        ToolCategory.BuildingTools => "BUILDING TOOLS",
        ToolCategory.Events => "EVENTS",
        ToolCategory.Other => "OTHER",
        _ => category.ToString().ToUpperInvariant(),
    };

    private enum ToolCategory
    {
        WorldTools,
        TerrainTools,
        CritterTools,
        BuildingTools,
        Events,
        Other,
    }

    private enum WorldTool
    {
        Elevation,
        Smoothing,
        SeaLevel,
        OceanSeed,
        Temperature,
        Moisture,
        EvolutionChance,
        Seasons,
        Life,
        Volcano,
        Meteor,
        Tsunami,
        WatershedShift,
        Evolve,
        NaturalEvents,
        River,
        Plankton,
        Jellyfish,
        Worm,
        Trilobite,
        SeaScorpion,
        Nautilus,
        Squid,
        SquidEgg,
        Fish,
        Newt,
        MegaToad,
        Therapsid,
        Monkey,
        Ape,
        Deer,
        Elk,
        Gazelle,
        Wolf,
        Crab,
        ToothedWhale,
        BaleenWhale,
        WolfDen,
        Teleporter,
        Stone,
        Lava,
        JumpStart,
        Colonist,
        Population,
        Controls,
        Inspect,
        Plague,
        ZombiePlague,
    }
}
