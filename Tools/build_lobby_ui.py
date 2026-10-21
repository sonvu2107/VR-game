"""Build the hand-lettered lobby overlays without modifying the dungeon painting.

Run from the project root with Python and Pillow. The output is imported as
Unity UI sprites from Assets/Resources/UI/Menu.
"""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets/Resources/UI/Menu"
FONT_SERIF = Path("C:/Windows/Fonts/cambriab.ttf")
FONT_CALLIGRAPHY = ROOT / "Tools/fonts/Shalimar-Regular.ttf"
FONT_FALLBACK = ROOT / "Assets/Resources/Story/StoryVietnamese.ttf"
TITLE_SOURCE = ROOT / "Tools/source_art/LabyrinthGoldBrushSource.png"


def font(path, size):
    return ImageFont.truetype(str(path if path.exists() else FONT_FALLBACK), size)


def centered_text(draw, xy, text, face, fill, stroke=0, stroke_fill=None):
    x, y = xy
    box = draw.textbbox((0, 0), text, font=face, stroke_width=stroke)
    draw.text((x - (box[2] - box[0]) / 2 - box[0], y - box[1]), text,
              font=face, fill=fill, stroke_width=stroke, stroke_fill=stroke_fill)


def rule(draw, x1, x2, y, color="#b38b55"):
    draw.line((x1, y, x2, y), fill=color, width=3)
    draw.line((x1, y + 7, x2, y + 7), fill="#453426", width=1)
    for x in (x1 + 13, x2 - 13):
        draw.polygon([(x, y - 9), (x + 10, y + 2), (x, y + 13), (x - 10, y + 2)],
                     fill="#b8975d", outline="#403026")


def title():
    w, h = 1500, 400
    # The source artwork has original hand-painted thick/thin strokes.
    # Keep it with the generator so rerunning this script does not restore
    # the old plain font logo.
    source = Image.open(TITLE_SOURCE).convert("RGBA")
    pixels = source.load()
    for y in range(source.height):
        for x in range(source.width):
            red, green, blue, alpha = pixels[x, y]
            # Neutralize the source's few red fringes; the title stays gold.
            if alpha and red > 100 and red > green * 2.3 and blue < 80:
                pixels[x, y] = (min(red, int(green * 1.8)), green, blue, alpha)
    bounds = source.getchannel("A").point(lambda a: 255 if a > 16 else 0).getbbox()
    source = source.crop(bounds).resize((1240, 330), Image.Resampling.LANCZOS)
    art = Image.new("RGBA", (w, h))
    art.alpha_composite(source, ((w - source.width) // 2, 5))
    art.save(OUT / "LabyrinthTitleLogo.png")


def tagline():
    w, h = 1300, 125
    art = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(art)
    text = "DÁM BƯỚC VÀO? LIỆU CÓ LỐI RA?"
    face = font(FONT_CALLIGRAPHY, 90)
    while d.textbbox((0, 0), text, font=face)[2] > 1190:
        face = font(FONT_CALLIGRAPHY, face.size - 1)
    centered_text(d, (w / 2 + 2, 12), text, face, "#180c19", 3, "#180c19")
    centered_text(d, (w / 2, 9), text, face, "#ffe1a0", 2, "#955725")
    rule(d, 13, 43, 58)
    rule(d, w - 43, w - 13, 58)
    art.save(OUT / "LabyrinthTagline.png")


def course_plaque():
    w, h = 1200, 115
    art = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(art)
    for y in range(11, 106):
        alpha = int(118 + 92 * (1 - abs(y - h / 2) / (h / 2)))
        d.line((115, y, 1085, y), fill=(11, 11, 24, alpha))
    d.line((130, 12, 1070, 12), fill="#9d7751", width=3)
    d.line((130, 103, 1070, 103), fill="#9d7751", width=3)
    for x in (125, 1075):
        d.polygon([(x, 40), (x + 17, 57), (x, 74), (x - 17, 57)],
                  fill="#b89962", outline="#352739")
        d.ellipse((x - 5, 52, x + 5, 62), fill="#70bdd3")
    centered_text(d, (w / 2 + 2, 22), "ĐỒ HỌA VÀ HIỆN THỰC ẢO",
                  font(FONT_SERIF, 56), "#0a1821", 2, "#0a1821")
    centered_text(d, (w / 2, 19), "ĐỒ HỌA VÀ HIỆN THỰC ẢO",
                  font(FONT_SERIF, 56), "#d9e8e8", 1, "#315e67")
    art.save(OUT / "LabyrinthCoursePlaque.png")


def button():
    w, h = 750, 200
    art = Image.new("RGBA", (w, h))
    d = ImageDraw.Draw(art)
    # A forged crimson gate plate, rather than the previous teal masonry.
    shape = [(45, 11), (705, 11), (743, 50), (743, 150),
             (705, 189), (45, 189), (7, 150), (7, 50)]
    d.polygon(shape, fill="#160d19", outline="#f0bc71", width=7)
    d.polygon([(56, 23), (694, 23), (730, 58), (730, 142),
               (694, 177), (56, 177), (20, 142), (20, 58)],
              fill="#72222a", outline="#cf683d", width=5)
    for y in range(32, 169):
        red = 108 + int(38 * (1 - abs(y - 100) / 100))
        d.line((76, y, 674, y), fill=(red, 31, 35, 255))
    d.line([(40, 60), (63, 36), (270, 36)], fill="#6ee4eb", width=6)
    d.line([(480, 36), (687, 36), (710, 60)], fill="#c681f0", width=6)
    d.line([(40, 140), (63, 164), (270, 164)], fill="#c681f0", width=6)
    d.line([(480, 164), (687, 164), (710, 140)], fill="#6ee4eb", width=6)
    d.line((83, 39, 667, 39), fill="#ffcf85", width=4)
    d.line((83, 162, 667, 162), fill="#431825", width=5)
    for x, sign in ((54, 1), (696, -1)):
        d.polygon([(x, 68), (x + sign * 25, 100), (x, 132),
                   (x - sign * 16, 100)], fill="#ec9a53", outline="#ffe0a4")
        d.polygon([(x, 83), (x + sign * 12, 100), (x, 117),
                   (x - sign * 8, 100)], fill="#67deea" if sign == 1 else "#bb73eb")
    d.polygon([(141, 46), (162, 47), (105, 152), (86, 151)], fill="#73d9e0")
    d.polygon([(624, 48), (644, 48), (583, 151), (564, 151)], fill="#c27ee4")
    face = font(FONT_CALLIGRAPHY, 130)
    centered_text(d, (w / 2 + 3, 46), "BẮT ĐẦU", face,
                  "#1c0d17", 5, "#1c0d17")
    centered_text(d, (w / 2, 41), "BẮT ĐẦU", face,
                  "#ffe4a6", 2, "#793b23")
    art.save(OUT / "LabyrinthEnterButton.png")


def credits():
    w, h = 1190, 710
    art = Image.new("RGBA", (w, h))
    outer = [(45, 13), (478, 13), (530, 2), (660, 2), (712, 13),
             (1145, 13), (1180, 73), (1160, 619), (1110, 698),
             (80, 698), (30, 619), (10, 73)]
    inner = [(60, 34), (480, 34), (540, 22), (650, 22), (710, 34),
             (1130, 34), (1155, 82), (1138, 611), (1094, 673),
             (96, 673), (52, 611), (35, 82)]
    body = Image.new("RGBA", (w, h))
    bd = ImageDraw.Draw(body)
    for y in range(h):
        blend = abs(y - h * 0.42) / h
        bd.line((0, y, w, y), fill=(19 + int(14 * blend),
                                    15 + int(6 * blend),
                                    28 + int(10 * blend), 246))
    mask = Image.new("L", (w, h))
    ImageDraw.Draw(mask).polygon(outer, fill=255)
    body.putalpha(mask)
    art.alpha_composite(body)
    d = ImageDraw.Draw(art)
    d.line(outer + [outer[0]], fill="#efaf64", width=9, joint="curve")
    d.line(inner + [inner[0]], fill="#8e2833", width=9, joint="curve")
    d.line([(75, 48), (465, 48), (535, 38), (655, 38),
            (725, 48), (1115, 48)], fill="#b77f52", width=3)
    d.line([(78, 659), (115, 656), (1075, 656), (1112, 659)],
           fill="#b77f52", width=3)
    # The upper crest is a small pointed labyrinth gate with a cyan portal.
    d.polygon([(558, 17), (595, 3), (632, 17), (626, 73),
               (595, 89), (564, 73)], fill="#842d38", outline="#ffd08c")
    d.polygon([(574, 31), (595, 15), (616, 31), (612, 66),
               (595, 75), (578, 66)], fill="#151e2e", outline="#dfa85e")
    d.polygon([(584, 43), (595, 27), (606, 43), (604, 59),
               (595, 66), (586, 59)], fill="#66b6cf")
    d.line((595, 29, 595, 65), fill="#c4f0ed", width=3)
    for x, sign in ((64, 1), (1126, -1)):
        d.polygon([(x, 75), (x + sign * 75, 44), (x + sign * 40, 84),
                   (x + sign * 97, 120), (x + sign * 21, 112)],
                  fill="#8d2633", outline="#dd935c")
        d.polygon([(x, 634), (x + sign * 67, 658), (x + sign * 37, 610),
                   (x + sign * 93, 573), (x + sign * 17, 597)],
                  fill="#8d2633", outline="#dd935c")
        d.line((x + sign * 16, 160, x + sign * 16, 546),
               fill="#8f3441", width=8)
        for y in (230, 335, 440):
            d.polygon([(x + sign * 16, y - 25),
                       (x + sign * 39, y),
                       (x + sign * 16, y + 25),
                       (x - sign * 7, y)],
                      fill="#b95b41", outline="#f2b36b")
            d.polygon([(x + sign * 16, y - 13),
                       (x + sign * 28, y),
                       (x + sign * 16, y + 13),
                       (x + sign * 4, y)], fill="#76c1cf")
    header = "NHÓM THỰC HIỆN"
    centered_text(d, (w / 2, 92), header, font(FONT_SERIF, 64),
                  "#fff0bd", 2, "#1a0d18")
    rule(d, 260, 930, 178, "#c27345")
    entries = [
        "Vũ Ngọc Sơn (Nhóm trưởng)",
        "Trần Văn Thịnh",
        "Nguyễn Minh Đức",
        "Nguyễn Thúy Hiền",
        "Lê Thị Dịu",
    ]
    for i, entry in enumerate(entries):
        face = font(FONT_SERIF, 54 if i == 0 else 52)
        centered_text(d, (w / 2, 207 + i * 66), entry, face,
                      "#fff0c7" if i == 0 else "#f2e2c2", 2, "#1a0d18")
    rule(d, 235, 955, 579, "#c27345")
    centered_text(d, (w / 2, 597), "GVHD: Th.S Nguyễn Mạnh Hùng",
                  font(FONT_SERIF, 49), "#fff0bd", 2, "#1a0d18")
    art.save(OUT / "LabyrinthCreditsPanel.png")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    title()
    tagline()
    course_plaque()
    button()
    credits()
