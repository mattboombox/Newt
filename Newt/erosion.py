import random
from terrain import terrainLib

def erodeStone(board):
    cols = len(board)
    rows = len(board[0])

    #8-way adjacency (orthogonal + diagonals)
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    #Get stone tiles adjacent to ocean
    stoneTiles = []
    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name == "stone":
                #Check all neighbors
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        if board[nx][ny].terrain.name == "ocean":
                            stoneTiles.append((x, y))
                            break

    if not stoneTiles:
        #print("Could not find any ocean-adjacent stone tiles to erode!")
        return None

    #Pick one random tile and erode it
    spawnX, spawnY = random.choice(stoneTiles)
    board[spawnX][spawnY].terrain = terrainLib["beach"]()
    #print("Stone tile", spawnX, spawnY, "eroded into a beach!")

def turnToSoil(board):
    cols = len(board)
    rows = len(board[0])

    #8-way adjacency (orthogonal + diagonals)
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    candidateTiles = []
    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name == "stone":
                hasSoilOrBeach = False
                touchesOcean = False

                #Check all neighbors
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cols and 0 <= ny < rows:
                        neighbor = board[nx][ny].terrain.name
                        if neighbor in ("beach", "soil", "grass"):
                            hasSoilOrBeach = True
                        if neighbor == "ocean":
                            touchesOcean = True

                # Eligible if near soil/beach but not near ocean
                if hasSoilOrBeach and not touchesOcean:
                    candidateTiles.append((x, y))

    if not candidateTiles:
        #print("No valid stone tiles to turn into soil!")
        return None

    #Pick one random tile and convert it
    spawnX, spawnY = random.choice(candidateTiles)
    board[spawnX][spawnY].terrain = terrainLib["soil"]()
    print("Stone tile", spawnX, spawnY, "turned into soil!")

def spawnLake(board):
    cols = len(board)
    rows = len(board[0])

    # 8-way adjacency (orthogonal + diagonals)
    directions = [
        (-1, -1), (0, -1), (1, -1),
        (-1,  0),          (1,  0),
        (-1,  1), (0,  1), (1,  1)
    ]

    candidateTiles = set()

    for x in range(cols):
        for y in range(rows):
            if board[x][y].terrain.name == "mountain":
                #Check each neighbor of the mountain as a potential lake spot
                for dx, dy in directions:
                    nx, ny = x + dx, y + dy
                    if not (0 <= nx < cols and 0 <= ny < rows):
                        continue

                    #Skip tiles that already are water/mountain
                    if board[nx][ny].terrain.name in ("ocean", "lake", "mountain"):
                        continue

                    #Ensure this potential lake tile is NOT adjacent to ocean (8-way)
                    touchesOcean = False
                    for ddx, ddy in directions:
                        ax, ay = nx + ddx, ny + ddy
                        if 0 <= ax < cols and 0 <= ay < rows:
                            if board[ax][ay].terrain.name == "ocean":
                                touchesOcean = True
                                break

                    if not touchesOcean:
                        candidateTiles.add((nx, ny))

    if not candidateTiles:
        #print("No valid tiles to spawn a lake!")
        return None

    # Pick one random candidate and make it a lake
    lx, ly = random.choice(list(candidateTiles))
    board[lx][ly].terrain = terrainLib["lake"]()
    print(f"Lake spawned at {lx}, {ly}")

def spawnGrass(board):
    cols = len(board)
    rows = len(board[0])

    def lakeWithinRadius3(x, y):
        #Check all neighbors with Chebyshev distance <= 2 (skip self)
        for dx in range(-3, 4):
            for dy in range(-3, 4):
                if dx == 0 and dy == 0:
                    continue
                nx, ny = x + dx, y + dy
                if 0 <= nx < cols and 0 <= ny < rows:
                    if board[nx][ny].terrain.name == "lake":
                        return True
        return False

    #Soil tiles that have a lake within radius 2
    candidateSoil = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name == "soil" and lakeWithinRadius3(x, y)
    ]

    if not candidateSoil:
        #print("No lake-adjacent soil tiles (radius 2) found.")
        return None

    gx, gy = random.choice(candidateSoil)
    board[gx][gy].terrain = terrainLib["grass"]()
    print(f"Grass spawned at {gx}, {gy}!")
