import random


def is_lake_nearby(world, tile, radius=2):
    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            if dx == 0 and dy == 0:
                continue

            nearby = world.get_tile(tile.x + dx, tile.y + dy)
            if nearby is not None and nearby.terrain == "lake":
                return True

    return False


def grow_tile(world, tile):
    if tile is None:
        return False

    # Sand near lakes can become grass
    if tile.terrain == "sand":
        if is_lake_nearby(world, tile, radius=2):
            tile.set_terrain("grass")
            return True

    return False


def get_growable_tiles(world):
    growable = []

    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.board[x][y]
            if tile.terrain == "sand":
                growable.append(tile)

    return growable


def trigger_random_growth(world):
    growable_tiles = get_growable_tiles(world)

    if not growable_tiles:
        return False

    tile = random.choice(growable_tiles)
    return grow_tile(world, tile)