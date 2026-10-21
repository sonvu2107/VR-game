"""Draw fitted key inlays on the same UV canvas as Story/KeyDais.png."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import re
import uuid

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'Assets/Resources/Story'
base = Image.open(DEST / 'KeyDais.png').convert('RGBA')
# Coordinates are traced inside the gold rims, on an 8-source-pixel grid.
outline = [(63,40),(72,40),(76,42),(78,46),(78,51),(76,54),
           (70,56),(70,65),(74,65),(74,66),(69,66),(69,71),
           (74,71),(74,73),(69,73),(68,76),(64,76),(64,75),
           (64,56),(60,55),(57,52),(57,46),(59,43)]
for index, offset in enumerate((0,57)):
    mask = Image.new('L', (192,128))
    d = ImageDraw.Draw(mask)
    d.polygon([(x+offset,y) for x,y in outline], fill=255)
    d.ellipse((62+offset,44,73+offset,51), fill=0)
    inset = mask.filter(ImageFilter.MinFilter(3))
    art = Image.new('RGBA', mask.size)
    for y in range(128):
        for x in range(192):
            if not mask.getpixel((x,y)):
                continue
            if not mask.getpixel((x,y-1)):
                color = (246,211,122,255)
            elif not mask.getpixel((x+1,y)) or not mask.getpixel((x,y+1)):
                color = (112,73,30,255)
            else:
                color = (204,159,67,255) if inset.getpixel((x,y)) else (170,121,45,255)
            art.putpixel((x,y),color)
    path = DEST / f'KeyDaisInlay{index+1}.png'
    art.save(path)
    meta = Path(str(path)+'.meta')
    if not meta.exists():
        template = (DEST/'KeyDais.png.meta').read_text()
        template = re.sub(r'guid: [a-f0-9]+', 'guid: '+uuid.uuid4().hex, template, count=1)
        meta.write_text(template)
    base.alpha_composite(art.resize(base.size, Image.Resampling.NEAREST))
out = ROOT/'Captures/KeyDais/fitted-inlays.png'
out.parent.mkdir(parents=True,exist_ok=True)
base.resize((768,512), Image.Resampling.NEAREST).save(out)
print(out)
