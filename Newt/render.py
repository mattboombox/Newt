import pygame
from city import City
from config import HUD_HEIGHT
from terrain import TERRAIN_DATA


def draw_tile(screen, tile, tile_size):
    rect = pygame.Rect(tile.x * tile_size, tile.y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, tile.get_color(), rect)


def draw_hud(screen, game, background_color):
    font = pygame.font.SysFont(None, 18)

    status = "Paused" if game.paused else "Running"

    fields = [
        f"Critters: {len(game.critters)}",
        f"Brush: {game.current_terrain}",
        f"Critter Tool: {game.current_critter}",
        f"Size: {game.brush_size}",
        f"Status: {status}",
    ]

    if game.hovered_tile is not None:
        tile = game.hovered_tile
        fields.append(f"Tile: ({tile.x}, {tile.y}) {tile.terrain}")

        if tile.critter is not None:
            fields.append(f"Critter: {type(tile.critter).__name__} ID {tile.critter.id}")

        if tile.building is not None:
            if isinstance(tile.building, City):
                fields.append(f"Building: {tile.building.level.title()}")
            else:
                fields.append(f"Building: {type(tile.building).__name__}")
    else:
        fields.append("Tile: none")

    hud_text = " | ".join(fields)

    hud_rect = pygame.Rect(0, screen.get_height() - HUD_HEIGHT, screen.get_width(), HUD_HEIGHT)

    pygame.draw.rect(screen, (0, 0, 0), hud_rect)
    text_surface = font.render(hud_text, True, (220, 220, 220))
    screen.blit(text_surface, (6, screen.get_height() - HUD_HEIGHT + 2))


def draw_critter(screen, critter, tile_size, sprites):
    sprite = sprites.get(critter.sprite)

    if sprite is not None:
        screen.blit(sprite, (critter.x * tile_size, critter.y * tile_size))
    else:
        inset = max(2, tile_size // 4)
        rect = pygame.Rect(
            critter.x * tile_size + inset,
            critter.y * tile_size + inset,
            tile_size - inset * 2,
            tile_size - inset * 2
        )
        pygame.draw.rect(screen, critter.color, rect)


def draw_building(screen, building, tile_size):
    rect = pygame.Rect(
        building.x * tile_size,
        building.y * tile_size,
        tile_size,
        tile_size
    )

    pygame.draw.rect(screen, (200, 50, 50), rect)

    if isinstance(building, City):
        font = pygame.font.SysFont(None, 14)
        text_surface = font.render(building.level[0].upper(), True, (255, 255, 255))
        screen.blit(text_surface, (building.x * tile_size + 2, building.y * tile_size + 1))


def draw_tsunami_wave(screen, x, y, tile_size):
    rect = pygame.Rect(x * tile_size, y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, TERRAIN_DATA["tsunami"]["color"], rect, max(1, tile_size // 6))


def draw_wave_ring(screen, x, y, tile_size, color):
    rect = pygame.Rect(x * tile_size, y * tile_size, tile_size, tile_size)
    pygame.draw.rect(screen, color, rect, max(1, tile_size // 6))


def render(screen, game, background_color):
    screen.fill(background_color)

    for x in range(game.world.cols):
        for y in range(game.world.rows):
            tile = game.world.board[x][y]
            draw_tile(screen, tile, game.tile_size)

            if tile.building is not None:
                draw_building(screen, tile.building, game.tile_size)

    active_wave_tiles = set()
    for tsunami in game.tsunamis:
        active_wave_tiles.update(tsunami.current_ring)

    for x, y in active_wave_tiles:
        draw_tsunami_wave(screen, x, y, game.tile_size)

    for impact_wave in game.impact_waves:
        wave_color = TERRAIN_DATA[impact_wave.target_terrain]["color"]
        for x, y in impact_wave.current_ring:
            draw_wave_ring(screen, x, y, game.tile_size, wave_color)

    for critter in game.critters:
        draw_critter(screen, critter, game.tile_size, game.sprites)

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
