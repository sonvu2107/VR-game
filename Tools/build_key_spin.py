"""Render a gold voxel key turning around its vertical axis, one PNG per frame."""
from pathlib import Path
import math
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/Pickups/KeySpin"
PREVIEW = ROOT / "Captures/KeySpin"
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
voxels = []
for y in range(4, 43):
    for x in range(-8, 9):
        ring = 4 <= y < 20 and -7 <= x < 7 and (y < 7 or y >= 17 or x < -4 or x >= 4)
        shaft = 18 <= y < 40 and -2 <= x < 2
        teeth = 33 <= y < 37 and 1 <= x < 8 or 39 <= y < 42 and -2 <= x < 7
        if ring or shaft or teeth:
            for z in range(-2, 2):
                voxels.append((x, y, z))

frames = []
for frame in range(16):
    angle = frame * math.tau / 16
    c, s = math.cos(angle), math.sin(angle)
    image = Image.new("RGBA", (32, 48))
    draw = ImageDraw.Draw(image)
    faces = []
    def project(x, y, z):
        return (round(16 + x * c + z * s), y)
    for x, y, z in voxels:
        # Two vertical faces and the top/bottom rims; depth sorting keeps the hole open.
        for normal, corners, color in [
            ((0, 0, -1), [(x,y,z),(x+1,y,z),(x+1,y+1,z),(x,y+1,z)], "#ffd867"),
            ((0, 0, 1), [(x,y,z+1),(x+1,y,z+1),(x+1,y+1,z+1),(x,y+1,z+1)], "#edb83f"),
            ((-1,0,0), [(x,y,z),(x,y,z+1),(x,y+1,z+1),(x,y+1,z)], "#b87722"),
            ((1,0,0), [(x+1,y,z),(x+1,y,z+1),(x+1,y+1,z+1),(x+1,y+1,z)], "#d3942e"),
        ]:
            nx, _, nz = normal
            if nx * -s + nz * c >= 0:
                continue
            depth = sum(-px*s+pz*c for px,py,pz in corners)/4
            faces.append((depth, [project(*v) for v in corners], color))
    for _, points, color in sorted(faces, reverse=True):
        draw.polygon(points, fill=color)
    # A one-pixel silhouette separates the gold from bright floors.
    alpha = image.getchannel("A")
    outlined = Image.new("RGBA", image.size)
    od = ImageDraw.Draw(outlined)
    for y in range(1,47):
        for x in range(1,31):
            if not alpha.getpixel((x,y)) and any(alpha.getpixel((x+dx,y+dy)) for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]):
                od.point((x,y), fill="#593217")
    outlined.alpha_composite(image)
    outlined.save(OUT / f"KeySpin_{frame:02d}.png")
    frames.append(outlined)

sheet = Image.new("RGBA", (32*8,48*2), "#211519")
for i, frame in enumerate(frames):
    sheet.alpha_composite(frame, ((i%8)*32,(i//8)*48))
sheet.resize((768,288),Image.Resampling.NEAREST).save(PREVIEW/"frames.png")
preview = []
for frame in frames:
    bg = Image.new("RGBA", (32,48), "#211519")
    bg.alpha_composite(frame)
    preview.append(bg.resize((192,288),Image.Resampling.NEAREST).convert("RGB"))
preview[0].save(PREVIEW/"key-spin.gif",save_all=True,append_images=preview[1:],duration=80,loop=0)
print("Created 16 separate 32x48 PNGs; full 360-degree turn, 1.28 seconds.")
