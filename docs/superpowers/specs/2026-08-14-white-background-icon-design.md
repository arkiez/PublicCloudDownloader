# White Background for GitHub Octicons Cloud Icon

**Date:** 2026-08-14  
**Status:** Approved direction; awaiting written-spec review  
**Asset:** `assets/Github-Octicons-Cloud-16.ico`

## Purpose

Improve the supplied cloud icon by replacing its transparent background with a
solid white background. The application UI and its header logo are outside this
change.

## Approved Design

- Preserve the existing 32 x 32 icon canvas.
- Preserve the existing black cloud outline, including its position and
  antialiased edge appearance.
- Replace every transparent or partially transparent background pixel with its
  visually equivalent composite over solid white.
- Make the final canvas fully opaque white outside the cloud artwork.
- Keep the original ICO format and the filename
  `Github-Octicons-Cloud-16.ico`.
- Do not edit `MainWindow.xaml`, the application icon, or other assets.

## Implementation Approach

Load the existing ICO frame, composite it over a 32 x 32 white bitmap, and save
the result back as an ICO file. This deterministic local conversion is preferred
over generative image editing because the artwork must remain pixel-for-pixel
visually consistent.

## Verification

1. Confirm the output remains a readable ICO file with a 32 x 32 frame.
2. Confirm every output pixel is fully opaque.
3. Render an enlarged preview on a neutral background and visually confirm the
   black cloud outline is unchanged and the background is white.
4. Confirm Git reports no changes outside the requested icon and this design
   documentation.

## Acceptance Criteria

- `assets/Github-Octicons-Cloud-16.ico` opens successfully as an icon.
- Its background is solid white rather than transparent.
- Its cloud outline remains black and visually unchanged.
- Its dimensions, format, and filename remain unchanged.
