import random


def is_ocean_adjacent(world, tile):
    neighbors = world.get_neighbors_all(tile.x, tile.y)
    return any(neighbor.terrain == "ocean" for neighbor in neighbors)


def try_spawn_lake_from_mountain(world, mountain_tile):
    candidate_positions = [
        (mountain_tile.x - 1, mountain_tile.y),      # west
        (mountain_tile.x - 1, mountain_tile.y - 1),  # northwest
        (mountain_tile.x,     mountain_tile.y - 1),  # north
    ]

    valid_tiles = []

    for x, y in candidate_positions:
        tile = world.get_tile(x, y)

        if tile is None:
            continue

        if tile.terrain != "sand" and tile.terrain != "grass":
            continue

        if is_ocean_adjacent(world, tile):
            continue

        valid_tiles.append(tile)

    if not valid_tiles:
        return False

    if random.random() < 0.35:
        chosen_tile = random.choice(valid_tiles)
        chosen_tile.set_terrain("lake")
        return True

    return False


def erode_tile(world, tile):
    if tile is None:
        return False

    # Lava cools into stone
    if tile.terrain == "lava":
        tile.set_terrain("stone")
        return True

    # Lake touching ocean becomes ocean
    if tile.terrain == "lake":
        if is_ocean_adjacent(world, tile):
            tile.set_terrain("ocean")
            return True
        return False

    # Mountains can create lakes to their west
    if tile.terrain == "mountain":
        return try_spawn_lake_from_mountain(world, tile)

    near_ocean = is_ocean_adjacent(world, tile)

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


def trigger_random_erosion(world):
    erodible_tiles = get_erodible_tiles(world)

    if not erodible_tiles:
        return False

    tile = random.choice(erodible_tiles)
    return erode_tile(world, tile)