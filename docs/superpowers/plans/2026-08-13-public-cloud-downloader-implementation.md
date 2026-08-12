# Public Cloud Downloader Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Public Cloud Downloader 1.0.0 as a new English-only Windows desktop application that anonymously downloads public Google Drive and OneDrive Personal files and recursive folders to a safe local destination, then produces both a per-user installer EXE and portable ZIP.

**Architecture:** A provider-neutral Core owns link parsing, normalized manifests, safe Windows path planning, collision policies, and coordination. Google Drive and OneDrive Personal adapters each own an isolated in-memory HTTP session and translate current anonymous public-share responses into the Core contract. A WPF App consumes a workflow facade, while Infrastructure owns atomic file writes, bounded retry/concurrency, redacted logs, and packaging services.

**Tech Stack:** .NET 8 (`net8.0` and `net8.0-windows`), C# 12, WPF, `HttpClient`, `System.Text.Json`, AngleSharp 1.7.1, xUnit 2.9.3, Microsoft.NET.Test.Sdk 18.8.1, Inno Setup 6.7.1, PowerShell 7/Windows PowerShell-compatible release scripts.

## Global Constraints

- Product name is exactly `Public Cloud Downloader`; executable is `PublicCloudDownloader.exe`; initial version is `1.0.0`.
- Support Windows 10 and Windows 11 x64 with a self-contained .NET 8 WPF publish.
- Support only public Google Drive and OneDrive Personal file/folder links; OneDrive for Business and SharePoint are rejected explicitly.
- The only transfer direction is `Public cloud file or folder link -> Local Windows folder`.
- No rclone binary/code, OAuth, account storage, API credentials, upload, sync, cloud destination, local source, service, scheduled task, shell extension, or auto-update.
- UI text is English-only and implements approved Layout A Revision 2: full-width public-link input, local destination, compact right-aligned `Download` button.
- `Download` remains disabled until the source URL format is complete and the selected destination is an existing writable directory.
- A folder downloads to `Destination\Source folder name\...`; a single file downloads directly into `Destination`.
- Existing files require one modal choice: `Cancel`, `Skip existing`, or `Overwrite existing`.
- Files are written to job-owned `.partial` siblings and promoted only after completion; overwrite does not remove the old file until the new file is complete.
- Anonymous cookies, resource keys, Badger tokens, redeem values, auth keys, and temporary download URLs remain in memory and are redacted from logs.
- Release filenames derive from the canonical version: `PublicCloudDownloader-v<version>-Setup.exe` and `PublicCloudDownloader-v<version>-win-x64.zip`.
- The installer is per-user (`PrivilegesRequired=lowest`), defaults to `%LocalAppData%\Programs\PublicCloudDownloader`, and the installed application remains runnable after its full folder is copied elsewhere.

## File Structure

```text
PublicCloudDownloader.sln
Version.props
Directory.Build.props
README.md
src/
  PublicCloudDownloader.Core/
    Links/CloudLinkParser.cs
    Models/CloudModels.cs
    Providers/IPublicCloudProvider.cs
    Planning/WindowsPathPlanner.cs
    Planning/CollisionAnalyzer.cs
    Downloads/DownloadCoordinator.cs
    Downloads/DownloadModels.cs
    Workflow/DownloadWorkflow.cs
  PublicCloudDownloader.Providers.GoogleDrive/
    GoogleDriveProvider.cs
    GoogleDriveHtmlParser.cs
    GoogleDriveDownloadResolver.cs
    GoogleDriveOptions.cs
  PublicCloudDownloader.Providers.OneDrivePersonal/
    OneDrivePersonalProvider.cs
    OneDriveShareResolver.cs
    OneDriveJsonModels.cs
    OneDrivePersonalOptions.cs
  PublicCloudDownloader.Infrastructure/
    Files/SafeFileWriter.cs
    Logging/JobLog.cs
    Runtime/AppPaths.cs
    Runtime/ProviderFactory.cs
  PublicCloudDownloader.App/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    ViewModels/MainWindowViewModel.cs
    ViewModels/DownloadMonitorViewModel.cs
    Views/ExistingFilesDialog.xaml
    Views/DownloadMonitorWindow.xaml
    Services/DialogService.cs
    Assets/PublicCloudDownloader.svg
    Assets/PublicCloudDownloader.ico
tests/
  PublicCloudDownloader.Tests/
    Links/CloudLinkParserTests.cs
    Planning/WindowsPathPlannerTests.cs
    Planning/CollisionAnalyzerTests.cs
    Providers/GoogleDriveProviderTests.cs
    Providers/OneDrivePersonalProviderTests.cs
    Downloads/DownloadCoordinatorTests.cs
    Workflow/DownloadWorkflowTests.cs
    ViewModels/MainWindowViewModelTests.cs
    Support/StubHttpMessageHandler.cs
    Support/LoopbackServer.cs
    Fixtures/GoogleDrive/*.html
    Fixtures/OneDrive/*.json
installer/PublicCloudDownloader.iss
scripts/generate-icon.ps1
scripts/install-build-tools.ps1
scripts/package.ps1
scripts/version-test.ps1
scripts/release-test.ps1
scripts/live-smoke-test.ps1
docs/PublicCloudDownloader-README.txt
```

## External Protocol References

- Google Drive anonymous folder listing reference: `https://github.com/wkentaro/gdown/blob/main/gdown/download_folder.py` (`embeddedfolderview`, MIT-licensed reference implementation).
- Google Drive confirmation/export reference: `https://github.com/wkentaro/gdown/blob/main/gdown/download.py`.
- Microsoft share addressing: `https://learn.microsoft.com/en-us/onedrive/developer/rest-api/api/shares_get`.
- Current OneDrive Personal anonymous-session behavior: `https://github.com/felixrieseberg/onedrive-link/issues/1` and `https://gist.github.com/NTFSvolume/e0395fe6eeaa9b47a5c8874ec8133987`.
- Inno Setup non-administrative install mode: `https://jrsoftware.org/ishelp/topic_setup_privilegesrequired.htm`.

---

### Task 1: Scaffold the Versioned Solution

**Files:**
- Create: `PublicCloudDownloader.sln`
- Create: `Version.props`
- Create: `Directory.Build.props`
- Create: `src/PublicCloudDownloader.Core/PublicCloudDownloader.Core.csproj`
- Create: `src/PublicCloudDownloader.Providers.GoogleDrive/PublicCloudDownloader.Providers.GoogleDrive.csproj`
- Create: `src/PublicCloudDownloader.Providers.OneDrivePersonal/PublicCloudDownloader.Providers.OneDrivePersonal.csproj`
- Create: `src/PublicCloudDownloader.Infrastructure/PublicCloudDownloader.Infrastructure.csproj`
- Create: `src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj`
- Create: `tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj`
- Create: `tests/PublicCloudDownloader.Tests/Architecture/AssemblyBoundaryTests.cs`

**Interfaces:**
- Consumes: Approved design spec at `docs/superpowers/specs/2026-08-13-public-cloud-downloader-design.md`.
- Produces: Buildable six-project solution; canonical `Version=1.0.0`; project-reference direction `App -> Infrastructure + providers + Core`, `Infrastructure/providers -> Core`, `Tests -> all projects`.

- [ ] **Step 1: Create solution and project configuration**

Run the following from the repository root:

```powershell
dotnet new sln -n PublicCloudDownloader
dotnet new classlib -n PublicCloudDownloader.Core -o src/PublicCloudDownloader.Core -f net8.0
dotnet new classlib -n PublicCloudDownloader.Providers.GoogleDrive -o src/PublicCloudDownloader.Providers.GoogleDrive -f net8.0
dotnet new classlib -n PublicCloudDownloader.Providers.OneDrivePersonal -o src/PublicCloudDownloader.Providers.OneDrivePersonal -f net8.0
dotnet new classlib -n PublicCloudDownloader.Infrastructure -o src/PublicCloudDownloader.Infrastructure -f net8.0
dotnet new wpf -n PublicCloudDownloader.App -o src/PublicCloudDownloader.App -f net8.0
dotnet new xunit -n PublicCloudDownloader.Tests -o tests/PublicCloudDownloader.Tests -f net8.0
dotnet sln add (Get-ChildItem src,tests -Recurse -Filter *.csproj).FullName
dotnet add src/PublicCloudDownloader.Providers.GoogleDrive reference src/PublicCloudDownloader.Core
dotnet add src/PublicCloudDownloader.Providers.OneDrivePersonal reference src/PublicCloudDownloader.Core
dotnet add src/PublicCloudDownloader.Infrastructure reference src/PublicCloudDownloader.Core src/PublicCloudDownloader.Providers.GoogleDrive src/PublicCloudDownloader.Providers.OneDrivePersonal
dotnet add src/PublicCloudDownloader.App reference src/PublicCloudDownloader.Core src/PublicCloudDownloader.Infrastructure
dotnet add tests/PublicCloudDownloader.Tests reference src/PublicCloudDownloader.Core src/PublicCloudDownloader.Providers.GoogleDrive src/PublicCloudDownloader.Providers.OneDrivePersonal src/PublicCloudDownloader.Infrastructure src/PublicCloudDownloader.App
dotnet add src/PublicCloudDownloader.Providers.GoogleDrive package AngleSharp --version 1.7.1
```

Delete generated `Class1.cs` files. Set the App target framework to `net8.0-windows`, `OutputType=WinExe`, `UseWPF=true`, assembly name `PublicCloudDownloader`, and application icon `Assets\PublicCloudDownloader.ico`.
Set the test project target framework to `net8.0-windows` before adding the App reference; this lets one test assembly exercise both provider-neutral `net8.0` libraries and WPF view models.

- [ ] **Step 2: Add canonical version and common compiler rules**

Create `Version.props`:

```xml
<Project>
  <PropertyGroup>
    <Version>1.0.0</Version>
    <FileVersion>1.0.0.0</FileVersion>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
  </PropertyGroup>
</Project>
```

Create `Directory.Build.props`:

```xml
<Project>
  <Import Project="$(MSBuildThisFileDirectory)Version.props" />
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ContinuousIntegrationBuild>true</ContinuousIntegrationBuild>
  </PropertyGroup>
</Project>
```

Pin test packages in the test project to `Microsoft.NET.Test.Sdk 18.8.1`, `xunit 2.9.3`, and `xunit.runner.visualstudio 3.1.5`; add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for the loopback test server.

- [ ] **Step 3: Write the assembly-boundary test**

```csharp
public sealed class AssemblyBoundaryTests
{
    [Fact]
    public void Core_does_not_reference_WPF_or_provider_assemblies()
    {
        var names = typeof(PublicCloudDownloader.Core.AssemblyMarker)
            .Assembly.GetReferencedAssemblies().Select(x => x.Name).ToArray();

        Assert.DoesNotContain("PresentationFramework", names);
        Assert.DoesNotContain(names, n => n!.StartsWith("PublicCloudDownloader.Providers.", StringComparison.Ordinal));
    }
}
```

Add an empty public `AssemblyMarker` type in Core. Run:

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
```

Expected: PASS with no warnings.

- [ ] **Step 4: Commit the scaffold**

```powershell
git add PublicCloudDownloader.sln Version.props Directory.Build.props src tests
git commit -m "build: scaffold public cloud downloader solution"
```

---

### Task 2: Parse and Classify Supported Public Links

**Files:**
- Create: `src/PublicCloudDownloader.Core/Links/CloudLinkParser.cs`
- Create: `src/PublicCloudDownloader.Core/Models/CloudModels.cs`
- Create: `tests/PublicCloudDownloader.Tests/Links/CloudLinkParserTests.cs`

**Interfaces:**
- Consumes: No earlier runtime interfaces.
- Produces: `ProviderKind`, `SourceHint`, `ParsedCloudLink`, `LinkParseError`, and `CloudLinkParser.TryParse(string?, out ParsedCloudLink?, out LinkParseError?)`.

- [ ] **Step 1: Write failing parser tests**

```csharp
[Theory]
[InlineData("https://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx", ProviderKind.GoogleDrive, SourceHint.Folder)]
[InlineData("https://drive.google.com/file/d/1AbCdEfGhIjKlMnOpQrStUvWx/view", ProviderKind.GoogleDrive, SourceHint.File)]
[InlineData("https://docs.google.com/document/d/1AbCdEfGhIjKlMnOpQrStUvWx/edit", ProviderKind.GoogleDrive, SourceHint.File)]
[InlineData("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken", ProviderKind.OneDrivePersonal, SourceHint.Folder)]
[InlineData("https://onedrive.live.com/?cid=ABC123&id=ABC123!456", ProviderKind.OneDrivePersonal, SourceHint.Unknown)]
public void TryParse_accepts_complete_supported_links(string value, ProviderKind provider, SourceHint hint)
{
    Assert.True(CloudLinkParser.TryParse(value, out var parsed, out var error));
    Assert.Null(error);
    Assert.Equal(provider, parsed!.Provider);
    Assert.Equal(hint, parsed.Hint);
}

[Theory]
[InlineData("")]
[InlineData("https://drive.google.com/drive/folders/")]
[InlineData("http://drive.google.com/drive/folders/1AbCdEfGhIjKlMnOpQrStUvWx")]
[InlineData("https://tenant.sharepoint.com/:f:/s/Test/Example")]
[InlineData("C:\\Downloads")]
public void TryParse_rejects_incomplete_unsafe_or_unsupported_links(string value)
{
    Assert.False(CloudLinkParser.TryParse(value, out _, out var error));
    Assert.NotNull(error);
}
```

- [ ] **Step 2: Run the parser tests and verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~CloudLinkParserTests
```

Expected: compile failure because `CloudLinkParser` and its models do not exist.

- [ ] **Step 3: Implement exact parsing rules**

```csharp
public enum ProviderKind { GoogleDrive, OneDrivePersonal }
public enum SourceHint { Unknown, File, Folder }
public sealed record ParsedCloudLink(ProviderKind Provider, SourceHint Hint, Uri Uri, string? ResourceId, string? ResourceKey);
public sealed record LinkParseError(string Code, string Message);
```

`CloudLinkParser` must accept only HTTPS and these input hosts:

- `drive.google.com`: `/drive/folders/{id}`, `/file/d/{id}`, `/open?id={id}`, `/uc?id={id}`.
- `docs.google.com`: `/document/d/{id}`, `/spreadsheets/d/{id}`, `/presentation/d/{id}`.
- `1drv.ms`: non-empty supported short paths beginning `/f/`, `/u/`, `/t/`, `/b/`, `/w/`, `/x/`, `/p/`, `/o/`; `/f/` is a folder hint and the others are unknown/file hints until preflight.
- `onedrive.live.com`: query includes `resid`, `id`, or `redeem`; retain the full original URI for anonymous resolution.

Reject `.sharepoint.com`, non-HTTPS schemes, missing resource identifiers, user-info components, and all unrecognized hosts with stable error codes `empty`, `incomplete`, `insecure`, `business-not-supported`, or `unsupported`.

- [ ] **Step 4: Run parser and full tests**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
```

Expected: all tests PASS.

- [ ] **Step 5: Commit link parsing**

```powershell
git add src/PublicCloudDownloader.Core/Links src/PublicCloudDownloader.Core/Models tests/PublicCloudDownloader.Tests/Links
git commit -m "feat: validate supported public cloud links"
```

---

### Task 3: Normalize Manifests and Plan Safe Windows Paths

**Files:**
- Extend: `src/PublicCloudDownloader.Core/Models/CloudModels.cs`
- Create: `src/PublicCloudDownloader.Core/Planning/WindowsPathPlanner.cs`
- Create: `src/PublicCloudDownloader.Core/Planning/CollisionAnalyzer.cs`
- Create: `tests/PublicCloudDownloader.Tests/Planning/WindowsPathPlannerTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Planning/CollisionAnalyzerTests.cs`

**Interfaces:**
- Consumes: `ProviderKind`, `SourceHint`.
- Produces: `ManifestItemKind`, `DownloadVariant`, `ManifestItem`, `PublicManifest`, `PlannedItem`, `DownloadPlan`, `WindowsPathPlanner.CreatePlan(...)`, `ExistingFilePolicy`, `FileCollision`, `CollisionAnalyzer.Find(...)`.

- [ ] **Step 1: Write failing path-safety tests**

```csharp
[Fact]
public void Folder_manifest_is_rooted_below_destination_and_keeps_hierarchy()
{
    var manifest = Manifest("Project Assets",
        File("1", "docs/brief.pdf"), Directory("2", "images"), File("3", "images/logo.png"));

    var plan = WindowsPathPlanner.CreatePlan(manifest, @"C:\Downloads");

    Assert.Equal(@"C:\Downloads\Project Assets", plan.OutputRoot);
    Assert.Contains(plan.Items, x => x.FinalPath == @"C:\Downloads\Project Assets\docs\brief.pdf");
}

[Theory]
[InlineData("../escape.txt")]
[InlineData("C:/Windows/win.ini")]
[InlineData("folder/../../escape.txt")]
public void Planner_never_allows_manifest_paths_to_escape_destination(string relativePath)
{
    var manifest = Manifest("Safe", File("1", relativePath));
    var ex = Assert.Throws<ManifestPathException>(() => WindowsPathPlanner.CreatePlan(manifest, @"C:\Downloads"));
    Assert.Equal("path-escape", ex.Code);
}

[Fact]
public void Sanitized_case_insensitive_collisions_receive_deterministic_suffixes()
{
    var manifest = Manifest("Root", File("1", "A?.txt"), File("2", "a*.txt"));
    var names = WindowsPathPlanner.CreatePlan(manifest, @"C:\Downloads").Items.Select(x => Path.GetFileName(x.FinalPath)).ToArray();
    Assert.Equal(new[] { "A_.txt", "a_ (2).txt" }, names);
}
```

Also test `CON`, `PRN`, trailing dots/spaces, empty names, invalid characters, a single-file source writing directly to the base, and canonical containment using `Path.GetFullPath` plus a trailing separator.

- [ ] **Step 2: Run planning tests and verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter "FullyQualifiedName~Planning"
```

Expected: compile failure for missing planner/models.

- [ ] **Step 3: Implement immutable manifest and plan types**

```csharp
public enum ManifestItemKind { Directory, File, Shortcut }
public enum DownloadVariant { Binary, GoogleDocument, GoogleSpreadsheet, GooglePresentation }
public sealed record ManifestItem(string ProviderItemId, string RelativePath, ManifestItemKind Kind, long? Size, string? ContentType, DownloadVariant Variant = DownloadVariant.Binary, string? DriveId = null);
public sealed record PublicManifest(ProviderKind Provider, SourceHint SourceKind, string RootName, IReadOnlyList<ManifestItem> Items);
public sealed record PlannedItem(ManifestItem Source, string FinalPath, string RelativeOutputPath);
public sealed record DownloadPlan(string DestinationBase, string OutputRoot, IReadOnlyList<PlannedItem> Items);
public enum ExistingFilePolicy { Cancel, Skip, Overwrite }
public sealed record FileCollision(PlannedItem Item, long ExistingLength);
```

Implement segment-by-segment Windows sanitization. Invalid characters become `_`; reserved device names gain `_`; collisions gain ` (2)`, ` (3)` before the extension in stable manifest order. Reject rooted input segments and `.`/`..` traversal before sanitization. Verify every final canonical path begins with `OutputRoot + Path.DirectorySeparatorChar` using `StringComparison.OrdinalIgnoreCase`.

- [ ] **Step 4: Write collision-policy tests**

```csharp
[Fact]
public void Find_returns_only_existing_files_and_never_directories()
{
    using var temp = new TempDirectory();
    File.WriteAllText(Path.Combine(temp.Path, "exists.txt"), "old");
    var plan = PlanAt(temp.Path, "exists.txt", "new.txt");

    var conflicts = CollisionAnalyzer.Find(plan);

    var conflict = Assert.Single(conflicts);
    Assert.Equal("exists.txt", conflict.Item.RelativeOutputPath);
}
```

- [ ] **Step 5: Run all tests and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.Core tests/PublicCloudDownloader.Tests/Planning
git commit -m "feat: plan safe local download paths"
```

---

### Task 4: Define Provider Sessions and Error Taxonomy

**Files:**
- Create: `src/PublicCloudDownloader.Core/Providers/IPublicCloudProvider.cs`
- Create: `src/PublicCloudDownloader.Core/Providers/ProviderExceptions.cs`
- Create: `tests/PublicCloudDownloader.Tests/Support/StubHttpMessageHandler.cs`
- Create: `tests/PublicCloudDownloader.Tests/Support/LoopbackServer.cs`

**Interfaces:**
- Consumes: `ParsedCloudLink`, `PublicManifest`, `ManifestItem`.
- Produces: `IPublicCloudProvider`, `IPublicCloudProviderFactory`, `DownloadLease`, `ProviderErrorKind`, `ProviderException`, deterministic HTTP test infrastructure.

- [ ] **Step 1: Write the contract test**

```csharp
[Fact]
public async Task DownloadLease_disposes_the_owned_stream()
{
    var stream = new TrackingStream();
    await using (var lease = new DownloadLease(stream, 10, "text/plain")) { }
    Assert.True(stream.WasDisposed);
}
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~DownloadLease
```

Expected: compile failure because the provider contract does not exist.

- [ ] **Step 3: Implement the provider contract**

```csharp
public interface IPublicCloudProvider : IAsyncDisposable
{
    ProviderKind Kind { get; }
    Task<PublicManifest> BuildManifestAsync(ParsedCloudLink link, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken);
    Task<DownloadLease> OpenReadAsync(ManifestItem item, CancellationToken cancellationToken);
}

public interface IPublicCloudProviderFactory
{
    IPublicCloudProvider Create(ProviderKind kind);
}

public sealed class DownloadLease(Stream content, long? length, string? contentType) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public long? Length { get; } = length;
    public string? ContentType { get; } = contentType;
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public enum ProviderErrorKind { PrivateOrExpired, DownloadDisabled, UnsupportedVariant, ResponseChanged, Throttled, TransientNetwork, ItemChanged }
public sealed class ProviderException(ProviderErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public ProviderErrorKind Kind { get; } = kind;
    public bool IsTransient => Kind is ProviderErrorKind.Throttled or ProviderErrorKind.TransientNetwork;
}
```

`ManifestProgress` contains discovered file/directory counts and current safe relative path, never a source URL.

- [ ] **Step 4: Add deterministic HTTP test helpers**

`StubHttpMessageHandler` queues assertions and responses, records sanitized request method/host/path, and fails a test if a request is unexpected. `LoopbackServer` uses `WebApplication` on `127.0.0.1` with a dynamically assigned port so redirect/cookie/stream cancellation tests exercise real `HttpClientHandler` behavior.

- [ ] **Step 5: Run and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.Core/Providers tests/PublicCloudDownloader.Tests/Support
git commit -m "feat: define anonymous provider sessions"
```

---

### Task 5: Implement Google Drive Anonymous Enumeration and Downloads

**Files:**
- Create: `src/PublicCloudDownloader.Providers.GoogleDrive/GoogleDriveOptions.cs`
- Create: `src/PublicCloudDownloader.Providers.GoogleDrive/GoogleDriveHtmlParser.cs`
- Create: `src/PublicCloudDownloader.Providers.GoogleDrive/GoogleDriveDownloadResolver.cs`
- Create: `src/PublicCloudDownloader.Providers.GoogleDrive/GoogleDriveProvider.cs`
- Create: `tests/PublicCloudDownloader.Tests/Providers/GoogleDriveProviderTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/GoogleDrive/root-folder.html`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/GoogleDrive/nested-folder.html`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/GoogleDrive/download-confirmation.html`

**Interfaces:**
- Consumes: `IPublicCloudProvider`, `ParsedCloudLink`, normalized manifest models.
- Produces: `GoogleDriveProvider`, which recursively reads `embeddedfolderview`, resolves Google-native export variants, follows large-file confirmation, and returns `DownloadLease` without saved credentials.

- [ ] **Step 1: Add sanitized provider fixtures**

`root-folder.html` contains a title and one binary file, one Google Doc, and one nested folder:

```html
<!doctype html><html><head><title>Project Assets</title></head><body>
<a href="https://drive.google.com/file/d/1BinaryFileIdentifier123456/view">logo.png</a>
<a href="https://docs.google.com/document/d/1GoogleDocIdentifier12345678/edit">brief</a>
<a href="https://drive.google.com/drive/folders/1NestedFolderIdentifier12345">docs</a>
</body></html>
```

`nested-folder.html` includes `guide.pdf`. `download-confirmation.html` includes `form#download-form`, an HTTPS action, and hidden confirmation inputs.

- [ ] **Step 2: Write failing Google provider tests**

```csharp
[Fact]
public async Task BuildManifestAsync_recurses_embedded_folder_view_and_maps_google_docs()
{
    var handler = GoogleFixtureHandler.RootAndNested();
    await using var provider = GoogleProvider(handler);
    var link = Parse("https://drive.google.com/drive/folders/1RootFolderIdentifier1234567");

    var manifest = await provider.BuildManifestAsync(link, null, default);

    Assert.Equal("Project Assets", manifest.RootName);
    Assert.Contains(manifest.Items, x => x.RelativePath == "logo.png" && x.Variant == DownloadVariant.Binary);
    Assert.Contains(manifest.Items, x => x.RelativePath == "brief.docx" && x.Variant == DownloadVariant.GoogleDocument);
    Assert.Contains(manifest.Items, x => x.RelativePath == "docs/guide.pdf");
}

[Fact]
public async Task OpenReadAsync_follows_confirmation_form_in_same_cookie_session()
{
    var handler = GoogleFixtureHandler.ConfirmationThenFile("payload");
    await using var provider = GoogleProvider(handler);
    await using var lease = await provider.OpenReadAsync(FileItem("1BinaryFileIdentifier123456"), default);
    Assert.Equal("payload", await new StreamReader(lease.Content).ReadToEndAsync());
}
```

Also test private folder response, missing title as `ResponseChanged`, repeated folder IDs as shortcut-cycle protection, resource-key propagation, and Docs/Sheets/Slides extensions.
Add single-file cases that resolve a binary filename from `Content-Disposition` and resolve Google Docs, Google Sheets, and Google Slides into `.docx`, `.xlsx`, and `.pptx` manifest items before path planning.

- [ ] **Step 3: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~GoogleDriveProviderTests
```

Expected: compile failure for missing Google provider classes.

- [ ] **Step 4: Implement folder enumeration**

`GoogleDriveOptions` exposes injectable base endpoints for tests and defaults to:

```csharp
public Uri DriveBaseUri { get; init; } = new("https://drive.google.com/");
public Uri DocsBaseUri { get; init; } = new("https://docs.google.com/");
public string UserAgent { get; init; } = "PublicCloudDownloader/1.0 (+Windows)";
```

For each folder ID, GET `embeddedfolderview?id={escapedId}` using `ResponseHeadersRead`. Parse with AngleSharp. Accept only anchors whose absolute hosts are `drive.google.com` or `docs.google.com`; classify `/file/d/`, `/drive/folders/`, `/document/d/`, `/spreadsheets/d/`, and `/presentation/d/`. Recurse with a `HashSet<string>` of visited folder IDs. Append `.docx`, `.xlsx`, or `.pptx` to Google-native names during manifest creation.

Map HTTP 401/403 and a valid access-denied page to `PrivateOrExpired`; a 200 page missing the expected title/anchors maps to `ResponseChanged`, not private.
For a single-file source, preflight only far enough to determine its safe final filename, content type, size when present, and download variant; dispose that response and reopen the content during execution.

- [ ] **Step 5: Implement file/export resolution**

Binary flow starts at `uc?id={id}` and follows only HTTPS Google-owned redirects. If HTML is returned, parse `#download-form`, its HTTPS action, and hidden inputs, or an escaped `downloadUrl`. Google-native URLs are:

```text
document/d/{id}/export?format=docx
spreadsheets/d/{id}/export?format=xlsx
presentation/d/{id}/export?format=pptx
```

Reject a final HTML page as `PrivateOrExpired` when it contains a sign-in/access message and `ResponseChanged` otherwise. Keep the response stream and response lifetime owned by `DownloadLease` by wrapping both in a `ResponseOwnedStream` that disposes the `HttpResponseMessage` after the stream.

- [ ] **Step 6: Run provider and full tests**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
```

Expected: all tests PASS without network access.

- [ ] **Step 7: Commit Google Drive support**

```powershell
git add src/PublicCloudDownloader.Providers.GoogleDrive tests/PublicCloudDownloader.Tests/Providers/GoogleDriveProviderTests.cs tests/PublicCloudDownloader.Tests/Fixtures/GoogleDrive
git commit -m "feat: download public Google Drive content"
```

---

### Task 6: Implement OneDrive Personal Anonymous Enumeration and Downloads

**Files:**
- Create: `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDrivePersonalOptions.cs`
- Create: `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDriveShareResolver.cs`
- Create: `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDriveJsonModels.cs`
- Create: `src/PublicCloudDownloader.Providers.OneDrivePersonal/OneDrivePersonalProvider.cs`
- Create: `tests/PublicCloudDownloader.Tests/Providers/OneDrivePersonalProviderTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/OneDrive/root-driveitem.json`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/OneDrive/children-page-1.json`
- Create: `tests/PublicCloudDownloader.Tests/Fixtures/OneDrive/children-page-2.json`

**Interfaces:**
- Consumes: `IPublicCloudProvider`, `ParsedCloudLink`, normalized manifest models.
- Produces: `OneDrivePersonalProvider` supporting current redeem/Badger shares and legacy `authkey` personal shares, recursive pagination, fresh temporary download URLs, and explicit business/SharePoint rejection.

- [ ] **Step 1: Create sanitized OneDrive fixtures**

Root fixture fields are `id`, `name`, `size`, `folder.childCount`, `parentReference.driveId`, and `webUrl`. Child fixtures use the v2 shape:

```json
{
  "value": [
    { "id": "drive!file1", "name": "photo.jpg", "size": 12, "file": { "mimeType": "image/jpeg" }, "parentReference": { "driveId": "drive" } },
    { "id": "drive!folder2", "name": "docs", "folder": { "childCount": 1 }, "parentReference": { "driveId": "drive" } }
  ],
  "@odata.nextLink": "https://my.microsoftpersonalcontent.com/_api/v2.0/drives/drive/items/root/children?$skiptoken=page2"
}
```

- [ ] **Step 2: Write failing OneDrive tests**

```csharp
[Fact]
public async Task BuildManifestAsync_redeems_badger_share_and_recurses_paginated_children()
{
    var handler = OneDriveFixtureHandler.RedeemRootAndChildren();
    await using var provider = OneDriveProvider(handler);
    var link = Parse("https://1drv.ms/f/c/bdbb59c1db4a58dd/ExampleToken");

    var manifest = await provider.BuildManifestAsync(link, null, default);

    Assert.Equal("Shared Photos", manifest.RootName);
    Assert.Contains(manifest.Items, x => x.RelativePath == "photo.jpg");
    Assert.Contains(manifest.Items, x => x.RelativePath == "docs/readme.txt");
    Assert.All(handler.BadgerRequests, r => Assert.Equal("Badger test-token", r.Authorization));
}

[Fact]
public async Task OpenReadAsync_refreshes_content_download_url_before_streaming()
{
    var handler = OneDriveFixtureHandler.ItemThenTemporaryDownload("new-content");
    await using var provider = OneDriveProvider(handler);
    await using var lease = await provider.OpenReadAsync(FileItem("drive!file1", driveId: "drive"), default);
    Assert.Equal("new-content", await new StreamReader(lease.Content).ReadToEndAsync());
}
```

Also test legacy `cid/resid/authkey`, 1drv redirect resolution, `remoteItem` shortcut traversal, repeated `(driveId,id)` cycle protection, `@odata.nextLink` validation, expired/private access, download-disabled items, and rejection of any input/final redirect to `.sharepoint.com`.
Add a single-file root case that creates one manifest file with no root-folder prefix.

- [ ] **Step 3: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~OneDrivePersonalProviderTests
```

Expected: compile failure for missing OneDrive provider classes.

- [ ] **Step 4: Implement current redeem/Badger resolution**

Resolve `1drv.ms` with GET and redirects enabled. Parse final `onedrive.live.com` query values. When `redeem` exists:

1. POST `https://api-badgerp.svc.ms/v1.0/token` with JSON `{"appId":"5cbed6ac-a083-4e14-b191-b4ba07653de2"}` and header `AppId: 1141147648`.
2. Store the returned token only in the provider instance.
3. GET `https://my.microsoftpersonalcontent.com/_api/v2.0/shares/u!{redeem}/driveitem` with `Authorization: Badger {token}` and `Prefer: autoredeem`.
4. Read root `parentReference.driveId` and `id`, then enumerate `/drives/{driveId}/items/{id}/children` recursively with the same headers.
5. Follow only HTTPS next links on `my.microsoftpersonalcontent.com` or `api.onedrive.com`.

For legacy `authkey`, query `https://api.onedrive.com/v1.0/drives/{cid}/items/{resid}?authkey={escapedAuthKey}` and propagate the auth key to child queries. Never log any of these values.

- [ ] **Step 5: Implement manifest and fresh downloads**

Map `folder` items to directories and recursive enumeration; map `file` items to files. Map `remoteItem` to its remote `(driveId,id)` and traverse only once. Before each download attempt, fetch the item metadata again with the active anonymous authorization and read `@content.downloadUrl`; validate HTTPS and a Microsoft-owned content host, then open with `ResponseHeadersRead`. This refresh prevents an hour-old temporary URL from being reused after retries.

Map 401/403/404 to `PrivateOrExpired`, `429` to `Throttled`, missing `@content.downloadUrl` with download restriction fields to `DownloadDisabled`, and incompatible JSON to `ResponseChanged`.

- [ ] **Step 6: Run and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.Providers.OneDrivePersonal tests/PublicCloudDownloader.Tests/Providers/OneDrivePersonalProviderTests.cs tests/PublicCloudDownloader.Tests/Fixtures/OneDrive
git commit -m "feat: download public OneDrive Personal content"
```

---

### Task 7: Download Safely with Retry, Cancellation, and Atomic Overwrite

**Files:**
- Create: `src/PublicCloudDownloader.Core/Downloads/DownloadModels.cs`
- Create: `src/PublicCloudDownloader.Core/Downloads/DownloadCoordinator.cs`
- Create: `src/PublicCloudDownloader.Core/Downloads/IFileWriter.cs`
- Create: `src/PublicCloudDownloader.Infrastructure/Files/SafeFileWriter.cs`
- Create: `tests/PublicCloudDownloader.Tests/Downloads/DownloadCoordinatorTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Downloads/SafeFileWriterTests.cs`

**Interfaces:**
- Consumes: `IPublicCloudProvider.OpenReadAsync`, `DownloadPlan`, `ExistingFilePolicy`.
- Produces: `IFileWriter`, `SafeFileWriter`, `DownloadCoordinator.RunAsync(...)`, `DownloadProgress`, `DownloadResult`, `DownloadFailure`, and completion states.

- [ ] **Step 1: Write failing safe-write tests**

```csharp
[Fact]
public async Task Overwrite_keeps_old_file_until_new_stream_completes()
{
    using var temp = new TempDirectory();
    var target = Path.Combine(temp.Path, "file.txt");
    await File.WriteAllTextAsync(target, "old");
    var source = new ThrowAfterBytesStream("new-but-broken", 3);

    await Assert.ThrowsAsync<IOException>(() => new SafeFileWriter().WriteAsync(source, target, overwrite: true, null, default));

    Assert.Equal("old", await File.ReadAllTextAsync(target));
    Assert.Empty(Directory.GetFiles(temp.Path, "*.partial.*"));
}

[Fact]
public async Task Successful_write_promotes_partial_and_replaces_old_atomically()
{
    using var temp = new TempDirectory();
    var target = Path.Combine(temp.Path, "file.txt");
    await File.WriteAllTextAsync(target, "old");
    await new SafeFileWriter().WriteAsync(new MemoryStream("new"u8.ToArray()), target, true, null, default);
    Assert.Equal("new", await File.ReadAllTextAsync(target));
}
```

- [ ] **Step 2: Write failing coordinator tests**

Cover exactly: skip does not open a provider stream; overwrite opens and replaces; transient provider failures retry three total attempts with delays supplied through an injectable `IDelay`; permanent failures continue with unrelated files; cancellation stops scheduling and removes current partials; progress counts known and unknown sizes; no more than three downloads run concurrently.

- [ ] **Step 3: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter "FullyQualifiedName~Downloads"
```

- [ ] **Step 4: Implement safe file writing**

Use a sibling name `.{finalName}.partial.{jobId:N}` opened with `FileMode.CreateNew`, `FileAccess.Write`, `FileShare.None`, and asynchronous/sequential options. Copy in 128 KiB buffers and report bytes. Flush asynchronously, close the stream, then call `File.Move(partial, final, overwrite)` only after completion. Always delete the job-owned partial in `finally` when promotion did not happen. Create parent directories only after the workflow has received conflict confirmation.

- [ ] **Step 5: Implement bounded coordination**

```csharp
public sealed record DownloadProgress(int CompletedFiles, int TotalFiles, long TransferredBytes, long? KnownTotalBytes, string? CurrentRelativePath, int FailedFiles);
public sealed record DownloadFailure(string RelativePath, string Category, string Message);
public enum DownloadCompletion { Completed, CompletedWithErrors, Cancelled, Failed }
public sealed record DownloadResult(DownloadCompletion Completion, int Downloaded, int Skipped, IReadOnlyList<DownloadFailure> Failures);
```

Use `Parallel.ForEachAsync` with `MaxDegreeOfParallelism=3`. Retry only `ProviderException.IsTransient`, `HttpRequestException`, and pre-body timeouts, for three total attempts with delays 500 ms then 1500 ms. Each retry calls `OpenReadAsync` again. Apply skip/overwrite consistently to every conflict. Convert per-file errors to `DownloadFailure` without including remote URLs.
After conflict confirmation and before scheduling files, create the planned output root and every directory item, including empty folders. Add a test proving an empty public folder creates its root/empty descendants and a test proving a file occupying the planned root path fails before any download stream opens.

- [ ] **Step 6: Run and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.Core/Downloads src/PublicCloudDownloader.Infrastructure/Files tests/PublicCloudDownloader.Tests/Downloads
git commit -m "feat: download files safely with retry and cancel"
```

---

### Task 8: Prepare Jobs and Write Redacted Logs

**Files:**
- Create: `src/PublicCloudDownloader.Core/Workflow/DownloadWorkflow.cs`
- Create: `src/PublicCloudDownloader.Core/Workflow/PreparedDownload.cs`
- Create: `src/PublicCloudDownloader.Infrastructure/Runtime/ProviderFactory.cs`
- Create: `src/PublicCloudDownloader.Infrastructure/Runtime/AppPaths.cs`
- Create: `src/PublicCloudDownloader.Infrastructure/Logging/JobLog.cs`
- Create: `tests/PublicCloudDownloader.Tests/Workflow/DownloadWorkflowTests.cs`
- Create: `tests/PublicCloudDownloader.Tests/Logging/JobLogTests.cs`

**Interfaces:**
- Consumes: parser, provider factory, manifest planner, collision analyzer, coordinator.
- Produces: `IDownloadWorkflow.PrepareAsync(...)`, `ExecuteAsync(...)`, `PreparedDownload : IAsyncDisposable`, runtime provider composition, and redacted job logs.

- [ ] **Step 1: Write failing workflow tests**

```csharp
[Fact]
public async Task PrepareAsync_builds_manifest_plan_and_conflicts_without_creating_output()
{
    using var destination = new TempDirectory();
    var provider = new FakeProvider(Manifest("Shared", File("1", "a.txt")));
    var workflow = Workflow(provider);

    await using var prepared = await workflow.PrepareAsync(GoogleFolderUrl, destination.Path, null, default);

    Assert.Equal(Path.Combine(destination.Path, "Shared"), prepared.Plan.OutputRoot);
    Assert.False(Directory.Exists(prepared.Plan.OutputRoot));
}

[Fact]
public async Task PrepareAsync_disposes_provider_when_manifest_fails()
{
    var provider = new FailingFakeProvider(new ProviderException(ProviderErrorKind.PrivateOrExpired, "private"));
    await Assert.ThrowsAsync<ProviderException>(() => Workflow(provider).PrepareAsync(GoogleFolderUrl, ExistingTempPath, null, default));
    Assert.True(provider.WasDisposed);
}
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter "FullyQualifiedName~Workflow|FullyQualifiedName~JobLog"
```

- [ ] **Step 3: Implement prepared-job ownership**

```csharp
public interface IDownloadWorkflow
{
    Task<PreparedDownload> PrepareAsync(string sourceLink, string destinationBase, IProgress<ManifestProgress>? progress, CancellationToken cancellationToken);
    Task<DownloadResult> ExecuteAsync(PreparedDownload prepared, ExistingFilePolicy policy, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken);
}
```

`PreparedDownload` exposes `ParsedLink.Provider`, `Manifest`, `Plan`, and `Conflicts`; it privately owns and disposes the provider session. `PrepareAsync` validates the destination with an actual create/delete probe file, creates no output root, builds the manifest, plans paths, and finds conflicts. `ExecuteAsync` rejects `Cancel`, delegates to the coordinator, and writes one log.

- [ ] **Step 4: Implement portable paths and redacted logs**

`AppPaths.Root = AppContext.BaseDirectory`; `Data` and `Logs` are sibling directories created at startup. Log filenames use `yyyyMMdd-HHmmss-{jobId:N}.log`. Content includes product version, provider name, safe local relative paths, counts, retry categories, and completion status. Tests assert that a source URI containing `resourcekey=secret`, OneDrive `redeem=secret`, `authkey=secret`, and a temporary `tempauth=secret` never appears in a log.

- [ ] **Step 5: Run and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.Core/Workflow src/PublicCloudDownloader.Infrastructure/Runtime src/PublicCloudDownloader.Infrastructure/Logging tests/PublicCloudDownloader.Tests/Workflow tests/PublicCloudDownloader.Tests/Logging
git commit -m "feat: prepare anonymous download jobs"
```

---

### Task 9: Build the Approved Main Window and Validation States

**Files:**
- Create/replace: `src/PublicCloudDownloader.App/App.xaml`
- Create/replace: `src/PublicCloudDownloader.App/App.xaml.cs`
- Create/replace: `src/PublicCloudDownloader.App/MainWindow.xaml`
- Create/replace: `src/PublicCloudDownloader.App/MainWindow.xaml.cs`
- Create: `src/PublicCloudDownloader.App/ViewModels/ObservableObject.cs`
- Create: `src/PublicCloudDownloader.App/ViewModels/AsyncCommand.cs`
- Create: `src/PublicCloudDownloader.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/PublicCloudDownloader.App/Services/IDialogService.cs`
- Create: `tests/PublicCloudDownloader.Tests/ViewModels/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `CloudLinkParser`, `IDownloadWorkflow`, prepared job/errors.
- Produces: Approved Layout A Revision 2, bindable validation state, compact right-aligned Download action, and dialog abstraction.

- [ ] **Step 1: Use the UI implementation skill before editing XAML**

Read and apply `frontend-ui-engineering/SKILL.md` and `ui-ux-pro-max/SKILL.md` for WPF accessibility, focus order, scaling, keyboard behavior, and status communication. Preserve the approved visual hierarchy; do not add navigation, accounts, mode selectors, or settings.

- [ ] **Step 2: Write failing view-model tests**

```csharp
[Fact]
public void Download_is_disabled_until_link_and_existing_writable_destination_are_valid()
{
    using var temp = new TempDirectory();
    var vm = ViewModel();
    Assert.False(vm.DownloadCommand.CanExecute(null));
    vm.SourceLink = GoogleFolderUrl;
    Assert.False(vm.DownloadCommand.CanExecute(null));
    vm.Destination = temp.Path;
    Assert.True(vm.DownloadCommand.CanExecute(null));
    Assert.Equal("Google Drive link format recognized", vm.LinkStatus);
}

[Fact]
public async Task Private_link_shows_public_access_warning_and_creates_no_output()
{
    var workflow = new ThrowingWorkflow(new ProviderException(ProviderErrorKind.PrivateOrExpired, "private"));
    var dialogs = new RecordingDialogs();
    var vm = ViewModel(workflow, dialogs, GoogleFolderUrl, ExistingTempPath);
    await vm.DownloadCommand.ExecuteAsync();
    Assert.Equal("This folder is not public", dialogs.LastTitle);
}
```

Also test unsupported SharePoint copy, provider-response-changed copy, `IsBusy` disabling every input, and Paste command reading only text from an injected clipboard service.

- [ ] **Step 3: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~MainWindowViewModelTests
```

- [ ] **Step 4: Implement view-model behavior**

`MainWindowViewModel` properties are `SourceLink`, `Destination`, `LinkStatus`, `ValidationMessage`, `IsBusy`, and `DownloadCommand`. `CanExecute` requires successful `CloudLinkParser.TryParse`, `Directory.Exists`, and an injected `IDestinationProbe.CanWrite`. During prepare, set `IsBusy=true` and `ValidationMessage="Checking public access and reading folder contents..."`. Map provider error kinds to the exact user-facing categories in the spec; never display raw response bodies or URLs.

- [ ] **Step 5: Implement Layout A Revision 2 in XAML**

Use a 900x560 centered window with minimum 720x500, a neutral background, white card, Segoe UI, visible focus states, and WCAG-compliant contrast. The `Download` button must be in a right-aligned horizontal container with content `Download`, padding approximately `22,10`, and `MinWidth=110`; it must not stretch. Bind `IsEnabled` through command state. Provide `AutomationProperties.Name`, `HelpText`, labels, logical tab order, Enter to download only when enabled, and Escape only for modal cancellation. Status uses both text and icon, never color alone.

- [ ] **Step 6: Run tests, build, and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
dotnet build src/PublicCloudDownloader.App --configuration Release
git add src/PublicCloudDownloader.App tests/PublicCloudDownloader.Tests/ViewModels
git commit -m "feat: add focused public download interface"
```

---

### Task 10: Add Conflict Confirmation and Download Monitor

**Files:**
- Create: `src/PublicCloudDownloader.App/Views/ExistingFilesDialog.xaml`
- Create: `src/PublicCloudDownloader.App/Views/ExistingFilesDialog.xaml.cs`
- Create: `src/PublicCloudDownloader.App/Views/DownloadMonitorWindow.xaml`
- Create: `src/PublicCloudDownloader.App/Views/DownloadMonitorWindow.xaml.cs`
- Create: `src/PublicCloudDownloader.App/ViewModels/DownloadMonitorViewModel.cs`
- Create: `src/PublicCloudDownloader.App/Services/DialogService.cs`
- Create: `tests/PublicCloudDownloader.Tests/ViewModels/DownloadMonitorViewModelTests.cs`

**Interfaces:**
- Consumes: `PreparedDownload.Conflicts`, `ExistingFilePolicy`, workflow execution and progress.
- Produces: one conflict decision, responsive monitor, cancel, completion/error summaries, and success destination disclosure.

- [ ] **Step 1: Write failing monitor tests**

```csharp
[Fact]
public async Task Cancel_requests_cancellation_and_reports_cancelled_state()
{
    var workflow = new BlockingWorkflow();
    var vm = Monitor(workflow);
    var run = vm.RunAsync();
    await workflow.Started;
    vm.CancelCommand.Execute(null);
    await run;
    Assert.Equal("Cancelled", vm.Status);
    Assert.True(vm.CanClose);
}

[Fact]
public void Completed_with_errors_lists_failed_relative_paths_without_urls()
{
    var vm = Monitor();
    vm.ApplyResult(new DownloadResult(DownloadCompletion.CompletedWithErrors, 2, 1,
        new[] { new DownloadFailure("docs/bad.pdf", "network", "Request failed") }));
    Assert.Equal("Completed with errors", vm.Status);
    Assert.Contains("docs/bad.pdf", vm.Details);
    Assert.DoesNotContain("http", vm.Details, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Verify RED**

```powershell
dotnet test tests/PublicCloudDownloader.Tests --configuration Release --filter FullyQualifiedName~DownloadMonitorViewModelTests
```

- [ ] **Step 3: Implement the existing-file dialog**

Show heading `{count} files already exist`, a virtualized list of safe relative paths, and exactly three actions: `Cancel`, `Skip existing`, `Overwrite existing`. Default keyboard focus is `Cancel`; closing the window equals Cancel; overwrite is visually primary only after the user explicitly focuses/selects it. Return `ExistingFilePolicy?` where null is Cancel.

- [ ] **Step 4: Implement the monitor**

Show overall file progress, known byte progress when available, current safe relative path, downloaded/skipped/failed counts, elapsed time, a scrolling sanitized activity list, and `Cancel`. Disable window close during active work; a close request first asks for cancellation. On completion show the four exact states from the spec and the output path. Do not auto-open downloaded files.

- [ ] **Step 5: Wire the complete UI flow**

Main window prepares a job; if conflicts exist it asks once; Cancel disposes the prepared job with no output; otherwise it opens the monitor, executes with the chosen policy, then disposes the prepared job. Private links show `This folder is not public`; unsupported business links show `OneDrive for Business and SharePoint links are not supported in this version`; response-format failures state that an application update may be required.

- [ ] **Step 6: Run tests and commit**

```powershell
dotnet test PublicCloudDownloader.sln --configuration Release
git add src/PublicCloudDownloader.App/Views src/PublicCloudDownloader.App/ViewModels/DownloadMonitorViewModel.cs src/PublicCloudDownloader.App/Services tests/PublicCloudDownloader.Tests/ViewModels
git commit -m "feat: confirm conflicts and monitor downloads"
```

---

### Task 11: Add Product Assets, Documentation, and Self-Test

**Files:**
- Create: `src/PublicCloudDownloader.App/Assets/PublicCloudDownloader.svg`
- Create: `src/PublicCloudDownloader.App/Assets/PublicCloudDownloader.ico`
- Create: `scripts/generate-icon.ps1`
- Create: `README.md`
- Create: `docs/PublicCloudDownloader-README.txt`
- Modify: `src/PublicCloudDownloader.App/App.xaml.cs`
- Create: `tests/PublicCloudDownloader.Tests/Runtime/AppSelfTestTests.cs`

**Interfaces:**
- Consumes: Runtime composition and canonical version.
- Produces: cloud-with-down-arrow product icon, English user guide, About text, and `--self-test` process mode for installer/portable verification.

- [ ] **Step 1: Write failing self-test test**

```csharp
[Fact]
public async Task Self_test_initializes_writable_portable_paths_without_opening_UI()
{
    var result = await AppSelfTest.RunAsync(new TestAppPaths(temp.Path), default);
    Assert.Equal(0, result.ExitCode);
    Assert.True(Directory.Exists(Path.Combine(temp.Path, "data")));
    Assert.True(Directory.Exists(Path.Combine(temp.Path, "logs")));
}
```

- [ ] **Step 2: Verify RED and implement self-test**

`--self-test` must initialize composition, create/write/delete a probe in `data` and `logs`, validate TLS support by constructing handlers without making a network request, print only `Public Cloud Downloader <version>: self-test passed`, and exit 0 without constructing `MainWindow`. Failures print a sanitized message and exit nonzero.

- [ ] **Step 3: Create vector icon and ICO generator**

Create an SVG master with a simple blue outlined cloud and centered downward arrow, readable at 16 px and distinct from sync/upload symbols. `generate-icon.ps1` renders 16, 24, 32, 48, 64, 128, and 256 px frames into one ICO and verifies the ICO header/frame count. Keep the SVG as source of truth and commit the generated ICO.

- [ ] **Step 4: Write English documentation**

README and packaged guide must explain supported Google Drive/OneDrive Personal links, `Anyone with the link`, folder output layout, conflict choices, partial/error behavior, portable storage, privacy/redaction, no-account design, OneDrive Business/SharePoint exclusion, uninstall behavior, and provider-page compatibility risk. The packaged guide contains `{{VERSION}}`, replaced during packaging.

- [ ] **Step 5: Run and commit**

```powershell
./scripts/generate-icon.ps1
dotnet test PublicCloudDownloader.sln --configuration Release
dotnet build src/PublicCloudDownloader.App --configuration Release
git add src/PublicCloudDownloader.App/Assets src/PublicCloudDownloader.App/App.xaml.cs scripts/generate-icon.ps1 README.md docs/PublicCloudDownloader-README.txt tests/PublicCloudDownloader.Tests/Runtime
git commit -m "docs: add product identity and portable guide"
```

---

### Task 12: Build Portable ZIP and Per-User Installer EXE

**Files:**
- Create: `installer/PublicCloudDownloader.iss`
- Create: `scripts/install-build-tools.ps1`
- Create: `scripts/package.ps1`
- Create: `scripts/version-test.ps1`
- Create: `scripts/release-test.ps1`
- Create: `scripts/live-smoke-test.ps1`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: canonical version, self-contained App publish, docs/icon, Inno Setup compiler.
- Produces: `PublicCloudDownloader-v1.0.0-Setup.exe`, `PublicCloudDownloader-v1.0.0-win-x64.zip`, automated install/uninstall/portable checks, optional live provider smoke tests.

- [ ] **Step 1: Write the failing version/release checks**

`version-test.ps1` must fail unless:

- `Version.props` contains a strict `major.minor.patch` version.
- App assembly/file/informational versions derive from it.
- README template uses `{{VERSION}}` rather than an independent product version.
- installer and packaging scripts accept the canonical version as an argument/preprocessor define.

`release-test.ps1` must fail unless a release directory has one root `PublicCloudDownloader.exe`, `README.txt`, `data/`, and `logs/`; contains no `rclone*.exe`, `.conf`, credential, token, or account-cache file. Separately scan repository production source names and code for retired feature identifiers (`RcloneConfigService`, `AccountsWindow`, `SyncPreview`, `TransferMode.Sync`, rclone process invocation, OAuth client/secret fields, and upload/cloud-destination commands). Do not naively reject normal words in documentation such as `no account`, or runtime substrings such as `SynchronizationContext`. Run now and confirm failure because packaging does not exist.

- [ ] **Step 2: Create the per-user Inno Setup script**

Core directives:

```ini
[Setup]
AppId={{8D802388-7367-4D1A-A5B7-78EDFA6DD4E9}
AppName=Public Cloud Downloader
AppVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\PublicCloudDownloader
DefaultGroupName=Public Cloud Downloader
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
OutputBaseFilename=PublicCloudDownloader-v{#AppVersion}-Setup
OutputDir={#OutputDir}
UninstallDisplayIcon={app}\PublicCloudDownloader.exe

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Public Cloud Downloader"; Filename: "{app}\PublicCloudDownloader.exe"
Name: "{autodesktop}\Public Cloud Downloader"; Filename: "{app}\PublicCloudDownloader.exe"; Tasks: desktopicon

[UninstallDelete]
Type: filesandordirs; Name: "{app}\data"
Type: filesandordirs; Name: "{app}\logs"
```

Do not delete or reference arbitrary download destinations during uninstall. The installer may remove only files/directories under `{app}` that it installed or the application created there.

- [ ] **Step 3: Add build-tool discovery/bootstrap**

`install-build-tools.ps1` first accepts `ISCC_PATH`, then checks Inno Setup 6.7.1 standard user/system locations. If absent, it invokes:

```powershell
winget install --id JRSoftware.InnoSetup -e -s winget --scope user --silent --accept-package-agreements --accept-source-agreements
```

It re-checks `ISCC.exe` and fails with the official `https://jrsoftware.org/isdl.php` instruction if unavailable. It must not install anything when a valid compiler is already present.

- [ ] **Step 4: Implement packaging from one payload**

`package.ps1` reads `Version.props`, clears only repository `dist/` and `artifacts/`, publishes:

```powershell
dotnet publish src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false -o dist/PublicCloudDownloader
```

It replaces `{{VERSION}}` in the packaged guide, creates empty `data` and `logs`, runs the published EXE with `--self-test`, runs `release-test.ps1`, creates the ZIP including empty directories, then calls ISCC with `/DAppVersion=<version>`, `/DPublishDir=<absolute publish>`, and `/DOutputDir=<absolute artifacts>`. It verifies exact output names and SHA-256 hashes them into `artifacts/SHA256SUMS.txt`.

- [ ] **Step 5: Implement installer and portable relocation tests**

`release-test.ps1 -InstallerPath ... -ZipPath ...` performs:

1. Extract ZIP into a temp path, run `--self-test`.
2. Move the extracted directory to a second temp path and run `--self-test` again.
3. Silent-install with `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CURRENTUSER /DIR=<temp install> /GROUP="Public Cloud Downloader Test" /MERGETASKS="!desktopicon"`.
4. Run installed `PublicCloudDownloader.exe --self-test`.
5. Create a sentinel file in a separate temp download directory.
6. Run the generated uninstaller silently.
7. Assert the install directory is removed and the external sentinel remains.

Always validate resolved temp/install targets before recursive cleanup.

- [ ] **Step 6: Add opt-in live provider smoke test**

`live-smoke-test.ps1` runs only when `PCD_GOOGLE_PUBLIC_URL` and/or `PCD_ONEDRIVE_PUBLIC_URL` are set. It downloads into an isolated temp destination through the Core workflow, asserts at least one completed file and no `.partial` files, prints provider/category-only diagnostics, and removes only the validated temp tree. Missing variables produce `SKIP`, not failure.

- [ ] **Step 7: Run complete verification and create artifacts**

```powershell
./scripts/version-test.ps1
dotnet test PublicCloudDownloader.sln --configuration Release
./scripts/install-build-tools.ps1
./scripts/package.ps1
./scripts/release-test.ps1 `
  -InstallerPath ./artifacts/PublicCloudDownloader-v1.0.0-Setup.exe `
  -ZipPath ./artifacts/PublicCloudDownloader-v1.0.0-win-x64.zip
```

Expected: every command exits 0; installer EXE, portable ZIP, and SHA256SUMS exist.

- [ ] **Step 8: Perform manual visual and installer QA**

Launch the installer normally, confirm no UAC prompt, install for the current user, open the application, inspect Layout A Revision 2 at 100%, 125%, 150%, and 200% scaling, verify keyboard-only navigation and disabled/enabled Download state, then uninstall. Install once more to a writable custom directory, copy the full installed folder, and run the copied executable. Record results in `artifacts/verification.txt` without committing generated artifacts.

- [ ] **Step 9: Commit release engineering**

```powershell
git add installer scripts .gitignore
git commit -m "build: package installer and portable release"
```

---

## Final Verification Gate

Before claiming completion, invoke `superpowers:verification-before-completion` and run fresh commands:

```powershell
git status --short
./scripts/version-test.ps1
dotnet test PublicCloudDownloader.sln --configuration Release
./scripts/package.ps1
./scripts/release-test.ps1 `
  -InstallerPath ./artifacts/PublicCloudDownloader-v1.0.0-Setup.exe `
  -ZipPath ./artifacts/PublicCloudDownloader-v1.0.0-win-x64.zip
```

Then inspect `artifacts/verification.txt`, installer/ZIP sizes, hashes, and exact paths. Report any skipped live provider test explicitly; do not describe anonymous provider compatibility as verified unless the live smoke variables were supplied and passed.
