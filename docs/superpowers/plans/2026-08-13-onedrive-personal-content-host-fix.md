# OneDrive Personal Content Host Compatibility Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore downloads from public OneDrive Personal shares whose temporary file URLs use the exact host `my.microsoftpersonalcontent.com` while preserving strict redirect validation.

**Architecture:** Keep the existing `OneDrivePersonalProvider` request flow and security boundary. Extend only `ValidateContentUri` with one case-insensitive exact-host allowance, prove it test-first through the provider's real `OpenDownloadAsync` path, then validate the supplied public share through the packaged headless workflow.

**Tech Stack:** .NET 8, C#, `HttpClient`, xUnit, WPF, PowerShell release scripts, self-contained `win-x64` single-file publishing.

## Global Constraints

- Accept only HTTPS content URLs.
- Add only the exact host `my.microsoftpersonalcontent.com`; do not accept its subdomains or lookalike suffixes.
- Preserve all existing trusted Microsoft content suffixes.
- Preserve SharePoint, arbitrary-host, and untrusted-final-redirect rejection.
- Do not change parsing, enumeration, retry, path-planning, logging, or UI behavior.
- Never add the supplied public-link token, anonymous session token, temporary content URL, or URL query string to source, fixtures, test output, logs, commits, or reports.
- Use serial MSBuild (`-m:1 --disable-build-servers`) because parallel MSBuild fails without diagnostics in this host environment.
- Publish the corrected executable to `C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader`.

## File Structure

- Modify `tests/PublicCloudDownloader.Tests/Providers/OneDrivePersonalProviderTests.cs`: exercise the trusted exact host and an untrusted lookalike through `OpenDownloadAsync`.
- Modify `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDrivePersonalProvider.cs`: add the exact-host content allowance.
- No new production files, dependencies, configuration, or UI resources.

---

### Task 1: Allow the Current OneDrive Personal Content Host Safely

**Files:**
- Modify: `tests/PublicCloudDownloader.Tests/Providers/OneDrivePersonalProviderTests.cs`
- Modify: `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDrivePersonalProvider.cs:188-195`

**Interfaces:**
- Consumes: `OneDrivePersonalProvider.OpenDownloadAsync(ManifestItem, CancellationToken)` and the private `ValidateContentUri(Uri)` call it applies before content requests and after redirects.
- Produces: unchanged public provider interface; the behavior change is limited to accepting HTTPS URLs whose `IdnHost` equals `my.microsoftpersonalcontent.com`, case-insensitively.

- [ ] **Step 1: Write the failing trusted-host regression test and fixture switch**

Add this test next to `Download_refreshes_temporary_url_before_streaming`:

```csharp
[Fact]
public async Task Download_accepts_the_exact_personal_content_service_host()
{
    using var handler = new OneDriveHandler { UsePersonalContentHost = true };
    await using var provider = new OneDrivePersonalProvider(handler);
    CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
    var manifest = await provider.BuildManifestAsync(link!, null, default);
    var item = manifest.Items.Single(x => x.RelativePath == "photo.jpg");

    await using var lease = await provider.OpenDownloadAsync(item, default);

    Assert.Equal("new-content", await new StreamReader(lease.Content).ReadToEndAsync());
}
```

Add the fixture flag beside the existing redirect flags:

```csharp
public bool UsePersonalContentHost { get; init; }
```

Replace the `/items/file1` and content response fixture branches with:

```csharp
if (uri.Contains("/items/file1", StringComparison.Ordinal))
{
    var contentUrl = UsePersonalContentHost
        ? "https://my.microsoftpersonalcontent.com/content"
        : "https://public.dm.files.1drv.com/content";
    return Json($"{{\"id\":\"file1\",\"@content.downloadUrl\":\"{contentUrl}\"}}", request);
}
if (uri.StartsWith("https://public.dm.files.1drv.com", StringComparison.Ordinal)
    || uri.StartsWith("https://my.microsoftpersonalcontent.com", StringComparison.Ordinal))
{
    if (RedirectContentToEvilHost) request.RequestUri = new Uri("https://evil.example/content");
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("new-content", Encoding.UTF8, "application/octet-stream"),
        RequestMessage = request
    });
}
```

- [ ] **Step 2: Run the targeted test and verify RED**

Run:

```powershell
dotnet test tests\PublicCloudDownloader.Tests\PublicCloudDownloader.Tests.csproj -c Release -m:1 --disable-build-servers --filter "FullyQualifiedName~Download_accepts_the_exact_personal_content_service_host"
```

Expected: one failed test with `ProviderResponseChangedException` and message `OneDrive returned an unexpected content host.` This proves the regression test reaches the current validation defect.

- [ ] **Step 3: Add a failing lookalike-host security test**

Add this test:

```csharp
[Fact]
public async Task Download_rejects_a_personal_content_host_lookalike()
{
    using var handler = new OneDriveHandler { UsePersonalContentLookalikeHost = true };
    await using var provider = new OneDrivePersonalProvider(handler);
    CloudLinkParser.TryParse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", out var link, out _);
    var manifest = await provider.BuildManifestAsync(link!, null, default);
    var item = manifest.Items.Single(x => x.RelativePath == "photo.jpg");

    await Assert.ThrowsAsync<ProviderResponseChangedException>(
        () => provider.OpenDownloadAsync(item, default));
}
```

Add the fixture flag:

```csharp
public bool UsePersonalContentLookalikeHost { get; init; }
```

Choose the fixture content URL in this order so the two cases remain mutually exclusive:

```csharp
var contentUrl = UsePersonalContentLookalikeHost
    ? "https://my.microsoftpersonalcontent.com.evil.example/content"
    : UsePersonalContentHost
        ? "https://my.microsoftpersonalcontent.com/content"
        : "https://public.dm.files.1drv.com/content";
```

- [ ] **Step 4: Run both new tests before production code**

Run:

```powershell
dotnet test tests\PublicCloudDownloader.Tests\PublicCloudDownloader.Tests.csproj -c Release -m:1 --disable-build-servers --filter "FullyQualifiedName~Download_accepts_the_exact_personal_content_service_host|FullyQualifiedName~Download_rejects_a_personal_content_host_lookalike"
```

Expected: trusted-host test FAILS with `ProviderResponseChangedException`; lookalike test PASSES. Do not modify production code until this exact RED/security-baseline result is observed.

- [ ] **Step 5: Add the minimal exact-host allowance**

Change the allowed-content expression inside `ValidateContentUri` to:

```csharp
var allowed = host.Equals("my.microsoftpersonalcontent.com", StringComparison.OrdinalIgnoreCase)
    || host.EndsWith(".1drv.com", StringComparison.OrdinalIgnoreCase)
    || host.EndsWith(".onedrive.com", StringComparison.OrdinalIgnoreCase)
    || host.EndsWith(".livefilestore.com", StringComparison.OrdinalIgnoreCase)
    || host.EndsWith(".microsoftusercontent.com", StringComparison.OrdinalIgnoreCase);
if (uri.Scheme != Uri.UriSchemeHttps
    || host.EndsWith(".sharepoint.com", StringComparison.OrdinalIgnoreCase)
    || !allowed)
    throw new ProviderResponseChangedException("OneDrive returned an unexpected content host.");
```

Do not add suffix matching for `microsoftpersonalcontent.com` and do not change redirect handling.

- [ ] **Step 6: Run the provider tests and verify GREEN**

Run:

```powershell
dotnet test tests\PublicCloudDownloader.Tests\PublicCloudDownloader.Tests.csproj -c Release -m:1 --disable-build-servers --filter "FullyQualifiedName~OneDrivePersonalProviderTests"
```

Expected: all six OneDrive provider tests pass, including the exact-host acceptance, lookalike rejection, and final-redirect rejection cases.

- [ ] **Step 7: Run the full Release suite and source hygiene checks**

Run:

```powershell
dotnet test PublicCloudDownloader.sln -c Release -m:1 --disable-build-servers
git diff --check
rg -n "https://1drv\.ms/f/c/[0-9a-fA-F]+/[A-Za-z0-9_-]+\?" src tests docs scripts
```

Expected: 44 tests pass with zero failures, `git diff --check` prints nothing, and the token scan prints nothing.

- [ ] **Step 8: Commit the focused provider fix**

```powershell
git add -- tests/PublicCloudDownloader.Tests/Providers/OneDrivePersonalProviderTests.cs src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDrivePersonalProvider.cs
git commit -m "fix: accept current OneDrive personal content host"
```

Expected: the commit contains only the provider validator and its regression/security tests.

- [ ] **Step 9: Build a temporary executable and run the supplied live smoke test**

Publish outside the tracked release directory:

```powershell
$smokePublish = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-onedrive-smoke-publish-" + [Guid]::NewGuid().ToString('N'))
$smokeOutput = Join-Path ([System.IO.Path]::GetTempPath()) ("pcd-onedrive-smoke-output-" + [Guid]::NewGuid().ToString('N'))
dotnet publish src\PublicCloudDownloader.App\PublicCloudDownloader.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o $smokePublish -m:1 --disable-build-servers
New-Item -ItemType Directory -Path $smokeOutput | Out-Null
$smokeProcess = Start-Process -FilePath (Join-Path $smokePublish 'PublicCloudDownloader.exe') -ArgumentList @('--headless-download', $env:PCD_APPROVED_ONEDRIVE_URL, $smokeOutput) -Wait -PassThru -WindowStyle Hidden
if ($smokeProcess.ExitCode -ne 0) { throw "OneDrive live smoke failed with exit code $($smokeProcess.ExitCode)." }
$downloaded = @(Get-ChildItem -LiteralPath $smokeOutput -Recurse -File | Where-Object { $_.Name -notmatch '\.partial\.' })
$partials = @(Get-ChildItem -LiteralPath $smokeOutput -Recurse -File | Where-Object { $_.Name -match '\.partial\.' })
if ($downloaded.Count -eq 0 -or $partials.Count -ne 0) { throw "OneDrive live smoke payload validation failed." }
Write-Output "OneDrive live smoke passed: $($downloaded.Count) files, 0 partials."
```

Set `PCD_APPROVED_ONEDRIVE_URL` only in the process environment immediately before this command; never write its value to disk or command output. The cleanup command must first resolve both temporary paths, verify each is under `[System.IO.Path]::GetTempPath()` and its leaf starts with the exact prefix above, then remove only those two paths.

- [ ] **Step 10: Publish the corrected `dist` payload**

Close any running `PublicCloudDownloader.exe` whose process path is under the target `dist` directory. Resolve the target and verify it equals:

```text
C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader
```

Replace only that directory, then run:

```powershell
dotnet publish src\PublicCloudDownloader.App\PublicCloudDownloader.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader -m:1 --disable-build-servers
```

Copy `docs/PublicCloudDownloader-README.txt` to `README.txt`, replacing `{{VERSION}}` with the canonical `Version` read from `Version.props`.

- [ ] **Step 11: Verify the packaged payload**

Run:

```powershell
& C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader\PublicCloudDownloader.exe --self-test
& scripts\release-test.ps1 -ReleaseDirectory C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader
Get-FileHash C:\Users\Acer27arkiez\Documents\PublicCloudDownloader\dist\PublicCloudDownloader\PublicCloudDownloader.exe -Algorithm SHA256
```

Expected: self-test exits 0, release validation prints `Release tests passed.`, and SHA-256 is reported.

- [ ] **Step 12: Launch-check and hand off**

Launch the exact packaged EXE, confirm the returned process path is under the target `dist` directory, and inspect the window using Computer Use. Verify the compact monochrome UI opens, the footer displays `Created by Arkie'z K. Khositkhanawut`, and no error dialog appears at startup. Close the verification instance afterward.

Report the executable link, full test count, live OneDrive smoke file/partial counts, release validation outcome, and SHA-256. Do not include the supplied share URL or any token-bearing URL.
