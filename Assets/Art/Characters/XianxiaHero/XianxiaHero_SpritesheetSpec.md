# XianxiaHero Spritesheet Spec

## Visual Direction

Simple illustrated xianxia field-guide style: beige xuan paper texture feeling, black hand-drawn ink outlines, low-saturation teal green, ochre red, warm ivory, and restrained eastern mythic cultivator details. The character should read clearly at small size and avoid dense costume clutter.

## Spritesheet Recommendation

- Sheet layout: 8 columns x 4 rows, 32 frames total.
- Rows: Down, Left, Right, Up.
- Columns per row: Idle 1-4, Walk 1-4.
- Single frame size: 128 x 128 px.
- Full sheet size: 1024 x 512 px.
- Character footprint: about 72-88 px tall inside each frame, with consistent feet placement.
- Padding: transparent or removable background around every frame; no frame borders.
- Pivot: Bottom Center, normalized `(0.5, 0.12)`.
- Pixels Per Unit: 128.
- Animation fps: Idle 4 fps, Walk 8 fps.
- Filter Mode: Point for crisp pixel-like sampling, or Bilinear if the final art is painterly and displayed larger.
- Compression: None during review; later use Normal Quality if memory needs it.

## Frame Map

| Row | Direction | Columns 1-4 | Columns 5-8 |
| --- | --- | --- | --- |
| 1 | Down | Idle_Down_00-03 | Walk_Down_00-03 |
| 2 | Left | Idle_Left_00-03 | Walk_Left_00-03 |
| 3 | Right | Idle_Right_00-03 | Walk_Right_00-03 |
| 4 | Up | Idle_Up_00-03 | Walk_Up_00-03 |

## Motion Notes

- Idle: subtle breathing, robe hem and ribbon movement only; keep feet planted.
- Walk: four-frame loop with readable step, robe sway, and sleeve/ribbon follow-through.
- Direction consistency: the same hero, same scale, same costume colors, same weapon or talisman placement in all frames.
- Avoid: dramatic perspective, large attack poses, particle effects, text labels, cast shadows, frame grids, inconsistent scale, or different character designs between rows.

