def createBoard(cols, rows, default = 'g'):
    #Creates a board of certain size and fills with default char
    return [[default for _ in range(cols)] for _ in range(rows)]

def printBoard(board):
    #Prints the board to console
    for row in board:
        print(" ".join(row))

def getClickedTile(mouse_x ,mouse_y):
    #Gives the tile which was clicked on
    x = mouse_x // 10
    y = mouse_y // 10
    return x,y

def ensureDisplaySize(windowWidth, windowHeight):
    def roundTen(value):
        return value if value % 10 == 0 else value + (10 - value % 10)
    
    return roundTen(windowWidth), roundTen(windowHeight)
    