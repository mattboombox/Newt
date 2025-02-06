
tiles = [
    {"char": "O", "name": "ocean", "rgb": (93, 171, 223)},
    {"char": "g", "name": "grass", "rgb": (144, 203, 129)},
    {"char": "P", "name": "Mover", "rgb": (240, 108, 135)},
    {"char": "B", "name": "Building", "rgb": (152, 135, 113)},
    {"char": "M", "name": "mountain", "rgb": (122, 117, 116)},
    {"char": "Q", "name": "Player", "rgb": (217, 255, 0)},
]

def getColor(tileChar):
    for tile in tiles:
        if tile["char"] == tileChar:
            return tile["rgb"]
    return (255, 79, 252) #Missing magenta