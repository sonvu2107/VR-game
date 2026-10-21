"""Render the lobby sprites at the StartMenuControls 1920x1080 reference size."""

from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MENU = ROOT / "Assets/Resources/UI/Menu"
CANVAS = (1920, 1080)


def place(canvas, name, left, top, width, height):
    sprite = Image.open(MENU / name).convert("RGBA")
    sprite = sprite.resize((width, height), Image.Resampling.LANCZOS)
    canvas.alpha_composite(sprite, (left, top))


preview = Image.open(MENU / "LabyrinthLobbyBackground.png").convert("RGBA")
preview = preview.resize(CANVAS, Image.Resampling.LANCZOS)
place(preview, "LabyrinthTitleLogo.png", 235, 40, 1450, 386)
place(preview, "LabyrinthTagline.png", 360, 365, 1200, 115)
place(preview, "LabyrinthCoursePlaque.png", 420, 475, 1080, 104)
place(preview, "LabyrinthCreditsPanel.png", 1258, 687, 620, 369)
place(preview, "LabyrinthEnterButton.png", 70, 899, 360, 96)
out = ROOT / "Captures/LobbyPreview.png"
out.parent.mkdir(parents=True, exist_ok=True)
preview.convert("RGB").save(out)
print(out)
