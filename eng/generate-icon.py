from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


size = 256
image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)

draw.rounded_rectangle((22, 28, 238, 244), radius=54, fill=(0, 0, 0, 46))
draw.rounded_rectangle((18, 18, 234, 234), radius=54, fill=(0, 103, 192, 255))
draw.rounded_rectangle((18, 18, 234, 134), radius=54, fill=(0, 120, 212, 255))

draw.rounded_rectangle((44, 48, 178, 132), radius=24, fill=(255, 255, 255, 246))
draw.polygon(((72, 128), (72, 154), (102, 130)), fill=(255, 255, 255, 246))
draw.rounded_rectangle((82, 126, 212, 206), radius=24, fill=(225, 243, 255, 255))
draw.polygon(((178, 202), (196, 224), (198, 198)), fill=(225, 243, 255, 255))

latin_font = ImageFont.truetype(r"C:\Windows\Fonts\segoeuib.ttf", 48)
chinese_font = ImageFont.truetype(r"C:\Windows\Fonts\msyhbd.ttc", 45)
draw.text((82, 84), "A", font=latin_font, fill=(0, 90, 170, 255), anchor="mm")
draw.text((150, 164), "译", font=chinese_font, fill=(0, 78, 146, 255), anchor="mm")

target = Path(__file__).resolve().parents[1] / "src" / "ScreenTranslator.App" / "Assets"
target.mkdir(parents=True, exist_ok=True)
image.save(
    target / "ScreenTranslator.ico",
    format="ICO",
    sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
)
