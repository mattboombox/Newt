import sys
import pygame
from tile import Tile


# -----------------------------
# Config
# -----------------------------
WINDOW_WIDTH = 1200
WINDOW_HEIGHT = 800
WINDOW_TITLE = "Newt"
TARGET_FPS = 60
BACKGROUND_COLOR = (20, 20, 20)


# -----------------------------
# Temporary game state
# -----------------------------
class Game:
    def __init__(self):
        self.running = True
        self.paused = False

        # later these move to world.py / game.py
        self.tile_size = 16
        self.cols = WINDOW_WIDTH // self.tile_size
        self.rows = WINDOW_HEIGHT // self.tile_size

        self.board = self.make_board()
        self.selected_tile = None

    def make_board(self):
        return [
            [Tile(x, y, "ocean") for y in range(self.rows)]
            for x in range(self.cols)
        ]


# -----------------------------
# Systems
# -----------------------------
def handle_input(game):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            game.running = False

        elif event.type == pygame.KEYDOWN:
            if event.key == pygame.K_x:
                game.running = False

            elif event.key == pygame.K_p:
                game.paused = not game.paused
                print("Paused" if game.paused else "Unpaused")


def update(game, dt):
    if game.paused:
        return

    # later:
    # update critters
    # update random events
    # update simulation ticks
    pass


def draw_tile(screen, tile, tile_size):
    rect = pygame.Rect(tile.x * tile_size, tile.y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, tile.get_color(), rect)


def render(screen, game):
    screen.fill(BACKGROUND_COLOR)

    for x in range(game.cols):
        for y in range(game.rows):
            draw_tile(screen, game.board[x][y], game.tile_size)

    pygame.display.flip()


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
        render(screen, game)

    pygame.quit()
    sys.exit()


if __name__ == "__main__":
    main()