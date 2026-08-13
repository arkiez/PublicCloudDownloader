# Compact Monochrome UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the oversized blue main window with the approved compact 720 x 500 monochrome interface and add the plain-text creator credit.

**Architecture:** Preserve every binding, event handler, and download behavior. Define the white, black, and neutral-gray design tokens plus accessible rounded controls in `App.xaml`, then rebuild only `MainWindow.xaml` as a compact single-column utility. Static XAML contract tests protect the dimensions, palette, workflow hooks, accessibility metadata, and creator copy.

**Tech Stack:** .NET 8, WPF XAML, C# 12, xUnit 2.9.3

## Global Constraints

- Set the default main-window size to exactly `720 x 500` and minimum size to `650 x 470`.
- Retain the native Windows title bar, window resizing, and center-screen startup.
- Use only white, near-black, and neutral grays; do not add blue, gradients, blur, or decorative animation.
- Keep Segoe UI and a minimum interactive-control height of 44 device-independent pixels.
- Keep visible keyboard focus and descriptive AutomationProperties metadata.
- Display `Created by Arkie'z K. Khositkhanawut` as plain text with no link, email, tooltip, or About flow.
- Do not change provider, validation, filesystem, conflict, download, progress, logging, or version behavior.
- Add no package, external font, bitmap, or icon library; use inline WPF vector paths.
- Keep visible application copy in English.

## File Map

- Modify `src/PublicCloudDownloader.App/App.xaml`: monochrome tokens and shared Window, TextBox, Button, ListBox, and ProgressBar styles.
- Modify `src/PublicCloudDownloader.App/MainWindow.xaml`: compact header, form, action row, support strip, and creator/version footer.
- Create `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`: XAML contract tests that need no WPF UI thread.
- Leave `MainWindow.xaml.cs`, `MainViewModel.cs`, provider code, and workflow code unchanged.

---

### Task 1: Establish the monochrome application visual system

**Files:**
- Create: `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`
- Modify: `src/PublicCloudDownloader.App/App.xaml`
- Test: `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`

**Interfaces:**
- Consumes: current resource keys `PrimaryBrush`, `AccentBrush`, `AccentHoverBrush`, `BackgroundBrush`, `SurfaceBrush`, `TextBrush`, `SecondaryTextBrush`, `BorderBrush`, `SuccessBrush`, `ErrorBrush`, `PrimaryButton`, and `SecondaryButton`.
- Produces: neutral resource keys `AccentPressedBrush`, `SubtleSurfaceBrush`, `DisabledSurfaceBrush`, `DisabledTextBrush`, `BorderHoverBrush`, and `FocusBrush`; named template borders `TextBoxChrome` and `ButtonChrome`.

- [ ] **Step 1: Write the failing application-resource contract test**

Create `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`:

```csharp
namespace PublicCloudDownloader.Tests.UI;

public sealed class MainWindowMarkupTests
{
    [Fact]
    public void Application_resources_define_the_monochrome_control_system()
    {
        var markup = File.ReadAllText(ProjectFile("src", "PublicCloudDownloader.App", "App.xaml"));

        Assert.Contains("x:Key=\"PrimaryBrush\" Color=\"#171717\"", markup);
        Assert.Contains("x:Key=\"AccentBrush\" Color=\"#171717\"", markup);
        Assert.Contains("x:Key=\"BackgroundBrush\" Color=\"#F5F5F5\"", markup);
        Assert.Contains("x:Key=\"SurfaceBrush\" Color=\"#FFFFFF\"", markup);
        Assert.Contains("x:Key=\"SecondaryTextBrush\" Color=\"#525252\"", markup);
        Assert.Contains("x:Key=\"BorderBrush\" Color=\"#D4D4D4\"", markup);
        Assert.Contains("x:Name=\"TextBoxChrome\"", markup);
        Assert.Contains("x:Name=\"ButtonChrome\"", markup);
        Assert.Contains("Property=\"IsKeyboardFocused\" Value=\"True\"", markup);
        Assert.False(markup.Contains("#0369A1", StringComparison.OrdinalIgnoreCase));
        Assert.False(markup.Contains("#075985", StringComparison.OrdinalIgnoreCase));
    }

    private static string ProjectFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PublicCloudDownloader.sln")))
            directory = directory.Parent;

        if (directory is null)
            throw new DirectoryNotFoundException("Could not locate the PublicCloudDownloader repository root.");

        return Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
    }
}
```

- [ ] **Step 2: Verify the test fails against the legacy palette**

Run:

```powershell
dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowMarkupTests.Application_resources" --no-restore
```

Expected: FAIL because `App.xaml` still uses blue resources and has no named rounded-control templates.

- [ ] **Step 3: Replace the palette with exact monochrome tokens**

Use this complete token set at the top of `Application.Resources`:

```xml
<SolidColorBrush x:Key="PrimaryBrush" Color="#171717" />
<SolidColorBrush x:Key="AccentBrush" Color="#171717" />
<SolidColorBrush x:Key="AccentHoverBrush" Color="#2B2B2B" />
<SolidColorBrush x:Key="AccentPressedBrush" Color="#000000" />
<SolidColorBrush x:Key="BackgroundBrush" Color="#F5F5F5" />
<SolidColorBrush x:Key="SurfaceBrush" Color="#FFFFFF" />
<SolidColorBrush x:Key="SubtleSurfaceBrush" Color="#FAFAFA" />
<SolidColorBrush x:Key="TextBrush" Color="#171717" />
<SolidColorBrush x:Key="SecondaryTextBrush" Color="#525252" />
<SolidColorBrush x:Key="DisabledSurfaceBrush" Color="#E5E5E5" />
<SolidColorBrush x:Key="DisabledTextBrush" Color="#666666" />
<SolidColorBrush x:Key="BorderBrush" Color="#D4D4D4" />
<SolidColorBrush x:Key="BorderHoverBrush" Color="#A3A3A3" />
<SolidColorBrush x:Key="FocusBrush" Color="#171717" />
<SolidColorBrush x:Key="SuccessBrush" Color="#262626" />
<SolidColorBrush x:Key="ErrorBrush" Color="#171717" />
```

- [ ] **Step 4: Implement the rounded TextBox and Button templates**

Keep the current style keys so every window inherits the redesign. Apply these exact structural rules:

```xml
<Style TargetType="TextBox">
    <Setter Property="MinHeight" Value="44" />
    <Setter Property="Padding" Value="12,9" />
    <Setter Property="Background" Value="{StaticResource SurfaceBrush}" />
    <Setter Property="Foreground" Value="{StaticResource TextBrush}" />
    <Setter Property="BorderBrush" Value="{StaticResource BorderBrush}" />
    <Setter Property="BorderThickness" Value="1" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="TextBox">
                <Border x:Name="TextBoxChrome" CornerRadius="8"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}">
                    <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="TextBoxChrome" Property="BorderBrush" Value="{StaticResource BorderHoverBrush}" />
                    </Trigger>
                    <Trigger Property="IsKeyboardFocused" Value="True">
                        <Setter TargetName="TextBoxChrome" Property="BorderBrush" Value="{StaticResource FocusBrush}" />
                        <Setter TargetName="TextBoxChrome" Property="BorderThickness" Value="2" />
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="TextBoxChrome" Property="Background" Value="{StaticResource DisabledSurfaceBrush}" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>

<Style x:Key="BaseButtonStyle" TargetType="Button">
    <Setter Property="MinHeight" Value="44" />
    <Setter Property="Padding" Value="16,9" />
    <Setter Property="Cursor" Value="Hand" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="HorizontalContentAlignment" Value="Center" />
    <Setter Property="VerticalContentAlignment" Value="Center" />
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="ButtonChrome" CornerRadius="8"
                        Padding="{TemplateBinding Padding}"
                        Background="{TemplateBinding Background}"
                        BorderBrush="{TemplateBinding BorderBrush}"
                        BorderThickness="{TemplateBinding BorderThickness}">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsKeyboardFocused" Value="True">
                        <Setter TargetName="ButtonChrome" Property="BorderBrush" Value="{StaticResource FocusBrush}" />
                        <Setter TargetName="ButtonChrome" Property="BorderThickness" Value="2" />
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

Define the implicit Button style from `BaseButtonStyle`. Define `PrimaryButton` with black/white normal, `#2B2B2B` hover, black pressed, and gray disabled states. Define `SecondaryButton` with white/black normal, `#FAFAFA` hover, and gray pressed/disabled states. Retain the existing Window style with Segoe UI and add layout rounding. Set ListBox to white/black/gray borders and ProgressBar foreground to `PrimaryBrush`.

- [ ] **Step 5: Run the targeted test and compile WPF XAML**

```powershell
dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowMarkupTests.Application_resources" --no-restore
dotnet build src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj -c Release --no-restore
```

Expected: PASS and a warning-free build.

- [ ] **Step 6: Commit the visual system**

```powershell
git add -- src/PublicCloudDownloader.App/App.xaml tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs
git commit -m "style: add monochrome WPF control system"
```

---

### Task 2: Build the compact main window and creator footer

**Files:**
- Modify: `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`
- Modify: `src/PublicCloudDownloader.App/MainWindow.xaml`
- Test: `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`

**Interfaces:**
- Consumes: Task 1 resources; bindings `SourceLink`, `DestinationPath`, `LinkStatus`, `DestinationStatus`, `CanDownload`, and `VersionText`; handlers `Paste_Click`, `Browse_Click`, and `Download_Click`.
- Produces: approved dimensions, compact monochrome layout, vector icons, and the plain creator footer.

- [ ] **Step 1: Add failing compact-layout contract tests**

Add `using System.Xml.Linq;` and these tests to `MainWindowMarkupTests`:

```csharp
[Fact]
public void Main_window_uses_the_approved_compact_dimensions()
{
    var document = XDocument.Load(ProjectFile("src", "PublicCloudDownloader.App", "MainWindow.xaml"));
    var window = Assert.IsType<XElement>(document.Root);

    Assert.Equal("720", (string?)window.Attribute("Width"));
    Assert.Equal("500", (string?)window.Attribute("Height"));
    Assert.Equal("650", (string?)window.Attribute("MinWidth"));
    Assert.Equal("470", (string?)window.Attribute("MinHeight"));
}

[Fact]
public void Main_window_contains_the_plain_creator_credit_and_version()
{
    var markup = File.ReadAllText(ProjectFile("src", "PublicCloudDownloader.App", "MainWindow.xaml"));
    Assert.Contains("Text=\"Created by Arkie'z K. Khositkhanawut\"", markup);
    Assert.Contains("Text=\"{Binding VersionText}\"", markup);
    Assert.DoesNotContain("<Hyperlink", markup);
    Assert.DoesNotContain("NavigateUri", markup);
}

[Fact]
public void Main_window_preserves_workflow_hooks_and_accessibility_names()
{
    var markup = File.ReadAllText(ProjectFile("src", "PublicCloudDownloader.App", "MainWindow.xaml"));
    Assert.Contains("x:Name=\"SourceLinkBox\"", markup);
    Assert.Contains("{Binding SourceLink, UpdateSourceTrigger=PropertyChanged}", markup);
    Assert.Contains("{Binding DestinationPath, UpdateSourceTrigger=PropertyChanged}", markup);
    Assert.Contains("Text=\"{Binding LinkStatus}\"", markup);
    Assert.Contains("Text=\"{Binding DestinationStatus}\"", markup);
    Assert.Contains("IsEnabled=\"{Binding CanDownload}\"", markup);
    Assert.Contains("Click=\"Paste_Click\"", markup);
    Assert.Contains("Click=\"Browse_Click\"", markup);
    Assert.Contains("Click=\"Download_Click\"", markup);
    Assert.Contains("AutomationProperties.Name=\"Public folder or file link\"", markup);
    Assert.Contains("AutomationProperties.Name=\"Local destination folder\"", markup);
    Assert.Contains("AutomationProperties.Name=\"Download\"", markup);
    Assert.False(markup.Contains("#0369A1", StringComparison.OrdinalIgnoreCase));
    Assert.False(markup.Contains("#0F172A", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Verify the tests fail against the current 840 x 610 window**

```powershell
dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowMarkupTests" --no-restore
```

Expected: resource test passes; dimension and creator tests fail.

- [ ] **Step 3: Rewrite `MainWindow.xaml` with the approved compact hierarchy**

Use this root and row contract:

```xml
<Window x:Class="PublicCloudDownloader.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Public Cloud Downloader"
        Width="720" Height="500" MinWidth="650" MinHeight="470"
        WindowStartupLocation="CenterScreen"
        AutomationProperties.Name="Public Cloud Downloader">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="68" />
            <RowDefinition Height="*" />
            <RowDefinition Height="36" />
        </Grid.RowDefinitions>
    </Grid>
</Window>
```

Implement the three rows exactly as follows:

- Header: white surface, one-pixel bottom border, `24` horizontal padding, 40 x 40 black rounded icon tile, inline white cloud/download vector, 20-point title, and 12-point subtitle `Download public Google Drive and OneDrive files - no sign-in required.`
- Form surface: margin `24,10`, padding `20,12`, white background, one-pixel `BorderBrush`, and corner radius `12`. Use ten rows for link label/field/status, 10-pixel gap, destination label/field/status, 12-pixel gap, action row, and support strip.
- Labels: `PUBLIC FILE OR FOLDER LINK` and `SAVE TO`, 12-point semibold secondary text.
- Link row: preserve `SourceLinkBox`, source binding, tooltip, automation name, and `Paste_Click`; use a 44-pixel input plus a 94-pixel secondary button with a 16-pixel clipboard vector and `Paste` text.
- Destination row: preserve destination binding, automation name, and `Browse_Click`; use the same sizing with a folder vector and `Browse` text.
- Status rows: preserve `LinkStatus`, `DestinationStatus`, and polite live regions; use 12-point secondary text, 17-pixel minimum height, and character ellipsis.
- Action row: left copy is `Public links only` plus `Access is checked before download.`; right action is a minimum 142-pixel black `PrimaryButton` with a white vector arrow, `Download` text, `CanDownload`, `Download_Click`, `IsDefault="True"`, and the existing automation name.
- Support strip: margin `0,8,0,0`, padding `10,7`, neutral surface and border, radius `8`, a 16-pixel vector information symbol, wrapping 11.5-point copy `Supports public Google Drive and OneDrive Personal files/folders. Business and SharePoint links aren't supported.`
- Do not add fixed pixel heights to the form rows; the support copy must wrap at minimum width.

- [ ] **Step 4: Add the exact creator/version footer**

Use this row-2 markup:

```xml
<Border Grid.Row="2" Background="{StaticResource SurfaceBrush}"
        BorderBrush="{StaticResource BorderBrush}" BorderThickness="0,1,0,0">
    <Grid Margin="24,0">
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <TextBlock Text="Created by Arkie'z K. Khositkhanawut" VerticalAlignment="Center"
                   FontSize="12" Foreground="{StaticResource SecondaryTextBrush}" />
        <TextBlock Grid.Column="1" Text="{Binding VersionText}" VerticalAlignment="Center"
                   FontSize="12" Foreground="{StaticResource SecondaryTextBrush}" />
    </Grid>
</Border>
```

- [ ] **Step 5: Run the markup tests and compile the application**

```powershell
dotnet test tests/PublicCloudDownloader.Tests/PublicCloudDownloader.Tests.csproj -c Release --filter "FullyQualifiedName~MainWindowMarkupTests" --no-restore
dotnet build src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj -c Release --no-restore
```

Expected: all markup tests pass and WPF XAML builds with zero warnings and errors.

- [ ] **Step 6: Commit the compact main window**

```powershell
git add -- src/PublicCloudDownloader.App/MainWindow.xaml tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs
git commit -m "style: compact the main downloader window"
```

---

### Task 3: Verify behavior, accessibility, and rendered layout

**Files:**
- Verify: `src/PublicCloudDownloader.App/App.xaml`
- Verify: `src/PublicCloudDownloader.App/MainWindow.xaml`
- Verify: `src/PublicCloudDownloader.App/DownloadMonitorWindow.xaml`
- Verify: `src/PublicCloudDownloader.App/ConflictDialog.xaml`
- Test: `tests/PublicCloudDownloader.Tests/UI/MainWindowMarkupTests.cs`

**Interfaces:**
- Consumes: complete UI from Tasks 1 and 2.
- Produces: verified Release build with unchanged workflow and an unclipped compact monochrome interface.

- [ ] **Step 1: Run the complete automated suite**

```powershell
dotnet test PublicCloudDownloader.sln -c Release
```

Expected: all tests pass with zero warnings and errors. Diagnose any failure; do not weaken unrelated assertions.

- [ ] **Step 2: Run the startup self-test**

```powershell
dotnet run --project src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj -c Release --no-build -- --self-test
```

Expected: exit code `0`.

- [ ] **Step 3: Launch and inspect the rendered UI**

```powershell
dotnet run --project src/PublicCloudDownloader.App/PublicCloudDownloader.App.csproj -c Release --no-build
```

Confirm the window opens centered at 720 x 500; no blue remains; every header/form/action/support/footer element is visible; monochrome icons are crisp; disabled Download is readable; and the complete creator credit is visible and non-interactive. Repeat the inspection at 100% and 150% Windows display scaling when both scaling modes are available.

- [ ] **Step 4: Verify keyboard and resize behavior**

Tab order must be link, Paste, destination, Browse, Download. Every focus state must be visible. Enter invokes Download only when enabled. At 650 x 470, controls must not overlap or clip; long status text trims and support copy wraps. Open Existing Files and Download Progress when test data permits and confirm shared monochrome controls remain usable.

- [ ] **Step 5: Review the final diff and status**

```powershell
git diff --check
git status --short
git log -3 --oneline
```

Expected: no whitespace errors, no unrelated modifications, and two implementation commits after the design-spec commit. If visual inspection required a correction, rerun all Task 3 checks and commit it as `fix: polish compact monochrome layout`; otherwise do not create an empty commit.
