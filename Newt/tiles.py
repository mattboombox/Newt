
tiles = [
    {"char": "O", "name": "ocean", "rgb": (19, 89, 135)},
    {"char": "L", "name": "lake", "rgb": (93, 171, 223)},
    {"char": "g", "name": "grass", "rgb": (144, 203, 129)},
    {"char": "P", "name": "mover", "rgb": (240, 108, 135)},
    {"char": "H", "name": "building", "rgb": (152, 135, 113)},
    {"char": "M", "name": "mountain", "rgb": (122, 117, 116)},
    {"char": "Q", "name": "player", "rgb": (217, 255, 0)},
    {"char": "r", "name": "rock", "rgb": (150, 143, 120)},
    {"char": "s", "name": "sand", "rgb": (214, 211, 171)},
]

def getColor(tileChar):
    for tile in tiles:
        if tile["char"] == tileChar:
            return tile["rgb"]
    return (255, 79, 252) #Missing magenta