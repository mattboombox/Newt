import random

from critter import Crab, Deer, Fish, Nautilus, Newt, Plankton, SpermWhale, Squid, Therapsid, Whale, Wolf

EVOLUTION_TREE = {
    Plankton: (
        {
            "result_type": Fish,
            "weight": 1.0,
        },
        {
            "result_type": Crab,
            "weight": 1.0,
        },
        {
            "result_type": Nautilus,
            "weight": 1.0,
        },
    ),
    Fish: (
        {
            "result_type": Newt,
            "weight": 1.0,
            "terrain": "shallows",
        },
    ),
    Nautilus: (
        {
            "result_type": Squid,
            "weight": 1.0,
        },
    ),
    Newt: (
        {
            "result_type": Therapsid,
            "weight": 1.0,
        },
    ),
    Therapsid: (
        {
            "result_type": Deer,
            "weight": 1.0,
        },
        {
            "result_type": Wolf,
            "weight": 1.0,
        },
        {
            "result_type": SpermWhale,
            "weight": 1.0,
            "terrain": "shallows",
        },
    ),
    SpermWhale: (
        {
            "result_type": Whale,
            "weight": 1.0,
        },
    ),
}


def option_allows_tile(option, tile):
    if tile is None:
        return False

    terrain = option.get("terrain")
    if terrain is not None:
        return tile.terrain == terrain

    terrains = option.get("terrains")
    if terrains is not None:
        return tile.terrain in terrains

    return tile.terrain in option["result_type"].ALLOWED_TERRAINS


def get_evolution_options(critter, tile):
    for source_type, options in EVOLUTION_TREE.items():
        if not isinstance(critter, source_type):
            continue

        return [option for option in options if option_allows_tile(option, tile)]

    return []


def choose_evolution_option(options):
    total_weight = sum(option["weight"] for option in options)
    roll = random.random() * total_weight
    cumulative = 0.0

    for option in options:
        cumulative += option["weight"]
        if roll <= cumulative:
            return option

    return options[-1]


def replace_critter(game, old_critter, new_critter, tile):
    tile.critter = new_critter

    try:
        index = game.critters.index(old_critter)
    except ValueError:
        game.critters.append(new_critter)
        return new_critter

    game.critters[index] = new_critter
    return new_critter


def evolve_critter(game, critter, tile=None):
    if critter is None or critter.current_behavior == "dying":
        return None

    if tile is None:
        tile = game.world.get_tile(critter.x, critter.y)

    if tile is None or tile.critter is not critter:
        return None

    options = get_evolution_options(critter, tile)
    if not options:
        return None

    evolution_option = choose_evolution_option(options)
    evolved_critter = evolution_option["result_type"](critter.x, critter.y)
    return replace_critter(game, critter, evolved_critter, tile)


def get_evolution_candidates(game):
    candidates = []

    for critter in game.critters:
        if critter.current_behavior == "dying":
            continue

        tile = game.world.get_tile(critter.x, critter.y)
        if tile is None or tile.critter is not critter:
            continue

        options = get_evolution_options(critter, tile)
        if options:
            candidates.append((critter, tile, options))

    return candidates


def trigger_random_evolution(game):
    candidates = get_evolution_candidates(game)
    if not candidates or random.random() >= game.evolution_chance:
        return None

    critter, tile, options = random.choice(candidates)
    return evolve_critter(game, critter, tile)
