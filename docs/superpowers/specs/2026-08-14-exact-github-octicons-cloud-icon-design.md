# Exact GitHub Octicons Cloud Icon Design

## Goal

Replace every application and release-payload icon with the user-supplied `assets/Github-Octicons-Cloud-16.ico` while preserving that ICO byte-for-byte, including its original directory-entry order and both 256-pixel frames.

## Canonical Asset

`assets/Github-Octicons-Cloud-16.ico` becomes the single canonical icon input. Its approved SHA-256 is:

```text
3BD295FCE4CD7F33A563F3B2D60CADAD53583491F29E52B571016E2B9E2B979E
```

The canonical ICO contains exactly ten 32-bit square entries, in this order:

```text
256, 256, 128, 96, 72, 64, 48, 32, 24, 16
```

The duplicate 256-pixel directory entries are intentional. The first entry embeds a 512×512 PNG despite its ICO directory byte encoding 256; the second embeds a 256×256 PNG. This approved mismatch is part of the supplied binary. No frame may be removed, reordered, resized, recompressed, redrawn, or regenerated.

## Source and Build Flow

`scripts/New-AppIcon.ps1` keeps its existing output-path interface but changes from a vector renderer into a deterministic byte-for-byte copier. Its default input is the canonical asset and its default output remains `src/PublicCloudDownloader.App/Assets/PublicCloudDownloader.ico`.

The WPF project continues to embed `src/PublicCloudDownloader.App/Assets/PublicCloudDownloader.ico`; no C# or XAML change is needed. Existing package and installer paths remain unchanged.

## Validation

`scripts/icon-test.ps1` independently parses the target ICO and verifies:

- the ICO header and ten directory entries;
- exact entry order, including both 256-pixel entries;
- square dimensions, one plane, and 32-bit metadata for every entry;
- safe payload bounds; valid PNG headers with exact embedded dimensions 512×512 and 256×256 for the two 256-pixel directory entries; and valid 32-bit DIB headers for the 128, 96, 72, 64, 48, 32, 24, and 16-pixel entries;
- byte-for-byte equality with the canonical asset;
- the approved canonical SHA-256.

Regression tests cover altered bytes, missing or reordered frames, and malformed directory/payload metadata. The canonical asset and generated application asset must both pass.

## Release Payload

Build and publish the version 1.1.0 self-contained `win-x64` single-file application to:

```text
C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader
```

Replace the executable, `PublicCloudDownloader.ico`, and versioned `README.txt`. Preserve existing `data` and `logs` directories and their contents. The canonical asset, application source asset, and packaged ICO must have identical SHA-256 hashes.

## Verification

Run focused icon tests, the full Release suite using serial MSBuild, executable metadata and self-test checks, and packaged release validation. Launch the exact packaged executable and inspect the real WPF UI to confirm:

- the title bar and taskbar use the supplied cloud icon;
- the footer displays `Version 1.1.0`;
- `Created by Arkie'z K. Khositkhanawut` remains visible;
- the compact layout is unclipped and no startup error dialog appears.

Close only the verification process afterward and report the executable SHA-256.

## Scope Boundaries

- Do not alter the supplied icon binary.
- Do not change product naming, provider behavior, UI layout, colors, text, animation, or branding.
- Do not modify historical 1.0.0 specifications.
- Do not delete, empty, or recreate existing release `data` or `logs` directories.
- Use serial MSBuild (`-m:1 --disable-build-servers`).
