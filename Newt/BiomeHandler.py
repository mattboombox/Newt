import random

class Biome:
     def __init__(self):
        self.biomes = ['L', 'G', 'O', 'B', 'M', 'D']

     def char_to_color(self, char):
        if char == 'L': # lake
            return (52, 152, 219) # Light Blue
        elif char == 'G': # Grassland
            return (46, 204, 113) # Green
        elif char == 'O': # Ocean
            return (0, 105, 250)  # Dark Blue
        elif char == 'B': # Beach
            return (244, 164, 96)  # Yellow
        elif char == 'M': # Mountain
            return (128, 128, 128)  # Gray
        elif char == 'D': # Desert
            return (237, 201, 175)  # Sand

     # Function to check if the placement of a biome follows the rules
     def can_place_biome(self, board, row, col, biome):
        # Get the diagonal neighbors
        neighbors = []

        # Check top-left
        if row > 0 and col > 0:
            neighbors.append(board[row - 1][col - 1])
        # Check top-right
        if row > 0 and col < len(board[0]) - 1:
            neighbors.append(board[row - 1][col + 1])
        # Check bottom-left
        if row < len(board) - 1 and col > 0:
            neighbors.append(board[row + 1][col - 1])
        # Check bottom-right
        if row < len(board) - 1 and col < len(board[0]) - 1:
            neighbors.append(board[row + 1][col + 1])

        # Rule: Lake 'L' cannot be adjacent to Ocean 'O'
        if biome == 'L' and 'O' in neighbors:
            return False
        if biome == 'O' and 'L' in neighbors:
            return False

        # Add more rules as necessary for other biome interactions
        # For example, 'B' (Beach) should be next to 'O' (Ocean)
        if biome == 'B' and 'O' not in neighbors:
            return False
        if biome == 'O' and 'B' not in neighbors:
            return False

        # If no rule is violated, return True
        return True
        
     # Function to place a biome on the board according to the rules
     def place_biome(self, board, row, col, biome):
        if self.can_place_biome(board, row, col, biome):
            board[row][col] = biome
            return True
        return False

     # Function to randomly fill the board according to biome placement rules
     def fill_board_with_biomes(self, board):
        for row in range(len(board)):
            for col in range(len(board[0])):
                # Randomly try to place a biome from the biomes list
                placed = False
                while not placed:
                    biome = random.choice(self.biomes)
                    placed = self.place_biome(board, row, col, biome)
   
