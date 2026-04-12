import pygame
from brush import paint_radius
from critter import Critter
from terrain import TERRAIN_DATA
from critter import Crab
from city import City


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

            elif event.key == pygame.K_a:
                terrain_names = list(TERRAIN_DATA.keys())
                current_index = terrain_names.index(game.current_terrain)
                new_index = (current_index - 1) % len(terrain_names)
                game.current_terrain = terrain_names[new_index]
                print("Brush terrain:", game.current_terrain)

            elif event.key == pygame.K_d:
                terrain_names = list(TERRAIN_DATA.keys())
                current_index = terrain_names.index(game.current_terrain)
                new_index = (current_index + 1) % len(terrain_names)
                game.current_terrain = terrain_names[new_index]
                print("Brush terrain:", game.current_terrain)

            elif event.key == pygame.K_q:
                game.brush_size = max(0, game.brush_size - 1)
                print("Brush size:", game.brush_size)

            elif event.key == pygame.K_e:
                game.brush_size += 1
                print("Brush size:", game.brush_size)

            elif event.key == pygame.K_PERIOD:
                game.speed = min(16, game.speed * 2)
                print("Speed:", game.speed)

            elif event.key == pygame.K_COMMA:
                game.speed = max(0.25, game.speed / 2)
                print("Speed:", game.speed)

            elif event.key == pygame.K_r:
                tile = game.hovered_tile
                if tile and tile.building is None:
                    if tile.has_tag("land"):
                        tile.building = City(tile.x, tile.y, level="village", population=10)
                        print(f"Placed city at ({tile.x}, {tile.y})")

            elif event.key == pygame.K_c:
                mx, my = pygame.mouse.get_pos()
                tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

                if tile is not None and tile.terrain in Crab.ALLOWED_TERRAINS and tile.critter is None:
                    critter = Crab(tile.x, tile.y)
                    tile.critter = critter
                    game.critters.append(critter)
                    print(f"Spawned crab {critter.id} at ({tile.x}, {tile.y})")

        elif event.type == pygame.MOUSEBUTTONDOWN:
            mx, my = pygame.mouse.get_pos()
            tile = game.world.get_tile_at_pixel(mx, my, game.tile_size)

            if event.button == 1:
                if tile is not None:
                    paint_radius(game, tile, game.current_terrain, game.brush_size)

                if game.current_terrain not in ("meteor", "comet", "tectonic_uplift", "tsunami"):
                    game.left_mouse_held = True

            elif event.button == 3:
                if tile is not None and tile.critter is not None:
                    critter = tile.critter
                    tile.critter = None
                    if critter in game.critters:
                        game.critters.remove(critter)
                    print(f"Deleted critter {critter.id} at ({tile.x}, {tile.y})")

        elif event.type == pygame.MOUSEBUTTONUP:
            if event.button == 1:
                game.left_mouse_held = False