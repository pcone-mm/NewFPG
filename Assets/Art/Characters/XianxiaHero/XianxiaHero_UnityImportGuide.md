# XianxiaHero Unity Import Guide

## Files To Create

- `XianxiaHero_spritesheet_source.png`: generated source sheet on flat chroma-key background.
- `XianxiaHero_spritesheet.png`: final transparent spritesheet after background removal.
- `XianxiaHero.controller`: Animator Controller.

## Texture Import Settings

- Texture Type: Sprite (2D and UI).
- Sprite Mode: Multiple.
- Pixels Per Unit: 128.
- Mesh Type: Full Rect.
- Pivot: Custom `(0.5, 0.12)` or Bottom Center if custom pivot is unavailable.
- Filter Mode: Point for sharper game scale, Bilinear for smoother illustrated scaling.
- Compression: None while iterating.
- Generate Mip Maps: Off.
- Alpha Is Transparency: On for the transparent final sheet.

## Slicing

- Type: Grid By Cell Size.
- Cell Size: 128 x 128.
- Offset: 0 x 0.
- Padding: 0 x 0.
- Pivot: Custom `(0.5, 0.12)`.

## Suggested Sprite Names

- `XianxiaHero_Idle_Down_00` to `XianxiaHero_Idle_Down_03`
- `XianxiaHero_Walk_Down_00` to `XianxiaHero_Walk_Down_03`
- `XianxiaHero_Idle_Left_00` to `XianxiaHero_Idle_Left_03`
- `XianxiaHero_Walk_Left_00` to `XianxiaHero_Walk_Left_03`
- `XianxiaHero_Idle_Right_00` to `XianxiaHero_Idle_Right_03`
- `XianxiaHero_Walk_Right_00` to `XianxiaHero_Walk_Right_03`
- `XianxiaHero_Idle_Up_00` to `XianxiaHero_Idle_Up_03`
- `XianxiaHero_Walk_Up_00` to `XianxiaHero_Walk_Up_03`

## Suggested Animation Clips

- `XianxiaHero_Idle_Down.anim`: 4 fps, loop on.
- `XianxiaHero_Idle_Left.anim`: 4 fps, loop on.
- `XianxiaHero_Idle_Right.anim`: 4 fps, loop on.
- `XianxiaHero_Idle_Up.anim`: 4 fps, loop on.
- `XianxiaHero_Walk_Down.anim`: 8 fps, loop on.
- `XianxiaHero_Walk_Left.anim`: 8 fps, loop on.
- `XianxiaHero_Walk_Right.anim`: 8 fps, loop on.
- `XianxiaHero_Walk_Up.anim`: 8 fps, loop on.

## Animator Parameter Suggestion

- `MoveX` float.
- `MoveY` float.
- `Speed` float.
- Optional `LastMoveX` float and `LastMoveY` float to preserve facing direction during idle.

Recommended state naming:

- `Idle_Down`, `Idle_Left`, `Idle_Right`, `Idle_Up`
- `Walk_Down`, `Walk_Left`, `Walk_Right`, `Walk_Up`

Do not wire this automatically until the gameplay controller's current movement parameters are confirmed.

