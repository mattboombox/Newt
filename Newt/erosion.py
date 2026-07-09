import random
from lake import try_spawn_lake_from_mountain, convert_landlocked_shallows_to_lake

COASTAL_WATER_TERRAINS = {"ocean", "trench", "shallows"}
POLAR_WATER_TERRAINS = {"ocean", "trench", "lake", "shallows"}
ERODIBLE_TERRAINS = {"lava", "lake", "stone", "beach", "sand", "grass", "mountain", "trench", "shallows", "snow"}


def is_erodible_terrain(terrain_name):
    return terrain_name in ERODIBLE_TERRAINS


def get_shallowable_ocean_neighbors(world, tile):
    ocean_neighbors = []

    for neighbor in world.get_neighbors_all(tile.x, tile.y):
        if neighbor.terrain != "ocean":
            continue

        if world.is_adjacent_to_terrain(neighbor.x, neighbor.y, {"trench"}):
            continue

        ocean_neighbors.append(neighbor)

    return ocean_neighbors


def erode_tile(world, tile):
    if tile is None:
        return False

    near_ocean = world.is_adjacent_to_terrain(tile.x, tile.y, COASTAL_WATER_TERRAINS)

    # Lava cools into stone
    if tile.terrain == "lava":
        tile.set_terrain("stone")
        return True

    # Lake touching ocean/shallows becomes ocean
    if tile.terrain == "lake":
        if near_ocean:
            tile.set_terrain("ocean")
            return True
        return False

    # Trenches reclaim nearby shallows so shallow-water creep does not fill them in
    if tile.terrain == "trench":
        changed = False
        for neighbor in world.get_neighbors_all(tile.x, tile.y):
            if neighbor.terrain == "shallows":
                neighbor.set_terrain("ocean")
                changed = True
        return changed

    # Mountains can rarely create lakes
    if tile.terrain == "mountain":
        if random.random() < 0.01:
            return try_spawn_lake_from_mountain(world, tile)

    # Stone near ocean becomes beach, otherwise sand
    if tile.terrain == "stone":
        tile.set_terrain("beach" if near_ocean else "sand")
        return True

    # Beach should only stay coastal if still near ocean
    if tile.terrain == "beach":
        if not near_ocean:
            tile.set_terrain("sand")
            return True

        # Chance to create shallows in adjacent ocean
        ocean_neighbors = get_shallowable_ocean_neighbors(world, tile)
        if ocean_neighbors:
            chosen_ocean = random.choice(ocean_neighbors)
            chosen_ocean.set_terrain("shallows")
            return True

        return False

    # Snow can also create shallows, but never turns into sand
    if tile.terrain == "snow":
        ocean_neighbors = get_shallowable_ocean_neighbors(world, tile)
        if ocean_neighbors:
            chosen_ocean = random.choice(ocean_neighbors)
            chosen_ocean.set_terrain("shallows")
            return True

        return False

    # Sand and grass can erode into beach if near ocean
    if tile.terrain in ("sand", "grass"):
        if near_ocean:
            tile.set_terrain("beach")
            return True
        return False

    # Shallows surrounded by land become lake
    if tile.terrain == "shallows":
        convert_landlocked_shallows_to_lake(world)


def get_erodible_tiles(world):
    erodible = []

    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.board[x][y]
            if is_erodible_terrain(tile.terrain):
                erodible.append(tile)

    return erodible


def apply_polar_climate(world):
    changed = False

    for x in range(world.cols):
        polar_depth = 3

        if (x % 6 == 0) or (x % 11 == 0):
            polar_depth += 1
        if x % 8 == 0:
            polar_depth -= 1

        polar_depth = max(2, min(4, polar_depth))

        # Top pole
        for y in range(polar_depth):
            tile = world.get_tile(x, y)
            if tile is None:
                continue

            if tile.terrain in POLAR_WATER_TERRAINS:
                tile.set_terrain("ice_sheet")
                changed = True
            elif tile.terrain not in ("ice_sheet","mountain", "active_volcano", "dormant_volcano", "lava"):
                tile.set_terrain("snow")
                changed = True

        # Bottom pole
        for y in range(world.rows - polar_depth, world.rows):
            tile = world.get_tile(x, y)
            if tile is None:
                continue

            if tile.terrain in POLAR_WATER_TERRAINS:
                tile.set_terrain("ice_sheet")
                changed = True
            elif tile.terrain not in ("ice_sheet", "mountain", "active_volcano", "dormant_volcano", "lava"):
                tile.set_terrain("snow")
                changed = True

    return changed


def trigger_random_erosion(world):
    tile = world.get_random_erodible_tile()
    if tile is not None:
        return erode_tile(world, tile)

    erodible_tiles = get_erodible_tiles(world)
    if not erodible_tiles:
        return False

    tile = random.choice(erodible_tiles)
    return erode_tile(world, tile)
