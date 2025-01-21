def get_color(tile):
    switch = {
        'o': (23, 65, 138),#Ocean
        'l': (108, 195, 230),#Lake
        'g': (44, 201, 86),#Grassland
        'p': (250, 250, 95)#Mover
    }
    return switch.get(tile.lower(), (255, 79, 252)) #Missing Magenta
