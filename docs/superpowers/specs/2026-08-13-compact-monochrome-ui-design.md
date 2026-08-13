# Compact Monochrome UI Redesign

**Date:** 2026-08-13  
**Status:** Approved  
**Product:** Public Cloud Downloader

## Purpose

Modernize the existing WPF interface without changing the download workflow.
The redesign makes the main window visibly smaller and denser, replaces the
navy-and-blue visual system with a monochrome white, black, and neutral-gray
palette, and credits the creator without introducing a link or an About flow.

## Approved Direction

Use a compact Fluent-inspired, single-column utility layout. It should feel
native on Windows 10 and Windows 11, remain easy to scan, and keep the source
link, destination, and Download action as the only primary workflow.

The main window will open at approximately `720 x 500` device-independent
pixels with a minimum size near `650 x 460`. It remains resizable. The native
Windows title bar is retained for familiar window controls, keyboard behavior,
and accessibility.

## Visual System

- Canvas: soft neutral gray (`#F5F5F5`) to distinguish the window from white
  surfaces.
- Primary surface: white (`#FFFFFF`).
- Primary text and primary action: near-black (`#171717`).
- Hover/pressed states: progressively lighter or darker neutral grays.
- Secondary text: dark gray with at least 4.5:1 contrast on its background.
- Borders and information surfaces: light neutral grays.
- Status meaning must not rely on color. Text and simple vector symbols convey
  recognized links, invalid input, readiness, and informational notices.
- Use Segoe UI, the native Windows typeface already available to the app.
- Use subtle one-pixel borders and restrained shadows. Do not use gradients,
  glass blur, decorative animation, emoji, or mixed icon styles.

Shared WPF resources will provide consistent rounded TextBox and Button
templates, visible keyboard focus, and distinct hover, pressed, and disabled
states. Interactive controls retain a minimum height of 44 pixels.

## Main Window Layout

### Compact header

The header is approximately 72 pixels tall, white, and separated from the
content by a subtle bottom border. A compact monochrome cloud-download vector
icon sits beside:

- `Public Cloud Downloader`
- `Download public Google Drive and OneDrive files - no sign-in required.`

The icon, title, and subtitle form one left-aligned identity group. The header
does not contain navigation or an About button.

### Download form

The content area uses approximately 24 pixels of outer padding and avoids the
large nested card and excess empty space in the current interface.

1. `PUBLIC FILE OR FOLDER LINK` remains a persistent visible label.
2. The link field and Paste button share one 44-pixel row. Paste receives a
   small monochrome clipboard vector icon plus its text label.
3. Existing inline link validation remains directly below the field.
4. `SAVE TO` follows with a tighter section gap.
5. The destination field and Browse button share one 44-pixel row. Browse uses
   a monochrome folder vector icon plus its text label.
6. Existing destination validation remains directly below the field.
7. A compact trust statement (`Public links only`) sits to the left of the
   black Download button. The button uses a download vector icon plus text and
   remains the only filled primary action.
8. A 44-pixel secondary information ToggleButton appears immediately before
   Download. Hovering it reveals a wrapped tooltip; activating it with mouse,
   Enter, or Space toggles a non-interactive popup with the same full supported-
   services copy. Keyboard and screen-reader users receive the full copy and
   the toggle's disclosure state.

Tab order follows the visual order: link, Paste, destination, Browse,
supported-services information, Download. Enter continues to invoke Download
when it is enabled. All existing AutomationProperties names remain present or
are made more descriptive.

### Footer and creator credit

The compact footer has a top divider and one horizontal row:

- Left: `Created by Arkie'z K. Khositkhanawut`
- Right: the existing bound version text

The creator credit is plain text. It is not clickable and has no URL, email,
tooltip, or additional profile information.

## Other Windows

The Download Monitor and Existing Files dialog keep their current structure
and behavior. They inherit the new monochrome application resources so buttons,
inputs, typography, borders, focus indicators, and neutral palette remain
consistent. Their content and workflows are outside this redesign's scope.

## Behavior and Data Flow

No provider, filesystem, workflow, validation, download, conflict, or logging
logic changes. Existing bindings and event handlers remain authoritative:

- SourceLink and DestinationPath continue to update through the view model.
- Paste and Browse retain their current event handlers.
- CanDownload continues to control the primary action.
- LinkStatus, DestinationStatus, and VersionText remain bound as today.
- Download still opens preflight, conflict handling, and the monitor in the
  same sequence.

The redesign is implemented primarily in `App.xaml` and `MainWindow.xaml`.
Code-behind changes are permitted only if needed for presentation or
accessibility and must not alter workflow behavior.

## Error and Edge States

- Invalid and private-link messages retain their existing wording and location.
- Disabled buttons remain readable and visually distinct without appearing
  interactive.
- Long local paths and status text use trimming or wrapping so they cannot push
  actions outside the window.
- At minimum size and common Windows scaling levels, no control overlaps or is
  clipped.
- Focus remains visible in keyboard-only navigation.

## Verification

1. Build the WPF application and run the complete automated test suite.
2. Confirm existing MainViewModel behavior tests still pass.
3. Launch and visually inspect the main window at default size, minimum size,
   and at least 100% and 150% Windows scaling where available.
4. Verify white/black contrast, hover, pressed, focused, and disabled states.
5. Navigate the complete form using only Tab, Shift+Tab, Enter, and Space.
6. Confirm the creator credit exactly matches
   `Arkie'z K. Khositkhanawut` and is not interactive.
7. Confirm long links, long paths, and validation errors remain readable
   without expanding the default window; hover the supported-services toggle
   and verify its full wrapped tooltip, keyboard focus, screen-reader text,
   popup open/close behavior, and toggle state.
8. Open the conflict and monitor windows and verify shared styles remain
   usable and no behavior has changed.

## Acceptance Criteria

- The default main window is approximately `720 x 500`, feels compact, and has
  materially less unused space than the current `840 x 610` layout.
- The interface uses only white, black, and neutral grays for its product
  palette.
- The Download button is the clearest action and all controls have modern,
  consistent rounded styling.
- The footer displays `Created by Arkie'z K. Khositkhanawut` as plain text.
- No existing download, validation, conflict, progress, or version behavior is
  changed.
- The UI remains readable, keyboard accessible, and unclipped at its supported
  minimum size.
