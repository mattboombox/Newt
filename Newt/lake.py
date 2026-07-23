import random
from collections import deque


MAX_FLOOD_FILL_LAKE_TILES = 15


def try_spawn_lake_from_mountain(world, mountain_tile):
    # Define the range once. Range(2, 5) covers distances 2, 3, and 4.
    # We use a set for faster lookup of blocked terrains.
    BLOCKED_TERRAINS = {"mountain", "active_volcano", "dormant_volcano"}
    
    # Generate coordinates in the valid ring (Chebyshev distance 2 to 4)
    offsets = [(dx, dy) for dx in range(-4, 5) for dy in range(-4, 5)
               if 2 <= max(abs(dx), abs(dy)) <= 4 and dx <= 0 and dy <= 0]

    valid_tiles = []
    for dx, dy in offsets:
        tile = world.get_tile(mountain_tile.x + dx, mountain_tile.y + dy)
        
        # Combined Validation: 
        # 1. Must exist 2. Must be sand 3. No oceans nearby 4. No mountains nearby
        if (tile and tile.terrain == "sand" and
            not world.is_adjacent_to_terrain(tile.x, tile.y, {"ocean", "trench"}) and
            not world.is_adjacent_to_terrain(tile.x, tile.y, BLOCKED_TERRAINS)):
            
            # Final check: ensure the area is clear enough
            if world.count_terrain_in_radius(tile.x, tile.y, {"sand", "grass"}, radius=1) >= 5:
                valid_tiles.append(tile)

    if not valid_tiles:
        return False

    random.choice(valid_tiles).set_terrain("lake")
    return True


def convert_landlocked_ocean_to_lake(world, max_lake_tiles=MAX_FLOOD_FILL_LAKE_TILES):
    visited = set()
    queue = deque()

    water_terrain = {"ocean", "trench", "shallows"}

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

    # Convert only small enclosed bodies.  Larger bodies remain oceanic so a
    # single flood fill cannot create a huge lake habitat.
    changed = False
    for x in range(world.cols):
        for y in range(world.rows):
            start_tile = world.get_tile(x, y)
            start_pos = (x, y)
            if (
                start_tile is None
                or start_tile.terrain not in water_terrain
                or start_pos in visited
            ):
                continue

            component = []
            component_queue = deque([start_pos])
            visited.add(start_pos)

            while component_queue:
                current_x, current_y = component_queue.popleft()
                component.append((current_x, current_y))

                for neighbor in world.get_neighbors_all(current_x, current_y):
                    pos = (neighbor.x, neighbor.y)
                    if neighbor.terrain in water_terrain and pos not in visited:
                        visited.add(pos)
                        component_queue.append(pos)

            if len(component) > max_lake_tiles:
                continue

            for lake_x, lake_y in component:
                world.get_tile(lake_x, lake_y).set_terrain("lake")
            changed = True

    return changed


def convert_landlocked_shallows_to_lake(world):

    for x in range(world.cols):
        for y in range(world.rows):
            tile = world.get_tile(x, y)
            if tile is None:
                continue

            if tile.terrain == "shallows":
                if not world.is_adjacent_to_terrain(tile.x, tile.y, {"ocean", "trench", "shallows"}):
                    tile.set_terrain("lake")
