from city import City


def describe_building(building):
    if isinstance(building, City):
        return f"{building.level.title()} city"

    return type(building).__name__


def remove_critter(game, critter, reason):
    tile = game.world.get_tile(critter.x, critter.y)
    if tile is not None and tile.critter is critter:
        tile.critter = None

    if critter in game.critters:
        game.critters.remove(critter)

    print(
        f"Removed {type(critter).__name__} {critter.id} at "
        f"({critter.x}, {critter.y}) because {reason}."
    )
    return True


def remove_building_at_tile(tile, reason):
    building = tile.building
    if building is None:
        return False

    tile.building = None
    print(
        f"Removed {describe_building(building)} at "
        f"({tile.x}, {tile.y}) because {reason}."
    )
    return True


def clear_tile_occupants(game, tile, reason):
    if tile is None:
        return

    if tile.critter is not None:
        remove_critter(game, tile.critter, reason)

    if tile.building is not None:
        remove_building_at_tile(tile, reason)


def remove_stranded_critters(game):
    for critter in game.critters[:]:
        tile = game.world.get_tile(critter.x, critter.y)
        if critter.is_habitable_tile(tile):
            continue

        terrain_name = "missing terrain" if tile is None else tile.terrain
        remove_critter(
            game,
            critter,
            f"it was stranded on {terrain_name} and could no longer move",
        )
