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
        print("Could not find any ocean tiles to make island seed!")
        return None

    #Get random ocean tile
    spawnX, spawnY = random.choice(oceanTiles)

    #Spawn volcano
    board[spawnX][spawnY].terrain = terrainLib["volcano"]()

def getRandomRadiusPoint(cx, cy, radius):
    theta = random.uniform(0, 2*math.pi)
    r = radius * math.sqrt(random.random())
    x = cx + int(round(r * math.cos(theta)))
    y = cy + int(round(r * math.sin(theta)))
    return x, y

def eruptVolcano(board):
    cols = len(board)
    rows = len(board[0])
    radius = 3

    #Get all volcano tiles
    volcanoTiles = [(x, y) for x in range(cols) for y in range(rows)
                   if board[x][y].terrain.name == "volcano"]
    
    #If no volcano tiles
    if not volcanoTiles:
        print("Could not find any volcano tiles to erupt!")
        return None
    
    #Randomly pick volcano to erupt
    VX, VY = random.choice(volcanoTiles)

    #Get random point in radius of volcano
    EX, EY = getRandomRadiusPoint(VX, VY, radius)
    
    #Get new point if out of bounds
    while not (0 <= EX < cols and 0 <= EY < rows) or (EX,EY) == (VX,VY):
        EX, EY = getRandomRadiusPoint(VX, VY, radius)

    board[EX][EY].terrain = terrainLib["lava"]()

    