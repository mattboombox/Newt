import random
from lake import try_spawn_lake_from_mountain, trigger_random_lake_growth


def erode_tile(world, tile):
    if tile is None:
        return False

    # Lava cools into stone
    if tile.terrain == "lava":
        tile.set_terrain("stone")
        return True

    # Lake touching ocean becomes ocean
    if tile.terrain == "lake":
        if world.is_adjacent_to_terrain(tile.x, tile.y, {"ocean"}):
            tile.set_terrain("ocean")
            return True
        return False

    # Mountains can rarely create lakes
    if tile.terrain == "mountain":
        if random.random() < 0.01:
            return try_spawn_lake_from_mountain(world, tile)
    
    if random.random() < 0.01:
        trigger_random_lake_growth(world)

    near_ocean = world.is_adjacent_to_terrain(tile.x, tile.y, {"ocean"})

    # Stone erodes into beach or sand
    if tile.terrain == "stone":
        if near_ocean:
            tile.set_terrain("beach")
        else:
            tile.set_terrain("sand")
        return True

    # Beach should only stay beach if still coastal
    if tile.terrain == "beach":
        if not near_ocean:
            tile.set_terrain("sand")
            return True
        return False

    # Sand touching ocean should become beach
    if tile.terrain == "sand":
        if near_ocean:
            tile.set_terrain("beach")
            return True
        return False

    # Grass touching ocean should become beach
    if tile.terrain == "grass":
        if near_ocean:
            tile.set_terrain("beach")
            return True
        return False

    return False


def get_erodible_tiles(world):
    erodible = []

    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.board[x][y]
            if tile.terrain in ("lava", "lake", "stone", "beach", "sand", "grass", "mountain"):
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

            if tile.terrain == "ocean":
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

            if tile.terrain == "ocean":
                tile.set_terrain("ice_sheet")
                changed = True
            elif tile.terrain not in ("ice_sheet", "mountain", "active_volcano", "dormant_volcano", "lava"):
                tile.set_terrain("snow")
                changed = True

    return changed


def trigger_random_erosion(world):
    erodible_tiles = get_erodible_tiles(world)

    if not erodible_tiles:
        return False

    tile = random.choice(erodible_tiles)
    return erode_tile(world, tile)