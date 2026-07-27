from pathlib import Path


BASE_DIR = Path(__file__).resolve().parent

# Window and rendering
WINDOW_TITLE = "Newt"
HUD_HEIGHT = 60
TARGET_FPS = 60
BACKGROUND_COLOR = (0, 0, 0)
BUILDING_COLOR = (200, 50, 50)

# World setup
TILE_SIZE = 16
OVERVIEW_TILE_SIZE = 8
ZOOMED_IN_TILE_SIZE = 32
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
    "ape": BASE_DIR / "sprites" / "ape.png",
    "ape_sailor": BASE_DIR / "sprites" / "ape_sailor.png",
    "ape_warrior": BASE_DIR / "sprites" / "ape_warrior.png",
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

SOUND_PATHS = {
    "zoom_in": BASE_DIR / "sounds" / "zoom_in[zoom_in].wav",
    "zoom_out": BASE_DIR / "sounds" / "zoom_out[zoom_out].wav",
}
