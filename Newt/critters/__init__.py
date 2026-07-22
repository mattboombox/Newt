from .crab import Crab
from .critter import (
    AMPHIBIOUS_LAND_TERRAINS,
    CARDINAL_DIRECTIONS,
    Critter,
    NON_ARCTIC_LAND_TERRAINS,
)
from .deer import Deer
from .fish import Fish
from .giga_slug import GigaSlug
from .land_kraken import LandKraken
from .nautilus import Nautilus
from .newt import Newt
from .plankton import Plankton
from .snail import Snail
from .sperm_whale import SpermWhale
from .squid import Squid
from .squid_egg import SquidEgg
from .therapsid import Therapsid
from .whale import Whale
from .wolf import Wolf

Crab.DISPLACEABLE_CRITTER_TYPES = (Plankton,)
Deer.DISPLACEABLE_CRITTER_TYPES = (Crab, Newt)
Fish.DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Newt)
Nautilus.DISPLACEABLE_CRITTER_TYPES = (Plankton, Newt)
Squid.DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Newt)
SpermWhale.DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Newt)
Therapsid.DISPLACEABLE_CRITTER_TYPES = (Crab, Newt)
Whale.DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Newt)
Wolf.DISPLACEABLE_CRITTER_TYPES = (Crab, Newt)

CRITTER_TYPES = {
    "crab": Crab,
    "deer": Deer,
    "fish": Fish,
    "giga_slug": GigaSlug,
    "land_kraken": LandKraken,
    "nautilus": Nautilus,
    "newt": Newt,
    "plankton": Plankton,
    "snail": Snail,
    "sperm_whale": SpermWhale,
    "squid": Squid,
    "therapsid": Therapsid,
    "whale": Whale,
    "wolf": Wolf,
}

CRITTER_ORDER = list(CRITTER_TYPES.keys())

__all__ = [
    "AMPHIBIOUS_LAND_TERRAINS",
    "CARDINAL_DIRECTIONS",
    "CRITTER_ORDER",
    "CRITTER_TYPES",
    "Critter",
    "Crab",
    "Deer",
    "Fish",
    "GigaSlug",
    "LandKraken",
    "Nautilus",
    "Newt",
    "NON_ARCTIC_LAND_TERRAINS",
    "Plankton",
    "Snail",
    "SpermWhale",
    "Squid",
    "SquidEgg",
    "Therapsid",
    "Whale",
    "Wolf",
]
