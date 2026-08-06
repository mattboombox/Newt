from .ape import Ape
from .ape_sailor import ApeSailor
from .ape_warrior import ApeCavalry, ApeWarrior
from .crab import Crab
from .critter import (
    AQUATIC_TERRAINS,
    CARDINAL_DIRECTIONS,
    Critter,
    LAND_TERRAINS,
    PreyRule,
)
from .deer import Deer
from .dog import Dog
from .fish import Fish
from .giga_slug import GigaSlug
from .jelly_fish import Jellyfish
from .land_kraken import LandKraken
from .lich import Lich, Undead, UndeadBeast, UndeadCavalry
from .mega_spider import MegaSpider
from .messiah import Messiah
from .merfolk import Merfolk
from .merfolk_warrior import MerfolkWarrior
from .nautilus import Nautilus
from .newt import Newt
from .plankton import Plankton
from .sand_worm import SandWorm
from .shark import Shark
from .snail import Snail
from .smasher import SaintSmasher, Smasher
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
    "lich": Lich,
    "mega_spider": MegaSpider,
    "messiah": Messiah,
    "nautilus": Nautilus,
    "newt": Newt,
    "plankton": Plankton,
    "sand_worm": SandWorm,
    "shark": Shark,
    "sea_scorpion": SeaScorpion,
    "snail": Snail,
    "smasher": Smasher,
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
    "PreyRule",
    "Ape",
    "ApeSailor",
    "ApeCavalry",
    "ApeWarrior",
    "Crab",
    "Deer",
    "Dog",
    "Fish",
    "GigaSlug",
    "Jellyfish",
    "LandKraken",
    "Lich",
    "MegaSpider",
    "Messiah",
    "Merfolk",
    "MerfolkWarrior",
    "Nautilus",
    "Newt",
    "LAND_TERRAINS",
    "Plankton",
    "SandWorm",
    "Shark",
    "SeaScorpion",
    "Snail",
    "SaintSmasher",
    "Smasher",
    "SpermWhale",
    "Squid",
    "SquidEgg",
    "Therapsid",
    "Trilobite",
    "Undead",
    "UndeadBeast",
    "UndeadCavalry",
    "Whale",
    "Wolf",
]
