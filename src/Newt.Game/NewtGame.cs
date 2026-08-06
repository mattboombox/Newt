using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Newt.Simulation;

namespace Newt.Game;

/// <summary>MonoGame host responsible only for input, timing, and presentation.</summary>
public sealed class NewtGame : Microsoft.Xna.Framework.Game
{
    private const int TileSize = 8;
    private static readonly TimeSpan SimulationStep = TimeSpan.FromSeconds(1d / SimulationWorld.TicksPerSecond);
    private readonly GraphicsDeviceManager _graphics;
    private readonly SimulationWorld _world;
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private TimeSpan _accumulator;

    public NewtGame()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        _world = CreateDemonstrationWorld();
        _graphics.PreferredBackBufferWidth = _world.Width * TileSize;
        _graphics.PreferredBackBufferHeight = _world.Height * TileSize;
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
            Keyboard.GetState().IsKeyDown(Keys.Escape))
        {
            Exit();
            return;
        }

        _accumulator += gameTime.ElapsedGameTime;
        while (_accumulator >= SimulationStep)
        {
            _world.AdvanceOneTick();
            _accumulator -= SimulationStep;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);
        if (_spriteBatch is null || _pixel is null)
        {
            return;
        }

        _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        for (var y = 0; y < _world.Height; y++)
        {
            for (var x = 0; x < _world.Width; x++)
            {
                var terrain = _world.GetTerrain(new GridPosition(x, y));
                _spriteBatch.Draw(_pixel, new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize), GetTerrainColor(terrain));
            }
        }

        for (var index = 0; index < _world.CritterCount; index++)
        {
            var critter = _world.GetCritter(index);
            _spriteBatch.Draw(
                _pixel,
                new Rectangle(critter.Position.X * TileSize, critter.Position.Y * TileSize, TileSize, TileSize),
                GetCritterColor(critter.Species));
        }

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private static SimulationWorld CreateDemonstrationWorld()
    {
        var world = new SimulationWorld(80, 48, Terrain.Ocean, seed: 20260805);
        for (var y = 0; y < world.Height; y++)
        {
            for (var x = 0; x < world.Width; x++)
            {
                if (y > 27)
                {
                    world.SetTerrain(new GridPosition(x, y), Terrain.Grass);
                }
                else if (y is 26 or 27)
                {
                    world.SetTerrain(new GridPosition(x, y), Terrain.Shallows);
                }
            }
        }

        world.AddCritter(CritterSpecies.Plankton, new GridPosition(20, 12));
        world.AddCritter(CritterSpecies.Crab, new GridPosition(30, 26));
        world.AddCritter(CritterSpecies.Ape, new GridPosition(40, 36));
        return world;
    }

    private static Color GetTerrainColor(Terrain terrain) => terrain switch
    {
        Terrain.Ocean => new Color(24, 68, 128),
        Terrain.Shallows => new Color(66, 145, 180),
        Terrain.Grass => new Color(55, 130, 70),
        _ => Color.Magenta,
    };

    private static Color GetCritterColor(CritterSpecies species) => species switch
    {
        CritterSpecies.Plankton => new Color(160, 255, 180),
        CritterSpecies.Crab => new Color(255, 80, 80),
        CritterSpecies.Ape => new Color(145, 105, 70),
        _ => Color.Magenta,
    };
}
