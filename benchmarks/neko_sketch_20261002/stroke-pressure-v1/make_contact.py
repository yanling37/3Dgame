"""Make a visual contact sheet from measured PNGs, without changing drawings."""
import argparse
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

parser = argparse.ArgumentParser()
parser.add_argument("results", nargs="?", default="local-results")
args = parser.parse_args()
root = Path(__file__).resolve().parent
results = root / args.results
font_path = Path("C:/Windows/Fonts/msyh.ttc")
if not font_path.exists():
    font_path = Path("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf")
font = ImageFont.truetype(str(font_path), 20)
small = ImageFont.truetype(str(font_path), 16)
modes = [("constant", "Constant width"), ("sketchling", "Sketchling 0.6.0"),
         ("pfh-auto", "Distance simulation"), ("pressure-arc", "Arclength pressure")]
rows = [("T02", "Same curved stroke"), ("T03", "Closed circle"), ("C01", "Same hug geometry")]
sheet = Image.new("RGB", (1280, 1140), "#f4f3ef")
draw = ImageDraw.Draw(sheet)
for j, (_, label) in enumerate(modes):
    draw.text((j*320+14, 12), label, font=font, fill="#171717")
for i, (case, label) in enumerate(rows):
    y = 50+i*355
    for j, (mode, _) in enumerate(modes):
        source = results / "png" / f"{case}_{mode}_s17_320.png"
        sheet.paste(Image.open(source).convert("RGB"), (j*320, y))
        draw.text((j*320+14, y+324), label, font=small, fill="#454545")
draw.text((14, 1120), "Synthetic pressure only. Fixed centerlines; no model inference; human ratings pending.", font=small, fill="#454545")
sheet.save(root / "brush-comparison.png")
pair = Image.new("RGB", (640, 405), "#f4f3ef")
pair_draw = ImageDraw.Draw(pair)
for j, (mode, label) in enumerate([( "constant", "等宽对照"), ("pressure-arc", "单笔内粗细变化")]):
    pair_draw.text((j*320+14, 12), label, font=font, fill="#171717")
    source = Image.open(results / "png" / f"T02_{mode}_s17_320.png").convert("RGB")
    pair.paste(source, (j*320, 50))
pair_draw.text((14, 378), "相同路径；模拟笔压，尚未进行人工评分。", font=small, fill="#454545")
pair.save(root / "brush-before-after.png")
print(root / "brush-comparison.png")
