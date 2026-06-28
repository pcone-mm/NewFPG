# UI Prototype Notes

## Current Direction

Use local clickable HTML prototypes as the first working format, then convert approved screens into Unity UGUI prefabs.

This keeps iteration fast while preserving a clear path to the final Unity implementation.

## Key Decisions

- Do not start with Figma unless visual polish becomes the main bottleneck.
- Build early prototypes in `UIPrototypes/html/`.
- Record system-specific screen maps in `UIPrototypes/docs/`.
- Keep prototype files outside `Assets/` until Unity actually needs imported assets.
- Prefer reusable screen/popup/widget naming from the start.

## Important Reminders

- HTML is for validating interaction, hierarchy, and layout direction.
- Unity remains the source of truth for real gameplay data and behavior.
- Prototype click flow should be simple and readable.
- Final Unity UI should use real UGUI components, prefabs, layout groups, and explicit event bindings.
- Avoid relying on a one-click Figma-to-Unity pipeline as a production solution.

## Suggested First Prototype Scope

A good first prototype should include:

- Main menu or hub screen.
- One primary feature screen.
- One reusable top bar or currency bar.
- One reusable list/grid item.
- One confirmation popup.
- Back/cancel behavior.

This is enough to test the workflow without overbuilding.
