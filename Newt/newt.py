import sys
import pygame
from world import World
from brush import paint_radius
from render import render
from input import handle_input
from critter import Critter


# -----------------------------
# Config
# -----------------------------
WINDOW_WIDTH = 1200
WINDOW_HEIGHT = 800
WINDOW_TITLE = "Newt"
HUD_HEIGHT = 18
TARGET_FPS = 60
BACKGROUND_COLOR = (0, 0, 0)


# -----------------------------
# Temporary game state
# -----------------------------
class Game:
    def __init__(self):
        self.running = True
        self.paused = False

        self.tile_size = 16

        cols = WINDOW_WIDTH // self.tile_size
        rows = (WINDOW_HEIGHT - HUD_HEIGHT) // self.tile_size
        self.world = World(cols, rows, "ocean")

        self.selected_tile = None
        self.hovered_tile = None

        self.current_terrain = "grass"
        self.left_mouse_held = False
        self.brush_size = 0

        self.critters = []

        starter = self.world.get_tile(5, 5)
        if starter is not None:
            starter.set_terrain("grass")
            critter = Critter(5, 5)
            starter.critter = critter
            self.critters.append(critter)


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

    for critter in game.critters:
        critter.update(game.world, dt)


# -----------------------------
# Main
# -----------------------------
def main():
    pygame.init()

    screen = pygame.display.set_mode((WINDOW_WIDTH, WINDOW_HEIGHT))
    pygame.display.set_caption(WINDOW_TITLE)
    clock = pygame.time.Clock()

    game = Game()

    while game.running:
        dt = clock.tick(TARGET_FPS) / 1000.0

        handle_input(game)
        update(game, dt)
        render(screen, game, BACKGROUND_COLOR)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()