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
EROSION_INTERVAL = 0.10
LIFE_INTERVAL = 0.35
EVOLUTION_CHANCE = 0.25
IMPACT_INTERVAL = 5.0
IMPACT_CHANCE = 0.001
TECTONIC_INTERVAL = 5.0
POLAR_INTERVAL = 10.0

# Simulation controls
DEFAULT_GAME_SPEED = 1.0

# Web viewer
WEB_MIRROR_ENABLED = True
WEB_MIRROR_HOST = "127.0.0.1"
WEB_MIRROR_PORT = 8765
WEB_MIRROR_FPS = 5

# Assets
SPRITE_PATHS = {
    "crab": BASE_DIR / "sprites" / "crab.png",
    "deer": BASE_DIR / "sprites" / "deer.png",
    "fish": BASE_DIR / "sprites" / "fish.png",
    "giga_slug": BASE_DIR / "sprites" / "mega_slug.png",
    "land_kraken": BASE_DIR / "sprites" / "land_kraken.png",
    "mega_spider": BASE_DIR / "sprites" / "mega_spider.png",
    "nautilus": BASE_DIR / "sprites" / "nautilus.png",
    "newt": BASE_DIR / "sprites" / "newt.png",
    "plankton": BASE_DIR / "sprites" / "plankton.png",
    "sea_scorpion": BASE_DIR / "sprites" / "sea_scorpion.png",
    "snail": BASE_DIR / "sprites" / "snail.png",
    "sperm_whale": BASE_DIR / "sprites" / "sperm_whale.png",
    "squid": BASE_DIR / "sprites" / "squid.png",
    "squid_egg": BASE_DIR / "sprites" / "squid_egg.png",
    "therapsid": BASE_DIR / "sprites" / "therapsid.png",
    "trilobite": BASE_DIR / "sprites" / "trilobite.png",
    "whale": BASE_DIR / "sprites" / "whale.png",
    "wolf": BASE_DIR / "sprites" / "wolf.png",
}
