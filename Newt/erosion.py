import random


def is_ocean_adjacent(world, tile):
    neighbors = world.get_neighbors_all(tile.x, tile.y)
    return any(neighbor.terrain == "ocean" for neighbor in neighbors)


def erode_tile(world, tile):
    if tile is None:
        return False

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
            if tile.terrain in ("stone", "beach", "sand", "grass"):
                erodible.append(tile)

    return erodible


def trigger_random_erosion(world):
    erodible_tiles = get_erodible_tiles(world)

    if not erodible_tiles:
        return False

    tile = random.choice(erodible_tiles)
    return erode_tile(world, tile)