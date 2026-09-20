"""Regenerate Windows app assets from the Presentation Timer icon geometry.

Requires Pillow. The editable vector reference is Assets/AppIcon.svg.
"""

from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


ASSETS = Path(__file__).resolve().parents[1] / "src" / "PresentationTimer.App" / "Assets"
INK = "#152333"
ACCENT = "#64D1F5"
WHITE = "#F5FBFF"
WARM = "#FFB297"


def icon(size):
    canvas = Image.new("RGBA", (1024, 1024))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((24, 24, 1000, 1000), radius=216, fill=INK)
    draw.arc((194, 194, 830, 830), 140, 400, fill=ACCENT, width=72)
    for x, y in ((268, 714), (760, 714)):
        draw.ellipse((x - 36, y - 36, x + 36, y + 36), fill=ACCENT)
    draw.line(((512, 512), (512, 300)), fill=WHITE, width=64)
    draw.line(((512, 512), (660, 594)), fill=WHITE, width=64)
    for x, y, r in ((512, 300, 32), (660, 594, 32), (512, 512, 42)):
        draw.ellipse((x - r, y - r, x + r, y + r), fill=WHITE)
    draw.ellipse((724, 678, 796, 750), fill=WARM)
    return canvas.resize((size, size), Image.Resampling.LANCZOS)


def placed_icon(width, height, icon_size):
    image = Image.new("RGBA", (width, height), INK)
    mark = icon(icon_size)
    image.alpha_composite(mark, ((width - icon_size) // 2, (height - icon_size) // 2))
    return image


def wide_tile(width, height, icon_size, title_size):
    image = Image.new("RGBA", (width, height), INK)
    mark = icon(icon_size)
    image.alpha_composite(mark, (height // 2 - icon_size // 2, (height - icon_size) // 2))
    draw = ImageDraw.Draw(image)
    font_path = Path("C:/Windows/Fonts/segoeuib.ttf")
    font = ImageFont.truetype(str(font_path), title_size)
    draw.text((height, height // 2 - title_size * 1.05), "PRESENTATION", font=font, fill=WHITE)
    draw.text((height, height // 2 + title_size * .1), "TIMER", font=font, fill=ACCENT)
    return image


def main():
    for filename, size in (
        ("Square44x44Logo.scale-200.png", 88),
        ("Square150x150Logo.scale-200.png", 300),
        ("Square44x44Logo.targetsize-24_altform-unplated.png", 24),
        ("Square44x44Logo.targetsize-48_altform-lightunplated.png", 48),
        ("StoreLogo.png", 50),
    ):
        icon(size).save(ASSETS / filename)
    placed_icon(48, 48, 42).save(ASSETS / "LockScreenLogo.scale-200.png")
    placed_icon(1240, 600, 300).save(ASSETS / "SplashScreen.scale-200.png")
    wide_tile(620, 300, 220, 37).save(ASSETS / "Wide310x150Logo.scale-200.png")
    icon(256).save(ASSETS / "AppIcon.ico", sizes=[(size, size) for size in (16, 24, 32, 48, 64, 128, 256)])


if __name__ == "__main__":
    main()
