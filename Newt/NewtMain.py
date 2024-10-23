import pygame
import BiomeHandler

pygame.init()

screen_size = 500
rect_size = screen_size // 50
screen = pygame.display.set_mode((screen_size,screen_size))
pygame.display.set_caption("Newt")

print("Hello")

gray = (128,128,128)

#Game board
board = [['' for _ in range(50)] for _ in range(50)]

B = BiomeHandler.Biome()
B.fill_board_with_biomes(board)

running = True
while(running):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running  = False


    screen.fill(gray)

    for row in range(50):
        for col in range(50):
            # Determine the color based on the character
            color = B.char_to_color(board[row][col])

            # Draw the rectangle at the correct position
            pygame.draw.rect(screen, color, pygame.Rect(col * rect_size, row * rect_size, rect_size, rect_size))


    pygame.display.flip()
    