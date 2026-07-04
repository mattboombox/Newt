import sys

import pygame

from brush import paint_radius
from config import (
    BACKGROUND_COLOR,
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
    SPRITE_PATHS,
    TARGET_FPS,
    TECTONIC_INTERVAL,
    TILE_SIZE,
    WINDOW_HEIGHT,
    WINDOW_TITLE,
    WINDOW_WIDTH,
)
from critter import CRITTER_ORDER
from events import update_events
from input import handle_input
from render import render
from world import World


# -----------------------------
# Runtime game state
# -----------------------------
class Game:
    def __init__(self):
        self.running = True
        self.paused = False

        self.tile_size = TILE_SIZE

        cols = WINDOW_WIDTH // self.tile_size
        rows = (WINDOW_HEIGHT - HUD_HEIGHT) // self.tile_size
        self.world = World(cols, rows, INITIAL_TERRAIN)

        self.selected_tile = None
        self.hovered_tile = None

        self.current_terrain = DEFAULT_PAINT_TERRAIN
        self.current_critter = CRITTER_ORDER[0]
        self.left_mouse_held = False
        self.brush_size = DEFAULT_BRUSH_SIZE

        self.critters = []
        self.tsunamis = []
        self.volcanoes = []

        # Timers are live state; their interval values come from config.py.
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

        self.speed = DEFAULT_GAME_SPEED
        self.sprites = {}


# -----------------------------
# Systems
# -----------------------------
def update(game, dt):
    mx, my = pygame.mouse.get_pos()
    game.hovered_tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

    if game.paused:
        return

    if game.left_mouse_held and game.hovered_tile is not None:
        paint_radius(game, game.hovered_tile, game.current_terrain, game.brush_size)

    update_events(game, dt)

    for critter in game.critters:
        critter.update(game.world, dt)


# -----------------------------
# Sprites
# -----------------------------
def load_sprite(path, size):
    img = pygame.image.load(path).convert_alpha()
    return pygame.transform.scale(img, size)


def load_sprites(tile_size):
    return {
        name: load_sprite(path, (tile_size, tile_size))
        for name, path in SPRITE_PATHS.items()
    }


# -----------------------------
# Main
# -----------------------------
def main():
    pygame.init()

    screen = pygame.display.set_mode((WINDOW_WIDTH, WINDOW_HEIGHT))
    pygame.display.set_caption(WINDOW_TITLE)
    clock = pygame.time.Clock()

    game = Game()
    game.sprites = load_sprites(game.tile_size)

    while game.running:
        dt = clock.tick(TARGET_FPS) / 1000.0
        dt *= game.speed

        handle_input(game)
        update(game, dt)
        render(screen, game, BACKGROUND_COLOR)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()
