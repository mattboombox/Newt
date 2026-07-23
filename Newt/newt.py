import sys
import random

import pygame

from config import (
    BACKGROUND_COLOR,
    DEFAULT_BRUSH_SIZE,
    DEFAULT_GAME_SPEED,
    DEFAULT_PAINT_TERRAIN,
    EROSION_INTERVAL,
    EVOLUTION_CHANCE,
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
from erosion import get_polar_depth
from events import update_events
from input import apply_active_tool, handle_input
from render import render
from tectonics import TRENCH_CARVABLE_TERRAINS, trigger_trench_event
from webmirror import start_frame_mirror
from world import World

SIZE_PRESET_LABELS = [
    ("Micro", 40, 24),
    ("Small", 100, 60),
    ("Medium", WINDOW_WIDTH // TILE_SIZE, (WINDOW_HEIGHT - HUD_HEIGHT) // TILE_SIZE),
    ("Large", 180, 90),
    ("Huge", 220, 100),
    ("Ring World", 280, 26),
]

TEMPERATURE_PRESETS = [
    ("Hot", 0, "No polar ice caps"),
    ("Normal", 3, "Default polar ice caps"),
    ("Cold", 6, "Larger polar ice caps"),
    ("Frozen", 0.40, "Near-global ice with a narrow equatorial strip"),
]

WORLD_TYPE_PRESETS = [
    ("Wet", "ocean", "World starts covered in ocean"),
    ("Dry", "sand", "World starts covered in sand"),
    ("Molten", "lava", "Young world covered in lava"),
]

INITIAL_TRENCH_AREA_STEP = 5000
MAX_INITIAL_TRENCHES = 6
MICRO_HUD_HEIGHT = 48
CUSTOM_MIN_COLS = 40
CUSTOM_MIN_ROWS = 24
CUSTOM_MAX_WIDTH = 3840
CUSTOM_MAX_HEIGHT = 2160
CUSTOM_WINDOW_SAFETY = 10


# -----------------------------
# Runtime game state
# -----------------------------
class Game:
    def __init__(
        self,
        cols,
        rows,
        temperature="Normal",
        polar_depth=3,
        world_type="Wet",
        initial_terrain=INITIAL_TERRAIN,
    ):
        self.running = True
        self.paused = False

        self.tile_size = TILE_SIZE
        self.hud_height = get_hud_height_for_map(cols)
        self.bottom_panel_height = self.hud_height
        self.window_width, self.window_height = get_window_size_for_map(cols, rows)

        self.world = World(cols, rows, initial_terrain)
        self.world_type = world_type

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
        # Lets scavengers avoid a map-wide path search when no corpse exists.
        self.dying_critters = set()
        self.impact_waves = []
        self.tsunamis = []
        self.volcanoes = []

        # Timers are live state; their interval values come from config.py.
        self.erosion_timer = 0.0
        self.erosion_interval = EROSION_INTERVAL

        self.life_timer = 0.0
        self.life_interval = LIFE_INTERVAL

        self.evolution_chance = EVOLUTION_CHANCE

        self.impact_timer = 0.0
        self.impact_interval = IMPACT_INTERVAL
        self.impact_chance = IMPACT_CHANCE

        self.tectonic_timer = 0.0
        self.tectonic_interval = TECTONIC_INTERVAL

        self.polar_timer = POLAR_INTERVAL
        self.polar_interval = POLAR_INTERVAL
        self.temperature = temperature
        self.polar_depth = polar_depth

        self.speed = DEFAULT_GAME_SPEED
        self.sprites = {}


def update_buildings(game, dt):
    updated_buildings = set()

    for x in range(game.world.cols):
        for y in range(game.world.rows):
            tile = game.world.board[x][y]
            building = tile.building
            if building is None or id(building) in updated_buildings:
                continue

            updated_buildings.add(id(building))
            building.update(game, dt)


def update(game, dt):
    mx, my = pygame.mouse.get_pos()
    game.hovered_tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

    # A tile's occupant is the authority movement and the HUD consult.  Keep
    # it synchronized with the active critter list so an already-removed
    # critter cannot become an invisible, permanent movement blocker.
    clear_stale_tile_critters(game)

    if game.paused:
        return

    if game.left_mouse_held and game.hovered_tile is not None:
        apply_active_tool(game, game.hovered_tile)

    update_events(game, dt)
    remove_stranded_critters(game)
    update_buildings(game, dt)

    for critter in game.critters[:]:
        critter.update(game, dt)


def get_initial_trench_count(cols, rows):
    area = cols * rows
    additional_trenches, _ = divmod(max(0, area - 1), INITIAL_TRENCH_AREA_STEP)
    return min(MAX_INITIAL_TRENCHES, 1 + additional_trenches)


def get_equatorial_trench_start(game):
    world = game.world
    center_y = world.rows // 2
    candidate_rows = sorted(range(world.rows), key=lambda y: abs(y - center_y))
    candidate_columns = list(range(world.cols))
    random.shuffle(candidate_columns)

    for y in candidate_rows:
        for x in candidate_columns:
            polar_depth = get_polar_depth(world, x, game.polar_depth)
            if y < polar_depth or y >= world.rows - polar_depth:
                continue

            tile = world.get_tile(x, y)
            if tile is not None and tile.terrain in TRENCH_CARVABLE_TERRAINS:
                return x, y

    return None


def seed_initial_trenches(game, trench_count=None):
    if getattr(game, "world_type", "Wet") != "Wet":
        return 0

    if trench_count is None:
        trench_count = get_initial_trench_count(game.world.cols, game.world.rows)
    if trench_count <= 0:
        return 0

    seeded_count = 0
    equatorial_start = get_equatorial_trench_start(game)
    if equatorial_start is not None and trigger_trench_event(game, *equatorial_start):
        seeded_count = 1

    for _ in range(seeded_count, trench_count):
        seeded = False
        for _ in range(32):
            start_x = random.randint(0, game.world.cols - 1)
            start_y = random.randint(0, game.world.rows - 1)

            if trigger_trench_event(game, start_x, start_y):
                seeded = True
                break

        if not seeded:
            for column in game.world.board:
                for tile in column:
                    if tile.terrain not in TRENCH_CARVABLE_TERRAINS:
                        continue
                    if trigger_trench_event(game, tile.x, tile.y):
                        seeded = True
                        break
                if seeded:
                    break

        if not seeded:
            break

        seeded_count += 1

    return seeded_count


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


def get_hud_height_for_map(cols):
    return MICRO_HUD_HEIGHT if cols * TILE_SIZE <= 500 else HUD_HEIGHT


def get_window_size_for_map(cols, rows):
    return cols * TILE_SIZE, rows * TILE_SIZE + get_hud_height_for_map(cols)


def get_available_size_presets():
    info = pygame.display.Info()
    max_width = max(640, info.current_w - 120)
    max_height = max(480, info.current_h - 120)
    presets = []

    for label, cols, rows in SIZE_PRESET_LABELS:
        fitted_cols = cols
        if label == "Ring World":
            fitted_cols = min(cols, info.current_w // TILE_SIZE)

        window_width, window_height = get_window_size_for_map(fitted_cols, rows)
        allowed_width = info.current_w if label == "Ring World" else max_width
        if window_width <= allowed_width and window_height <= max_height:
            presets.append((label, fitted_cols, rows, window_width, window_height))

    custom_width, custom_height = get_window_size_for_map(CUSTOM_MIN_COLS, CUSTOM_MIN_ROWS)
    presets.append(
        ("Custom", CUSTOM_MIN_COLS, CUSTOM_MIN_ROWS, custom_width, custom_height)
    )

    if presets:
        return presets

    fallback_cols = max(40, max_width // TILE_SIZE)
    fallback_rows = max(30, (max_height - HUD_HEIGHT) // TILE_SIZE)
    fallback_width, fallback_height = get_window_size_for_map(fallback_cols, fallback_rows)
    return [("Auto Fit", fallback_cols, fallback_rows, fallback_width, fallback_height)]


def get_custom_size_bounds():
    desktop_sizes = pygame.display.get_desktop_sizes()
    if desktop_sizes:
        desktop_width, desktop_height = desktop_sizes[0]
    else:
        info = pygame.display.Info()
        desktop_width, desktop_height = info.current_w, info.current_h

    safe_width = min(desktop_width, CUSTOM_MAX_WIDTH) - CUSTOM_WINDOW_SAFETY
    safe_height = min(desktop_height, CUSTOM_MAX_HEIGHT) - CUSTOM_WINDOW_SAFETY
    max_cols = max(CUSTOM_MIN_COLS, safe_width // TILE_SIZE)
    max_rows = max(
        CUSTOM_MIN_ROWS,
        (safe_height - max(HUD_HEIGHT, MICRO_HUD_HEIGHT)) // TILE_SIZE,
    )
    return CUSTOM_MIN_COLS, CUSTOM_MIN_ROWS, max_cols, max_rows


def select_custom_size():
    min_cols, min_rows, max_cols, max_rows = get_custom_size_bounds()
    cols = min(max_cols, WINDOW_WIDTH // TILE_SIZE)
    rows = min(max_rows, (WINDOW_HEIGHT - HUD_HEIGHT) // TILE_SIZE)

    selector_width = 620
    selector_height = 300
    screen = pygame.display.set_mode((selector_width, selector_height))
    pygame.display.set_caption(f"{WINDOW_TITLE} - Custom World Size")
    title_font = pygame.font.SysFont(None, 42)
    body_font = pygame.font.SysFont(None, 30)
    hint_font = pygame.font.SysFont(None, 22)

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return None

            if event.type == pygame.KEYDOWN:
                step = 10 if event.mod & pygame.KMOD_SHIFT else 1
                if event.key == pygame.K_ESCAPE:
                    return None
                if event.key in (pygame.K_LEFT, pygame.K_a):
                    cols = max(min_cols, cols - step)
                elif event.key in (pygame.K_RIGHT, pygame.K_d):
                    cols = min(max_cols, cols + step)
                elif event.key in (pygame.K_UP, pygame.K_w):
                    rows = min(max_rows, rows + step)
                elif event.key in (pygame.K_DOWN, pygame.K_s):
                    rows = max(min_rows, rows - step)
                elif event.key in (pygame.K_RETURN, pygame.K_SPACE):
                    return cols, rows

        screen.fill((0, 0, 0))
        title = title_font.render("Choose a Custom World Size", True, (255, 255, 255))
        screen.blit(title, (40, 34))

        window_width, window_height = get_window_size_for_map(cols, rows)
        size_text = body_font.render(
            f"{cols} x {rows} tiles  |  {window_width} x {window_height} window",
            True,
            (255, 255, 255),
        )
        screen.blit(size_text, (40, 112))

        bounds_text = hint_font.render(
            f"Bounds: {min_cols} x {min_rows} to {max_cols} x {max_rows} tiles",
            True,
            (200, 200, 200),
        )
        screen.blit(bounds_text, (40, 158))

        controls = hint_font.render(
            "A/Left: narrower  D/Right: wider  W/Up: taller  S/Down: shorter",
            True,
            (255, 255, 255),
        )
        screen.blit(controls, (40, 208))
        confirm = hint_font.render(
            "Hold Shift for 10 tiles. Enter confirms. Esc cancels.",
            True,
            (255, 255, 255),
        )
        screen.blit(confirm, (40, 238))
        pygame.display.flip()


def resolve_size_preset(preset):
    label, cols, rows, _, _ = preset
    if label == "Custom":
        return select_custom_size()
    return cols, rows


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

    selected_index = next(
        (index for index, preset in enumerate(presets) if preset[0] == "Medium"),
        0,
    )
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
                    return resolve_size_preset(presets[selected_index])
                elif pygame.K_1 <= event.key <= pygame.K_9:
                    number_index = event.key - pygame.K_1
                    if number_index < len(presets):
                        return resolve_size_preset(presets[number_index])

            if event.type == pygame.MOUSEMOTION:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        selected_index = index
                        break

            if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        return resolve_size_preset(presets[index])

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

            if label == "Custom":
                label_caption = f"{index + 1}. Custom Size"
                detail_caption = "Choose exact tile dimensions"
            else:
                label_caption = (
                    f"{index + 1}. {label}: {window_width} x {window_height} window"
                )
                detail_caption = f"Map size: {cols} x {rows} tiles"
            label_text = body_font.render(label_caption, True, text_color)
            detail_text = hint_font.render(detail_caption, True, text_color)
            screen.blit(label_text, (56, rect.y + 8))
            screen.blit(detail_text, (min(selector_width - 260, 430), rect.y + 14))

        hints = hint_font.render(
            "Use mouse, number keys, or Up/Down + Enter. Esc closes Newt.",
            True,
            (255, 255, 255),
        )
        screen.blit(hints, (40, selector_height - 48))

        pygame.display.flip()


def select_temperature():
    info = pygame.display.Info()
    selector_width = min(680, max(520, info.current_w - 100))
    selector_height = min(500, max(420, info.current_h - 100))
    screen = pygame.display.set_mode((selector_width, selector_height))
    pygame.display.set_caption(f"{WINDOW_TITLE} - Select Temperature")

    title_font = pygame.font.SysFont(None, 42)
    body_font = pygame.font.SysFont(None, 28)
    hint_font = pygame.font.SysFont(None, 22)
    selected_index = 1
    buttons = [
        pygame.Rect(40, 108 + index * 66, selector_width - 80, 54)
        for index in range(len(TEMPERATURE_PRESETS))
    ]

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return None

            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    return None
                if event.key in (pygame.K_UP, pygame.K_w):
                    selected_index = (selected_index - 1) % len(TEMPERATURE_PRESETS)
                elif event.key in (pygame.K_DOWN, pygame.K_s):
                    selected_index = (selected_index + 1) % len(TEMPERATURE_PRESETS)
                elif event.key in (pygame.K_RETURN, pygame.K_SPACE):
                    label, polar_depth, _ = TEMPERATURE_PRESETS[selected_index]
                    return label, polar_depth
                elif pygame.K_1 <= event.key <= pygame.K_4:
                    number_index = event.key - pygame.K_1
                    label, polar_depth, _ = TEMPERATURE_PRESETS[number_index]
                    return label, polar_depth

            if event.type == pygame.MOUSEMOTION:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        selected_index = index
                        break

            if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        label, polar_depth, _ = TEMPERATURE_PRESETS[index]
                        return label, polar_depth

        screen.fill((0, 0, 0))
        title = title_font.render("Choose a World Temperature", True, (255, 255, 255))
        screen.blit(title, (40, 36))

        for index, (label, polar_depth, description) in enumerate(TEMPERATURE_PRESETS):
            rect = buttons[index]
            is_selected = index == selected_index
            fill = (255, 255, 255) if is_selected else (0, 0, 0)
            text_color = (0, 0, 0) if is_selected else (255, 255, 255)
            pygame.draw.rect(screen, fill, rect, border_radius=8)
            pygame.draw.rect(screen, (255, 255, 255), rect, width=2, border_radius=8)

            label_text = body_font.render(f"{index + 1}. {label}", True, text_color)
            detail_text = hint_font.render(description, True, text_color)
            screen.blit(label_text, (56, rect.y + 8))
            screen.blit(detail_text, (220, rect.y + 18))

        hints = hint_font.render(
            "Use mouse, number keys, or Up/Down + Enter. Esc closes Newt.",
            True,
            (255, 255, 255),
        )
        screen.blit(hints, (40, selector_height - 42))
        pygame.display.flip()


def select_world_type():
    info = pygame.display.Info()
    selector_width = min(680, max(520, info.current_w - 100))
    selector_height = min(370, max(350, info.current_h - 100))
    screen = pygame.display.set_mode((selector_width, selector_height))
    pygame.display.set_caption(f"{WINDOW_TITLE} - Select World Type")

    title_font = pygame.font.SysFont(None, 42)
    body_font = pygame.font.SysFont(None, 28)
    hint_font = pygame.font.SysFont(None, 22)
    selected_index = 0
    buttons = [
        pygame.Rect(40, 108 + index * 66, selector_width - 80, 54)
        for index in range(len(WORLD_TYPE_PRESETS))
    ]

    while True:
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                return None

            if event.type == pygame.KEYDOWN:
                if event.key == pygame.K_ESCAPE:
                    return None
                if event.key in (pygame.K_UP, pygame.K_w, pygame.K_DOWN, pygame.K_s):
                    selected_index = (selected_index + 1) % len(WORLD_TYPE_PRESETS)
                elif event.key in (pygame.K_RETURN, pygame.K_SPACE):
                    label, terrain, _ = WORLD_TYPE_PRESETS[selected_index]
                    return label, terrain
                elif pygame.K_1 <= event.key <= pygame.K_3:
                    label, terrain, _ = WORLD_TYPE_PRESETS[event.key - pygame.K_1]
                    return label, terrain

            if event.type == pygame.MOUSEMOTION:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        selected_index = index
                        break

            if event.type == pygame.MOUSEBUTTONDOWN and event.button == 1:
                for index, rect in enumerate(buttons):
                    if rect.collidepoint(event.pos):
                        label, terrain, _ = WORLD_TYPE_PRESETS[index]
                        return label, terrain

        screen.fill((0, 0, 0))
        title = title_font.render("Choose a World Type", True, (255, 255, 255))
        screen.blit(title, (40, 36))

        for index, (label, terrain, description) in enumerate(WORLD_TYPE_PRESETS):
            rect = buttons[index]
            is_selected = index == selected_index
            fill = (255, 255, 255) if is_selected else (0, 0, 0)
            text_color = (0, 0, 0) if is_selected else (255, 255, 255)
            pygame.draw.rect(screen, fill, rect, border_radius=8)
            pygame.draw.rect(screen, (255, 255, 255), rect, width=2, border_radius=8)
            screen.blit(body_font.render(f"{index + 1}. {label}", True, text_color), (56, rect.y + 8))
            screen.blit(hint_font.render(description, True, text_color), (220, rect.y + 18))

        hints = hint_font.render(
            "Use mouse, number keys, or Up/Down + Enter. Esc closes Newt.",
            True,
            (255, 255, 255),
        )
        screen.blit(hints, (40, selector_height - 42))
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

    selected_temperature = select_temperature()
    if selected_temperature is None:
        pygame.quit()
        return

    selected_world_type = select_world_type()
    if selected_world_type is None:
        pygame.quit()
        return

    cols, rows = selected_map_size
    temperature, polar_depth = selected_temperature
    world_type, initial_terrain = selected_world_type
    window_width, window_height = get_window_size_for_map(cols, rows)
    screen = pygame.display.set_mode((window_width, window_height))
    pygame.display.set_caption(WINDOW_TITLE)
    clock = pygame.time.Clock()

    game = Game(cols, rows, temperature, polar_depth, world_type, initial_terrain)
    seed_initial_trenches(game)
    game.sprites = load_sprites(game.tile_size)

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
