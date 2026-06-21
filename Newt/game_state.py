from config import (
    DEFAULT_BRUSH_SIZE,
    DEFAULT_GAME_SPEED,
    DEFAULT_PAINT_TERRAIN,
    EROSION_INTERVAL,
    HUD_HEIGHT,
    IMPACT_CHANCE,
    IMPACT_INTERVAL,
    INITIAL_TERRAIN,
    LIFE_INTERVAL,
    POLAR_INTERVAL,
    TECTONIC_INTERVAL,
    TILE_SIZE,
    WINDOW_HEIGHT,
    WINDOW_WIDTH,
)
from world import World


class GameState:
    """Container for all mutable simulation and input state."""

    def __init__(self):
        # Application state
        self.running = True
        self.paused = False
        self.speed = DEFAULT_GAME_SPEED

        # World state
        self.tile_size = TILE_SIZE
        cols = WINDOW_WIDTH // self.tile_size
        rows = (WINDOW_HEIGHT - HUD_HEIGHT) // self.tile_size
        self.world = World(cols, rows, INITIAL_TERRAIN)

        # Input and editor state
        self.selected_tile = None
        self.hovered_tile = None
        self.current_terrain = DEFAULT_PAINT_TERRAIN
        self.left_mouse_held = False
        self.brush_size = DEFAULT_BRUSH_SIZE

        # Simulation entities and effects
        self.critters = []
        self.tsunamis = []
        self.volcanoes = []

        # Event-system timers
        self.erosion_timer = 0.0
        self.erosion_interval = EROSION_INTERVAL

        self.life_timer = 0.0
        self.life_interval = LIFE_INTERVAL

        self.impact_timer = 0.0
        self.impact_interval = IMPACT_INTERVAL
        self.impact_chance = IMPACT_CHANCE

        self.tectonic_timer = 0.0
        self.tectonic_interval = TECTONIC_INTERVAL

        self.polar_timer = POLAR_INTERVAL
        self.polar_interval = POLAR_INTERVAL

        # Loaded runtime resources
        self.sprites = {}