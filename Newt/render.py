import pygame
from building import CritterPrinter, Farm, MilitaryDistrict, ResidentialDistrict, Ruins, SpiderWeb, WolfDen
from city import City
from terrain import TERRAIN_DATA


def format_tool_name(name):
    return name.replace("_", " ").title()


def draw_tile(screen, tile, tile_size):
    rect = pygame.Rect(tile.x * tile_size, tile.y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, tile.get_color(), rect)


def draw_hud(screen, game, background_color):
    font = pygame.font.SysFont(None, 18)
    building_field = None
    critter_field = None

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
            critter_parts = [
                f"Critter: {type(tile.critter).__name__} ID {tile.critter.id}"
            ]
            if hasattr(tile.critter, "body_positions"):
                critter_parts.append(
                    f"Behavior: {format_tool_name(tile.critter.current_behavior)} "
                    f"(Length: {len(tile.critter.body_positions)}/{tile.critter.MAX_LENGTH}, "
                    f"Growth: {tile.critter.tiles_since_growth}/"
                    f"{tile.critter.TILES_PER_GROWTH})"
                )
            else:
                critter_parts.append(
                    f"Behavior: {format_tool_name(tile.critter.current_behavior)} "
                    f"(Meals: {tile.critter.meals_eaten}/"
                    f"{tile.critter.REPRODUCTION_MEAL_THRESHOLD})"
                )
                if hasattr(tile.critter, "carrying_food"):
                    village = tile.critter.home_building
                    home_status = "None"
                    if isinstance(village, City):
                        home_status = (
                            f"Food {village.food}, "
                            f"Population {village.population}/{village.population_cap}"
                        )
                    critter_parts.append(
                        f"Carrying Food: {tile.critter.carrying_food}"
                    )
                    critter_parts.append(f"Home Village: {home_status}")
            critter_field = " | ".join(critter_parts)

        if tile.building is not None:
            if isinstance(tile.building, City):
                building_field = (
                    f"Building: {tile.building.level.title()} "
                    f"(Food: {tile.building.food}, Population: "
                    f"{tile.building.population}/{tile.building.population_cap})"
                )
            elif isinstance(tile.building, Farm):
                settlement = tile.building.settlement
                food = 0 if settlement is None else settlement.food
                building_field = f"Building: Farm (Settlement food: {food})"
            elif isinstance(tile.building, ResidentialDistrict):
                settlement = tile.building.settlement
                population = 0 if settlement is None else settlement.population
                population_cap = 0 if settlement is None else settlement.population_cap
                building_field = (
                    f"Building: Residential District "
                    f"(Population: {population}/{population_cap})"
                )
            elif isinstance(tile.building, MilitaryDistrict):
                settlement = tile.building.settlement
                food = 0 if settlement is None else settlement.food
                warriors = tile.building.get_village_warriors(game)
                capacity = tile.building.get_connected_military_capacity(game.world)
                building_field = (
                    f"Building: Military District "
                    f"(Warriors: {len(warriors)}/{capacity}, Food: {food}, "
                    f"Recruitment: {max(0, tile.building.recruitment_timer):.0f}s)"
                )
            elif isinstance(tile.building, Ruins):
                former_type = tile.building.former_building_type or "building"
                building_field = (
                    f"Building: {format_tool_name(former_type)} Ruins "
                    f"(Decay: {max(0, tile.building.decay_timer):.0f}s)"
                )
            elif isinstance(tile.building, WolfDen):
                building_field = (
                    f"Building: Wolf Den ({tile.building.charges} charges, "
                    f"{len(tile.building.resident_wolf_ids)} wolves)"
                )
            elif isinstance(tile.building, SpiderWeb):
                building_field = (
                    f"Building: Spider Web ({tile.building.charges} stored prey)"
                )
            elif isinstance(tile.building, CritterPrinter):
                last_printed = tile.building.last_printed_critter or "nothing yet"
                building_field = (
                    f"Building: Critter Printer "
                    f"({tile.building.printed_count} printed, last: "
                    f"{format_tool_name(last_printed)})"
                )
            else:
                building_field = f"Building: {type(tile.building).__name__}"
    else:
        fields.append("Tile: none")

    max_text_width = screen.get_width() - 12
    lines = []
    current_line = ""
    for field in fields:
        candidate = f"{current_line} | {field}" if current_line else field
        if not current_line or font.size(candidate)[0] <= max_text_width:
            current_line = candidate
        else:
            lines.append(current_line)
            current_line = field
    if current_line:
        lines.append(current_line)
    if building_field is not None:
        lines.append(building_field)
    if critter_field is not None:
        lines.append(critter_field)

    hud_rect = pygame.Rect(
        0,
        screen.get_height() - game.bottom_panel_height,
        screen.get_width(),
        game.bottom_panel_height,
    )
    pygame.draw.rect(screen, (0, 0, 0), hud_rect)
    line_height = font.get_linesize()
    for index, line in enumerate(lines):
        text_surface = font.render(line, True, (220, 220, 220))
        screen.blit(text_surface, (6, hud_rect.y + 2 + index * line_height))


def draw_critter(screen, critter, tile_size, sprites):
    sprite = sprites.get(critter.sprite)

    if sprite is not None:
        if critter.current_behavior == "dying":
            sprite = pygame.transform.flip(sprite, False, True)
        screen.blit(sprite, (critter.x * tile_size, critter.y * tile_size))
    else:
        inset = max(2, tile_size // 4)
        positions = critter.get_occupied_positions()
        for index, (x, y) in enumerate(positions):
            color = critter.color if index == 0 else tuple(
                max(0, channel - 25) for channel in critter.color
            )
            rect = pygame.Rect(
                x * tile_size + inset,
                y * tile_size + inset,
                tile_size - inset * 2,
                tile_size - inset * 2,
            )
            pygame.draw.rect(screen, color, rect)


def draw_building(screen, building, tile_size):
    rect = pygame.Rect(
        building.x * tile_size,
        building.y * tile_size,
        tile_size,
        tile_size
    )

    pygame.draw.rect(screen, (200, 50, 50), rect)

    font = pygame.font.SysFont(None, 14)
    if isinstance(building, City):
        text_surface = font.render(building.level[0].upper(), True, (255, 255, 255))
        screen.blit(text_surface, (building.x * tile_size + 2, building.y * tile_size + 1))
    elif isinstance(building, Farm):
        text_surface = font.render("F", True, (175, 255, 150))
        screen.blit(text_surface, (building.x * tile_size + 2, building.y * tile_size + 1))
    elif isinstance(building, ResidentialDistrict):
        text_surface = font.render("R", True, (255, 220, 150))
        screen.blit(text_surface, (building.x * tile_size + 1, building.y * tile_size + 1))
    elif isinstance(building, MilitaryDistrict):
        text_surface = font.render("M", True, (255, 145, 125))
        screen.blit(text_surface, (building.x * tile_size + 1, building.y * tile_size + 1))
    elif isinstance(building, Ruins):
        pygame.draw.rect(screen, (75, 70, 65), rect)
        text_surface = font.render("X", True, (170, 160, 145))
        screen.blit(text_surface, (building.x * tile_size + 1, building.y * tile_size + 1))
    elif isinstance(building, WolfDen):
        text_surface = font.render("W", True, (255, 255, 255))
        screen.blit(text_surface, (building.x * tile_size + 1, building.y * tile_size + 1))
    elif isinstance(building, SpiderWeb):
        pygame.draw.circle(
            screen,
            (230, 230, 230),
            (building.x * tile_size + tile_size // 2, building.y * tile_size + tile_size // 2),
            max(1, tile_size // 3),
            1,
        )
    elif isinstance(building, CritterPrinter):
        text_surface = font.render("P", True, (150, 255, 210))
        screen.blit(text_surface, (building.x * tile_size + 1, building.y * tile_size + 1))


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
    pygame.display.flip()
