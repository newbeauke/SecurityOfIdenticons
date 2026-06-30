import itertools
from PIL import Image, ImageDraw, ImageFont

patterns = []
blue_patterns = []
grey_patterns = []

for combo in itertools.combinations(range(1, 16), 4):
    if (combo[0] ^ combo[1] ^ combo[2] ^ combo[3]) == 0:
        rows = set((idx - 1) // 3 for idx in combo)
        cols = set((idx - 1) % 3 for idx in combo)
        if len(rows) == 2 and len(cols) == 2:
            blue_patterns.append(combo)
        else:
            grey_patterns.append(combo)

all_patterns = blue_patterns + grey_patterns

cell_s = 10
pat_w = 3 * cell_s
pat_h = 5 * cell_s
cols = 15
rows = 7
pad_x = 15
pad_y = 15

width = cols * pat_w + (cols + 1) * pad_x
height = rows * pat_h + (rows + 1) * pad_y + 40

img = Image.new('RGB', (width, height), color='white')
draw = ImageDraw.Draw(img)

for i, pattern in enumerate(all_patterns):
    grid_c = i % cols
    grid_r = i // cols
    
    x_offset = pad_x + grid_c * (pat_w + pad_x)
    y_offset = pad_y + grid_r * (pat_h + pad_y)
    
    is_blue = (i < len(blue_patterns))
    color = "#105b9e" if is_blue else "#5a5a5a"
    
    for r in range(5):
        for c in range(3):
            cx = x_offset + c * cell_s
            cy = y_offset + r * cell_s
            
            idx = 3 * r + c + 1
            if idx in pattern:
                draw.rectangle([cx, cy, cx + cell_s - 1, cy + cell_s - 1], fill=color)
            else:
                draw.rectangle([cx, cy, cx + cell_s - 1, cy + cell_s - 1], outline="#e0e0e0")

# Draw legend
leg_y = height - 25
draw.rectangle([pad_x, leg_y, pad_x + cell_s - 1, leg_y + cell_s - 1], fill="#5a5a5a")
draw.text((pad_x + 15, leg_y - 2), "Scattered pattern (97)", fill="#333333")

blue_leg_x = pad_x + 150
draw.rectangle([blue_leg_x, leg_y, blue_leg_x + cell_s - 1, leg_y + cell_s - 1], fill="#105b9e")
draw.text((blue_leg_x + 15, leg_y - 2), "2x2 Rectangle (8)", fill="#333333")

artifact_path = r"C:\Users\beauk\.gemini\antigravity-ide\brain\c82cfe65-821e-49a0-8aad-bac8d03358bd\overlap.png"
img.save(artifact_path)
print("PNG generated successfully at " + artifact_path)
