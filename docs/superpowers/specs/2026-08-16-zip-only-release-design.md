# ZIP-Only Release Design

**Date:** 2026-08-16

**Status:** Approved

**Target version:** 1.1.2

## Purpose

Simplify Public Cloud Downloader distribution by removing the Windows installer
release path and producing one portable ZIP package. Keep the existing built-in
application self-test and make packaging verify both the published payload and
the contents extracted from the finished ZIP.

## Approved Direction

Remove the Inno Setup integration completely from the active build and release
workflow. The only distributable application package is:

```text
PublicCloudDownloader-v1.1.2-win-x64.zip
```

`SHA256SUMS.txt` and `verification.txt` remain release metadata rather than
additional application packages. The unpacked `dist/PublicCloudDownloader`
directory remains an intermediate publish payload used to create and validate
the ZIP.

The Inno Setup application currently installed in Windows is outside repository
scope and will not be uninstalled or modified.

## Release Contents

The ZIP contains the portable self-contained Windows x64 payload:

- `PublicCloudDownloader.exe`
- `PublicCloudDownloader.ico`
- `README.txt`
- `THIRD-PARTY-NOTICES.md`
- an empty `data` directory
- an empty `logs` directory

The package must not contain an installer, uninstaller, installation script,
credential cache, account cache, token cache, or an `rclone` executable.

## Packaging Flow

`scripts/package.ps1` performs the following ordered workflow:

1. Resolve and validate repository-local `dist` and `artifacts` paths.
2. Clear generated contents only within those validated directories.
3. Read version `1.1.2` from `Version.props`.
4. Publish the application as a self-contained, single-file `win-x64` payload.
5. Add the generated README, product icon, third-party notice, and empty runtime
   directories.
6. Run `PublicCloudDownloader.exe --self-test` from the publish directory and
   fail packaging on a non-zero exit code.
7. Validate the unpacked publish payload with `scripts/release-test.ps1`.
8. Create `PublicCloudDownloader-v1.1.2-win-x64.zip`.
9. Extract the completed ZIP into a uniquely named temporary directory, validate
   its contents, and run its extracted executable with `--self-test`.
10. Remove the validated temporary directory.
11. Confirm no `*-Setup.exe` or other installer artifact was produced.
12. Write the ZIP SHA-256 to `artifacts/SHA256SUMS.txt` and record build,
    payload-validation, and both self-test results in
    `artifacts/verification.txt`.

Packaging must fail immediately if publishing, payload validation, ZIP creation,
ZIP extraction, either self-test, artifact-set validation, or hashing fails.

## Self-Test Contract

The self-test remains part of the main executable and is invoked as:

```powershell
PublicCloudDownloader.exe --self-test
```

No separate self-test executable or user-facing self-test script is added to the
ZIP. `App.xaml.cs` continues to route the argument to `AppSelfTest.Run`, and the
existing runtime tests continue to verify its return code and temporary-file
cleanup behavior.

The packaging workflow verifies the self-test twice:

- once against the publish directory before compression;
- once against the executable extracted from the final ZIP.

This proves that the generated portable package, not only the build output, can
start its diagnostic path successfully.

## Repository Changes

Active release code and documentation will be made ZIP-only:

- Remove `installer/PublicCloudDownloader.iss`.
- Remove `scripts/install-build-tools.ps1`.
- Remove Inno Setup discovery, installation, compilation, installer artifact,
  installer hashing, silent install, and silent uninstall paths from release
  scripts.
- Update `scripts/version-test.ps1` so canonical-version verification no longer
  depends on an installer definition.
- Keep `scripts/release-test.ps1` focused on unpacked and ZIP payload validation.
- Update `README.md` and current release requirements to describe only the
  portable ZIP and .NET 8 SDK build requirement.
- Preserve historical design and implementation documents as records of earlier
  releases; the present specification supersedes their installer requirements
  for version 1.1.2 and later.

## Test Strategy

Implementation follows test-first development for observable release behavior:

1. Add or update a release-script test that fails while packaging still requires
   Inno Setup or produces an installer artifact.
2. Change the smallest release-script surface needed to make the ZIP-only test
   pass.
3. Run the canonical version test for `1.1.2`.
4. Build the complete solution with zero warnings and errors.
5. Run all automated .NET tests.
6. Run the built application self-test.
7. Run full packaging and validate the extracted ZIP.
8. Verify `SHA256SUMS.txt` contains exactly the ZIP filename and its correct
   SHA-256 value.
9. Verify no Setup EXE exists in the generated artifact directory.

## Error Handling and Safety

- Resolve destructive cleanup targets to absolute paths and require them to be
  children of the current repository before removing generated contents.
- Use a unique child of the system temporary directory for ZIP extraction and
  delete only that validated directory.
- Preserve existing user download destinations and runtime data outside generated
  release directories.
- Do not install, uninstall, update, or invoke Inno Setup.
- Do not publish, upload, sign, or externally distribute generated artifacts.

## Acceptance Criteria

- `scripts/package.ps1` completes without locating or invoking Inno Setup.
- The only distributable application artifact is
  `PublicCloudDownloader-v1.1.2-win-x64.zip`.
- No `*-Setup.exe` is created.
- The publish-directory and extracted-ZIP self-tests both exit with code `0`.
- The unpacked and extracted payloads pass release validation.
- The ZIP contains the required portable files and runtime directories, including
  `THIRD-PARTY-NOTICES.md`.
- `SHA256SUMS.txt` contains one correct entry for the ZIP.
- `verification.txt` records build, validation, and self-test outcomes.
- The solution builds with zero warnings and errors and all automated tests pass.
- Active documentation no longer tells contributors to install Inno Setup or
  promises an installer EXE.
- The installed Inno Setup application on the workstation remains unchanged.
