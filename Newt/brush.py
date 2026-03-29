from tectonics import remove_volcano_at, sync_volcano_at_tile, generate_uplift_chain
from Newt.impact import trigger_impact_event
from lake import convert_landlocked_ocean_to_lake


def paint_radius(game, center_tile, terrain_name, radius=0):
    if center_tile is None:
        return

    if terrain_name == "impact_event":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_impact_event(game.world, center_tile.x, center_tile.y)
        convert_landlocked_ocean_to_lake(game.world)
        return

    if terrain_name == "tectonic_uplift":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        generate_uplift_chain(game, center_tile.x, center_tile.y)
        convert_landlocked_ocean_to_lake(game.world)
        return

    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            x = center_tile.x + dx
            y = center_tile.y + dy

            tile = game.world.get_tile(x, y)
            if tile is not None:
                sync_volcano_at_tile(game, tile, terrain_name)
                tile.set_terrain(terrain_name)

    convert_landlocked_ocean_to_lake(game.world)