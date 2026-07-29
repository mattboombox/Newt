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
    "ape": BASE_DIR / "critter_sprites" / "ape.png",
    "ape_sailor": BASE_DIR / "critter_sprites" / "ape_sailor.png",
    "ape_warrior": BASE_DIR / "critter_sprites" / "ape_warrior.png",
    "crab": BASE_DIR / "critter_sprites" / "crab.png",
    "deer": BASE_DIR / "critter_sprites" / "deer.png",
    "dog": BASE_DIR / "critter_sprites" / "dog.png",
    "fish": BASE_DIR / "critter_sprites" / "fish.png",
    "giga_slug": BASE_DIR / "critter_sprites" / "mega_slug.png",
    "jelly_fish": BASE_DIR / "critter_sprites" / "jelly_fish.png",
    "land_kraken": BASE_DIR / "critter_sprites" / "land_kraken.png",
    "mega_spider": BASE_DIR / "critter_sprites" / "mega_spider.png",
    "nautilus": BASE_DIR / "critter_sprites" / "nautilus.png",
    "newt": BASE_DIR / "critter_sprites" / "newt.png",
    "plankton": BASE_DIR / "critter_sprites" / "plankton.png",
    "sea_scorpion": BASE_DIR / "critter_sprites" / "sea_scorpion.png",
    "snail": BASE_DIR / "critter_sprites" / "snail.png",
    "sperm_whale": BASE_DIR / "critter_sprites" / "sperm_whale.png",
    "squid": BASE_DIR / "critter_sprites" / "squid.png",
    "squid_egg": BASE_DIR / "critter_sprites" / "squid_egg.png",
    "therapsid": BASE_DIR / "critter_sprites" / "therapsid.png",
    "trilobite": BASE_DIR / "critter_sprites" / "trilobite.png",
    "whale": BASE_DIR / "critter_sprites" / "whale.png",
    "wolf": BASE_DIR / "critter_sprites" / "wolf.png",
    "ape_farm": BASE_DIR / "building_sprites" / "ape_farm.png",
    "ape_fort": BASE_DIR / "building_sprites" / "ape_fort.png",
    "ape_harbor": BASE_DIR / "building_sprites" / "ape_harbor.png",
    "ape_hut": BASE_DIR / "building_sprites" / "ape_hut.png",
    "ape_village": BASE_DIR / "building_sprites" / "ape_village.png",
    "den": BASE_DIR / "building_sprites" / "den.png",
    "ruins": BASE_DIR / "building_sprites" / "ruins.png",
    "web": BASE_DIR / "building_sprites" / "web.png",
}

SOUND_PATHS = {
    "zoom_in": BASE_DIR / "sounds" / "zoom_in[zoom_in].wav",
    "zoom_out": BASE_DIR / "sounds" / "zoom_out[zoom_out].wav",
}
