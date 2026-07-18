import sys
import random
import math
from collections import Counter, deque

import pygame

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
    POPULATION_GRAPH_HEIGHT,
    POLAR_INTERVAL,
    SPRITE_PATHS,
    TARGET_FPS,
    TECTONIC_INTERVAL,
    TILE_SIZE,
    WEB_MIRROR_ENABLED,
    WEB_MIRROR_FPS,
    WEB_MIRROR_HOST,
    WEB_MIRROR_PORT,
    WINDOW_HEIGHT,
    WINDOW_TITLE,
    WINDOW_WIDTH,
)
from critter import CRITTER_ORDER
from entity_cleanup import clear_stale_tile_critters, remove_stranded_critters
from events import update_events
from input import apply_active_tool, handle_input
from render import render
from tectonics import trigger_trench_event
from webmirror import start_frame_mirror
from world import World

SIZE_PRESET_LABELS = [
    ("Small", 100, 60),
    ("Medium", WINDOW_WIDTH // TILE_SIZE, (WINDOW_HEIGHT - HUD_HEIGHT) // TILE_SIZE),
    ("Large", 180, 90),
    ("Huge", 220, 100),
]

RING_WORLD_ROWS = 34
RING_WORLD_MIN_COLS = 140
RING_WORLD_MAX_COLS = 240
INITIAL_TRENCH_TILE_BUDGET = 2000
POPULATION_HISTORY_INTERVAL = 0.5


# -----------------------------
# Runtime game state
# -----------------------------
class Game:
    def __init__(self, cols, rows):
        self.running = True
        self.paused = False

        self.tile_size = TILE_SIZE
        self.hud_height = HUD_HEIGHT
        self.population_graph_height = POPULATION_GRAPH_HEIGHT
        self.bottom_panel_height = HUD_HEIGHT + POPULATION_GRAPH_HEIGHT
        self.window_width, self.window_height = get_window_size_for_map(cols, rows)

        self.world = World(cols, rows, INITIAL_TERRAIN)

        self.selected_tile = None
        self.hovered_tile = None

        self.current_terrain = DEFAULT_PAINT_TERRAIN
        self.current_critter = CRITTER_ORDER[0]
        self.current_building = "village"
        self.current_event = "meteor"
        self.current_tool = "terrain"
        self.left_mouse_held = False
        self.brush_size = DEFAULT_BRUSH_SIZE

        self.critters = []
        self.impact_waves = []
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
        self.population_history_interval = POPULATION_HISTORY_INTERVAL
        self.population_history_timer = 0.0
        self.population_history = {
            critter_name: deque(maxlen=max(120, self.window_width - 24))
            for critter_name in CRITTER_ORDER
        }


# -----------------------------
# Systems
# -----------------------------
def record_population_snapshot(game, dt, force=False):
    game.population_history_timer += dt
    if not force and game.population_history_timer < game.population_history_interval:
        return

    game.population_history_timer = 0.0
    counts = Counter(critter.sprite for critter in game.critters)
    for critter_name in CRITTER_ORDER:
        game.population_history[critter_name].append(counts.get(critter_name, 0))


def update(game, dt):
    clear_stale_tile_critters(game)

    mx, my = pygame.mouse.get_pos()
    game.hovered_tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

    remove_stranded_critters(game)

    if game.paused:
        record_population_snapshot(game, dt)
        return

    if game.left_mouse_held and game.hovered_tile is not None:
        apply_active_tool(game, game.hovered_tile)

    update_events(game, dt)
    remove_stranded_critters(game)

    for critter in game.critters[:]:
        critter.update(game, dt)

    clear_stale_tile_critters(game)
    record_population_snapshot(game, dt)


def get_initial_trench_count(cols, rows):
    area = cols * rows
    return max(1, min(6, math.ceil(area / INITIAL_TRENCH_TILE_BUDGET)))


def seed_initial_trenches(game, trench_count=None):
    if trench_count is None:
        trench_count = get_initial_trench_count(game.world.cols, game.world.rows)

    for _ in range(trench_count):
        for _ in range(32):
            start_x = random.randint(0, game.world.cols - 1)
            start_y = random.randint(0, game.world.rows - 1)

            if trigger_trench_event(game, start_x, start_y):
                break


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


def get_window_size_for_map(cols, rows):
    return cols * TILE_SIZE, rows * TILE_SIZE + HUD_HEIGHT + POPULATION_GRAPH_HEIGHT


def get_available_size_presets():
    info = pygame.display.Info()
    max_width = max(640, info.current_w - 120)
    max_height = max(480, info.current_h - 120)
    presets = []

    for label, cols, rows in SIZE_PRESET_LABELS:
        window_width, window_height = get_window_size_for_map(cols, rows)
        if window_width <= max_width and window_height <= max_height:
            presets.append((label, cols, rows, window_width, window_height))

    ring_world_cols = min(RING_WORLD_MAX_COLS, max_width // TILE_SIZE)
    ring_world_cols = max(RING_WORLD_MIN_COLS, ring_world_cols)
    ring_world_width, ring_world_height = get_window_size_for_map(ring_world_cols, RING_WORLD_ROWS)
    if ring_world_width <= max_width and ring_world_height <= max_height:
        presets.append(("Ring World", ring_world_cols, RING_WORLD_ROWS, ring_world_width, ring_world_height))

    if presets:
        return presets

    fallback_cols = max(40, max_width // TILE_SIZE)
    fallback_rows = max(30, (max_height - HUD_HEIGHT - POPULATION_GRAPH_HEIGHT) // TILE_SIZE)
    fallback_width, fallback_height = get_window_size_for_map(fallback_cols, fallback_rows)
    return [("Auto Fit", fallback_cols, fallback_rows, fallback_width, fallback_height)]


def select_window_size():
    info = pygame.display.Info()
    presets = get_available_size_presets()
    selector_width = min(760, max(520, info.current_w - 100))
    required_height = 180 + len(presets) * 62 + 70
    selector_height = min(required_height, max(360, info.current_h - 100))
    screen = pygame.display.set_mode((selector_width, selector_height))
    pygame.display.set_caption(f"{WINDOW_TITLE} - Select Window Size")

    title_font = pygame.font.SysFont(None, 42)
    body_font = pygame.font.SysFont(None, 28)
    hint_font = pygame.font.SysFont(None, 22)

    selected_index = 1 if len(presets) > 1 else 0
    buttons = [
        pygame.Rect(40, 140 + index * 62, selector_width - 80, 48)
        for index in range(len(presets))
    ]

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return None

            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    return None
                if event.key in (pygame.K_UP, pygame.K_w):
                    selected_index = (selected_index - 1) % len(presets)
                elif event.key in (pygame.K_DOWN, pygame.K_s):
                    selected_index = (selected_index + 1) % len(presets)
                elif event.key in (pygame.K_RETURN, pygame.K_SPACE):
                    _, cols, rows, _, _ = presets[selected_index]
                    return cols, rows
                elif pygame.K_1 <= event.key <= pygame.K_9:
                    number_index = event.key - pygame.K_1
                    if number_index < len(presets):
                        _, cols, rows, _, _ = presets[number_index]
                        return cols, rows

            if event.type == pygame.MOUSEMOTION:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        selected_index = index
                        break

            if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        _, cols, rows, _, _ = presets[index]
                        return cols, rows

        screen.fill((0, 0, 0))

        title = title_font.render("Choose a Newt Window Size", True, (255, 255, 255))
        subtitle = body_font.render(
            f"Tile size is {TILE_SIZE}px, so each option creates an exact tile-aligned map.",
            True,
            (255, 255, 255),
        )
        screen.blit(title, (40, 36))
        screen.blit(subtitle, (40, 82))

        for index, (label, cols, rows, window_width, window_height) in enumerate(presets):
            rect = buttons[index]
            is_selected = index == selected_index
            fill = (255, 255, 255) if is_selected else (0, 0, 0)
            outline = (255, 255, 255)
            text_color = (0, 0, 0) if is_selected else (255, 255, 255)
            pygame.draw.rect(screen, fill, rect, border_radius=8)
            pygame.draw.rect(screen, outline, rect, width=2, border_radius=8)

            label_text = body_font.render(
                f"{index + 1}. {label}: {window_width} x {window_height} window",
                True,
                text_color,
            )
            detail_text = hint_font.render(
                f"Map size: {cols} x {rows} tiles",
                True,
                text_color,
            )
            screen.blit(label_text, (56, rect.y + 8))
            screen.blit(detail_text, (min(selector_width - 260, 430), rect.y + 14))

        hints = hint_font.render(
            "Use mouse, number keys, or Up/Down + Enter. Esc closes Newt.",
            True,
            (255, 255, 255),
        )
        screen.blit(hints, (40, selector_height - 48))

        pygame.display.flip()


# -----------------------------
# Main
# -----------------------------
def main():
    pygame.init()
    frame_mirror = None

    selected_map_size = select_window_size()
    if selected_map_size is None:
        pygame.quit()
        return

    cols, rows = selected_map_size
    window_width, window_height = get_window_size_for_map(cols, rows)
    screen = pygame.display.set_mode((window_width, window_height))
    pygame.display.set_caption(WINDOW_TITLE)
    clock = pygame.time.Clock()

    game = Game(cols, rows)
    seed_initial_trenches(game)
    game.sprites = load_sprites(game.tile_size)
    record_population_snapshot(game, 0.0, force=True)

    if WEB_MIRROR_ENABLED:
        try:
            frame_mirror = start_frame_mirror(WEB_MIRROR_HOST, WEB_MIRROR_PORT, WEB_MIRROR_FPS)
        except OSError as exc:
            print(f"Web mirror unavailable on {WEB_MIRROR_HOST}:{WEB_MIRROR_PORT}: {exc}")

    try:
        while game.running:
            dt = clock.tick(TARGET_FPS) / 1000.0
            dt *= game.speed

            handle_input(game)
            update(game, dt)
            render(screen, game, BACKGROUND_COLOR)

            if frame_mirror is not None:
                frame_mirror.publish(screen)
    finally:
        if frame_mirror is not None:
            frame_mirror.stop()
        pygame.quit()

    sys.exit()


if __name__ == "__main__":
    main()
