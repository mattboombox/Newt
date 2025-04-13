import random

def createBoard(rows, cols, default = 'g'):
    #Creates a board with 'rows' (height) and 'cols' (width)
    return [[default for _ in range(cols)] for _ in range(rows)]

def printBoard(board):
    #Prints the board to console
    for row in board:
        print(" ".join(row))

def getClickedTile(mouse_x ,mouse_y):
    #Gives the tile which was clicked on
    row = mouse_y // 10
    col = mouse_x // 10
    return row, col

def ensureDisplaySize(windowWidth, windowHeight):
    #Makes sure inputed width and height are mutiples of 10
    def roundTen(value):
        return value if value % 10 == 0 else value + (10 - value % 10)
    
    return roundTen(windowWidth), roundTen(windowHeight)

def placeStamp(board, rows, cols, stamp, row, col):
    stampRow = len(stamp)
    print("stampRow =", stampRow)
    stampCol = len(stamp[0])
    print("stampCol =", stampCol)
    print("stamp = ", stamp)

    for i in range(stampRow):
        for j in range(stampCol):
            if (row + i < rows) and (col + j < cols):
                if stamp[i][j] != ' ':
                    board[row + i][col + j] = stamp[i][j]
    
def generateTerrain(board, rows, cols, mode):
    match mode:
        case 1:
            print("Phase 1")
            for col in range (cols):
                for row in range(rows):
                    board[row][col] = 'g'
                            
        