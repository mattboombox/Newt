import random

from critter import Crab, Deer, Fish, GigaSlug, LandKraken, MegaSpider, Nautilus, Newt, Plankton, SeaScorpion, Snail, SpermWhale, Squid, Therapsid, Trilobite, Whale, Wolf

EVOLUTION_TREE = {
    Plankton: (
        {
            "result_type": Fish,
            "weight": 1.0,
        },
        {
            "result_type": Trilobite,
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
        },
    ),
    Trilobite: (
        {
            "result_type": Crab,
            "weight": 1.0,
        },
        {
            "result_type": SeaScorpion,
            "weight": 1.0,
        },
    ),
    SeaScorpion: (
        {
            "result_type": MegaSpider,
            "weight": 1.0,
        },
    ),
    Nautilus: (
        {
            "result_type": Snail,
            "weight": 1.0,
        },
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
    Snail: (
        {
            "result_type": GigaSlug,
            "weight": 1.0,
        },
    ),
    Squid: (
        {
            "result_type": LandKraken,
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
        },
    ),
    SpermWhale: (
        {
            "result_type": Whale,
            "weight": 1.0,
        },
    ),
}


def get_evolution_options(critter):
    for source_type, options in EVOLUTION_TREE.items():
        # Evolution steps are exact species transitions: a source can only
        # use its own entry, never an entry inherited through a superclass.
        if type(critter) is not source_type:
            continue

        return options

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


def create_evolved_offspring(parent, x, y):
    """Create a mutation of a parent's offspring, regardless of its birthplace."""
    options = get_evolution_options(parent)
    if not options:
        return None

    evolution_option = choose_evolution_option(options)
    offspring = evolution_option["result_type"](x, y)
    offspring.needs_habitat_relocation = True
    return offspring


def replace_with_evolved_offspring(parent, offspring, world):
    """Replace a just-spawned offspring with its parent's evolved descendant."""
    evolved_offspring = create_evolved_offspring(parent, offspring.x, offspring.y)
    if evolved_offspring is None:
        return None

    tile = world.get_tile(offspring.x, offspring.y)
    if tile is None or tile.critter is not offspring:
        return None

    tile.critter = evolved_offspring
    return evolved_offspring
