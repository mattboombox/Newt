import sys
import random
import pygame

import critter
import volcano
import erosion
import brush
import critterSpawner
from tile import Tile
from printControls import printControls

# -----------------------------
# Init
# -----------------------------
pygame.init()

# Display
WINDOW_WIDTH = 1200
WINDOW_HEIGHT = 800
WINDOW_TITLE = "Pygame Window"
WINDOW_BG = (25, 25, 25)

# Board
TILE = 10
COLS = WINDOW_WIDTH // TILE
ROWS = WINDOW_HEIGHT // TILE
board = [[Tile(x, y) for y in range(ROWS)] for x in range(COLS)]
clicked_tile = [0, 0]

print("Display size:", WINDOW_WIDTH, WINDOW_HEIGHT)
print("Number of tiles:", COLS, "x", ROWS, "=", ROWS * COLS)

# Brush
paint_brush = brush.Brush()
paint_brush.setBrush("stone")

# Initial terrain gen
num_islands = random.randint(1, 4)
for _ in range(num_islands):
    volcano.getIslandSeed(board)

# Sync volcano cached sets to board
if hasattr(volcano, "init_caches"):
    volcano.init_caches(board)

# Critters
MAX_CRITTERS = 1000
critter_list = []

# Window
screen = pygame.display.set_mode((WINDOW_WIDTH, WINDOW_HEIGHT))
pygame.display.set_caption(WINDOW_TITLE)
printControls()

# Load + pre-scale sprites ONCE
raw_sprites = {
    "deer": pygame.image.load("Newt/sprites/deer.png").convert_alpha(),
    "fish": pygame.image.load("Newt/sprites/fish.png").convert_alpha(),
}
sprites = {k: pygame.transform.smoothscale(v, (TILE, TILE)) for k, v in raw_sprites.items()}

# -----------------------------
# Timing / Simulation
# -----------------------------
clock = pygame.time.Clock()
TARGET_FPS = 60

SPEED_SLOWEST = 2     # steps/sec
SPEED_SLOW = 6
SPEED_FAST = 20
SPEED_FASTEST = 60
speed_levels = [SPEED_SLOWEST, SPEED_SLOW, SPEED_FAST, SPEED_FASTEST]
speed_index = 1  # start at SLOW
paused = False

accum = 0.0

EVENT_CHECK_EVERY_STEPS = 5
sim_step_counter = 0

# Odds
COMMON = 10
UNCOMMON = 100
RARE = 500
RARER = 1000
UNIQUE = 10000
ASTRONOMICAL = 100000

# -----------------------------
# Dirty rendering helpers
# -----------------------------
def pos_to_tile(mx: int, my: int):
    tx = mx // TILE
    ty = my // TILE
    if 0 <= tx < COLS and 0 <= ty < ROWS:
        return tx, ty
    return None

def tile_rect(x: int, y: int) -> pygame.Rect:
    return pygame.Rect(x * TILE, y * TILE, TILE, TILE)

def draw_tile(x: int, y: int) -> pygame.Rect:
    """Draw exactly one tile + its critter (if any). Returns the rect updated."""
    t = board[x][y]
    r = tile_rect(x, y)
    pygame.draw.rect(screen, t.getTerrainColor(), r)

    if t.critter is not None:
        spr = sprites.get(t.critter.species)
        if spr is not None:
            screen.blit(spr, (x * TILE, y * TILE))
    return r

def full_redraw() -> None:
    screen.fill(WINDOW_BG)
    for x in range(COLS):
        for y in range(ROWS):
            draw_tile(x, y)
    pygame.display.flip()

def spawn_random_critter():
    """Try to spawn fish or land critter. Returns new critter or None."""
    if len(critter_list) >= MAX_CRITTERS:
        print("Critter limit reached!", len(critter_list))
        return None

    if random.random() < 0.5:
        order = (critterSpawner.spawnFishCritter, critterSpawner.spawnLandCritter)
    else:
        order = (critterSpawner.spawnLandCritter, critterSpawner.spawnFishCritter)

    new_c = None
    for fn in order:
        new_c = fn(board)
        if new_c is not None:
            break

    if new_c is None:
        print("No valid tiles to spawn a fish or land critter.")
        return None

    new_c.name = str(len(critter_list))
    critter_list.append(new_c)

    if getattr(new_c, "fish", False):
        print("A fish has spawned at", new_c.x, new_c.y, "!")
    else:
        print("A land dweller has spawned at", new_c.x, new_c.y, "!")
    print("Number of critters:", len(critter_list))
    return new_c

# First full draw
full_redraw()

# Painting state
is_painting = False

# Main loop
running = True
dirty_tiles = set()

while running:
    dt = clock.tick(TARGET_FPS) / 1000.0

    # -----------------------------
    # Input
    # -----------------------------
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        elif event.type == pygame.MOUSEBUTTONDOWN:
            pos = pos_to_tile(*event.pos)

            if event.button == 1:  # left: paint
                is_painting = True
                if pos:
                    tx, ty = pos
                    clicked_tile[0], clicked_tile[1] = tx, ty
                    paint_brush.paint(board, tx, ty)
                    dirty_tiles.add((tx, ty))

            elif event.button == 2:  # middle: describe
                if pos:
                    tx, ty = pos
                    clicked_tile[0], clicked_tile[1] = tx, ty
                    board[tx][ty].describe()

            elif event.button == 3:  # right: pick terrain
                if pos:
                    tx, ty = pos
                    picked = board[tx][ty].terrain.name
                    paint_brush.setBrush(picked)
                    print(f"Picked brush: {picked}")

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                is_painting = False

        elif event.type == pygame.MOUSEMOTION:
            if is_painting:
                pos = pos_to_tile(*event.pos)
                if pos:
                    tx, ty = pos
                    paint_brush.paint(board, tx, ty)
                    dirty_tiles.add((tx, ty))

        elif event.type == pygame.KEYDOWN:
            if event.key == pygame.K_EQUALS:
                speed_index = min(speed_index + 1, len(speed_levels) - 1)
                labels = ["Slowest", "Slow", "Fast", "Fastest"]
                print(labels[speed_index])

            elif event.key == pygame.K_MINUS:
                speed_index = max(speed_index - 1, 0)
                labels = ["Slowest", "Slow", "Fast", "Fastest"]
                print(labels[speed_index])

            elif event.key == pygame.K_p:
                paused = not paused
                print("Paused" if paused else "Unpaused")

            elif event.key == pygame.K_x:
                print("Goodbye!")
                running = False

            elif event.key == pygame.K_h:
                printControls()

            elif event.key == pygame.K_m:
                new_c = spawn_random_critter()
                if new_c is not None:
                    dirty_tiles.add((new_c.x, new_c.y))

            elif event.key == pygame.K_o:
                paint_brush.cycleBrush(forward=True)

            elif event.key == pygame.K_i:
                paint_brush.cycleBrush(forward=False)

    # -----------------------------
    # Simulation (fixed timestep)
    # -----------------------------
    steps_per_sec = speed_levels[speed_index]
    step_dt = 1.0 / steps_per_sec if steps_per_sec > 0 else 0.1

    if not paused:
        accum += dt

        while accum >= step_dt:
            accum -= step_dt
            sim_step_counter += 1

            # Critter movement
            for c in critter_list:
                oldx, oldy = c.x, c.y
                if critter.wander(c, board, COLS, ROWS):
                    dirty_tiles.add((oldx, oldy))
                    dirty_tiles.add((c.x, c.y))

            # Random critter spawn
            if random.randrange(UNIQUE) == 0:
                new_c = spawn_random_critter()
                if new_c is not None:
                    dirty_tiles.add((new_c.x, new_c.y))

            # Environment events (volcano + erosion now both return dirty sets)
            if sim_step_counter % EVENT_CHECK_EVERY_STEPS == 0:
                # Volcano
                if random.randrange(COMMON) == 0:
                    dirty_tiles |= volcano.eruptVolcano(board, critter_list)
                if random.randrange(COMMON) == 0:
                    dirty_tiles |= volcano.coolLava(board)
                if random.randrange(UNCOMMON) == 0:
                    dirty_tiles |= volcano.toggleVolcano(board)
                if random.randrange(RARER) == 0:
                    dirty_tiles |= volcano.killVolcano(board)
                if random.randrange(ASTRONOMICAL) == 0:
                    dirty_tiles |= volcano.getIslandSeed(board)

                # Erosion
                if random.randrange(UNCOMMON) == 0:
                    dirty_tiles |= erosion.spawnDesert(board)
                if random.randrange(UNCOMMON) == 0:
                    dirty_tiles |= erosion.erodeCoast(board)
                if random.randrange(RARE) == 0:
                    dirty_tiles |= erosion.spawnLake(board)
                if random.randrange(UNCOMMON) == 0:
                    dirty_tiles |= erosion.spawnShallows(board)
                if random.randrange(ASTRONOMICAL) == 0:
                    dirty_tiles |= erosion.meteorStrike(board, critter_list)
                if random.randrange(RARE) == 0:
                    dirty_tiles |= erosion.spawnGrass(board)

    # -----------------------------
    # Render (dirty tiles)
    # -----------------------------
    if dirty_tiles:
        rects = []
        for (x, y) in dirty_tiles:
            if 0 <= x < COLS and 0 <= y < ROWS:
                rects.append(draw_tile(x, y))
        pygame.display.update(rects)
        dirty_tiles.clear()

pygame.quit()
sys.exit()