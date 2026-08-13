# Versioning and Release Requirements

**Status:** Approved  
**Effective date:** 2026-08-14  
**Scope:** Every delivered change to Public Cloud Downloader

## Purpose

Every change delivered to a user must be traceable to a short written requirement,
have a new patch version, and be represented by a verified `dist` payload. This
keeps the source, executable, UI, and release documentation in agreement.

## Requirements

### 1. Requirement record per delivery

Each delivery must have a dated Markdown requirement record under
`docs/requirements/`. The record must state:

- the user problem and intended outcome;
- the scope and explicit non-goals;
- the target product version;
- acceptance criteria that can be checked from the source or packaged UI; and
- the verification evidence required before handoff.

Multiple commits that belong to one delivery use the same requirement record and
the same product version.

### 2. Patch version per delivery

For the approved delivery on 2026-08-14, the product version changes from
`1.1.0` to `1.1.1`.

For every later delivery, increment the patch component exactly once per
user-facing delivery (for example, `1.1.1` to `1.1.2`). A single delivery may
contain multiple commits. Major or minor changes require explicit approval and
must update this requirement record or add a new one.

`Version.props` is the only canonical version source:

- `Version` is `major.minor.patch`;
- `FileVersion` is `Version.0`; and
- `AssemblyVersion` is `Version.0`.

Project files, production source, UI text, package names, and installer metadata
must derive their versions from this source rather than duplicating a literal.

### 3. Release and data safety

Every delivered version must publish a self-contained `win-x64` single-file
application to `dist/PublicCloudDownloader`. Publishing must update the
executable, packaged README, and application icon while preserving all existing
files and contents under the payload's `data` and `logs` directories.

### 4. Verification gate

Before handoff, run and record successful results for:

```powershell
./scripts/version-test.ps1 -ExpectedVersion <version>
dotnet build src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj -c Release
dotnet test PublicCloudDownloader.sln -c Release
./scripts/release-test.ps1 -ReleaseDirectory <dist-path>
```

The published executable must pass `--self-test`, report the expected file
version, and show the expected product version in the UI and packaged README.
The final report must include the executable path and SHA-256, test count, and
the before/after preservation result for `data` and `logs`.

## Current delivery record: 1.1.1

The focus highlight fix keeps the TextBox border thickness constant and changes
only its border color. The acceptance criteria are:

- focusing an input still provides a visible focus highlight;
- the input content and surrounding layout do not move when focus is received;
- the clear buttons continue to clear and focus their respective inputs; and
- the published `1.1.1` payload passes all verification gates above.

## Template for future delivery records

Copy this structure to a new dated file before implementing a later delivery:

```markdown
# <Change title>

**Status:** Proposed / Approved
**Date:** YYYY-MM-DD
**Target version:** x.y.z

## Problem and outcome

## Scope

## Non-goals

## Acceptance criteria

## Verification evidence
```
