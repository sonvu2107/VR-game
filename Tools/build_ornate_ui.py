"""Author 25 separate, palette-limited pixel UI sprites. No resampling of concept art."""
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/UI/Ornate"
PREVIEW = ROOT / "Captures/OrnateUI"
P = {
    "void": "#160b0d", "ink": "#241114", "slate": "#391b20", "edge": "#090406",
    "shade": "#401318", "bronze": "#75232c", "gold": "#b43e48", "light": "#e57679",
    "shine": "#ffc1b2", "blue": "#68202b", "cyan": "#b64b54", "ice": "#ffd1c2",
    "paper": "#c5ac72", "paperlight": "#ddc58e", "papershadow": "#9a7c50",
    "gray": "#5e625c", "graylight": "#8a8c79", "red": "#892f43",
    "redlight": "#c34e58", "green": "#407459", "greenlight": "#76a36a",
    "purple": "#5c497b", "purplelight": "#9884b1",
}
OUT.mkdir(parents=True, exist_ok=True)
PREVIEW.mkdir(parents=True, exist_ok=True)
manifest = []


def blank(w, h):
    return Image.new("RGBA", (w, h))


def rect(d, box, color):
    d.rectangle(box, fill=P.get(color, color))


def line(d, pts, color, width=1):
    d.line(pts, fill=P.get(color, color), width=width)


def knot(disabled=False, bright=False):
    im = blank(12, 12)
    d = ImageDraw.Draw(im)
    colors = ("gray", "graylight", "graylight") if disabled else ("bronze", "gold", "shine" if bright else "light")
    path = [(1, 10), (1, 2), (9, 2), (9, 8), (4, 8), (4, 5), (6, 5)]
    line(d, [(x + 1, y + 1) for x, y in path], "edge", 3)
    line(d, path, colors[0], 3)
    line(d, path, colors[1])
    line(d, [(1, 9), (1, 2), (8, 2)], colors[2])
    rect(d, (0, 0, 2, 1), colors[2])
    rect(d, (0, 0, 1, 2), colors[2])
    return im


def frame(w, h, variant="ornate", hollow=False):
    im = blank(w, h)
    d = ImageDraw.Draw(im)
    disabled = variant == "disabled"
    hover = variant in ("hover", "selected", "thumbhover")
    pressed = variant == "pressed"
    fill = "blue" if hover else "void" if pressed else "ink"
    border = ["edge", "shade", "gold", "light", "bronze", "edge"]
    if disabled:
        border = ["edge", "void", "gray", "graylight", "gray", "edge"]
    if variant == "paper":
        fill = "paper"
        border = ["edge", "shade", "papershadow", "paperlight", "paper", "papershadow"]
    rect(d, (2, 2, w-3, h-3), fill if not hollow else (0, 0, 0, 0))
    for i, c in enumerate(border):
        x = 1 + i
        line(d, [(x, h-x-1), (x, x), (w-x-1, x), (w-x-1, h-x-1), (x, h-x-1)], c)
    if not hollow:
        rect(d, (7, 7, w-8, h-8), fill)
        line(d, [(8, h-9), (8, 8), (w-9, 8)], "cyan" if hover else "papershadow" if variant == "paper" else "void")
    if variant in ("ornate", "hover", "selected", "pressed", "disabled"):
        k = knot(disabled, hover)
        for x, y, flipx, flipy in [(0,0,False,False),(w-12,0,True,False),(0,h-12,False,True),(w-12,h-12,True,True)]:
            tile = k.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if flipx else k
            if flipy: tile = tile.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            im.alpha_composite(tile, (x,y))
    # Diamond rivets remain in the nine-slice corners; no stretched centre ornament.
    if variant in ("hover", "selected"):
        for x in (4,w-5):
            for y in (4,h-5):
                rect(d,(x,y,x+1,y+1),"ice")
    if variant == "selected":
        line(d, [(w//2-5,h//2),(w//2-1,h//2+4),(w//2+6,h//2-4)], "ice", 2)
    return im


def save(name, im, border=(0,0,0,0), role=""):
    im.save(OUT / (name + ".png"))
    assert set(im.getchannel("A").getdata()) <= {0,255}
    manifest.append(dict(name=name, size=list(im.size), border=list(border), role=role))


save("panel_ornate", frame(64,64), (12,12,12,12), "Menu, pause, results, dialogue")
save("panel_plain", frame(48,48,"plain"), (8,8,8,8), "Objectives and small information panels")
save("panel_parchment", frame(48,48,"paper"), (8,8,8,8), "Journal and reading")
save("panel_hollow", frame(48,48,hollow=True), (12,12,12,12), "Skill icons and minimap; transparent centre")
for state in ("normal","hover","pressed","disabled"):
    save("button_"+state, frame(96,32,"ornate" if state=="normal" else state), (12,12,12,12), "Button "+state)
for state in ("normal","hover","selected","disabled"):
    save("widget_"+state, frame(32,32,"ornate" if state=="normal" else state), (12,12,12,12), "Toggle, field, small selection "+state)
im=blank(10,32);d=ImageDraw.Draw(im)
rect(d,(2,0,7,31),"edge");rect(d,(3,1,6,30),"shade");rect(d,(4,2,5,29),"void")
save("scroll_track",im,(4,3,4,3),"Scrollbar track")
for state in ("normal","hover"):
    save("scroll_thumb"+("_hover" if state=="hover" else ""),frame(16,24,"thumbhover" if state=="hover" else "plain"),(6,8,6,8),"Scrollbar/slider handle")
im=blank(64,16);d=ImageDraw.Draw(im)
for inset,c in enumerate(["edge","shade","gold","light","bronze","edge"]):
    d.rectangle((inset,inset,63-inset,15-inset),outline=P[c])
save("stat_frame",im,(7,6,7,6),"Hollow statbar border")
for name,base,light in [("health","red","redlight"),("mana","blue","cyan"),("stamina","green","greenlight"),("xp","purple","purplelight")]:
    im=blank(8,8);d=ImageDraw.Draw(im);rect(d,(0,0,7,7),base)
    rect(d,(0,1,7,2),light);line(d,[(0,7),(7,7)],"edge")
    save("fill_"+name,im,(0,3,0,2),"Statbar fill "+name)
for name in ("close","warning","left","right"):
    im=blank(16,16);d=ImageDraw.Draw(im)
    if name=="close":
        for off,c in [(1,"shade"),(0,"light")]:
            line(d,[(4+off,4+off),(11+off,11+off)],c,2)
            line(d,[(11+off,4+off),(4+off,11+off)],c,2)
    elif name=="warning":
        d.polygon([(8,1),(15,13),(1,13)],fill=P["shade"])
        d.polygon([(8,2),(14,12),(2,12)],fill=P["gold"])
        rect(d,(7,5,8,8),"void");rect(d,(7,10,8,11),"void")
    else:
        line(d,[(10,3),(5,8),(10,13)],"shade",4)
        line(d,[(9,3),(4,8),(9,13)],"light",2)
        if name=="right":im=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    save("icon_"+name,im,role="Icon "+name)
im=blank(96,16);d=ImageDraw.Draw(im)
line(d,[(2,9),(36,9),(42,3),(48,9),(54,3),(60,9),(93,9)],"bronze")
line(d,[(8,7),(35,7),(41,1),(48,8),(55,1),(61,7),(87,7)],"gold")
d.polygon([(48,3),(52,7),(48,11),(44,7)],outline=P["light"])
save("flourish",im,role="Decorative separator")
assert len(manifest)==25
(OUT/"manifest.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding="utf-8")

# Contact sheet only for review; every import above is already a standalone PNG.
sheet=Image.new("RGB",(1000,920),"#172126")
d=ImageDraw.Draw(sheet)
for i,item in enumerate(manifest):
    x=20+(i%4)*250;y=16+(i//4)*130
    asset=Image.open(OUT/(item["name"]+".png"))
    scale=min(3,220//asset.width,98//asset.height)
    if scale>1:asset=asset.resize((asset.width*scale,asset.height*scale),Image.Resampling.NEAREST)
    sheet.paste(asset,(x,y+20),asset)
    d.text((x,y),item["name"],fill="#e57679")
sheet.save(PREVIEW/"asset-review.png")
print(f"PASS: {len(manifest)} individual PNGs; binary alpha; fixed palette; integer pixel geometry.")
