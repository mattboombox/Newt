import random, math
from terrain import terrainLib

def getIslandSeed(board):
    cols = len(board)
    rows = len(board[0])

    #Get all ocean tiles
    oceanTiles = [(x, y) for x in range(cols) for y in range(rows)
                   if board[x][y].terrain.name == "ocean"]

    #If no ocean tiles
    if not oceanTiles:
        #print("Could not find any ocean tiles to make island seed!")
        return None

    #Get random ocean tile
    spawnX, spawnY = random.choice(oceanTiles)

    #Spawn volcano
    board[spawnX][spawnY].terrain = terrainLib["activeVolcano"]()

def getRandomRadiusPoint(cx, cy, radius):
    theta = random.uniform(0, 2*math.pi)
    r = radius * math.sqrt(random.random())
    x = cx + int(round(r * math.cos(theta)))
    y = cy + int(round(r * math.sin(theta)))
    return x, y

def eruptVolcano(board, critter_list=None):
    cols = len(board)
    rows = len(board[0])
    radius = 5

    # Get all active volcano tiles
    volcanoTiles = [
        (x, y) for x in range(cols) for y in range(rows)
        if board[x][y].terrain.name == "activeVolcano"
    ]
    if not volcanoTiles:
        return None

    # Randomly pick volcano to erupt
    VX, VY = random.choice(volcanoTiles)

    # Get random point in radius of volcano
    EX, EY = getRandomRadiusPoint(VX, VY, radius)

    # Reroll if OOB or center tile
    while not (0 <= EX < cols and 0 <= EY < rows) or (EX, EY) == (VX, VY):
        EX, EY = getRandomRadiusPoint(VX, VY, radius)

    target = board[EX][EY]

    # Only overwrite if not volcano/mountain
    if target.terrain.name not in ("activeVolcano", "dormantVolcano", "mountain"):
        # If a critter is on the target tile, remove it from the board
        if target.critter is not None:
            victim = target.critter
            print("Critter ", target.critter.name, " has been killed by an eruption")
            target.critter = None
            # And remove it from the critter list if provided
            if critter_list is not None:
                try:
                    critter_list.remove(victim)
                except ValueError:
                    # Already removed or not tracked—ignore
                    pass

        # Turn the tile to lava
        target.terrain = terrainLib["lava"]()

def coolLava(board):
    cols = len(board)
    rows = len(board[0])

    #Get all lava tiles
    lavaTiles = [(x, y) for x in range(cols) for y in range(rows)
                   if board[x][y].terrain.name == "lava"]

    #If no lava tiles
    if not lavaTiles:
        #print("Could not find any lava tiles to cool!")
        return None

    #Get random lava tile
    coolX, coolY = random.choice(lavaTiles)

    #Cool lava into stone
    board[coolX][coolY].terrain = terrainLib["stone"]()

def toggleVolcano(board):
    cols = len(board)
    rows = len(board[0])

    #Get all volcano tiles
    volcanoTiles = [
    (x, y)
    for x in range(cols)
    for y in range(rows)
    if board[x][y].terrain.name in ("activeVolcano", "dormantVolcano")
    ]

    #If no volcano tiles
    if not volcanoTiles:
        #print("Could not find any volcano tiles to toggle!")
        return None

    #Get random activeVolcano tile
    VX, VY = random.choice(volcanoTiles)

    #Toggle
    if board[VX][VY].terrain.name == "activeVolcano":
        board[VX][VY].terrain = terrainLib["dormantVolcano"]()
        #print("Volcano at", VX, VY, "has gone dormant!")
    else:
        board[VX][VY].terrain = terrainLib["activeVolcano"]()
        #print("Volcano at", VX, VY, "has awoken!")

def killVolcano(board):
    cols = len(board)
    rows = len(board[0])

    # Get all volcano tiles (active or dormant)
    volcanoTiles = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].terrain.name in ("activeVolcano", "dormantVolcano")
    ]

    if not volcanoTiles:
        # print("Could not find any volcano tiles to kill!")
        return None

    # Pick a random volcano to go extinct
    VX, VY = random.choice(volcanoTiles)

    # Turn it into a mountain
    board[VX][VY].terrain = terrainLib["mountain"]()
    #print(f"Volcano at {VX}, {VY} has gone extinct!")

    # 3/4 chance to spawn a new activeVolcano in any adjacent 8-direction
    if random.random() < 0.90:
        directions = [
            (-1, -1), (0, -1), (1, -1),
            (-1,  0),          (1,  0),
            (-1,  1), (0,  1), (1,  1)
        ]

        # In-bounds neighbors only
        neighbors = [
            (VX + dx, VY + dy)
            for dx, dy in directions
            if 0 <= VX + dx < cols and 0 <= VY + dy < rows
        ]

        if neighbors:
            nx, ny = random.choice(neighbors)
            # No restrictions on what it can spawn onto — overwrite whatever is there
            board[nx][ny].terrain = terrainLib["activeVolcano"]()
            #print(f"Volcano has shifted to {nx}, {ny}")