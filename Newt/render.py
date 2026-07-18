import pygame
from city import City
from config import HUD_HEIGHT, POPULATION_GRAPH_HEIGHT
from terrain import TERRAIN_DATA

GRAPH_COLORS = {
    "crab": (255, 80, 80),
    "deer": (180, 140, 90),
    "fish": (80, 180, 255),
    "plankton": (160, 255, 180),
    "sperm_whale": (95, 105, 125),
    "squid": (180, 120, 220),
    "whale": (110, 150, 190),
    "wolf": (160, 160, 160),
}


def format_tool_name(name):
    return name.replace("_", " ").title()


def draw_tile(screen, tile, tile_size):
    rect = pygame.Rect(tile.x * tile_size, tile.y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, tile.get_color(), rect)


def draw_hud(screen, game, background_color):
    font = pygame.font.SysFont(None, 18)

    status = "Paused" if game.paused else "Running"
    active_tool = game.current_tool.title()
    if game.current_tool == "critter":
        active_selection = f"Critter: {format_tool_name(game.current_critter)}"
    elif game.current_tool == "building":
        active_selection = f"Building Tool: {format_tool_name(game.current_building)}"
    elif game.current_tool == "event":
        active_selection = f"Event: {format_tool_name(game.current_event)}"
    else:
        active_selection = f"Brush: {format_tool_name(game.current_terrain)}"

    fields = [
        f"Tool: {active_tool}",
        active_selection,
        f"Critters: {len(game.critters)}",
        f"Size: {game.brush_size}",
        f"Status: {status}",
    ]

    if game.hovered_tile is not None:
        tile = game.hovered_tile
        fields.append(f"Tile: ({tile.x}, {tile.y}) {tile.terrain}")

        if tile.critter is not None:
            fields.append(f"Critter: {type(tile.critter).__name__} ID {tile.critter.id}")
            fields.append(f"Behavior: {format_tool_name(tile.critter.current_behavior)}")

        if tile.building is not None:
            if isinstance(tile.building, City):
                fields.append(f"Building: {tile.building.level.title()}")
            else:
                fields.append(f"Building: {type(tile.building).__name__}")
    else:
        fields.append("Tile: none")

    hud_text = " | ".join(fields)

    graph_top = screen.get_height() - POPULATION_GRAPH_HEIGHT
    hud_rect = pygame.Rect(0, graph_top - HUD_HEIGHT, screen.get_width(), HUD_HEIGHT)

    pygame.draw.rect(screen, (0, 0, 0), hud_rect)
    text_surface = font.render(hud_text, True, (220, 220, 220))
    screen.blit(text_surface, (6, hud_rect.y + 2))


def draw_population_graph(screen, game):
    font = pygame.font.SysFont(None, 16)
    graph_rect = pygame.Rect(0, screen.get_height() - POPULATION_GRAPH_HEIGHT, screen.get_width(), POPULATION_GRAPH_HEIGHT)
    pygame.draw.rect(screen, (0, 0, 0), graph_rect)
    pygame.draw.line(screen, (70, 70, 70), (0, graph_rect.y), (screen.get_width(), graph_rect.y))

    inner_padding = 8
    legend_height = 18
    plot_left = graph_rect.x + inner_padding
    plot_top = graph_rect.y + legend_height + inner_padding
    plot_width = max(1, graph_rect.width - inner_padding * 2)
    plot_height = max(1, graph_rect.height - legend_height - inner_padding * 2 - 6)
    plot_rect = pygame.Rect(plot_left, plot_top, plot_width, plot_height)

    pygame.draw.rect(screen, (25, 25, 25), plot_rect)
    pygame.draw.rect(screen, (70, 70, 70), plot_rect, 1)

    history = game.population_history
    max_population = 1
    for series in history.values():
        if series:
            max_population = max(max_population, max(series))

    max_label = font.render(str(max_population), True, (170, 170, 170))
    screen.blit(max_label, (plot_rect.x + 4, plot_rect.y + 2))

    if plot_rect.height > 12:
        mid_y = plot_rect.y + plot_rect.height // 2
        pygame.draw.line(screen, (40, 40, 40), (plot_rect.x, mid_y), (plot_rect.right, mid_y))

    legend_x = graph_rect.x + inner_padding
    legend_y = graph_rect.y + 3
    for critter_name, series in history.items():
        color = GRAPH_COLORS.get(critter_name, (220, 220, 220))
        label = font.render(format_tool_name(critter_name), True, color)
        screen.blit(label, (legend_x, legend_y))
        legend_x += label.get_width() + 10

        if len(series) < 2:
            continue

        point_step = plot_rect.width / max(1, len(series) - 1)
        points = []
        for index, population in enumerate(series):
            x = plot_rect.x + round(index * point_step)
            y_ratio = population / max_population
            y = plot_rect.bottom - round(y_ratio * (plot_rect.height - 1))
            points.append((x, y))

        pygame.draw.lines(screen, color, False, points, 2)


def draw_critter(screen, critter, tile_size, sprites):
    sprite = sprites.get(critter.sprite)

    if sprite is not None:
        screen.blit(sprite, (critter.x * tile_size, critter.y * tile_size))
    else:
        inset = max(2, tile_size // 4)
        rect = pygame.Rect(
            critter.x * tile_size + inset,
            critter.y * tile_size + inset,
            tile_size - inset * 2,
            tile_size - inset * 2
        )
        pygame.draw.rect(screen, critter.color, rect)


def draw_building(screen, building, tile_size):
    rect = pygame.Rect(
        building.x * tile_size,
        building.y * tile_size,
        tile_size,
        tile_size
    )

    pygame.draw.rect(screen, (200, 50, 50), rect)

    if isinstance(building, City):
        font = pygame.font.SysFont(None, 14)
        text_surface = font.render(building.level[0].upper(), True, (255, 255, 255))
        screen.blit(text_surface, (building.x * tile_size + 2, building.y * tile_size + 1))


def draw_tsunami_wave(screen, x, y, tile_size):
    rect = pygame.Rect(x * tile_size, y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, TERRAIN_DATA["tsunami"]["color"], rect, max(1, tile_size // 6))


def draw_wave_ring(screen, x, y, tile_size, color):
    rect = pygame.Rect(x * tile_size, y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, color, rect, max(1, tile_size // 6))


def render(screen, game, background_color):
    screen.fill(background_color)

    for x in range(game.world.cols):
        for y in range(game.world.rows):
            tile = game.world.board[x][y]
            draw_tile(screen, tile, game.tile_size)

            if tile.building is not None:
                draw_building(screen, tile.building, game.tile_size)

    active_wave_tiles = set()
    for tsunami in game.tsunamis:
        active_wave_tiles.update(tsunami.current_ring)

    for x, y in active_wave_tiles:
        draw_tsunami_wave(screen, x, y, game.tile_size)

    for impact_wave in game.impact_waves:
        wave_color = TERRAIN_DATA[impact_wave.target_terrain]["color"]
        for x, y in impact_wave.current_ring:
            draw_wave_ring(screen, x, y, game.tile_size, wave_color)

    for critter in game.critters:
        draw_critter(screen, critter, game.tile_size, game.sprites)

    if game.hovered_tile is not None:
        rect = pygame.Rect(
            game.hovered_tile.x * game.tile_size,
            game.hovered_tile.y * game.tile_size,
            game.tile_size,
            game.tile_size
        )
        pygame.draw.rect(screen, (255, 255, 255), rect, 1)

    draw_hud(screen, game, background_color)
    draw_population_graph(screen, game)
    pygame.display.flip()
