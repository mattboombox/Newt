print("working")
import pygame
import board
import tiles
import sys

#Initialize Pygame
pygame.init()

#Set up the display
window_width = 800
window_height = 800
window_title = "My Pygame Window"
window_color = (0, 0, 0)

#Game board
rows = 80
cols = 80
Board = board.create_board(rows, cols)
board.print_board(Board)

#Generate terrain

#Thingies


#Create the display window
screen = pygame.display.set_mode((window_width, window_height))
pygame.display.set_caption(window_title)

# Main game loop
running = True
while running:
    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        if event.type == pygame.MOUSEBUTTONDOWN:
            mouse_pos = pygame.mouse.get_pos()
            p, q = board.get_clicked_tile(mouse_pos[0] , mouse_pos[1])
            print(mouse_pos[0], mouse_pos[1])
            print(p,q)
            Board[q][p] = 'X'
            board.print_board(Board)


    #Fill the screen with the background color
    screen.fill(window_color)

    #Draw baord
    for i in range (rows):
        for j in range(cols):
            pygame.draw.rect(screen, tiles.get_color(Board[i][j]), (j * 10, i * 10, 10, 10))
    

    #Update the display
    pygame.display.flip()

#Quit Pygame
pygame.quit()
sys.exit()