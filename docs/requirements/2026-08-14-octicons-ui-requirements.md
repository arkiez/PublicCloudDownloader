# Consistent Octicons across the UI

**Status:** Approved<br>
**Date:** 2026-08-14<br>
**Target version:** 1.1.2

## Problem and outcome

The application currently mixes hand-drawn vector symbols, a text glyph, and
text-only actions. Replace these with a consistent set of GitHub Primer
Octicons so every window shares one recognizable icon language.

## Scope

- Use official Octicons for the main-window cloud mark, Paste, Browse, input
  clear, supported-services information, and Download.
- Add Octicons to Retry, Cancel, and Close in Download Progress.
- Add Octicons to Cancel, Skip existing, and Overwrite existing in Existing
  Files.
- Store vector geometry in one reusable WPF resource dictionary.
- Preserve visible action text and existing automation labels.
- Include the Octicons MIT license and pinned source commit in release payloads.

## Non-goals

- No workflow, provider, validation, filesystem, or window-layout changes.
- No change to the executable/application ICO file.
- No decorative icons in status text or activity-log rows.

## Acceptance criteria

- Every user-facing button and toggle in all three windows uses the approved
  Octicon while retaining its text or accessible name.
- Icons render at 16 device-independent pixels; the header cloud renders at 24.
- Icons inherit control foreground colors in normal, hover, disabled, and
  primary-button states.
- The application builds without XAML errors and all automated tests pass.
- The portable ZIP payload contains `THIRD-PARTY-NOTICES.md`.
- The application reports version 1.1.2.

## Verification evidence

- Run `./scripts/version-test.ps1 -ExpectedVersion 1.1.2`.
- Run `dotnet build PublicCloudDownloader.sln -c Release`.
- Run `dotnet test PublicCloudDownloader.sln -c Release`.
- Inspect all XAML buttons/toggles for Octicon content and accessible text/name.
- Run the packaging and release tests and record artifact hashes.
