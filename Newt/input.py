import pygame
from brush import paint_radius, trigger_event_tool, trigger_evolution_tool
from config import (
    CLOSE_UP_TILE_SIZE,
    OVERVIEW_TILE_SIZE,
    SPRITE_PATHS,
    TILE_SIZE,
    ZOOMED_IN_TILE_SIZE,
)
from terrain import TERRAIN_DATA
from critter import (
    Ape,
    ApeSailor,
    ApeWarrior,
    CRITTER_ORDER,
    CRITTER_TYPES,
    Dog,
    Eagle,
    Merfolk,
    MerfolkWarrior,
    SaintSmasher,
    Undead,
    UndeadBeast,
)
from city import City
from building import (
    Church,
    CritterPrinter,
    Farm,
    MilitaryDistrict,
    NavalDistrict,
    ResidentialDistrict,
    Ruins,
    WolfDen,
)
from entity_cleanup import remove_building_at_tile, remove_critter
from evolution import get_evolution_result_types


TOOL_CATEGORY_ORDER = ["terrain", "event", "spawning", "tools"]
SPAWNING_TOOL_ORDER = ["critter", "alt_critter", "building"]
TOOLS_ORDER = ["inspect", "evolve", "war"]
BUILDING_ORDER = [
    "village",
    "farm",
    "residential_district",
    "church",
    "naval_district",
    "military_district",
    "wolf_den",
    "critter_printer",
]
EVENT_TOOL_ORDER = [
    "meteor",
    "mega_meteor",
    "comet",
    "tsunami",
    "tectonic_uplift",
    "island_uplift",
    "trench_event",
]
EVENT_ONLY_TERRAINS = {"meteor", "comet", "tectonic_uplift", "tsunami"}
EVOLUTION_RESULT_TYPES = get_evolution_result_types()
MANUAL_ALT_CRITTER_TYPES = {
    "eagle": Eagle,
    "merfolk": Merfolk,
    "merfolk_warrior": MerfolkWarrior,
    "ape_sailor": ApeSailor,
    "ape_warrior": ApeWarrior,
    "saint_smasher": SaintSmasher,
    "undead": Undead,
    "undead_beast": UndeadBeast,
}
SPAWN_CRITTER_TYPES = {
    **CRITTER_TYPES,
    **MANUAL_ALT_CRITTER_TYPES,
}
REGULAR_SPAWN_EXCEPTIONS = {"plankton"}
REGULAR_CRITTER_ORDER = [
    critter_name
    for critter_name in CRITTER_ORDER
    if (
        CRITTER_TYPES[critter_name] in EVOLUTION_RESULT_TYPES
        or critter_name in REGULAR_SPAWN_EXCEPTIONS
    )
]
ALT_CRITTER_ORDER = [
    critter_name
    for critter_name in CRITTER_ORDER
    if (
        CRITTER_TYPES[critter_name] not in EVOLUTION_RESULT_TYPES
        and critter_name not in REGULAR_SPAWN_EXCEPTIONS
    )
] + list(MANUAL_ALT_CRITTER_TYPES.keys())
ZOOM_TILE_SIZES = (
    OVERVIEW_TILE_SIZE,
    TILE_SIZE,
    ZOOMED_IN_TILE_SIZE,
    CLOSE_UP_TILE_SIZE,
)
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
    if game.current_tool == "alt_critter":
        order = ALT_CRITTER_ORDER
        attribute = "current_alt_critter"
        label = "Alt critter"
    else:
        order = REGULAR_CRITTER_ORDER
        attribute = "current_critter"
        label = "Critter"

    current_critter = getattr(game, attribute)
    current_index = order.index(current_critter)
    new_critter = order[(current_index + step) % len(order)]
    setattr(game, attribute, new_critter)
    print(f"{label}:", new_critter)


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


def cycle_tools_action(game, step):
    current_index = TOOLS_ORDER.index(game.current_tools_action)
    new_index = (current_index + step) % len(TOOLS_ORDER)
    game.current_tools_action = TOOLS_ORDER[new_index]
    print("Tool:", game.current_tools_action)


def get_tool_category(current_tool):
    return "spawning" if current_tool in SPAWNING_TOOL_ORDER else current_tool


def cycle_tool_mode(game):
    current_category = get_tool_category(game.current_tool)
    current_index = TOOL_CATEGORY_ORDER.index(current_category)
    new_category = TOOL_CATEGORY_ORDER[
        (current_index + 1) % len(TOOL_CATEGORY_ORDER)
    ]
    game.current_tool = (
        game.current_spawning_tool
        if new_category == "spawning"
        else new_category
    )
    print("Tool mode:", game.current_tool)


def cycle_spawning_tool(game):
    if game.current_tool not in SPAWNING_TOOL_ORDER:
        return False

    current_index = SPAWNING_TOOL_ORDER.index(game.current_tool)
    game.current_tool = SPAWNING_TOOL_ORDER[
        (current_index + 1) % len(SPAWNING_TOOL_ORDER)
    ]
    game.current_spawning_tool = game.current_tool
    print("Spawning mode:", game.current_tool)
    return True


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
    sound_name = "zoom_out" if game.tile_size == OVERVIEW_TILE_SIZE else "zoom_in"
    sound = game.sounds.get(sound_name)
    if sound is not None:
        sound.play()
    print(f"Zoom: {game.tile_size}x{game.tile_size} pixels per tile")


def spawn_current_critter(game, tile):
    if tile is None:
        return False

    critter_name = (
        game.current_alt_critter
        if game.current_tool == "alt_critter"
        else game.current_critter
    )
    critter_cls = SPAWN_CRITTER_TYPES[critter_name]
    allowed_terrains = critter_cls.ALLOWED_TERRAINS
    if allowed_terrains is not None and tile.terrain not in allowed_terrains:
        return False

    if tile.critter is not None:
        remove_critter(game, tile.critter, f"it was replaced by a spawned {critter_name}")

    critter = critter_cls(tile.x, tile.y)
    if isinstance(tile.building, WolfDen) and critter_name == "wolf":
        critter.set_home_building(tile.building)
    elif (
        isinstance(tile.building, City)
        and isinstance(critter, Dog)
        and tile.building.has_dog_space()
    ):
        critter.set_home_building(tile.building)
    elif (
        isinstance(tile.building, City)
        and isinstance(critter, Ape)
        and tile.building.has_population_space()
        and tile.building.accepts_resident(critter)
    ):
        critter.set_home_building(tile.building)
    tile.critter = critter
    game.critters.append(critter)

    on_spawn = getattr(critter, "on_spawn", None)
    if on_spawn is not None and not on_spawn(game):
        if tile.critter is critter:
            tile.critter = None
        game.critters.remove(critter)
        print(f"Could not spawn {critter_name}: it needs more open habitat.")
        return False

    print(f"Spawned {critter_name} {critter.id} at ({tile.x}, {tile.y})")
    return True


def place_current_building(game, tile):
    if (
        tile is None
        or (
            tile.building is not None
            and not isinstance(tile.building, Ruins)
        )
    ):
        return False

    if (
        game.current_building == "village"
        and City.can_place_on_tile(tile)
    ):
        tile.building = City(tile.x, tile.y, level="village", world=game.world)
        tile.building.try_build_initial_farm(game.world)
        print(f"Placed village at ({tile.x}, {tile.y})")
        return True

    if game.current_building == "farm":
        village = City.find_connectable_village(game.world, tile)
        if village is None or not village.is_valid_farm_tile(tile):
            return False

        farm = Farm(tile.x, tile.y, settlement=village)
        tile.building = farm
        village.add_aux_building(farm)
        print(f"Placed farm for village at ({village.x}, {village.y})")
        return True

    if game.current_building == "residential_district":
        village = City.find_connectable_village(game.world, tile)
        if village is None or not village.is_valid_auxiliary_tile(tile):
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
        game.current_building == "church"
        and tile.has_tag("land")
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None:
            return False

        church = Church(tile.x, tile.y, settlement=village)
        tile.building = church
        village.add_aux_building(church)
        print(f"Placed church for village at ({village.x}, {village.y})")
        return True

    if game.current_building == "military_district":
        village = City.find_connectable_village(game.world, tile)
        if village is None or not village.is_valid_auxiliary_tile(tile):
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
        and tile.terrain == "shallows"
    ):
        village = City.find_connectable_village(game.world, tile)
        if village is None or village.faction != "ape":
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


def trigger_war_tool(game, tile):
    if tile is None or not isinstance(tile.building, City):
        print("The War tool must be used on a village.")
        return False

    attacker = tile.building
    villages = City.get_villages(game.world)
    for village in villages:
        village.reconcile_residents(game)

    if attacker.population == 0:
        print("That village has no residents and cannot declare war.")
        return False

    candidates = [
        village
        for village in villages
        if village is not attacker and village.population > 0
    ]
    if not candidates:
        print("There is no other populated village to attack.")
        return False

    def distance_to(village):
        dx = abs(attacker.x - village.x)
        dx = min(dx, game.world.cols - dx)
        return dx + abs(attacker.y - village.y)

    defender = min(
        candidates,
        key=lambda village: (
            distance_to(village),
            village.x,
            village.y,
        ),
    )
    if not attacker.declare_war(defender, game.world):
        return False

    print(
        f"War declared between villages at ({attacker.x}, {attacker.y}) "
        f"and ({defender.x}, {defender.y})."
    )
    return True


def apply_active_tool(game, tile):
    if tile is None:
        return False

    if game.current_tool == "tools":
        if game.current_tools_action == "war":
            return trigger_war_tool(game, tile)

        if game.current_tools_action == "evolve":
            return trigger_evolution_tool(game, tile)

        game.selected_critter = tile.critter
        if tile.critter is None:
            game.selection_notice = None
            print("Critter selection cleared")
            return False
        game.selection_notice = None
        print(
            f"Selected {type(tile.critter).__name__} {tile.critter.id} "
            f"at ({tile.critter.x}, {tile.critter.y})"
        )
        game.follow_selected_critter()
        return True

    if game.current_tool in {"critter", "alt_critter"}:
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
                if game.current_tool in {"critter", "alt_critter"}:
                    cycle_critter(game, -1)
                elif game.current_tool == "building":
                    cycle_building(game, -1)
                elif game.current_tool == "event":
                    cycle_event_tool(game, -1)
                elif game.current_tool == "terrain":
                    cycle_terrain(game, -1)
                elif game.current_tool == "tools":
                    cycle_tools_action(game, -1)

            elif event.key == pygame.K_d:
                if game.current_tool in {"critter", "alt_critter"}:
                    cycle_critter(game, 1)
                elif game.current_tool == "building":
                    cycle_building(game, 1)
                elif game.current_tool == "event":
                    cycle_event_tool(game, 1)
                elif game.current_tool == "terrain":
                    cycle_terrain(game, 1)
                elif game.current_tool == "tools":
                    cycle_tools_action(game, 1)

            elif event.key == pygame.K_q:
                if game.current_tool == "terrain":
                    game.brush_size = max(0, game.brush_size - 1)
                    print("Brush size:", game.brush_size)

            elif event.key == pygame.K_e:
                if game.current_tool == "terrain":
                    game.brush_size += 1
                    print("Brush size:", game.brush_size)
                else:
                    cycle_spawning_tool(game)

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

                if game.current_tool in {"terrain", "critter", "alt_critter"}:
                    game.left_mouse_held = True

            elif event.button == 3:
                if (
                    game.current_tool == "tools"
                    and game.current_tools_action == "evolve"
                ):
                    trigger_evolution_tool(game, tile, devolve=True)
                else:
                    remove_tile_occupant(game, tile)

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                game.left_mouse_held = False
