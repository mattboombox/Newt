def getColor(tile):
    switch = {
        'O': (93, 171, 223),   # Ocean
        'L': (108, 195, 230),  # Lake
        'g': (144, 203, 129),  # Grassland
        'P': (240, 108, 135),  # Mover
        'B': (152, 135, 113),  # Building
        'M': (122, 117, 116)   # Mountain
    }
    
    return switch.get(tile, (255, 79, 252))  # Missing Magenta

def getNumTiles():
    return False