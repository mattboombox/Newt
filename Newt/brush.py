from volcano import Volcano
from meteor import trigger_meteor_strike

def remove_volcano_at(game, x, y):
    for volcano in game.volcanoes[:]:
        if volcano.x == x and volcano.y == y:
            game.volcanoes.remove(volcano)


def get_volcano_at(game, x, y):
    for volcano in game.volcanoes:
        if volcano.x == x and volcano.y == y:
            return volcano
    return None


def sync_volcano_at_tile(game, tile, terrain_name):
    # If painting over an existing volcano with something non-volcanic, remove it
    if terrain_name not in ("active_volcano", "dormant_volcano"):
        remove_volcano_at(game, tile.x, tile.y)
        return

    # If painting a volcano terrain, make sure a volcano object exists
    existing = get_volcano_at(game, tile.x, tile.y)

    if existing is None:
        state = "active" if terrain_name == "active_volcano" else "dormant"
        game.volcanoes.append(Volcano(tile.x, tile.y, state=state))
    else:
        existing.state = "active" if terrain_name == "active_volcano" else "dormant"


def paint_radius(game, center_tile, terrain_name, radius=0):
    if center_tile is None:
        return
    
    if terrain_name == "meteor":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_meteor_strike(game.world, center_tile.x, center_tile.y)
        return

    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            x = center_tile.x + dx
            y = center_tile.y + dy

            tile = game.world.get_tile(x, y)
            if tile is not None:
                sync_volcano_at_tile(game, tile, terrain_name)
                tile.set_terrain(terrain_name)