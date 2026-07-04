from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent

# Window and rendering
WINDOW_WIDTH = 1400
WINDOW_HEIGHT = 800
WINDOW_TITLE = "Newt"
HUD_HEIGHT = 20
TARGET_FPS = 60
BACKGROUND_COLOR = (0, 0, 0)
BUILDING_COLOR = (200, 50, 50)

# World setup
TILE_SIZE = 10
INITIAL_TERRAIN = "ocean"
DEFAULT_PAINT_TERRAIN = "stone"
DEFAULT_BRUSH_SIZE = 0

# Simulation timing, in seconds
EROSION_INTERVAL = 0.25
LIFE_INTERVAL = 0.35
IMPACT_INTERVAL = 5.0
IMPACT_CHANCE = 0.001
TECTONIC_INTERVAL = 5.0
POLAR_INTERVAL = 10.0

# Simulation controls
DEFAULT_GAME_SPEED = 1.0

# Assets
SPRITE_PATHS = {
    "crab": BASE_DIR / "sprites" / "crab.png",
    "fish": BASE_DIR / "sprites" / "fish.png",
}
