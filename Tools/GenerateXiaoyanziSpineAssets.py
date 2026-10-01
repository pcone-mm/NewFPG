from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import json

ROOT = Path(r"D:/Unity/NewFPG")
SRC = ROOT / "Assets/Art/Characters/xiaoyanzi.png"
OUT = ROOT / "Assets/Art/Characters/XiaoyanziSpine"
OUT.mkdir(parents=True, exist_ok=True)

im = Image.open(SRC).convert("RGBA")
pix = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, a = pix[x, y]
        # Remove the neon green screen, with a soft edge for antialiasing.
        green = max(0, min(255, int((g - max(r, b) * 0.55 - 35) * 4.0)))
        pix[x, y] = (r, g, b, max(0, 255 - green))

im.save(OUT / "xiaoyanzi_full.png")

parts = {
    "back_hair": [(240, 0), (740, 0), (760, 760), (320, 900), (210, 520)],
    "head_front": [(420, 150), (900, 150), (900, 650), (420, 650)],
    "torso": [(380, 510), (850, 510), (900, 1100), (360, 1100)],
    "left_arm": [(270, 590), (540, 590), (540, 1030), (260, 1030)],
    "right_arm": [(740, 560), (1200, 560), (1200, 950), (730, 950)],
    "cloak": [(730, 650), (1160, 650), (1160, 1450), (760, 1230)],
    "left_leg": [(270, 900), (620, 900), (620, 1572), (240, 1572)],
    "right_leg": [(570, 900), (950, 900), (950, 1572), (560, 1572)],
    "flower_branch": [(0, 820), (540, 820), (540, 1370), (0, 1370)],
}

for name, poly in parts.items():
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).polygon(poly, fill=255)
    # Keep only the non-green pixels inside the part mask.
    alpha = im.getchannel("A")
    alpha = Image.composite(alpha, Image.new("L", im.size, 0), mask)
    layer = im.copy()
    layer.putalpha(alpha)
    bbox = layer.getbbox()
    if bbox:
        layer.crop(bbox).save(OUT / f"{name}.png")

regions = {}
atlas_lines = []
packed = Image.new("RGBA", (2048, 2048), (0, 0, 0, 0))
pack_x = pack_y = row_h = 0
for p in sorted(OUT.glob("*.png")):
    if p.name in ("xiaoyanzi_full.png", "xiaoyanzi_packed.png"):
        continue
    q = Image.open(p)
    regions[p.stem] = {"x": 0, "y": 0, "width": q.width, "height": q.height, "path": p.stem}
    if pack_x + q.width > 2048:
        pack_x = 0
        pack_y += row_h
        row_h = 0
    packed.alpha_composite(q, (pack_x, pack_y))
    regions[p.stem].update({"x": pack_x, "y": pack_y})
    pack_x += q.width
    row_h = max(row_h, q.height)

packed.save(OUT / "xiaoyanzi_packed.png")
atlas_lines += ["xiaoyanzi_packed.png", "size: 2048,2048", "format: RGBA8888", "filter: Linear,Linear", "repeat: none", ""]
for name, r in regions.items():
    atlas_lines += [name, "  rotate: false", f"  xy: {r['x']}, {r['y']}", f"  size: {r['width']}, {r['height']}", f"  orig: {r['width']}, {r['height']}", "  offset: 0, 0", "  index: -1", ""]

bones = [{"name": "root"}, {"name": "body", "parent": "root", "x": 0, "y": 0},
         {"name": "head", "parent": "body", "x": 0, "y": 420},
         {"name": "left_arm", "parent": "body", "x": -180, "y": 230},
         {"name": "right_arm", "parent": "body", "x": 220, "y": 230},
         {"name": "left_leg", "parent": "body", "x": -90, "y": -420},
         {"name": "right_leg", "parent": "body", "x": 100, "y": -420},
         {"name": "cloak", "parent": "body", "x": 250, "y": 50},
         {"name": "flower_branch", "parent": "body", "x": -250, "y": -20}]
slots = []
attachments = {}
bone_for = {"back_hair": "body", "head_front": "head", "torso": "body", "left_arm": "left_arm", "right_arm": "right_arm", "cloak": "cloak", "left_leg": "left_leg", "right_leg": "right_leg", "flower_branch": "flower_branch"}
for name, r in regions.items():
    slots.append({"name": name, "bone": bone_for[name], "attachment": name})
    attachments[name] = {name: {"type": "region", "path": name, "x": 0, "y": 0, "rotation": 0, "scaleX": 1, "scaleY": 1, "width": r["width"], "height": r["height"]}}

skeleton = {
    "skeleton": {"hash": "xiaoyanzi-generated", "spine": "3.8.75", "x": 0, "y": 0, "width": im.width, "height": im.height, "images": "./"},
    "bones": bones,
    "slots": slots,
    "skins": [{"name": "default", "attachments": attachments}],
    "animations": {"idle": {
        "bones": {"body": {"rotate": [{"time": 0, "angle": -1.2}, {"time": 0.8, "angle": 1.2}, {"time": 1.6, "angle": -1.2}], "translate": [{"time": 0, "y": 0}, {"time": 0.8, "y": 5}, {"time": 1.6, "y": 0}]},
                   "head": {"rotate": [{"time": 0, "angle": -0.8}, {"time": 0.8, "angle": 0.8}, {"time": 1.6, "angle": -0.8}]},
                   "cloak": {"rotate": [{"time": 0, "angle": -2}, {"time": 0.8, "angle": 2}, {"time": 1.6, "angle": -2}]},
                   "flower_branch": {"rotate": [{"time": 0, "angle": 1.5}, {"time": 0.8, "angle": -1.5}, {"time": 1.6, "angle": 1.5}]}},
        "slots": {}, "draworder": [{"time": 0, "offsets": []}]}}
}
(OUT / "xiaoyanzi.json").write_text(json.dumps(skeleton, ensure_ascii=False, indent=2), encoding="utf-8")
(OUT / "xiaoyanzi.atlas").write_text("\n".join(atlas_lines), encoding="utf-8")
(OUT / "README_导入说明.txt").write_text("将本文件夹中的 PNG、xiaoyanzi.atlas、xiaoyanzi.json 放入同一目录，在 Spine 中选择 Import Data 导入 xiaoyanzi.json。idle 为 1.6 秒循环待机：身体/头部轻微呼吸，披风和花枝摆动。原图是单张绿幕立绘，切图中的遮挡区域为基础补全，做大幅动作前建议继续修图。", encoding="utf-8")
print(f"Generated {len(regions)} layers in {OUT}")
