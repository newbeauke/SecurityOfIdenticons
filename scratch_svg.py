import itertools

patterns = []
blue_patterns = []
grey_patterns = []

# Cells are 1 to 15
for combo in itertools.combinations(range(1, 16), 4):
    if (combo[0] ^ combo[1] ^ combo[2] ^ combo[3]) == 0:
        rows = set((idx - 1) // 3 for idx in combo)
        cols = set((idx - 1) % 3 for idx in combo)
        if len(rows) == 2 and len(cols) == 2:
            blue_patterns.append(combo)
        else:
            grey_patterns.append(combo)

all_patterns = blue_patterns + grey_patterns

# Dimensions
cell_s = 10
pat_w = 3 * cell_s
pat_h = 5 * cell_s
cols = 15
rows = 7
pad_x = 15
pad_y = 15

width = cols * pat_w + (cols + 1) * pad_x
height = rows * pat_h + (rows + 1) * pad_y + 40 # extra space for legend

svg = []
svg.append(f'<svg width="{width}" height="{height}" xmlns="http://www.w3.org/2000/svg" style="background-color: white; font-family: sans-serif;">')

# Draw patterns
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
                svg.append(f'<rect x="{cx}" y="{cy}" width="{cell_s}" height="{cell_s}" fill="{color}"/>')
            else:
                svg.append(f'<rect x="{cx}" y="{cy}" width="{cell_s}" height="{cell_s}" fill="none" stroke="#e0e0e0" stroke-width="1"/>')

# Draw legend
leg_y = height - 25
# grey leg
svg.append(f'<rect x="{pad_x}" y="{leg_y}" width="{cell_s}" height="{cell_s}" fill="#5a5a5a"/>')
svg.append(f'<text x="{pad_x + 15}" y="{leg_y + 9}" font-size="10" fill="#333">Scattered pattern (97)</text>')

# blue leg
blue_leg_x = pad_x + 150
svg.append(f'<rect x="{blue_leg_x}" y="{leg_y}" width="{cell_s}" height="{cell_s}" fill="#105b9e"/>')
svg.append(f'<text x="{blue_leg_x + 15}" y="{leg_y + 9}" font-size="10" fill="#333">2x2 Rectangle (8)</text>')

svg.append('</svg>')

# Save directly to artifacts
artifact_path = r"C:\Users\beauk\.gemini\antigravity-ide\brain\c82cfe65-821e-49a0-8aad-bac8d03358bd\overlap.svg"
with open(artifact_path, 'w') as f:
    f.write("\n".join(svg))
print("SVG generated successfully at " + artifact_path)
