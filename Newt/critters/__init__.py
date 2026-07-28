from .ape import Ape
from .ape_sailor import ApeSailor
from .ape_warrior import ApeWarrior
from .crab import Crab
from .critter import (
    AQUATIC_TERRAINS,
    CARDINAL_DIRECTIONS,
    Critter,
    LAND_TERRAINS,
)
from .deer import Deer
from .dog import Dog
from .fish import Fish
from .giga_slug import GigaSlug
from .jelly_fish import Jellyfish
from .land_kraken import LandKraken
from .mega_spider import MegaSpider
from .nautilus import Nautilus
from .newt import Newt
from .plankton import Plankton
from .sand_worm import SandWorm
from .snail import Snail
from .sperm_whale import SpermWhale
from .squid import Squid
from .squid_egg import SquidEgg
from .sea_scorpion import SeaScorpion
from .therapsid import Therapsid
from .trilobite import Trilobite
from .whale import Whale
from .wolf import Wolf

CRITTER_TYPES = {
    "ape": Ape,
    "crab": Crab,
    "deer": Deer,
    "dog": Dog,
    "fish": Fish,
    "giga_slug": GigaSlug,
    "jelly_fish": Jellyfish,
    "land_kraken": LandKraken,
    "mega_spider": MegaSpider,
    "nautilus": Nautilus,
    "newt": Newt,
    "plankton": Plankton,
    "sand_worm": SandWorm,
    "sea_scorpion": SeaScorpion,
    "snail": Snail,
    "sperm_whale": SpermWhale,
    "squid": Squid,
    "therapsid": Therapsid,
    "trilobite": Trilobite,
    "whale": Whale,
    "wolf": Wolf,
}

CRITTER_ORDER = list(CRITTER_TYPES.keys())

__all__ = [
    "AQUATIC_TERRAINS",
    "CARDINAL_DIRECTIONS",
    "CRITTER_ORDER",
    "CRITTER_TYPES",
    "Critter",
    "Ape",
    "ApeSailor",
    "ApeWarrior",
    "Crab",
    "Deer",
    "Dog",
    "Fish",
    "GigaSlug",
    "Jellyfish",
    "LandKraken",
    "MegaSpider",
    "Nautilus",
    "Newt",
    "LAND_TERRAINS",
    "Plankton",
    "SandWorm",
    "SeaScorpion",
    "Snail",
    "SpermWhale",
    "Squid",
    "SquidEgg",
    "Therapsid",
    "Trilobite",
    "Whale",
    "Wolf",
]
