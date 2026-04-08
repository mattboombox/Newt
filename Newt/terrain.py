TERRAIN_DATA = {
    "ocean": {
        "color": (30, 60, 160),
        "walkable": False,
        "tags": {"water", "saltwater", "deep_water"},
    },
    "shallows": {
        "color": (32, 67, 186),
        "walkable": True,
        "tags": {"water", "saltwater", "coastal"},
    },
    "lake": {
        "color": (60, 120, 200),
        "walkable": False,
        "tags": {"water", "freshwater"},
    },
    "grass": {
        "color": (70, 150, 70),
        "walkable": True,
        "tags": {"land"},
    },
    "stone": {
        "color": (100, 100, 100),
        "walkable": True,
        "tags": {"land", "rocky"},
    },
    "sand": {
        "color": (194, 178, 128),
        "walkable": True,
        "tags": {"land", "coastal"},
    },
    "beach": {
        "color": (214, 198, 140),
        "walkable": True,
        "tags": {"land", "coastal"},
    },
    "mountain": {
        "color": (89, 85, 84),
        "walkable": False,
        "tags": {"mountain", "rocky"},
    },
    "lava": {
        "color": (255, 80, 0),
        "walkable": False,
        "tags": {"lava", "hot"},
    },
    "active_volcano": {
        "color": (180, 40, 40),
        "walkable": False,
        "tags": {"lava", "mountain", "hot"},
    },
    "dormant_volcano": {
        "color": (230, 230, 230),
        "walkable": False,
        "tags": {"mountain", "rocky"},
    },
    "ice_sheet": {
        "color": (199, 236, 240),
        "walkable": True,
        "tags": {"land", "frozen"},
    },
    "snow": {
        "color": (237, 252, 255),
        "walkable": True,
        "tags": {"land", "cold"},
    },

    # Tools (no real gameplay tags needed)
    "meteor": {
        "color": (255, 0, 255),
        "walkable": False,
        "tags": set(),
    },
    "comet": {
        "color": (0, 0, 255),
        "walkable": False,
        "tags": set(),
    },
    "tectonic_uplift": {
        "color": (255, 0, 255),
        "walkable": False,
        "tags": set(),
    },
    "tsunami": {
        "color": (18, 119, 252),
        "walkable": False,
        "tags": set()
    },
}