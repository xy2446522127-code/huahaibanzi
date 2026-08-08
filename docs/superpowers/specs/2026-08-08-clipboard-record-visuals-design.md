# Clipboard Record Visuals Design

## Approved outcome

- A pinned record uses the approved D ruby material while preserving the original Windows `Segoe MDL2 Assets` `E718` pin silhouette.
- Only the pin glyph is colored; the button surface remains transparent. The favorite star keeps its yellow dimensional glyph-only state.
- A file row shows a readable file name as its title and the real path in its metadata line.
- An image copied from a file shows its original file name and source path. A bitmap copied without a source path keeps the generated `花海截图-*.png` title and source/dimension metadata.
- An image row replaces the generic image glyph with a centered `1:1` preview in the existing `33px × 33px` icon slot. Unavailable previews fall back to the current image glyph.

## Data and compatibility

`ClipboardRecord.PrimaryText` remains the canonical paste payload. File paths therefore stay newline-delimited in `PrimaryText`; display logic must not rewrite them. `ClipboardRecord.SourcePath` is an optional trailing field used only when an image originated from one file. Older encrypted history records deserialize with `SourcePath = null` and keep their current behavior.

`ClipboardRecordDisplay.From(record)` is the single display projection. It derives title, metadata detail and thumbnail availability without becoming a second paste authority. Search includes `SourcePath`; file deduplication continues to use the real paths in `PrimaryText`.

## Thumbnail flow

The native state message sends only `thumbnailAvailable`, never a plaintext cache path. The Web shell observes image rows and requests `requestThumbnail` only when a row becomes visible. Native code decrypts the existing PNG through `IClipboardImageStore` and replies with an in-memory data URL. The Web shell preserves received previews across state refreshes by record ID and shows the generic image glyph on missing, invalid or unavailable data.

## Protected behavior

Text/link display, image and file paste payloads, encrypted storage, privacy filtering, search, pin/favorite/delete, automatic cleanup, click-to-copy, immediate auto-hide, scrolling, scaling and dragging remain unchanged.

## Acceptance

- Unit tests prove file/image display projection, old-record compatibility, search and preview data URLs.
- Contract tests prove `requestThumbnail`, exact E718 vector source, ruby glyph-only styling, `1:1`/`object-fit: cover` thumbnail CSS and fallback markup.
- Focused Core tests, Web interaction smoke, and the x64 desktop build pass.
