import random
from terrain import terrainLib

def erodeCoast(board):
    cols = len(board)
    rows = len(board[0])

    # 8-way adjacency (orthogonal + diagonals)
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    source_types = {"stone", "desert", "grass"}
    water_edge = {"ocean", "reef"}

    # Collect tiles that should erode to beach
    candidates = []
    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name in source_types:
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        if board[nx][ny].terrain.name in water_edge:
                            candidates.append((x, y))
                            break  # one neighbor is enough

    if not candidates:
        # print("No coast-adjacent tiles (stone/desert next to ocean/reef).")
        return None

    # Pick one random coastal tile to erode
    ex, ey = random.choice(candidates)
    board[ex][ey].terrain = terrainLib["beach"]()
    # print(f"Tile {ex}, {ey} eroded into a beach!")

def spawnLake(board):
    cols = len(board)
    rows = len(board[0])

    allowed = {"stone", "desert", "grass", "beach"}
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]
    water_edge = {"ocean", "reef"}

    candidate_tiles = set()

    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name in ("mountain", "dormantVolcano"):
                # directly west by 2 and 3 tiles
                for dist in (2, 3):
                    nx, ny = x - dist, y  # west = negative x
                    if 0 <= nx < cols and 0 <= ny < rows:
                        tname = board[nx][ny].terrain.name
                        if tname in allowed:
                            # reject if adjacent to ocean or reef
                            touches_water = False
                            for dx, dy in directions:
                                ax, ay = nx + dx, ny + dy
                                if 0 <= ax < cols and 0 <= ay < rows:
                                    if board[ax][ay].terrain.name in water_edge:
                                        touches_water = True
                                        break
                            if not touches_water:
                                candidate_tiles.add((nx, ny))

    if not candidate_tiles:
        # print("No valid tiles to spawn a lake!")
        return None

    lx, ly = random.choice(list(candidate_tiles))
    board[lx][ly].terrain = terrainLib["lake"]()
    #print(f"Lake spawned at {lx}, {ly}")

def spawnGrass(board):
    cols = len(board)
    rows = len(board[0])

    def lakeWithinRadius3(x, y):
        # Check all neighbors with Chebyshev distance <= 3 (skip self)
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                if dx == 0 and dy == 0:
                    continue
                nx, ny = x + dx, y + dy
                if 0 <= nx < cols and 0 <= ny < rows:
                    if board[nx][ny].terrain.name == "lake":
                        return True
        return False

    # Desert tiles that have a lake within radius 3
    candidateDesert = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name == "desert" and lakeWithinRadius3(x, y)
    ]

    if not candidateDesert:
        # print("No desert tiles near lakes (radius 3) found.")
        return None

    gx, gy = random.choice(candidateDesert)
    board[gx][gy].terrain = terrainLib["grass"]()
    #print(f"Grass spawned at {gx}, {gy}!")

def spawnDesert(board):
    cols = len(board)
    rows = len(board[0])

    def touchesOcean(x, y):
        # 8-way adjacency
        directions = [
            (-1, -1), (0, -1), (1, -1),
            (-1,  0),          (1,  0),
            (-1,  1), (0,  1), (1,  1)
        ]
        for dx, dy in directions:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cols and 0 <= ny < rows:
                if board[nx][ny].terrain.name == "ocean":
                    return True
        return False

    # Any stone tile not adjacent to ocean
    candidateStone = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name == "stone"
           and not touchesOcean(x, y)
    ]

    if not candidateStone:
        # print("No valid stone tiles to spawn a desert!")
        return None

    gx, gy = random.choice(candidateStone)
    board[gx][gy].terrain = terrainLib["desert"]()
    #print(f"Desert spawned at {gx}, {gy}!")

def spawnReef(board):
    cols = len(board)
    rows = len(board[0])

    # 8-way adjacency directions
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    candidateOcean = []

    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name == "ocean":
                # check all neighbors
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        if board[nx][ny].terrain.name == "beach":
                            candidateOcean.append((x, y))
                            break  # only need one beach neighbor to qualify

    if not candidateOcean:
        # print("No ocean tiles adjacent to beaches.")
        return None

    rx, ry = random.choice(candidateOcean)
    board[rx][ry].terrain = terrainLib["reef"]()
    #print(f"Reef spawned at {rx}, {ry}!")

def meteorStrike(board, critter_list=None):
    cols = len(board)
    rows = len(board[0])

    # Pick a random strike center
    cx = random.randrange(cols)
    cy = random.randrange(rows)

    # Build the 5x5 area centered on (cx, cy)
    area = [
        (x, y)
        for x in range(cx - 2, cx + 3)
        for y in range(cy - 2, cy + 3)
        if 0 <= x < cols and 0 <= y < rows
    ]
    if not area:
        return None

    # Pick a random number of tiles from that area
    num_strikes = random.randint(1, len(area))
    strike_tiles = random.sample(area, num_strikes)

    # Convert them to lava (kill critters first)
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
                    pass  # not tracked; ignore
        tile.terrain = terrainLib["lava"]()

    print(f"Meteor strike at {cx}, {cy}! Spawned {num_strikes} lava tiles.")
    if kills != 0:
        print("Critters lost: {kills}.")

