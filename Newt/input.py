import pygame
from brush import paint_radius, trigger_event_tool
from config import SPRITE_PATHS
from terrain import TERRAIN_DATA
from critter import Ape, CRITTER_ORDER, CRITTER_TYPES
from city import City
from building import (
    CritterPrinter,
    Farm,
    MilitaryDistrict,
    NavalDistrict,
    ResidentialDistrict,
    WolfDen,
)
from entity_cleanup import remove_building_at_tile, remove_critter


TOOL_MODE_ORDER = ["terrain", "critter", "building", "event"]
BUILDING_ORDER = [
    "village",
    "farm",
    "residential_district",
    "naval_district",
    "military_district",
    "wolf_den",
    "critter_printer",
]
EVENT_TOOL_ORDER = ["meteor", "mega_meteor", "comet", "tsunami", "tectonic_uplift", "island_uplift", "trench_event", "evolve"]
EVENT_ONLY_TERRAINS = {"meteor", "comet", "tectonic_uplift", "tsunami"}
ZOOM_TILE_SIZES = (16, 8)
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


def reload_sprites_for_zoom(game):
    sprites = {}
    for name, path in SPRITE_PATHS.items():
        image = pygame.image.load(path).convert_alpha()
        target_size = (game.tile_size, game.tile_size)
        sprites[name] = (
            image
            if image.get_size() == target_size
            else pygame.transform.scale(image, target_size)
        )
    game.sprites = sprites


def toggle_zoom(game):
    mouse_x, mouse_y = pygame.mouse.get_pos()
    anchor_tile = game.screen_to_world_tile(mouse_x, mouse_y)
    current_index = ZOOM_TILE_SIZES.index(game.tile_size)
    game.tile_size = ZOOM_TILE_SIZES[(current_index + 1) % len(ZOOM_TILE_SIZES)]

    if anchor_tile is not None:
        game.camera_x = anchor_tile.x - mouse_x // game.tile_size
        game.camera_y = anchor_tile.y - mouse_y // game.tile_size

    game.clamp_camera()
    reload_sprites_for_zoom(game)
    print(f"Zoom: {game.tile_size}x{game.tile_size} pixels per tile")


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
    elif (
        isinstance(tile.building, City)
        and isinstance(critter, Ape)
        and tile.building.has_population_space()
    ):
        critter.set_home_building(tile.building)
    tile.critter = critter
    game.critters.append(critter)

    on_spawn = getattr(critter, "on_spawn", None)
    if on_spawn is not None and not on_spawn(game):
        if tile.critter is critter:
            tile.critter = None
        game.critters.remove(critter)
        print(f"Could not spawn {game.current_critter}: it needs more open habitat.")
        return False

    print(f"Spawned {game.current_critter} {critter.id} at ({tile.x}, {tile.y})")
    return True


def place_current_building(game, tile):
    if tile is None or tile.building is not None:
        return False

    if (
        game.current_building == "village"
        and City.can_place_on_tile(tile)
    ):
        tile.building = City(tile.x, tile.y, level="village", world=game.world)
        tile.building.try_build_initial_farm(game.world)
        print(f"Placed village at ({tile.x}, {tile.y})")
        return True

    if (
        game.current_building == "farm"
        and tile.critter is None
        and tile.terrain == "grass"
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None:
            return False

        farm = Farm(tile.x, tile.y, settlement=village)
        tile.building = farm
        village.add_aux_building(farm)
        print(f"Placed farm for village at ({village.x}, {village.y})")
        return True

    if (
        game.current_building == "residential_district"
        and tile.critter is None
        and tile.has_tag("land")
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None:
            return False

        district = ResidentialDistrict(tile.x, tile.y, settlement=village)
        tile.building = district
        village.add_aux_building(district)
        print(
            f"Placed residential district for village at "
            f"({village.x}, {village.y})"
        )
        return True

    if (
        game.current_building == "military_district"
        and tile.critter is None
        and tile.has_tag("land")
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None:
            return False

        district = MilitaryDistrict(tile.x, tile.y, settlement=village)
        tile.building = district
        village.add_aux_building(district)
        print(
            f"Placed military district for village at "
            f"({village.x}, {village.y})"
        )
        return True

    if (
        game.current_building == "naval_district"
        and tile.critter is None
        and tile.terrain == "shallows"
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None:
            return False

        district = NavalDistrict(tile.x, tile.y, settlement=village)
        tile.building = district
        village.add_aux_building(district)
        print(
            f"Placed naval district for village at "
            f"({village.x}, {village.y})"
        )
        return True

    if game.current_building == "wolf_den" and WolfDen.can_place_on_tile(tile):
        tile.building = WolfDen(tile.x, tile.y, charges=1)
        print(f"Placed wolf den at ({tile.x}, {tile.y})")
        return True

    if game.current_building == "critter_printer":
        tile.building = CritterPrinter(tile.x, tile.y)
        print(f"Placed critter printer at ({tile.x}, {tile.y})")
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


def remove_tile_occupant(game, tile):
    if tile is None:
        return False

    if tile.critter is not None:
        critter = tile.critter
        remove_critter(game, critter, "it was manually deleted")
        print(f"Deleted critter {critter.id} at ({tile.x}, {tile.y})")
        return True

    if tile.building is not None:
        return remove_building_at_tile(game, tile, "it was manually deleted")

    return False


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

            elif event.key == pygame.K_z:
                toggle_zoom(game)

            elif event.key == pygame.K_HOME:
                game.reset_camera()

            elif event.key in (
                pygame.K_LEFT,
                pygame.K_RIGHT,
                pygame.K_UP,
                pygame.K_DOWN,
            ):
                camera_step = 15 if event.mod & pygame.KMOD_SHIFT else 5
                dx = (
                    -camera_step
                    if event.key == pygame.K_LEFT
                    else camera_step
                    if event.key == pygame.K_RIGHT
                    else 0
                )
                dy = (
                    -camera_step
                    if event.key == pygame.K_UP
                    else camera_step
                    if event.key == pygame.K_DOWN
                    else 0
                )
                game.pan_camera(dx, dy)

        elif event.type == pygame.MOUSEBUTTONDOWN:
            mx, my = pygame.mouse.get_pos()
            tile = game.screen_to_world_tile(mx, my)

            if event.button == 1:
                apply_active_tool(game, tile)

                if game.current_tool in {"terrain", "critter"}:
                    game.left_mouse_held = True

            elif event.button == 3:
                remove_tile_occupant(game, tile)

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                game.left_mouse_held = False
