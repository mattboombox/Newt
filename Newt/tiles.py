
tiles = [
    {"char": "O", "name": "ocean", "rgb": (19, 89, 135)},
    {"char": "F", "name": "lake", "rgb": (93, 171, 223)},
    {"char": "g", "name": "grass", "rgb": (144, 203, 129)},
    {"char": "P", "name": "mover", "rgb": (240, 108, 135)},
    {"char": "H", "name": "building", "rgb": (152, 135, 113)},
    {"char": "M", "name": "mountain", "rgb": (122, 117, 116)},
    {"char": "Q", "name": "player", "rgb": (217, 255, 0)},
    {"char": "r", "name": "rock", "rgb": (150, 143, 120)},
    {"char": "s", "name": "sand", "rgb": (214, 211, 171)},
    {"char": "L", "name": "lava", "rgb": (255, 85, 0)},
]

stamps = [
    {"name": "pond", 
     "stamp": [
        ['g', 'g', 'g', ],
        ['g', 'F', 'g', ],
        ['g', 'g', 'g', ],
        ]},
    {"name": "rockIsland", 
     "stamp": [
        ['s', 's', 's'],
        ['s', 'r', 's'],
        ['s', 's', 's']
        ]},
    {"name": "volcano", 
     "stamp": [
        ['M', 'M', 'M'],
        ['M', 'L', 'M'],
        ['M', 'M', 'M']
        ]},
    {"name": "bigIsland", 
     "stamp": [
        [' ', ' ', ' ', 'g', 'g', 'g', 'g', 'g', ' ', ' '],
        [' ', ' ', 's', 'g', 'g', 'g', 'g', 'g', 's', ' '],
        [' ', 's', 'g', 'g', 'g', 'F', 'g', 'g', 'g', 's'],
        ['s', 'g', 'g', 'F', 'g', 'g', 'g', 'g', 'g', 'g'],
        ['s', 'g', 'g', 'g', 'g', 'g', 'g', 'g', 'g', 's'],
        [' ', 's', 'g', 'g', 'g', 'g', 'g', 'g', 's', ' '],
        [' ', ' ', 's', 'g', 'g', 'g', 'g', 's', ' ', ' '],
        [' ', ' ', ' ', 's', 's', ' ', ' ', ' ', ' ', ' ']
        ]},
]

def flipStamp(stamp):
    return [list(row) for row in zip(*stamp[::-1])]

def getColor(tileChar):
    for tile in tiles:
        if tile["char"] == tileChar:
            return tile["rgb"]
    return (255, 79, 252) #Missing magenta