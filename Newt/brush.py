def paint_radius(game, center_tile, terrain_name, radius=0):
    if center_tile is None:
        return

    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            x = center_tile.x + dx
            y = center_tile.y + dy

            tile = game.world.get_tile(x, y)
            if tile is not None:
                tile.set_terrain(terrain_name)