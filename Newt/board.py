def create_board(cols, rows, default = 'g'):
    #Creates a board of certain size and fills with default char
    return [[default for _ in range(cols)] for _ in range(rows)]

def print_board(board):
    #Prints the board to console
    for row in board:
        print(" ".join(row))


def get_clicked_tile(mouse_x ,mouse_y):
    #Gives the tile which was clicked on
    x = mouse_x // 10
    y = mouse_y // 10
    return x,y


    