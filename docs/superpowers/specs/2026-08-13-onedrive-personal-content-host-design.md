# OneDrive Personal Content Host Compatibility Fix

**Date:** 2026-08-13  
**Status:** Approved approach; pending written-spec review

## Problem

The application successfully resolves and enumerates a public OneDrive
Personal folder, but all file downloads fail with:

`OneDrive returned an unexpected content host.`

Read-only inspection of the supplied public share confirmed this request
flow:

1. The `1drv.ms` folder link resolves to `onedrive.live.com`.
2. Anonymous metadata enumeration succeeds and returns 34 items.
3. File metadata returns `@content.downloadUrl` on the exact host
   `my.microsoftpersonalcontent.com`.
4. `ValidateContentUri` rejects that host even though the same provider
   already trusts it for OneDrive Personal metadata requests.

The failure is therefore a stale content-host allowlist, not a private link,
an unsupported business account, or a UI problem.

## Scope

Add only the exact HTTPS host `my.microsoftpersonalcontent.com` to the
OneDrive Personal content URL allowlist.

The change must not:

- accept arbitrary HTTPS hosts;
- accept subdomains or lookalike suffixes such as
  `my.microsoftpersonalcontent.com.evil.example`;
- weaken the existing SharePoint rejection;
- alter link parsing, manifest enumeration, retry behavior, file paths, or UI;
- log temporary download URLs, query strings, anonymous tokens, or the supplied
  share token.

## Design

`OneDrivePersonalProvider.ValidateContentUri` will continue to require HTTPS.
Its existing Microsoft-owned suffix allowlist remains unchanged, with one new
case-insensitive exact-host comparison for
`my.microsoftpersonalcontent.com`.

The validator will continue to run both before the initial content request and
after every redirect. A trusted initial URL therefore cannot redirect to an
untrusted destination.

## Test-Driven Implementation

Before production code changes, add a provider regression test whose fixture
returns a temporary content URL on
`https://my.microsoftpersonalcontent.com/...`. Run that test and confirm it
fails with the current `ProviderResponseChangedException`.

Then add the single exact-host condition and confirm:

- the new trusted-host download test passes and returns the expected payload;
- a lookalike host remains rejected;
- the existing untrusted-final-redirect test remains green;
- the full Release test suite passes.

## Verification and Delivery

After automated verification:

1. Run the supplied public OneDrive link through the headless download flow in
   a temporary destination and confirm files are produced without partial-file
   residue.
2. Publish a fresh self-contained Windows x64 single-file executable to
   `dist/PublicCloudDownloader`.
3. Run the packaged self-test and release validation script.
4. Launch the packaged executable and confirm the existing compact monochrome
   UI still opens correctly.

The final report will include test counts, live-smoke outcome, executable path,
and SHA-256 hash without exposing source-link or temporary-download tokens.
