import random
from terrain import terrainLib


def _set_terrain(board, x, y, new_name: str):
    """Terrain setter compatible with cached Tile + old Tile."""
    tile = board[x][y]
    if hasattr(tile, "setTerrainByName"):
        tile.setTerrainByName(new_name)
    else:
        tile.terrain = terrainLib[new_name]()


def erodeCoast(board):
    cols = len(board)
    rows = len(board[0])

    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    source_types = {"stone", "desert", "grass"}
    water_edge = {"ocean", "shallows"}

    candidates = []
    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name in source_types:
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        if board[nx][ny].terrain.name in water_edge:
                            candidates.append((x, y))
                            break

    if not candidates:
        return set()

    ex, ey = random.choice(candidates)
    _set_terrain(board, ex, ey, "beach")
    return {(ex, ey)}


def spawnLake(board):
    cols = len(board)
    rows = len(board[0])

    allowed = {"stone", "desert", "grass", "beach"}
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]
    water_edge = {"ocean", "shallows"}

    # Mode A: west of mountains/dormantVolcano by 2 or 3 tiles, not touching ocean/shallows
    a_candidates = set()
    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name in ("mountain", "dormantVolcano"):
                for dist in (2, 3):
                    nx, ny = x - dist, y
                    if 0 <= nx < cols and 0 <= ny < rows:
                        tname = board[nx][ny].terrain.name
                        if tname in allowed:
                            touches_water = False
                            for dx, dy in directions:
                                ax, ay = nx + dx, ny + dy
                                if 0 <= ax < cols and 0 <= ay < rows:
                                    if board[ax][ay].terrain.name in water_edge:
                                        touches_water = True
                                        break
                            if not touches_water:
                                a_candidates.add((nx, ny))

    # Mode B: isolated ocean/shallows tile becomes lake
    b_candidates = []
    for x in range(1, cols - 1):
        for y in range(1, rows - 1):
            if board[x][y].terrain.name in water_edge:
                isolated = True
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if board[nx][ny].terrain.name in water_edge:
                        isolated = False
                        break
                if isolated:
                    b_candidates.append((x, y))

    use_mode_b = (random.random() < 0.5)

    if use_mode_b and b_candidates:
        lx, ly = random.choice(b_candidates)
        _set_terrain(board, lx, ly, "lake")
        return {(lx, ly)}
    elif (not use_mode_b) and a_candidates:
        lx, ly = random.choice(list(a_candidates))
        _set_terrain(board, lx, ly, "lake")
        return {(lx, ly)}
    else:
        # fallback to other mode
        if b_candidates:
            lx, ly = random.choice(b_candidates)
            _set_terrain(board, lx, ly, "lake")
            return {(lx, ly)}
        if a_candidates:
            lx, ly = random.choice(list(a_candidates))
            _set_terrain(board, lx, ly, "lake")
            return {(lx, ly)}

    return set()


def spawnGrass(board):
    cols = len(board)
    rows = len(board[0])

    def lake_within_radius3(x, y):
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                if dx == 0 and dy == 0:
                    continue
                nx, ny = x + dx, y + dy
                if 0 <= nx < cols and 0 <= ny < rows:
                    if board[nx][ny].terrain.name == "lake":
                        return True
        return False

    candidate_desert = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name == "desert" and lake_within_radius3(x, y)
    ]

    if not candidate_desert:
        # keep your original print (but you can remove later)
        print("No desert tiles near lakes (radius 3) found.")
        return set()

    gx, gy = random.choice(candidate_desert)
    _set_terrain(board, gx, gy, "grass")
    return {(gx, gy)}


def spawnDesert(board):
    cols = len(board)
    rows = len(board[0])

    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    def touches_water(x, y):
        for dx, dy in directions:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cols and 0 <= ny < rows:
                if board[nx][ny].terrain.name in ("ocean", "shallows"):
                    return True
        return False

    candidate_tiles = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name in ("stone", "beach")
        and not touches_water(x, y)
    ]

    if not candidate_tiles:
        return set()

    gx, gy = random.choice(candidate_tiles)
    _set_terrain(board, gx, gy, "desert")
    return {(gx, gy)}


def spawnShallows(board):
    cols = len(board)
    rows = len(board[0])

    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    candidate_ocean = []

    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name == "ocean":
                # qualifies only if ANY neighbor is NOT ocean AND NOT shallows
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        neigh = board[nx][ny].terrain.name
                        if neigh not in ("ocean", "shallows"):
                            candidate_ocean.append((x, y))
                            break

    if not candidate_ocean:
        return set()

    rx, ry = random.choice(candidate_ocean)
    if board[rx][ry].terrain.name == "ocean":
        _set_terrain(board, rx, ry, "shallows")
        return {(rx, ry)}

    return set()


def meteorStrike(board, critter_list=None):
    cols = len(board)
    rows = len(board[0])

    cx = random.randrange(cols)
    cy = random.randrange(rows)

    area = [
        (x, y)
        for x in range(cx - 2, cx + 3)
        for y in range(cy - 2, cy + 3)
        if 0 <= x < cols and 0 <= y < rows
    ]
    if not area:
        return set()

    num_strikes = random.randint(1, len(area))
    strike_tiles = random.sample(area, num_strikes)

    dirty = set()
    kills = 0
    for x, y in strike_tiles:
        tile = board[x][y]
        if tile.critter is not None:
            victim = tile.critter
            tile.critter = None
            kills += 1
            if critter_list is not None:
                try:
                    critter_list.remove(victim)
                except ValueError:
                    pass

        _set_terrain(board, x, y, "lava")
        dirty.add((x, y))

    print(f"Meteor strike at {cx}, {cy}! Spawned {num_strikes} lava tiles.")
    if kills != 0:
        print(f"Critters lost: {kills}.")

    return dirty