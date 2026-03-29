import random
from collections import deque


def try_spawn_lake_from_mountain(world, mountain_tile):
    valid_tiles = []

    for dx in range(-4, 5):
        for dy in range(-4, 5):
            if dx == 0 and dy == 0:
                continue

            distance = max(abs(dx), abs(dy))

            # Keep lake candidates a little away from mountains
            if distance < 2 or distance > 4:
                continue

            tile = world.get_tile(mountain_tile.x + dx, mountain_tile.y + dy)
            if tile is None:
                continue

            if tile.terrain not in ("sand", "grass"):
                continue

            if world.is_adjacent_to_terrain(tile.x, tile.y, {"ocean"}):
                continue

            if world.is_adjacent_to_terrain(
                tile.x, tile.y, {"mountain", "active_volcano", "dormant_volcano"}
            ):
                continue

            open_count = world.count_terrain_in_radius(
                tile.x, tile.y, {"sand"}, radius=1
            )
            if open_count < 5:
                continue

            valid_tiles.append(tile)

    if not valid_tiles:
        return False

    chosen_tile = random.choice(valid_tiles)
    chosen_tile.set_terrain("lake")
    return True


def convert_landlocked_ocean_to_lake(world):
    visited = set()
    queue = deque()

    water_terrain = {"ocean", "shallows"}

    # Start flood fill from edge water tiles
    for tile in world.get_edge_tiles():
        if tile.terrain in water_terrain:
            pos = (tile.x, tile.y)
            if pos not in visited:
                visited.add(pos)
                queue.append(pos)

    # Mark all water connected to the map edge
    while queue:
        x, y = queue.popleft()

        for neighbor in world.get_neighbors_all(x, y):
            if neighbor.terrain in water_terrain:
                pos = (neighbor.x, neighbor.y)
                if pos not in visited:
                    visited.add(pos)
                    queue.append(pos)

    # Any ocean not connected to edge water becomes lake
    changed = False
    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.get_tile(x, y)
            if tile is None:
                continue

            if tile.terrain == "ocean" and (x, y) not in visited:
                tile.set_terrain("lake")
                changed = True

    return changed


def grow_lake(world, tile):
    if tile is None or tile.terrain != "lake":
        return False

    valid_neighbors = []

    for neighbor in world.get_neighbors_all(tile.x, tile.y):
        if neighbor.terrain not in ("sand", "stone"):
            continue

        # Don't grow into coastline
        if world.is_adjacent_to_terrain(neighbor.x, neighbor.y, {"ocean"}):
            continue

        valid_neighbors.append(neighbor)

    if not valid_neighbors:
        return False

    chosen_tile = random.choice(valid_neighbors)
    chosen_tile.set_terrain("lake")
    return True


def get_lake_tiles(world):
    lake_tiles = []

    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.get_tile(x, y)
            if tile is not None and tile.terrain == "lake":
                lake_tiles.append(tile)

    return lake_tiles


def trigger_random_lake_growth(world):
    lake_tiles = get_lake_tiles(world)

    if not lake_tiles:
        return False

    chosen_lake = random.choice(lake_tiles)
    return grow_lake(world, chosen_lake)