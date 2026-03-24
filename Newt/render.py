import pygame


def draw_tile(screen, tile, tile_size):
    rect = pygame.Rect(tile.x * tile_size, tile.y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, tile.get_color(), rect)


def draw_hud(screen, game, background_color):
    font = pygame.font.SysFont(None, 18)

    status_text = "Paused" if game.paused else "Running"

    if game.hovered_tile is not None:
        tile = game.hovered_tile
        hover_text = f"Tile: ({tile.x}, {tile.y}) {tile.terrain}"

        if tile.critter is not None:
            critter = tile.critter
            hover_text += f" | Critter ID: {critter.id}"
    else:
        hover_text = "Tile: none"

    hud_text = (
        f"Brush: {game.current_terrain}   "
        f"Size: {game.brush_size}   "
        f"Status: {status_text}   "
        f"{hover_text}"
    )

    hud_height = 20
    hud_rect = pygame.Rect(0, screen.get_height() - hud_height, screen.get_width(), hud_height)

    pygame.draw.rect(screen, (0, 0, 0), hud_rect)
    text_surface = font.render(hud_text, True, (220, 220, 220))
    screen.blit(text_surface, (6, screen.get_height() - hud_height + 2))

def draw_critter(screen, critter, tile_size):
    inset = max(2, tile_size // 4)
    rect = pygame.Rect(
        critter.x * tile_size + inset,
        critter.y * tile_size + inset,
        tile_size - inset * 2,
        tile_size - inset * 2
    )
    pygame.draw.rect(screen, critter.color, rect)

def render(screen, game, background_color):
    screen.fill(background_color)

    for x in range(game.world.cols):
        for y in range(game.world.rows):
            draw_tile(screen, game.world.board[x][y], game.tile_size)

    for critter in game.critters:
        draw_critter(screen, critter, game.tile_size)        

    if game.hovered_tile is not None:
        rect = pygame.Rect(
            game.hovered_tile.x * game.tile_size,
            game.hovered_tile.y * game.tile_size,
            game.tile_size,
            game.tile_size
        )
        pygame.draw.rect(screen, (255, 255, 255), rect, 1)

    draw_hud(screen, game, background_color)

    pygame.display.flip()