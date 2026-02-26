import random
import math
from terrain import terrainLib

# Cached sets so we don't scan the whole board every time
ACTIVE = set()   # (x,y) where terrain == activeVolcano
DORMANT = set()  # (x,y) where terrain == dormantVolcano
LAVA = set()     # (x,y) where terrain == lava


def init_caches(board):
    """Scan board once to build caches. Call after initial terrain generation."""
    ACTIVE.clear()
    DORMANT.clear()
    LAVA.clear()

    cols = len(board)
    rows = len(board[0])
    for x in range(cols):
        for y in range(rows):
            name = board[x][y].terrain.name
            if name == "activeVolcano":
                ACTIVE.add((x, y))
            elif name == "dormantVolcano":
                DORMANT.add((x, y))
            elif name == "lava":
                LAVA.add((x, y))


def _set_terrain(board, x, y, new_name: str):
    """
    Central terrain setter:
    - uses Tile.setTerrainByName if present (keeps cached color correct)
    - updates volcano module caches
    """
    tile = board[x][y]
    old_name = tile.terrain.name

    # Write terrain (supports your new Tile class and old one)
    if hasattr(tile, "setTerrainByName"):
        tile.setTerrainByName(new_name)
    else:
        tile.terrain = terrainLib[new_name]()

    # Remove from old cache
    if old_name == "activeVolcano":
        ACTIVE.discard((x, y))
    elif old_name == "dormantVolcano":
        DORMANT.discard((x, y))
    elif old_name == "lava":
        LAVA.discard((x, y))

    # Add to new cache
    if new_name == "activeVolcano":
        ACTIVE.add((x, y))
    elif new_name == "dormantVolcano":
        DORMANT.add((x, y))
    elif new_name == "lava":
        LAVA.add((x, y))


def getIslandSeed(board):
    """Pick a random ocean tile and turn it into an active volcano. Returns dirty tiles."""
    cols = len(board)
    rows = len(board[0])

    # Try random sampling first to avoid a full scan most of the time
    for _ in range(500):
        x = random.randrange(cols)
        y = random.randrange(rows)
        if board[x][y].terrain.name == "ocean":
            _set_terrain(board, x, y, "activeVolcano")
            return {(x, y)}

    # Fallback scan (rare)
    ocean_tiles = [(x, y) for x in range(cols) for y in range(rows) if board[x][y].terrain.name == "ocean"]
    if not ocean_tiles:
        return set()

    x, y = random.choice(ocean_tiles)
    _set_terrain(board, x, y, "activeVolcano")
    return {(x, y)}


def _random_point_in_radius(cx, cy, radius):
    theta = random.uniform(0, 2 * math.pi)
    r = radius * math.sqrt(random.random())
    x = cx + int(round(r * math.cos(theta)))
    y = cy + int(round(r * math.sin(theta)))
    return x, y


def eruptVolcano(board, critter_list=None):
    """Random active volcano erupts: turns a random tile in radius into lava. Returns dirty tiles."""
    if not ACTIVE:
        return set()

    cols = len(board)
    rows = len(board[0])
    radius = 5

    vx, vy = random.choice(tuple(ACTIVE))

    ex, ey = _random_point_in_radius(vx, vy, radius)

    tries = 0
    while tries < 50 and (not (0 <= ex < cols and 0 <= ey < rows) or (ex, ey) == (vx, vy)):
        ex, ey = _random_point_in_radius(vx, vy, radius)
        tries += 1

    if not (0 <= ex < cols and 0 <= ey < rows) or (ex, ey) == (vx, vy):
        return set()

    target = board[ex][ey]
    if target.terrain.name in ("activeVolcano", "dormantVolcano", "mountain"):
        return set()

    # Kill critter if present
    if target.critter is not None:
        victim = target.critter
        print("Critter", victim.name, "has been killed by an eruption")
        target.critter = None
        if critter_list is not None:
            try:
                critter_list.remove(victim)
            except ValueError:
                pass

    _set_terrain(board, ex, ey, "lava")
    return {(ex, ey)}


def coolLava(board):
    """Turn a random lava tile into stone. Returns dirty tiles."""
    if not LAVA:
        return set()

    x, y = random.choice(tuple(LAVA))
    _set_terrain(board, x, y, "stone")
    return {(x, y)}


def toggleVolcano(board):
    """Toggle a random volcano between active/dormant. Returns dirty tiles."""
    if not ACTIVE and not DORMANT:
        return set()

    pool = tuple(ACTIVE | DORMANT)
    x, y = random.choice(pool)

    if (x, y) in ACTIVE:
        _set_terrain(board, x, y, "dormantVolcano")
    else:
        _set_terrain(board, x, y, "activeVolcano")

    return {(x, y)}


def killVolcano(board):
    """
    Turn a random volcano into mountain.
    90% chance spawn a new active volcano adjacent.
    Returns dirty tiles.
    """
    if not ACTIVE and not DORMANT:
        return set()

    cols = len(board)
    rows = len(board[0])

    vx, vy = random.choice(tuple(ACTIVE | DORMANT))
    dirty = {(vx, vy)}

    _set_terrain(board, vx, vy, "mountain")

    if random.random() < 0.90:
        directions = [
            (-1, -1), (0, -1), (1, -1),
            (-1,  0),          (1,  0),
            (-1,  1), (0,  1), (1,  1),
        ]
        neighbors = [(vx + dx, vy + dy) for dx, dy in directions if 0 <= vx + dx < cols and 0 <= vy + dy < rows]
        if neighbors:
            nx, ny = random.choice(neighbors)
            _set_terrain(board, nx, ny, "activeVolcano")
            dirty.add((nx, ny))

    return dirty