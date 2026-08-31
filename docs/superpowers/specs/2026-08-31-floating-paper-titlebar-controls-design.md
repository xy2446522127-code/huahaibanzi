# Floating Paper Title Bar Controls Design

## Outcome

Todo and note papers use the same title-bar interaction language as the approved main clipboard panel. The fixed six-dot drag button is removed. Users move a paper from any non-interactive title-bar area, while the editable title and action buttons keep their normal behavior.

This design applies to both todo and note papers so the two PaperTodo-derived surfaces do not teach different movement rules.

## Movement Contract

- A primary-button pointer press on a non-interactive part of the paper title bar starts movement immediately.
- The title input, pin button, capsule button, and close button never start movement.
- Movement remains constrained to the visible desktop bounds.
- Text editing, button activation, paper resizing, front-most selection, and desktop outside-click hiding remain unchanged.
- The removed six-dot control leaves no empty placeholder or invisible hit target.

The paper implementation reuses the same interactive-target exclusion rule as the main panel instead of introducing a second gesture model.

## Action Icons

The right side of the title bar contains three stable square icon buttons:

- **Pin:** a recognizable pushpin silhouette. The unpinned state uses an outline; the pinned state uses the existing dimensional red pin material already approved for clipboard records.
- **Collapse to capsule:** a horizontal capsule outline with inward-collapse marks. It must not resemble a wave, link, or minimize command.
- **Close:** the existing `x` glyph remains unchanged.

Every action retains an accessible label and a hover tooltip. Icon state changes cannot resize or shift the title bar.

## Layout And Visual Language

- Removing the drag button changes the title bar to two columns: editable title and fixed-width action group.
- Title text uses the same font weight, glass surface, border treatment, hover feedback, and spacing family as the main panel.
- The draggable title-bar background uses the move cursor only over valid drag areas. Inputs and buttons retain text and pointer cursors.
- Icons are monochrome in the resting state. Only the active pin gains the approved red dimensional material; capsule and close actions do not introduce unrelated colors.

## State And Error Handling

- Pinning changes only paper stacking state and icon appearance.
- Capsule collapse continues to obey the default-on capsule setting. When the setting is disabled, the capsule control remains visibly disabled and explains the reason in its tooltip.
- A collapsed paper is hidden and represented by exactly one capsule. Restoring it removes that capsule.
- Closing a paper removes both the paper and any capsule representation.

## Verification

- No six-dot drag control exists in todo or note papers.
- Dragging title-bar background moves each paper; dragging the title input or an action button does not.
- Paper movement stays inside desktop bounds.
- Pin toggles stacking and its outline/active visual state without layout shift.
- Capsule icon creates one capsule and hides its source paper when enabled.
- Capsule action remains disabled and creates no capsule when the setting is off.
- Close, resize, note editing, image paste, todo insertion sorting, auto-save, clipboard interactions, and main-panel motion remain regression-tested.
- The prototype has no console errors or viewport overflow at 1440 x 900.
