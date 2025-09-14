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
        print("Could not find any ocean-adjacent stone tiles to erode!")
        return None

    #Pick one random tile and erode it
    spawnX, spawnY = random.choice(stoneTiles)
    board[spawnX][spawnY].terrain = terrainLib["beach"]()
    print("Stone tile", spawnX, spawnY, "eroded into a beach!")