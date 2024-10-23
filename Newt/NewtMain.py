import pygame
import tile

pygame.init()

print("Hello")

color = (40,69,165)

screen_width = 500
screen_height = 500
screen = pygame.display.set_mode((screen_width,screen_height))
pygame.display.set_caption("Newt")

#Game board
board = [['X' for _ in range(10)] for _ in range(10)]
board[0][0] = 'Y'
print(board)


running = True
while(running):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running  = False


    screen.fill(color)
    pygame.display.flip()
    