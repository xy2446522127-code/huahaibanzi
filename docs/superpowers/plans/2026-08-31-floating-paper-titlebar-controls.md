# Floating Paper Title Bar Controls Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make todo and note papers move from their title-bar background and replace ambiguous pin/capsule controls with stable, function-matched icons.

**Architecture:** Keep the work inside the isolated PaperTodo flow prototype. Extend the existing browser QA flow first, then change the shared paper factory and movement binding so both paper types inherit one title-bar contract. Reuse the production shell's existing pin SVG definitions and glass-button language without changing production source or user data.

**Tech Stack:** HTML, CSS, browser JavaScript, Playwright, Node.js

## Global Constraints

- Modify only `.superpowers/brainstorm/papertodo-flow-20260831/`; production source remains unchanged.
- Apply the same title-bar behavior to todo and note papers.
- Preserve editable titles, close, resize, stacking, capsule setting, insertion sorting, image paste, auto-save, and original clipboard interactions.
- Keep the prototype service on `http://localhost:63744/` and use isolated browser `localStorage`.
- Do not publish, package, install, tag, push, or modify real user data.

---

### Task 1: Title-Bar Interaction Regression Evidence

**Files:**
- Modify: `.superpowers/brainstorm/papertodo-flow-20260831/state/qa-flow-v2.cjs`
- Test: `.superpowers/brainstorm/papertodo-flow-20260831/state/qa-flow-v2.cjs`

**Interfaces:**
- Consumes: Playwright locators for `.floating-paper`, `.floating-titlebar`, `.floating-title`, `.paper-pin`, and `.paper-collapse`.
- Produces: deterministic evidence that drag background moves papers while title input and action buttons do not, and that icon states retain fixed geometry.

- [ ] **Step 1: Write the failing title-bar contract assertions**

Add checks immediately after opening the todo paper:

```js
assert(await todoPaper.locator('.paper-drag-grip').count() === 0, 'Legacy paper drag grip still exists');
const titleBar = todoPaper.locator('.floating-titlebar');
const moveBefore = await todoPaper.boundingBox();
const titleBarBox = await titleBar.boundingBox();
await page.mouse.move(titleBarBox.x + 8, titleBarBox.y + titleBarBox.height / 2);
await page.mouse.down();
await page.mouse.move(titleBarBox.x + 108, titleBarBox.y + 65, { steps: 8 });
await page.mouse.up();
const moveAfter = await todoPaper.boundingBox();
assert(Math.abs(moveAfter.x - moveBefore.x) > 50, 'Paper title-bar background did not move the paper');
```

Add negative checks that pointer movement beginning on `.floating-title` and `.paper-pin` leaves the paper position unchanged within one pixel. Assert `.paper-pin` contains `.paper-pin-icon`, `.paper-collapse` contains `.paper-capsule-icon`, and button bounding boxes have the same width before and after pin activation.

- [ ] **Step 2: Run the browser flow and verify RED**

Run:

```powershell
$env:NODE_PATH='F:\Codex Data\worktrees\ba55\无限画布开发\node_modules'
$env:PROTOTYPE_URL='http://localhost:63744/?key=8844801ac0f5b95fd547c6eef7b2b2bb8dd94b6f4c58ca5a6aadee4bc6886923'
$env:PROTOTYPE_SCREENSHOT='F:\Users\DXY\Documents\桌面粘贴悬浮面板\.superpowers\brainstorm\papertodo-flow-20260831\state\flow-qa-titlebar-red.png'
node '.superpowers/brainstorm/papertodo-flow-20260831/state/qa-flow-v2.cjs'
```

Expected: FAIL because `.paper-drag-grip` still exists and the semantic icon classes do not.

- [ ] **Step 3: Commit the failing evidence**

Do not commit the isolated prototype QA script because `.superpowers/` evidence stays outside Git. Retain the failing output in the current execution log.

### Task 2: Shared Paper Title-Bar Implementation

**Files:**
- Modify: `.superpowers/brainstorm/papertodo-flow-20260831/content/full-experience-v2.html`
- Test: `.superpowers/brainstorm/papertodo-flow-20260831/state/qa-flow-v2.cjs`

**Interfaces:**
- Consumes: `createPaper(key, options)`, `makeMoveable(entry)`, `front(entry)`, `collapsePaper(entry)`, and the `#pinOutlinePath` / `#pinSolidPath` definitions already present in the production shell copy.
- Produces: `.floating-titlebar` as the shared movement surface, `.paper-pin-icon` and `.paper-capsule-icon` as stable semantic controls, and unchanged paper factory call sites.

- [ ] **Step 1: Replace the three-column title bar with a two-column layout**

Change `.floating-titlebar` to `grid-template-columns:minmax(0,1fr) auto`, remove `.paper-drag-grip` styling, and add:

```css
.floating-titlebar{cursor:move}
.floating-title,.floating-actions,.floating-action{cursor:auto}
.floating-action{display:grid;place-items:center}
.paper-pin-icon,.paper-capsule-icon{width:18px;height:18px;pointer-events:none}
.paper-pin-icon use{fill:none;stroke:currentColor;stroke-width:3}
.paper-pin.active .paper-pin-icon use{fill:url(#rubyPinFill);stroke:#ffb3c5}
.paper-capsule-icon{position:relative;border:1.8px solid currentColor;border-radius:999px}
.paper-capsule-icon::before,.paper-capsule-icon::after{content:"";position:absolute;top:50%;width:4px;border-top:1.8px solid currentColor}
```

Keep every action button at `31px` square in all states.

- [ ] **Step 2: Replace ambiguous title-bar markup**

Make `createPaper()` emit no drag-grip button and use:

```html
<input class="floating-title" value="..." aria-label="纸片标题">
<div class="floating-actions">
  <button class="floating-action paper-pin" title="置顶" aria-label="置顶">
    <svg class="paper-pin-icon" viewBox="0 -52 64 36" aria-hidden="true"><use href="#pinOutlinePath"></use></svg>
  </button>
  <button class="floating-action paper-collapse" title="折叠为胶囊" aria-label="折叠为胶囊"><i class="paper-capsule-icon" aria-hidden="true"></i></button>
  <button class="floating-action paper-close" title="关闭纸片" aria-label="关闭纸片">x</button>
</div>
```

On pin toggle, update the `<use href>` between `#pinOutlinePath` and `#pinSolidPath`, while preserving `topmost` and `active` state classes.

- [ ] **Step 3: Bind movement to title-bar background**

Replace grip listeners in `makeMoveable(entry)` with title-bar listeners. Ignore pointer starts inside `input,button,[contenteditable="true"]`; otherwise capture the pointer, bring the paper forward, and apply the existing desktop-bound position calculation. Preserve the existing resizer listeners unchanged.

```js
const titlebar=entry.window.querySelector('.floating-titlebar');let drag;
titlebar.addEventListener('pointerdown',event=>{
  if(event.button!==0||event.target.closest('input,button,[contenteditable="true"]'))return;
  const rect=entry.window.getBoundingClientRect();
  drag={id:event.pointerId,x:event.clientX,y:event.clientY,left:rect.left,top:rect.top};
  titlebar.setPointerCapture?.(event.pointerId);
  front(entry);
  event.preventDefault();
});
```

Use the current pointermove/pointerup/pointercancel boundary logic with `titlebar` replacing `grip`.

- [ ] **Step 4: Run the complete browser QA flow**

Run the command from Task 1 with screenshot paths ending in `flow-qa-titlebar-green.png` and `main-qa-titlebar-green.png`.

Expected: PASS with zero console errors, no viewport overflow, title-bar movement evidence, unchanged note movement/resizing/image-paste evidence, insertion ordering, and capsule setting coverage.

- [ ] **Step 5: Inspect visual evidence**

Open both screenshots and confirm the title fits, all three actions remain aligned, the active pin is distinguishable without changing button size, and the capsule icon reads as a horizontal capsule rather than a wave.

- [ ] **Step 6: Verify the live prototype service**

Run:

```powershell
Get-NetTCPConnection -LocalPort 63744 -State Listen
Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 'http://localhost:63744/?key=8844801ac0f5b95fd547c6eef7b2b2bb8dd94b6f4c58ca5a6aadee4bc6886923'
```

Expected: port `63744` is listening and HTTP status is `200`.

- [ ] **Step 7: Keep prototype artifacts out of Git**

Confirm:

```powershell
git status --short -- '.superpowers/brainstorm/papertodo-flow-20260831'
```

Expected: prototype files remain untracked or ignored; no production source is staged or committed.
