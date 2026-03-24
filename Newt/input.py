import pygame
from brush import paint_radius
from critter import Critter


def handle_input(game):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            game.running = False

        elif event.type == pygame.KEYDOWN:
            if event.key == pygame.K_x:
                game.running = False
            elif event.key == pygame.K_p:
                game.paused = not game.paused
                print("Paused" if game.paused else "Unpaused")
            elif event.key == pygame.K_1:
                game.current_terrain = "ocean"
            elif event.key == pygame.K_2:
                game.current_terrain = "grass"
            elif event.key == pygame.K_3:
                game.current_terrain = "stone"
            elif event.key == pygame.K_q:
                game.brush_size = max(0, game.brush_size - 1)
                print("Brush size:", game.brush_size)
            elif event.key == pygame.K_e:
                game.brush_size += 1
                print("Brush size:", game.brush_size)
            elif event.key == pygame.K_c:
                mx, my = pygame.mouse.get_pos()
                tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

                if tile is not None and tile.is_walkable() and tile.critter is None:
                    critter = Critter(tile.x, tile.y)
                    tile.critter = critter
                    game.critters.append(critter)
                    print(f"Spawned critter {critter.id} at ({tile.x}, {tile.y})")

        elif event.type == pygame.MOUSEBUTTONDOWN:
            if event.button == 1:
                game.left_mouse_held = True
                mx, my = pygame.mouse.get_pos()
                tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)
                if tile is not None:
                    paint_radius(game, tile, game.current_terrain, game.brush_size)

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                game.left_mouse_held = False