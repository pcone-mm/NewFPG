# UI Prototypes

This folder is for game UI flow design and clickable HTML prototypes before converting screens into Unity UGUI.

## Folder Layout

- `docs/`: UI design notes, screen maps, flow diagrams, and Unity conversion rules.
- `html/`: Clickable browser prototypes.
- `exports/`: Images, screenshots, temporary design exports, and review captures.

Keep these files outside `Assets/` unless they are ready to become Unity assets.

## Recommended Workflow

1. Draft the system design in Markdown.
2. Define screens, popups, widgets, and navigation flow.
3. Build a clickable HTML prototype for fast iteration.
4. Tune layout and visual details with CSS variables and browser DevTools.
5. Confirm interaction flow before rebuilding the UI as Unity UGUI prefabs.
6. In Unity, bind real game logic, data, navigation, animation, and input behavior.

## Prototype Principles

- Use HTML prototypes to validate flow, layout, and interaction speed.
- Keep UI elements separate instead of using one large flat image.
- Prefer semantic names that map cleanly to Unity objects.
- Keep shared visual values in a token/config file when possible.
- Use Flex/Grid for normal menus and lists.
- Use absolute positioning only for HUD-style elements anchored to screen edges.
- Make touch/click targets large enough for real play.
- Check small and large resolutions before treating a layout as approved.

## Naming Rules

Use names that can transfer directly into Unity prefabs and GameObjects.

- Screens: `Screen_MainMenu`, `Screen_Bag`, `Screen_LevelSelect`
- Popups: `Popup_ItemDetail`, `Popup_Confirm`, `Popup_Settings`
- Widgets: `Widget_TopBar`, `Widget_CurrencyBar`, `Widget_ItemCell`
- Buttons: `Btn_Back`, `Btn_Confirm`, `Btn_Start`
- Tabs: `Tab_Equipment`, `Tab_Consumable`
- Lists/grids: `List_Quest`, `Grid_Item`

## HTML To UGUI Mapping

- `Screen_xxx` -> Unity screen prefab.
- `Popup_xxx` -> Unity popup prefab.
- `Widget_xxx` -> reusable child prefab.
- CSS `flex-direction: column` -> Vertical Layout Group.
- CSS `flex-direction: row` -> Horizontal Layout Group.
- CSS grid -> Grid Layout Group.
- CSS absolute positioning -> RectTransform anchors and offsets.
- CSS variables/tokens -> Unity UI style constants or ScriptableObject config.
- HTML click handlers -> Unity Button `onClick` bindings.

## Unity UGUI Notes

- Use `Canvas Scaler` with `Scale With Screen Size` for most full-screen game UI.
- Pick a reference resolution early, such as `1920x1080`.
- Use anchors and layout groups before hand-placing every child.
- Use `CanvasGroup` for popup show/hide, blocking clicks, and fade transitions.
- Rebuild prototype navigation in Unity with a UI manager, for example:
  - `Open(Screen_Bag)`
  - `Back()`
  - `OpenPopup(Popup_ItemDetail, payload)`
- Configure keyboard/controller navigation manually for complex screens.
- Treat imported or copied layouts as first drafts, not final production prefabs.

## Optional Tooling

- Figma: strong for polished UI design and clickable prototypes.
- Excalidraw: fastest for rough UI flow sketches.
- diagrams.net: good for formal flow diagrams.
- Penpot: open-source Figma-like design tool.
- HTML prototype: best for fast local iteration and Unity-friendly mapping.

## Open Questions Per UI System

Before building each system prototype, answer:

- What is the player's main goal on this screen?
- What is the fastest path to the most common action?
- Which actions need confirmation?
- Which elements are dynamic game data?
- What happens on back/cancel?
- Does this screen need mouse, touch, keyboard, or controller support?
- Which parts should become reusable Unity widgets?
