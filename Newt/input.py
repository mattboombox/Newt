import pygame
from brush import paint_radius, trigger_event_tool
from terrain import TERRAIN_DATA
from critter import CRITTER_ORDER, CRITTER_TYPES
from city import City
from building import WolfDen
from entity_cleanup import remove_critter


TOOL_MODE_ORDER = ["terrain", "critter", "building", "event"]
BUILDING_ORDER = ["village", "wolf_den"]
EVENT_TOOL_ORDER = ["meteor", "mega_meteor", "comet", "tsunami", "tectonic_uplift", "island_uplift", "trench_event", "evolve"]
EVENT_ONLY_TERRAINS = {"meteor", "comet", "tectonic_uplift", "tsunami"}
TERRAIN_BRUSH_ORDER = [
    terrain_name
    for terrain_name in TERRAIN_DATA.keys()
    if terrain_name not in EVENT_ONLY_TERRAINS
]


def cycle_terrain(game, step):
    current_index = TERRAIN_BRUSH_ORDER.index(game.current_terrain)
    new_index = (current_index + step) % len(TERRAIN_BRUSH_ORDER)
    game.current_terrain = TERRAIN_BRUSH_ORDER[new_index]
    print("Brush terrain:", game.current_terrain)


def cycle_critter(game, step):
    current_index = CRITTER_ORDER.index(game.current_critter)
    new_index = (current_index + step) % len(CRITTER_ORDER)
    game.current_critter = CRITTER_ORDER[new_index]
    print("Critter:", game.current_critter)


def cycle_building(game, step):
    current_index = BUILDING_ORDER.index(game.current_building)
    new_index = (current_index + step) % len(BUILDING_ORDER)
    game.current_building = BUILDING_ORDER[new_index]
    print("Building:", game.current_building)


def cycle_event_tool(game, step):
    current_index = EVENT_TOOL_ORDER.index(game.current_event)
    new_index = (current_index + step) % len(EVENT_TOOL_ORDER)
    game.current_event = EVENT_TOOL_ORDER[new_index]
    print("Event:", game.current_event)


def cycle_tool_mode(game):
    current_index = TOOL_MODE_ORDER.index(game.current_tool)
    new_index = (current_index + 1) % len(TOOL_MODE_ORDER)
    game.current_tool = TOOL_MODE_ORDER[new_index]
    print("Tool mode:", game.current_tool)


def spawn_current_critter(game, tile):
    if tile is None:
        return False

    critter_cls = CRITTER_TYPES[game.current_critter]
    if tile.terrain not in critter_cls.ALLOWED_TERRAINS:
        return False

    if tile.critter is not None:
        remove_critter(game, tile.critter, f"it was replaced by a spawned {game.current_critter}")

    critter = critter_cls(tile.x, tile.y)
    if isinstance(tile.building, WolfDen) and game.current_critter == "wolf":
        critter.set_home_building(tile.building)
    tile.critter = critter
    game.critters.append(critter)
    print(f"Spawned {game.current_critter} {critter.id} at ({tile.x}, {tile.y})")
    return True


def place_current_building(game, tile):
    if tile is None or tile.building is not None:
        return False

    if game.current_building == "village" and tile.has_tag("land"):
        tile.building = City(tile.x, tile.y, level="village", population=10)
        print(f"Placed village at ({tile.x}, {tile.y})")
        return True

    if game.current_building == "wolf_den" and WolfDen.can_place_on_tile(tile):
        tile.building = WolfDen(tile.x, tile.y, charges=1)
        print(f"Placed wolf den at ({tile.x}, {tile.y})")
        return True

    return False


def apply_active_tool(game, tile):
    if tile is None:
        return False

    if game.current_tool == "critter":
        return spawn_current_critter(game, tile)

    if game.current_tool == "building":
        return place_current_building(game, tile)

    if game.current_tool == "event":
        return trigger_event_tool(game, tile, game.current_event)

    paint_radius(game, tile, game.current_terrain, game.brush_size)
    return True


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

            elif event.key == pygame.K_a:
                if game.current_tool == "critter":
                    cycle_critter(game, -1)
                elif game.current_tool == "building":
                    cycle_building(game, -1)
                elif game.current_tool == "event":
                    cycle_event_tool(game, -1)
                else:
                    cycle_terrain(game, -1)

            elif event.key == pygame.K_d:
                if game.current_tool == "critter":
                    cycle_critter(game, 1)
                elif game.current_tool == "building":
                    cycle_building(game, 1)
                elif game.current_tool == "event":
                    cycle_event_tool(game, 1)
                else:
                    cycle_terrain(game, 1)

            elif event.key == pygame.K_q:
                game.brush_size = max(0, game.brush_size - 1)
                print("Brush size:", game.brush_size)

            elif event.key == pygame.K_e:
                game.brush_size += 1
                print("Brush size:", game.brush_size)

            elif event.key == pygame.K_PERIOD:
                game.speed = min(16, game.speed * 2)
                print("Speed:", game.speed)

            elif event.key == pygame.K_COMMA:
                game.speed = max(0.25, game.speed / 2)
                print("Speed:", game.speed)

            elif event.key == pygame.K_r:
                cycle_tool_mode(game)

        elif event.type == pygame.MOUSEBUTTONDOWN:
            mx, my = pygame.mouse.get_pos()
            tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

            if event.button == 1:
                apply_active_tool(game, tile)

                if game.current_tool in {"terrain", "critter"}:
                    game.left_mouse_held = True

            elif event.button == 3:
                if tile is not None and tile.critter is not None:
                    critter = tile.critter
                    remove_critter(game, critter, "it was manually deleted")
                    print(f"Deleted critter {critter.id} at ({tile.x}, {tile.y})")

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                game.left_mouse_held = False
