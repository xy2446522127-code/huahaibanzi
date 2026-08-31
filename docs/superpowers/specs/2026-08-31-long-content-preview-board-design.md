# Long Content Preview Board Design

## Approved Outcome

Huahai Clipboard v1.1.12 adds one reusable, independent preview board for every clipboard history record. A user opens it by right-clicking a history card once, or by pressing a separately configurable preview shortcut while the main panel is visible and the pointer is hovering that card.

The board shows complete text instead of the main list's single-line ellipsis. It is also an editor:

- Text and link records expose their complete payload for editing.
- An edited link that is no longer a valid HTTP or HTTPS URL becomes a text record when saved.
- Image and file records expose an editable display name while keeping the real image, file paths, and paste payload unchanged.
- Saving updates the original record in place. The record ID, original copy time, source application, pin state, favorite state, preview asset, and real paths remain unchanged.

PaperTodo, sticky notes, Markdown notes, scripts, capsules, plugins, and all third-party PaperTodo code are explicitly outside this milestone.

## Classification And Delivery Boundary

- Existing product scale: medium.
- Change scale: medium.
- Risk: high because the feature crosses encrypted history compatibility, global input, a second native window, WebView routing, and installed update behavior.
- Runtime topology: desktop-first, Windows 10/11 x64.
- UI class: derivative of the existing approved production shell.
- Delivery target: a verified v1.1.12 release candidate. Publishing, pushing, and installing remain outside the boundary unless separately authorized.

The v1.1.11 clipboard capture, privacy exclusions, copy-origin guard, click-to-copy behavior, automatic cleanup, update integrity checks, and install-root data contract remain protected.

## Interaction Contract

### Open And Reuse

- A single right click on a record body opens the preview board for that record.
- Right-clicking a pin, favorite, or delete control does not trigger its left-click action and does not open a native context menu.
- Record-level context handling stops propagation. Right-button double-click summon remains available outside the app, but the global detector ignores the app's own panel HWND so two right clicks on a card cannot summon the panel over the preview.
- Only one preview board exists. Opening another record replaces the current content and brings the existing board forward.
- When the current board has unsaved changes, replacement, close, or record switch presents Save, Discard, and Cancel. Cancel preserves both the record and window.

### Preview Shortcut

- Settings gains a separate preview-shortcut recorder beside the existing summon shortcut. It uses the established shortcut parser and rejects shortcuts reserved by the summon action, Windows, or another registered preview action.
- The preview shortcut is leased only while all three conditions hold: the main panel surface is visible, a valid record is hovered, and settings are not open.
- Hover enter sends the record ID to native code and attempts registration. Hover leave, virtual-row removal, panel hide, settings entry, app suspension, or disposal releases the registration immediately.
- A valid shortcut still works when keyboard focus remains in another application because the registration is native and global during the lease.
- With no hovered record, the shortcut is not registered, so it does not consume the keystroke or interfere with another application.
- Registration conflict leaves the current shortcut value visible, shows an actionable warning, and leaves right-click preview available.

### Window Behavior

- The preview is an independent borderless `AppWindow`, not an overlay inside the 430 x 680 main panel.
- Default logical size is 680 x 510. Minimum size is 420 x 360. Maximum size is the active display work area minus a 16-pixel visible margin.
- The title bar drags the window. The lower-right handle resizes both axes without changing the main panel scale setting.
- The app stores preview X, Y, width, height, and topmost state independently from the main panel. A missing display or invalid saved rectangle is clamped into the current display work area.
- The board is topmost by default. A pin icon toggles topmost state and persists the result.
- Mouse leave starts a 500 ms auto-hide delay. Re-entry cancels it. The board does not auto-hide while an editor owns keyboard focus, while a confirmation is open, or while unsaved changes exist. After Save or Discard, it hides if the pointer is still outside.
- Auto-hide only hides the window. Reopening restores the same saved record, position, size, and editor state.
- The main panel follows its existing outside-click auto-hide setting when the independent preview receives focus.

## Visual Contract

The preview board is a new route in the approved `src/HuahaiClipboard.App/Assets/Web/product-shell.html`, loaded by a second WebView2 host. It is not a second visual implementation or a separate HTML shell.

- The board reuses the active theme ID, accent colors, glass opacity, reduced-motion state, typography, border treatment, control density, and specular feedback from the approved main panel.
- The upper-left corner contains only the textual title and record metadata. It has no fox, app, file-type, or decorative icon.
- The title bar's right side contains dynamic icon controls for auto-hide, topmost, hide, and close, each with a tooltip and visible active state.
- Text and links use a full-height, wrapping editor with text selection, scrolling, undo/redo, and stable layout while content changes.
- The footer shows saved or dirty status plus Copy, Discard, and Save. Save is also available through `Ctrl+S`.
- File and image records replace the text editor with a thumbnail, editable display-name field, read-only real path, and an explicit assurance that the disk file is unchanged.
- Every visible control must have browser interaction evidence or a disabled-with-reason state.

## Record And Persistence Model

`ClipboardRecord.PrimaryText` remains the canonical paste payload. The record gains one optional trailing property:

```text
string? DisplayName = null
```

Compatibility rules:

- Existing encrypted records deserialize with `DisplayName = null` and retain their current derived titles.
- Text and link edits update `PrimaryText`; they never use `DisplayName`.
- Image and file renames update only `DisplayName`.
- `ClipboardRecordDisplay` prefers a non-empty `DisplayName` for image and file titles, then falls back to the existing safe file-name derivation.
- File paths stay newline-delimited in `PrimaryText`; image source and protected preview paths stay unchanged.
- Saving calls a dedicated update operation that must find the existing ID and must not create a new record. If cleanup or deletion removed the record, Save fails visibly while leaving the edited text available for manual copying.
- A successful save refreshes the main history projection without changing copy order or `LastCopiedAt`.

Settings adds optional preview shortcut fields with defaults that keep the feature unbound until the user records a shortcut. Old settings files therefore load without migration prompts.

## Thumbnail Contract

Thumbnail work is asynchronous and cannot delay opening the preview board.

- Image records read their protected preview through the existing `IClipboardImageStore` and return an in-memory data URL. Missing, corrupt, or unreadable assets fall back to the image glyph.
- File records request the Windows Shell thumbnail for up to the first three real paths. PDF, Office, video, image, and other registered Shell providers can therefore supply native previews.
- When Shell has no preview, the service returns the registered file-type icon. Missing paths return the current unavailable-file fallback.
- Multi-file records show up to three stacked squares and a `+N` count. The read-only path area lists every canonical path.
- Generated thumbnails are bounded to 320 x 320, cancellation-aware, and cached in a process-memory LRU keyed by canonical path, last-write timestamp, and requested size. Windows Shell remains the persistent cache authority; Huahai does not add a plaintext thumbnail directory.
- Web messages return only image data URLs and display metadata, never `PreviewAssetPath` or another private cache path.

## Components And Data Flow

1. The main shell delegates `contextmenu`, pointer enter, and pointer leave from the virtualized record list.
2. `CursorPanelWindow` validates the record ID against the current history projection.
3. `PreviewWindowCoordinator` creates or reuses one `ContentPreviewWindow`, supplies the record, and owns replacement and dirty-state decisions.
4. `ContentPreviewWindow` loads the preview route from the same virtual host and posts a typed preview state after `previewReady`.
5. The preview shell edits a local draft. Save sends the record ID, expected record kind, and edited value through a dedicated bridge action.
6. A core `ClipboardRecordEditor` validates and applies in-place text, link, or display-name changes through `IClipboardHistorySource`.
7. Successful persistence refreshes both windows. Thumbnail requests run independently and update only the matching record/version.

The coordinator, record editor, thumbnail source, shortcut lease, and window geometry store remain separate units. The main `CursorPanelWindow.xaml.cs` delegates to them instead of absorbing all new behavior.

## Error And Recovery Behavior

- Invalid empty text, link, or display name is not saved; focus stays in the editor with a readable validation message.
- A non-HTTP(S) edited link saves as text after the user-approved automatic conversion.
- A deleted or expired record cannot be recreated accidentally. Save fails, keeps the draft, and leaves Copy enabled.
- Thumbnail timeout, provider failure, unsupported format, missing file, or decryption failure affects only the preview image and uses the type fallback.
- A hotkey registration conflict never disables right-click preview.
- Corrupt window geometry or settings use safe defaults and are repaired on the next successful move, resize, or settings save.
- WebView initialization failure closes only the preview window, reports the failure through the main panel or tray, and leaves clipboard capture running.

## Verification And Acceptance

### Deterministic Core Evidence

- Old `history.dat` and settings fixtures load with null display names and no preview shortcut.
- Text save, valid-link save, invalid-link conversion, image rename, file rename, and deleted-record conflicts follow the persistence contract.
- Every save preserves ID, copy time, source, pin, favorite, preview asset, and canonical file payload as applicable.
- Window geometry clamps across removed displays and DPI/work-area changes.
- Thumbnail resolution covers protected images, Shell preview, type-icon fallback, missing paths, multi-file limits, cancellation, and cache invalidation.
- Preview shortcut conflict, hover lease, panel hide, and disposal release registrations deterministically.

### Browser And Window Evidence

- Right click opens a record while left click still copies and follows the existing auto-hide setting.
- The preview shortcut opens exactly the hovered virtualized record and never acts with no hover target.
- The single preview window replaces clean records and guards dirty replacements.
- Drag, two-axis resize, topmost toggle, auto-hide delay, focus/dirty exemptions, Save, Discard, Cancel, Copy, `Ctrl+S`, and all four record modes produce dynamic evidence.
- A 100,000-character text record remains editable and scrollable without resizing the window or overlapping controls.
- Default and minimum window sizes render without clipping at 100%, 125%, 150%, and 200% Windows scaling.
- No preview action triggers the global right-double-click summon path inside the app.

### Regression And Release Evidence

- Focused Core, Web contract, native window, shortcut, installer, and update-policy suites pass.
- The x64 Release desktop build completes with zero errors and preserves the approved `product-shell.html` source/package digest relationship.
- An isolated installed-data journey proves old history opens, an item can be edited and renamed, real file payloads paste unchanged, and rollback to v1.1.11 still reads the pre-existing fields it understands.
- The v1.1.12 installer and update assets are not accepted unless the tracked publisher, SHA-256, data-preservation, and rollback contracts remain current.
