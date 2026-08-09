# WindowsBootX Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a free, open-source Windows desktop app that plays a user-chosen animation (video/GIF/image) as a fullscreen overlay immediately after sign-in, covering the gap before the desktop is ready — without touching BIOS, UEFI, Windows Boot Manager, or any system file.

**Architecture:** A single self-contained .NET 10 / WPF executable (`WindowsBootX.exe`) with two launch modes selected by command-line flag: no-flag opens a Configurator GUI (pick content, set options, install/uninstall); `--play` (invoked only by a per-user Windows Scheduled Task) goes straight to a minimal fullscreen overlay renderer. Pure decision logic (settings, boot-session tracking, timing, gating, trigger mapping) lives in a separate `WindowsBootX.Core` class library so it can be unit tested without spinning up WPF or touching the real OS; thin OS-integration wrappers (WMI, Win32 P/Invoke, Task Scheduler COM) are isolated behind small interfaces and verified manually.

**Tech Stack:** C# / .NET 10 (LTS) / WPF, xUnit for tests, `WpfAnimatedGif` (GIF playback), `TaskScheduler` NuGet package by dahall (Task Scheduler COM wrapper), Inno Setup for the installer.

## Global Constraints

- Target OS: Windows 10/11, 64-bit only (`win-x64`).
- No BIOS/UEFI/BCD/Windows Boot Manager/system-file modification, ever, under any task.
- No administrator privileges required for install, uninstall, or logon-trigger registration — everything is per-user (`HKCU` / current-user Scheduled Task).
- Fail-open is non-negotiable: a missing/corrupt asset, a slow shell, or any other failure must never trap the user at the sign-in transition. Any keypress/click always dismisses instantly.
- Skip entirely when Windows is in Safe Mode.
- Distribution: self-contained, single-file publish (zero external .NET runtime dependency), unsigned, MIT licensed.
- Out of scope for v1 (do not build): shift-hold skip gesture, code signing, auto-update, multi-monitor support beyond the primary display, admin/machine-wide install mode.

---

## Task 1: Solution & Project Scaffolding

**Files:**
- Create: `WindowsBootX.sln`
- Create: `src/WindowsBootX.Core/WindowsBootX.Core.csproj`
- Create: `src/WindowsBootX/WindowsBootX.csproj`
- Create: `src/WindowsBootX/App.xaml`, `src/WindowsBootX/App.xaml.cs`
- Create: `tests/WindowsBootX.Core.Tests/WindowsBootX.Core.Tests.csproj`
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `README.md`

**Interfaces:**
- Produces: a buildable, testable three-project solution that every later task adds files into. No app logic yet.

- [ ] **Step 1: Install the .NET 10 SDK (none is currently installed on this machine)**

Run:
```
winget install Microsoft.DotNet.SDK.10
```
If `winget` isn't available, download and run the installer from https://dotnet.microsoft.com/download/dotnet/10.0 (choose the x64 SDK, not just the runtime). Open a **new** terminal afterward so `PATH` picks it up.

Verify:
```
dotnet --version
```
Expected: a version starting with `10.`

- [ ] **Step 2: Initialize git and add a .gitignore**

```
cd D:\Coding\windowsBootX
git init
```

Create `.gitignore`:
```gitignore
bin/
obj/
*.user
.vs/
publish/
```

- [ ] **Step 3: Create the solution and three projects**

```
dotnet new sln -n WindowsBootX
dotnet new classlib -n WindowsBootX.Core -o src/WindowsBootX.Core
dotnet new wpf -n WindowsBootX -o src/WindowsBootX
dotnet new xunit -n WindowsBootX.Core.Tests -o tests/WindowsBootX.Core.Tests
dotnet sln add src/WindowsBootX.Core/WindowsBootX.Core.csproj src/WindowsBootX/WindowsBootX.csproj tests/WindowsBootX.Core.Tests/WindowsBootX.Core.Tests.csproj
dotnet add src/WindowsBootX/WindowsBootX.csproj reference src/WindowsBootX.Core/WindowsBootX.Core.csproj
dotnet add tests/WindowsBootX.Core.Tests/WindowsBootX.Core.Tests.csproj reference src/WindowsBootX.Core/WindowsBootX.Core.csproj
```

- [ ] **Step 4: Pin target frameworks and enable nullable/implicit usings**

Edit `src/WindowsBootX.Core/WindowsBootX.Core.csproj` to:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>WindowsBootX.Core</RootNamespace>
  </PropertyGroup>

</Project>
```

Edit `src/WindowsBootX/WindowsBootX.csproj` to:
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>WindowsBootX</RootNamespace>
    <AssemblyName>WindowsBootX</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WindowsBootX.Core\WindowsBootX.Core.csproj" />
  </ItemGroup>

</Project>
```

Edit `tests/WindowsBootX.Core.Tests/WindowsBootX.Core.Tests.csproj` — change only the `<TargetFramework>` value to `net10.0` (leave the generated package references as-is), and delete the generated sample file:

```
rm tests/WindowsBootX.Core.Tests/UnitTest1.cs
```

- [ ] **Step 5: Remove the WPF template's default window (we build our own two windows later)**

```
rm src/WindowsBootX/MainWindow.xaml src/WindowsBootX/MainWindow.xaml.cs
```

Edit `src/WindowsBootX/App.xaml` to remove the `StartupUri` attribute (App.xaml.cs will decide what to show, added in Task 12):
```xml
<Application x:Class="WindowsBootX.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
    </Application.Resources>
</Application>
```

Edit `src/WindowsBootX/App.xaml.cs` to a minimal placeholder (replaced fully in Task 12):
```csharp
using System.Windows;

namespace WindowsBootX;

public partial class App : Application
{
}
```

- [ ] **Step 6: Verify the solution builds and the (empty) test project runs**

```
dotnet build
dotnet test
```
Expected: build succeeds; test run reports 0 tests, 0 failures.

- [ ] **Step 7: Add LICENSE**

Create `LICENSE`:
```
MIT License

Copyright (c) 2026 WindowsBootX Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 8: Add a README stub (finalized in Task 15)**

Create `README.md`:
```markdown
# WindowsBootX

A free, open-source Windows app that plays a custom animation immediately
after you sign in — like an OEM boot animation, but purely a post-login
overlay. No BIOS/UEFI/Boot Manager/system-file changes, ever.

> Under construction — see `docs/superpowers/plans/2026-08-09-windowsbootx.md`
> for the implementation plan.
```

- [ ] **Step 9: Commit**

```
git add -A
git commit -m "chore: scaffold solution, projects, license, readme"
```

---

## Task 2: Core Domain Models & Launch-Mode Parsing

**Files:**
- Create: `src/WindowsBootX.Core/ContentType.cs`
- Create: `src/WindowsBootX.Core/ContentTypeResolver.cs`
- Create: `src/WindowsBootX.Core/PlaybackMode.cs`
- Create: `src/WindowsBootX.Core/LaunchMode.cs`
- Create: `src/WindowsBootX.Core/LaunchModeResolver.cs`
- Test: `tests/WindowsBootX.Core.Tests/LaunchModeResolverTests.cs`
- Test: `tests/WindowsBootX.Core.Tests/ContentTypeResolverTests.cs`

**Interfaces:**
- Produces: `enum ContentType { Video, Gif, Image }`; `static ContentTypeResolver.FromFilePath(string filePath) -> ContentType` (throws `NotSupportedException` for unrecognized extensions); `enum PlaybackMode { EveryBoot, EverySignIn }`; `enum LaunchMode { Configurator, Player }`; `static LaunchModeResolver.Resolve(string[] args) -> LaunchMode`.

- [ ] **Step 1: Write the failing tests for LaunchModeResolver**

Create `tests/WindowsBootX.Core.Tests/LaunchModeResolverTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class LaunchModeResolverTests
{
    [Fact]
    public void Resolve_NoArgs_ReturnsConfigurator()
    {
        var result = LaunchModeResolver.Resolve(Array.Empty<string>());
        Assert.Equal(LaunchMode.Configurator, result);
    }

    [Fact]
    public void Resolve_PlayFlag_ReturnsPlayer()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--play" });
        Assert.Equal(LaunchMode.Player, result);
    }

    [Fact]
    public void Resolve_PlayFlagCaseInsensitive_ReturnsPlayer()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--PLAY" });
        Assert.Equal(LaunchMode.Player, result);
    }

    [Fact]
    public void Resolve_UnrelatedArgs_ReturnsConfigurator()
    {
        var result = LaunchModeResolver.Resolve(new[] { "--foo", "bar" });
        Assert.Equal(LaunchMode.Configurator, result);
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter LaunchModeResolverTests`
Expected: build error — `LaunchMode` and `LaunchModeResolver` do not exist.

- [ ] **Step 3: Implement LaunchMode and LaunchModeResolver**

Create `src/WindowsBootX.Core/LaunchMode.cs`:
```csharp
namespace WindowsBootX.Core;

public enum LaunchMode
{
    Configurator,
    Player
}
```

Create `src/WindowsBootX.Core/LaunchModeResolver.cs`:
```csharp
namespace WindowsBootX.Core;

public static class LaunchModeResolver
{
    public static LaunchMode Resolve(string[] args)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, "--play", StringComparison.OrdinalIgnoreCase))
            {
                return LaunchMode.Player;
            }
        }

        return LaunchMode.Configurator;
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter LaunchModeResolverTests`
Expected: 4 passed.

- [ ] **Step 5: Write the failing tests for ContentTypeResolver**

Create `tests/WindowsBootX.Core.Tests/ContentTypeResolverTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class ContentTypeResolverTests
{
    [Theory]
    [InlineData("clip.mp4", ContentType.Video)]
    [InlineData("clip.MOV", ContentType.Video)]
    [InlineData("clip.mkv", ContentType.Video)]
    [InlineData("clip.wmv", ContentType.Video)]
    [InlineData("clip.avi", ContentType.Video)]
    [InlineData("anim.gif", ContentType.Gif)]
    [InlineData("frame.png", ContentType.Image)]
    [InlineData("frame.jpg", ContentType.Image)]
    [InlineData("frame.jpeg", ContentType.Image)]
    [InlineData("frame.bmp", ContentType.Image)]
    public void FromFilePath_KnownExtensions_ReturnsExpectedType(string fileName, ContentType expected)
    {
        var result = ContentTypeResolver.FromFilePath(fileName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FromFilePath_UnknownExtension_Throws()
    {
        Assert.Throws<NotSupportedException>(() => ContentTypeResolver.FromFilePath("file.txt"));
    }
}
```

- [ ] **Step 6: Run tests, verify they fail to compile**

Run: `dotnet test --filter ContentTypeResolverTests`
Expected: build error — `ContentType` and `ContentTypeResolver` do not exist.

- [ ] **Step 7: Implement ContentType, ContentTypeResolver, and PlaybackMode**

Create `src/WindowsBootX.Core/ContentType.cs`:
```csharp
namespace WindowsBootX.Core;

public enum ContentType
{
    Video,
    Gif,
    Image
}
```

Create `src/WindowsBootX.Core/ContentTypeResolver.cs`:
```csharp
namespace WindowsBootX.Core;

public static class ContentTypeResolver
{
    public static ContentType FromFilePath(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        return extension switch
        {
            ".mp4" or ".wmv" or ".avi" or ".mov" or ".mkv" => ContentType.Video,
            ".gif" => ContentType.Gif,
            ".png" or ".jpg" or ".jpeg" or ".bmp" => ContentType.Image,
            _ => throw new NotSupportedException($"Unsupported content file extension: {extension}")
        };
    }
}
```

Create `src/WindowsBootX.Core/PlaybackMode.cs` (no dedicated test — plain enum, exercised by later tasks):
```csharp
namespace WindowsBootX.Core;

public enum PlaybackMode
{
    EveryBoot,
    EverySignIn
}
```

- [ ] **Step 8: Run all tests, verify they pass**

Run: `dotnet test`
Expected: all passing, no failures.

- [ ] **Step 9: Commit**

```
git add -A
git commit -m "feat: add content type, playback mode, and launch mode resolution"
```

---

## Task 3: Settings Persistence

**Files:**
- Create: `src/WindowsBootX.Core/AppSettings.cs`
- Create: `src/WindowsBootX.Core/ISettingsStore.cs`
- Create: `src/WindowsBootX.Core/SettingsStore.cs`
- Test: `tests/WindowsBootX.Core.Tests/SettingsStoreTests.cs`

**Interfaces:**
- Consumes: `ContentType`, `PlaybackMode` (Task 2).
- Produces: `class AppSettings` with properties `ContentFilePath (string?)`, `ContentType`, `AudioEnabled (bool)`, `AudioFilePath (string?)`, `PlaybackMode`, `Enabled (bool)`, `MinDisplayMilliseconds (int)`, `MaxDisplayMilliseconds (int)`; `interface ISettingsStore { AppSettings Load(); void Save(AppSettings settings); }`; `class SettingsStore(string filePath) : ISettingsStore` with `static string DefaultFilePath`.

- [ ] **Step 1: Write the failing tests**

Create `tests/WindowsBootX.Core.Tests/SettingsStoreTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WindowsBootXTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    [Fact]
    public void Load_NoFileExists_ReturnsDefaults()
    {
        var store = new SettingsStore(_filePath);

        var settings = store.Load();

        Assert.Equal(PlaybackMode.EveryBoot, settings.PlaybackMode);
        Assert.False(settings.Enabled);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new SettingsStore(_filePath);
        var original = new AppSettings
        {
            ContentFilePath = @"C:\clips\boot.mp4",
            ContentType = ContentType.Video,
            AudioEnabled = true,
            AudioFilePath = null,
            PlaybackMode = PlaybackMode.EverySignIn,
            Enabled = true,
            MinDisplayMilliseconds = 1500,
            MaxDisplayMilliseconds = 6000
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.ContentFilePath, loaded.ContentFilePath);
        Assert.Equal(original.ContentType, loaded.ContentType);
        Assert.Equal(original.AudioEnabled, loaded.AudioEnabled);
        Assert.Equal(original.PlaybackMode, loaded.PlaybackMode);
        Assert.Equal(original.Enabled, loaded.Enabled);
        Assert.Equal(original.MinDisplayMilliseconds, loaded.MinDisplayMilliseconds);
        Assert.Equal(original.MaxDisplayMilliseconds, loaded.MaxDisplayMilliseconds);
    }

    [Fact]
    public void Save_CreatesParentDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "nested", "settings.json");
        var store = new SettingsStore(nestedPath);

        store.Save(new AppSettings());

        Assert.True(File.Exists(nestedPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter SettingsStoreTests`
Expected: build error — `AppSettings` and `SettingsStore` do not exist.

- [ ] **Step 3: Implement AppSettings, ISettingsStore, SettingsStore**

Create `src/WindowsBootX.Core/AppSettings.cs`:
```csharp
namespace WindowsBootX.Core;

public class AppSettings
{
    public string? ContentFilePath { get; set; }
    public ContentType ContentType { get; set; } = ContentType.Video;
    public bool AudioEnabled { get; set; }
    public string? AudioFilePath { get; set; }
    public PlaybackMode PlaybackMode { get; set; } = PlaybackMode.EveryBoot;
    public bool Enabled { get; set; }
    public int MinDisplayMilliseconds { get; set; } = 2000;
    public int MaxDisplayMilliseconds { get; set; } = 8000;
}
```

Create `src/WindowsBootX.Core/ISettingsStore.cs`:
```csharp
namespace WindowsBootX.Core;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
```

Create `src/WindowsBootX.Core/SettingsStore.cs`:
```csharp
using System.Text.Json;

namespace WindowsBootX.Core;

public class SettingsStore : ISettingsStore
{
    private readonly string _filePath;

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsBootX",
        "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppSettings();
        }

        var json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter SettingsStoreTests`
Expected: 3 passed.

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "feat: add AppSettings model and JSON-backed SettingsStore"
```

---

## Task 4: Boot Session Tracking

**Files:**
- Create: `src/WindowsBootX.Core/IBootIdProvider.cs`
- Create: `src/WindowsBootX.Core/WmiBootIdProvider.cs`
- Create: `src/WindowsBootX.Core/BootSessionTracker.cs`
- Test: `tests/WindowsBootX.Core.Tests/BootSessionTrackerTests.cs`

**Interfaces:**
- Produces: `interface IBootIdProvider { string GetCurrentBootId(); }`; `class WmiBootIdProvider : IBootIdProvider` (thin OS wrapper, not unit tested — verified manually in Task 15); `class BootSessionTracker(IBootIdProvider bootIdProvider, string markerFilePath)` with `bool ShouldPlay()` and `void MarkPlayed()`.

- [ ] **Step 1: Add the System.Management package (needed for WmiBootIdProvider)**

```
dotnet add src/WindowsBootX.Core/WindowsBootX.Core.csproj package System.Management
```

- [ ] **Step 2: Write the failing tests**

Create `tests/WindowsBootX.Core.Tests/BootSessionTrackerTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class BootSessionTrackerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _markerPath;

    public BootSessionTrackerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WindowsBootXTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _markerPath = Path.Combine(_tempDir, "last-boot.marker");
    }

    private class FakeBootIdProvider : IBootIdProvider
    {
        public string BootId { get; set; } = "boot-1";
        public string GetCurrentBootId() => BootId;
    }

    [Fact]
    public void ShouldPlay_NoMarkerFile_ReturnsTrue()
    {
        var tracker = new BootSessionTracker(new FakeBootIdProvider(), _markerPath);

        Assert.True(tracker.ShouldPlay());
    }

    [Fact]
    public void ShouldPlay_AfterMarkPlayed_SameBoot_ReturnsFalse()
    {
        var provider = new FakeBootIdProvider { BootId = "boot-1" };
        var tracker = new BootSessionTracker(provider, _markerPath);
        tracker.MarkPlayed();

        Assert.False(tracker.ShouldPlay());
    }

    [Fact]
    public void ShouldPlay_AfterMarkPlayed_DifferentBoot_ReturnsTrue()
    {
        var provider = new FakeBootIdProvider { BootId = "boot-1" };
        var tracker = new BootSessionTracker(provider, _markerPath);
        tracker.MarkPlayed();

        provider.BootId = "boot-2";

        Assert.True(tracker.ShouldPlay());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 3: Run tests, verify they fail to compile**

Run: `dotnet test --filter BootSessionTrackerTests`
Expected: build error — `IBootIdProvider` and `BootSessionTracker` do not exist.

- [ ] **Step 4: Implement IBootIdProvider and BootSessionTracker**

Create `src/WindowsBootX.Core/IBootIdProvider.cs`:
```csharp
namespace WindowsBootX.Core;

public interface IBootIdProvider
{
    string GetCurrentBootId();
}
```

Create `src/WindowsBootX.Core/BootSessionTracker.cs`:
```csharp
namespace WindowsBootX.Core;

public class BootSessionTracker
{
    private readonly IBootIdProvider _bootIdProvider;
    private readonly string _markerFilePath;

    public BootSessionTracker(IBootIdProvider bootIdProvider, string markerFilePath)
    {
        _bootIdProvider = bootIdProvider;
        _markerFilePath = markerFilePath;
    }

    public bool ShouldPlay()
    {
        if (!File.Exists(_markerFilePath))
        {
            return true;
        }

        var storedBootId = File.ReadAllText(_markerFilePath).Trim();
        return storedBootId != _bootIdProvider.GetCurrentBootId();
    }

    public void MarkPlayed()
    {
        var directory = Path.GetDirectoryName(_markerFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_markerFilePath, _bootIdProvider.GetCurrentBootId());
    }
}
```

- [ ] **Step 5: Run tests, verify they pass**

Run: `dotnet test --filter BootSessionTrackerTests`
Expected: 3 passed.

- [ ] **Step 6: Implement WmiBootIdProvider (thin OS wrapper, not unit tested)**

Create `src/WindowsBootX.Core/WmiBootIdProvider.cs`:
```csharp
using System.Management;

namespace WindowsBootX.Core;

// Thin OS-integration wrapper — not unit tested. Uses WMI's LastBootUpTime,
// which stays stable for the duration of a single boot session and changes
// on every cold boot/restart. Verified manually in Task 15.
public class WmiBootIdProvider : IBootIdProvider
{
    public string GetCurrentBootId()
    {
        using var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem");
        foreach (ManagementObject result in searcher.Get())
        {
            var value = result["LastBootUpTime"];
            if (value != null)
            {
                return value.ToString()!;
            }
        }

        throw new InvalidOperationException("Unable to determine boot time from WMI.");
    }
}
```

- [ ] **Step 7: Run the full test suite and build**

Run: `dotnet build && dotnet test`
Expected: build succeeds, all tests pass.

- [ ] **Step 8: Commit**

```
git add -A
git commit -m "feat: add boot session tracking for once-per-boot playback"
```

---

## Task 5: Dismiss Timing Controller

**Files:**
- Create: `src/WindowsBootX.Core/IClock.cs`
- Create: `src/WindowsBootX.Core/SystemClock.cs`
- Create: `src/WindowsBootX.Core/DismissDecision.cs`
- Create: `src/WindowsBootX.Core/DismissTimingController.cs`
- Test: `tests/WindowsBootX.Core.Tests/DismissTimingControllerTests.cs`

**Interfaces:**
- Produces: `interface IClock { DateTime UtcNow { get; } }`; `class SystemClock : IClock`; `enum DismissDecision { Continue, DismissClean, DismissTimeout }`; `class DismissTimingController(IClock clock, TimeSpan minDuration, TimeSpan maxDuration)` with `void Start()` and `DismissDecision Evaluate(bool shellReady)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/WindowsBootX.Core.Tests/DismissTimingControllerTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class DismissTimingControllerTests
{
    private class FakeClock : IClock
    {
        public DateTime UtcNow { get; set; } = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    [Fact]
    public void Evaluate_BeforeMinDuration_AlwaysContinuesEvenIfShellReady()
    {
        var clock = new FakeClock();
        var controller = new DismissTimingController(clock, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
        controller.Start();

        clock.UtcNow = clock.UtcNow.AddSeconds(1);

        Assert.Equal(DismissDecision.Continue, controller.Evaluate(shellReady: true));
    }

    [Fact]
    public void Evaluate_AfterMinDuration_ShellReady_ReturnsDismissClean()
    {
        var clock = new FakeClock();
        var controller = new DismissTimingController(clock, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
        controller.Start();

        clock.UtcNow = clock.UtcNow.AddSeconds(3);

        Assert.Equal(DismissDecision.DismissClean, controller.Evaluate(shellReady: true));
    }

    [Fact]
    public void Evaluate_BetweenMinAndMax_ShellNotReady_ReturnsContinue()
    {
        var clock = new FakeClock();
        var controller = new DismissTimingController(clock, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
        controller.Start();

        clock.UtcNow = clock.UtcNow.AddSeconds(5);

        Assert.Equal(DismissDecision.Continue, controller.Evaluate(shellReady: false));
    }

    [Fact]
    public void Evaluate_AtMaxDuration_ShellNotReady_ReturnsDismissTimeout()
    {
        var clock = new FakeClock();
        var controller = new DismissTimingController(clock, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
        controller.Start();

        clock.UtcNow = clock.UtcNow.AddSeconds(8);

        Assert.Equal(DismissDecision.DismissTimeout, controller.Evaluate(shellReady: false));
    }

    [Fact]
    public void Evaluate_WithoutStart_Throws()
    {
        var controller = new DismissTimingController(new FakeClock(), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));

        Assert.Throws<InvalidOperationException>(() => controller.Evaluate(shellReady: true));
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter DismissTimingControllerTests`
Expected: build error — `IClock`, `DismissDecision`, `DismissTimingController` do not exist.

- [ ] **Step 3: Implement IClock, SystemClock, DismissDecision, DismissTimingController**

Create `src/WindowsBootX.Core/IClock.cs`:
```csharp
namespace WindowsBootX.Core;

public interface IClock
{
    DateTime UtcNow { get; }
}
```

Create `src/WindowsBootX.Core/SystemClock.cs`:
```csharp
namespace WindowsBootX.Core;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
```

Create `src/WindowsBootX.Core/DismissDecision.cs`:
```csharp
namespace WindowsBootX.Core;

public enum DismissDecision
{
    Continue,
    DismissClean,
    DismissTimeout
}
```

Create `src/WindowsBootX.Core/DismissTimingController.cs`:
```csharp
namespace WindowsBootX.Core;

public class DismissTimingController
{
    private readonly IClock _clock;
    private readonly TimeSpan _minDuration;
    private readonly TimeSpan _maxDuration;
    private DateTime _startTimeUtc;
    private bool _started;

    public DismissTimingController(IClock clock, TimeSpan minDuration, TimeSpan maxDuration)
    {
        if (maxDuration < minDuration)
        {
            throw new ArgumentException("maxDuration must be greater than or equal to minDuration.", nameof(maxDuration));
        }

        _clock = clock;
        _minDuration = minDuration;
        _maxDuration = maxDuration;
    }

    public void Start()
    {
        _startTimeUtc = _clock.UtcNow;
        _started = true;
    }

    public DismissDecision Evaluate(bool shellReady)
    {
        if (!_started)
        {
            throw new InvalidOperationException("Start() must be called before Evaluate().");
        }

        var elapsed = _clock.UtcNow - _startTimeUtc;

        if (elapsed < _minDuration)
        {
            return DismissDecision.Continue;
        }

        if (shellReady)
        {
            return DismissDecision.DismissClean;
        }

        return elapsed >= _maxDuration ? DismissDecision.DismissTimeout : DismissDecision.Continue;
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter DismissTimingControllerTests`
Expected: 5 passed.

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "feat: add clamped dismiss-timing controller"
```

---

## Task 6: Safe Mode Detection & Player Gating Logic

**Files:**
- Create: `src/WindowsBootX.Core/ISafeModeDetector.cs`
- Create: `src/WindowsBootX.Core/SafeModeDetector.cs`
- Create: `src/WindowsBootX.Core/PlayerGate.cs`
- Test: `tests/WindowsBootX.Core.Tests/PlayerGateTests.cs`

**Interfaces:**
- Consumes: `AppSettings`, `PlaybackMode` (Task 3), `BootSessionTracker` (Task 4).
- Produces: `interface ISafeModeDetector { bool IsSafeMode(); }`; `class SafeModeDetector : ISafeModeDetector` (thin Win32 wrapper, not unit tested); `static PlayerGate.ShouldPlay(AppSettings settings, ISafeModeDetector safeModeDetector, BootSessionTracker bootSessionTracker) -> bool`.

- [ ] **Step 1: Write the failing tests for PlayerGate**

Create `tests/WindowsBootX.Core.Tests/PlayerGateTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class PlayerGateTests : IDisposable
{
    private class FakeSafeModeDetector : ISafeModeDetector
    {
        public bool SafeMode { get; set; }
        public bool IsSafeMode() => SafeMode;
    }

    private class FakeBootIdProvider : IBootIdProvider
    {
        public string BootId { get; set; } = "boot-1";
        public string GetCurrentBootId() => BootId;
    }

    private readonly string _tempDir;
    private readonly string _markerPath;

    public PlayerGateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WindowsBootXTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
        _markerPath = Path.Combine(_tempDir, "last-boot.marker");
    }

    [Fact]
    public void ShouldPlay_Disabled_ReturnsFalse()
    {
        var settings = new AppSettings { Enabled = false };
        var tracker = new BootSessionTracker(new FakeBootIdProvider(), _markerPath);

        var result = PlayerGate.ShouldPlay(settings, new FakeSafeModeDetector(), tracker);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPlay_SafeMode_ReturnsFalse()
    {
        var settings = new AppSettings { Enabled = true, PlaybackMode = PlaybackMode.EverySignIn };
        var tracker = new BootSessionTracker(new FakeBootIdProvider(), _markerPath);

        var result = PlayerGate.ShouldPlay(settings, new FakeSafeModeDetector { SafeMode = true }, tracker);

        Assert.False(result);
    }

    [Fact]
    public void ShouldPlay_EverySignInMode_EnabledNotSafeMode_ReturnsTrue()
    {
        var settings = new AppSettings { Enabled = true, PlaybackMode = PlaybackMode.EverySignIn };
        var tracker = new BootSessionTracker(new FakeBootIdProvider(), _markerPath);

        var result = PlayerGate.ShouldPlay(settings, new FakeSafeModeDetector(), tracker);

        Assert.True(result);
    }

    [Fact]
    public void ShouldPlay_EveryBootMode_DefersToBootSessionTracker()
    {
        var settings = new AppSettings { Enabled = true, PlaybackMode = PlaybackMode.EveryBoot };
        var provider = new FakeBootIdProvider { BootId = "boot-1" };
        var tracker = new BootSessionTracker(provider, _markerPath);
        tracker.MarkPlayed();

        var result = PlayerGate.ShouldPlay(settings, new FakeSafeModeDetector(), tracker);

        Assert.False(result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter PlayerGateTests`
Expected: build error — `ISafeModeDetector` and `PlayerGate` do not exist.

- [ ] **Step 3: Implement ISafeModeDetector and PlayerGate**

Create `src/WindowsBootX.Core/ISafeModeDetector.cs`:
```csharp
namespace WindowsBootX.Core;

public interface ISafeModeDetector
{
    bool IsSafeMode();
}
```

Create `src/WindowsBootX.Core/PlayerGate.cs`:
```csharp
namespace WindowsBootX.Core;

public static class PlayerGate
{
    public static bool ShouldPlay(AppSettings settings, ISafeModeDetector safeModeDetector, BootSessionTracker bootSessionTracker)
    {
        if (!settings.Enabled)
        {
            return false;
        }

        if (safeModeDetector.IsSafeMode())
        {
            return false;
        }

        if (settings.PlaybackMode == PlaybackMode.EveryBoot)
        {
            return bootSessionTracker.ShouldPlay();
        }

        return true;
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter PlayerGateTests`
Expected: 4 passed.

- [ ] **Step 5: Implement SafeModeDetector (thin Win32 wrapper, not unit tested)**

Create `src/WindowsBootX.Core/SafeModeDetector.cs`:
```csharp
using System.Runtime.InteropServices;

namespace WindowsBootX.Core;

// Thin OS-integration wrapper — not unit tested. GetSystemMetrics(SM_CLEANBOOT)
// returns non-zero when Windows is running in Safe Mode. Verified manually
// in Task 15.
public class SafeModeDetector : ISafeModeDetector
{
    private const int SM_CLEANBOOT = 67;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    public bool IsSafeMode() => GetSystemMetrics(SM_CLEANBOOT) != 0;
}
```

- [ ] **Step 6: Run the full test suite and build**

Run: `dotnet build && dotnet test`
Expected: build succeeds, all tests pass.

- [ ] **Step 7: Commit**

```
git add -A
git commit -m "feat: add Safe Mode detection and player gating logic"
```

---

## Task 7: Logon Scheduled Task Management

**Files:**
- Create: `src/WindowsBootX.Core/TaskTriggerType.cs`
- Create: `src/WindowsBootX.Core/TriggerMapper.cs`
- Create: `src/WindowsBootX.Core/ILogonTaskManager.cs`
- Create: `src/WindowsBootX.Core/LogonTaskManager.cs`
- Test: `tests/WindowsBootX.Core.Tests/TriggerMapperTests.cs`

**Interfaces:**
- Consumes: `PlaybackMode` (Task 2).
- Produces: `enum TaskTriggerType { AtLogOn, OnWorkstationUnlock }`; `static TriggerMapper.GetTriggersFor(PlaybackMode mode) -> IReadOnlyList<TaskTriggerType>`; `interface ILogonTaskManager { void Install(PlaybackMode mode, string executablePath); void Uninstall(); bool IsInstalled(); }`; `class LogonTaskManager : ILogonTaskManager` (thin Task Scheduler COM wrapper, not unit tested — verified manually in Task 15).

- [ ] **Step 1: Write the failing tests for TriggerMapper**

Create `tests/WindowsBootX.Core.Tests/TriggerMapperTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class TriggerMapperTests
{
    [Fact]
    public void GetTriggersFor_EveryBoot_ReturnsAtLogOnOnly()
    {
        var triggers = TriggerMapper.GetTriggersFor(PlaybackMode.EveryBoot);

        Assert.Equal(new[] { TaskTriggerType.AtLogOn }, triggers);
    }

    [Fact]
    public void GetTriggersFor_EverySignIn_ReturnsAtLogOnAndWorkstationUnlock()
    {
        var triggers = TriggerMapper.GetTriggersFor(PlaybackMode.EverySignIn);

        Assert.Contains(TaskTriggerType.AtLogOn, triggers);
        Assert.Contains(TaskTriggerType.OnWorkstationUnlock, triggers);
        Assert.Equal(2, triggers.Count);
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter TriggerMapperTests`
Expected: build error — `TaskTriggerType` and `TriggerMapper` do not exist.

- [ ] **Step 3: Implement TaskTriggerType and TriggerMapper**

Create `src/WindowsBootX.Core/TaskTriggerType.cs`:
```csharp
namespace WindowsBootX.Core;

public enum TaskTriggerType
{
    AtLogOn,
    OnWorkstationUnlock
}
```

Create `src/WindowsBootX.Core/TriggerMapper.cs`:
```csharp
namespace WindowsBootX.Core;

public static class TriggerMapper
{
    public static IReadOnlyList<TaskTriggerType> GetTriggersFor(PlaybackMode mode)
    {
        return mode switch
        {
            PlaybackMode.EveryBoot => new[] { TaskTriggerType.AtLogOn },
            PlaybackMode.EverySignIn => new[] { TaskTriggerType.AtLogOn, TaskTriggerType.OnWorkstationUnlock },
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown playback mode.")
        };
    }
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter TriggerMapperTests`
Expected: 2 passed.

- [ ] **Step 5: Add the TaskScheduler package**

```
dotnet add src/WindowsBootX.Core/WindowsBootX.Core.csproj package TaskScheduler
```

- [ ] **Step 6: Implement ILogonTaskManager and LogonTaskManager (thin COM wrapper, not unit tested)**

Create `src/WindowsBootX.Core/ILogonTaskManager.cs`:
```csharp
namespace WindowsBootX.Core;

public interface ILogonTaskManager
{
    void Install(PlaybackMode mode, string executablePath);
    void Uninstall();
    bool IsInstalled();
}
```

Create `src/WindowsBootX.Core/LogonTaskManager.cs`:
```csharp
using Microsoft.Win32.TaskScheduler;

namespace WindowsBootX.Core;

// Thin OS-integration wrapper around the Windows Task Scheduler COM API —
// not unit tested (would require a real, writable Task Scheduler on the
// test machine). Registers a per-user task under HKCU-equivalent user
// scope; never requests elevation. Verified manually in Task 15.
public class LogonTaskManager : ILogonTaskManager
{
    private const string FolderName = "WindowsBootX";
    private const string TaskName = "PlayAnimation";

    public void Install(PlaybackMode mode, string executablePath)
    {
        using var taskService = new TaskService();
        var folder = taskService.RootFolder.SubFolders.Exists(FolderName)
            ? taskService.RootFolder.SubFolders[FolderName]
            : taskService.RootFolder.CreateFolder(FolderName);

        var definition = taskService.NewTask();
        definition.RegistrationInfo.Description = "Plays the WindowsBootX post-login animation.";
        definition.Principal.LogonType = TaskLogonType.InteractiveToken;
        definition.Principal.RunLevel = TaskRunLevel.LUA;
        definition.Settings.DisallowStartIfOnBatteries = false;
        definition.Settings.StopIfGoingOnBatteries = false;
        definition.Settings.ExecutionTimeLimit = TimeSpan.Zero;

        definition.Triggers.Clear();
        foreach (var triggerType in TriggerMapper.GetTriggersFor(mode))
        {
            definition.Triggers.Add(triggerType switch
            {
                TaskTriggerType.AtLogOn => new LogonTrigger(),
                TaskTriggerType.OnWorkstationUnlock => new SessionStateChangeTrigger(TaskSessionStateChangeType.SessionUnlock),
                _ => throw new ArgumentOutOfRangeException(nameof(triggerType))
            });
        }

        definition.Actions.Add(new ExecAction(executablePath, "--play"));

        folder.RegisterTaskDefinition(TaskName, definition);
    }

    public void Uninstall()
    {
        using var taskService = new TaskService();
        if (!taskService.RootFolder.SubFolders.Exists(FolderName))
        {
            return;
        }

        var folder = taskService.RootFolder.SubFolders[FolderName];
        if (folder.Tasks.Exists(TaskName))
        {
            folder.DeleteTask(TaskName);
        }

        if (folder.Tasks.Count == 0)
        {
            taskService.RootFolder.DeleteFolder(FolderName);
        }
    }

    public bool IsInstalled()
    {
        using var taskService = new TaskService();
        if (!taskService.RootFolder.SubFolders.Exists(FolderName))
        {
            return false;
        }

        return taskService.RootFolder.SubFolders[FolderName].Tasks.Exists(TaskName);
    }
}
```

- [ ] **Step 7: Run the full test suite and build**

Run: `dotnet build && dotnet test`
Expected: build succeeds, all tests pass.

- [ ] **Step 8: Manual verification**

From a normal (non-elevated) terminal, write and run a small throwaway console snippet (or use `csi`/LINQPad, or a temporary `Console.WriteLine` in `Main` — remove after) that calls `new LogonTaskManager().Install(PlaybackMode.EveryBoot, @"C:\Windows\System32\notepad.exe")`, then open Task Scheduler (`taskschd.msc`) and confirm a `WindowsBootX\PlayAnimation` task exists under your user, with a "Log on" trigger, action pointing at notepad.exe with argument `--play`, and no elevation/highest-privileges checkbox set. Then call `Uninstall()` and confirm the task and folder are gone. Delete the throwaway snippet afterward.

- [ ] **Step 9: Commit**

```
git add -A
git commit -m "feat: add logon Scheduled Task registration via Task Scheduler API"
```

---

## Task 8: Overlay Player Window — Content Rendering

**Files:**
- Create: `src/WindowsBootX/Player/OverlayWindow.xaml`
- Create: `src/WindowsBootX/Player/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `ContentType` (Task 2).
- Produces: `class OverlayWindow : Window` with `void LoadContent(string contentFilePath, ContentType contentType, bool audioEnabled, string? audioFilePath)`. (This task's constructor is superseded by Task 9's rewrite; full behavioral verification happens at the end of Task 9/12.)

- [ ] **Step 1: Add the WpfAnimatedGif package**

```
dotnet add src/WindowsBootX/WindowsBootX.csproj package WpfAnimatedGif
```

- [ ] **Step 2: Create the overlay window XAML**

Create `src/WindowsBootX/Player/OverlayWindow.xaml`:
```xml
<Window x:Class="WindowsBootX.Player.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        WindowState="Maximized"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        Background="Black"
        Cursor="None">
    <Grid>
        <MediaElement x:Name="VideoElement"
                      Visibility="Collapsed"
                      LoadedBehavior="Manual"
                      UnloadedBehavior="Stop"
                      Stretch="Uniform"
                      HorizontalAlignment="Center"
                      VerticalAlignment="Center" />
        <Image x:Name="ImageElement"
               Visibility="Collapsed"
               Stretch="Uniform"
               HorizontalAlignment="Center"
               VerticalAlignment="Center" />
        <MediaElement x:Name="AudioElement"
                      Visibility="Collapsed"
                      LoadedBehavior="Manual"
                      UnloadedBehavior="Stop" />
    </Grid>
</Window>
```

Note: `Stretch="Uniform"` on both `MediaElement` and `Image`, centered in a fullscreen `Grid`, already gives correct letterbox/pillarbox scaling to any resolution or aspect ratio — no separate scaling math is needed.

- [ ] **Step 3: Implement the code-behind**

Create `src/WindowsBootX/Player/OverlayWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Media.Imaging;
using WindowsBootX.Core;
using WpfAnimatedGif;

namespace WindowsBootX.Player;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void LoadContent(string contentFilePath, ContentType contentType, bool audioEnabled, string? audioFilePath)
    {
        switch (contentType)
        {
            case ContentType.Video:
                VideoElement.Source = new Uri(contentFilePath, UriKind.Absolute);
                VideoElement.Visibility = Visibility.Visible;
                VideoElement.MediaEnded += (_, _) =>
                {
                    VideoElement.Position = TimeSpan.Zero;
                    VideoElement.Play();
                };
                VideoElement.Volume = audioEnabled ? 1.0 : 0.0;
                VideoElement.Play();
                break;

            case ContentType.Gif:
                var gifImage = new BitmapImage(new Uri(contentFilePath, UriKind.Absolute));
                ImageBehavior.SetAnimatedSource(ImageElement, gifImage);
                ImageBehavior.SetRepeatBehavior(ImageElement, System.Windows.Media.Animation.RepeatBehavior.Forever);
                ImageElement.Visibility = Visibility.Visible;
                PlayAudioFileIfEnabled(audioEnabled, audioFilePath);
                break;

            case ContentType.Image:
                ImageElement.Source = new BitmapImage(new Uri(contentFilePath, UriKind.Absolute));
                ImageElement.Visibility = Visibility.Visible;
                PlayAudioFileIfEnabled(audioEnabled, audioFilePath);
                break;

            default:
                throw new NotSupportedException($"Unsupported content type: {contentType}");
        }
    }

    private void PlayAudioFileIfEnabled(bool audioEnabled, string? audioFilePath)
    {
        if (!audioEnabled || string.IsNullOrEmpty(audioFilePath))
        {
            return;
        }

        AudioElement.Source = new Uri(audioFilePath, UriKind.Absolute);
        AudioElement.Visibility = Visibility.Visible;
        AudioElement.MediaEnded += (_, _) =>
        {
            AudioElement.Position = TimeSpan.Zero;
            AudioElement.Play();
        };
        AudioElement.Play();
    }
}
```

- [ ] **Step 4: Verify the project builds**

Run: `dotnet build`
Expected: build succeeds. (Behavioral verification of actual playback happens once Task 9 wires this window up to a runnable entry point.)

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "feat: add overlay window with video/gif/image content rendering"
```

---

## Task 9: Overlay Player — Dismiss / Fail-Safe / Timing Wiring

**Files:**
- Create: `src/WindowsBootX/Player/IShellReadySignal.cs`
- Create: `src/WindowsBootX/Player/ShellReadySignal.cs`
- Modify: `src/WindowsBootX/Player/OverlayWindow.xaml.cs`

**Interfaces:**
- Consumes: `DismissTimingController`, `DismissDecision`, `ContentTypeResolver`, `AppSettings` (Core, Tasks 2/3/5).
- Produces: `interface IShellReadySignal { bool IsReady(); }`; `class ShellReadySignal : IShellReadySignal` (thin Win32 wrapper, not unit tested); `OverlayWindow(IShellReadySignal shellReadySignal, DismissTimingController timingController)` constructor and `void Begin(AppSettings settings)` — used by Task 12.

- [ ] **Step 1: Implement IShellReadySignal and ShellReadySignal**

Create `src/WindowsBootX/Player/IShellReadySignal.cs`:
```csharp
namespace WindowsBootX.Player;

public interface IShellReadySignal
{
    bool IsReady();
}
```

Create `src/WindowsBootX/Player/ShellReadySignal.cs`:
```csharp
using System.Runtime.InteropServices;

namespace WindowsBootX.Player;

// Thin OS-integration wrapper — not unit tested. Polls for the taskbar
// window, which only exists once the shell has finished initializing.
// Verified manually in Task 12.
public class ShellReadySignal : IShellReadySignal
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    public bool IsReady() => FindWindow("Shell_TrayWnd", null) != IntPtr.Zero;
}
```

- [ ] **Step 2: Rewrite OverlayWindow.xaml.cs to add dismiss, timeout-fade, and fail-open behavior**

Replace the full contents of `src/WindowsBootX/Player/OverlayWindow.xaml.cs`:
```csharp
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowsBootX.Core;
using WpfAnimatedGif;

namespace WindowsBootX.Player;

public partial class OverlayWindow : Window
{
    private readonly IShellReadySignal _shellReadySignal;
    private readonly DismissTimingController _timingController;
    private readonly DispatcherTimer _pollTimer;

    public OverlayWindow(IShellReadySignal shellReadySignal, DismissTimingController timingController)
    {
        InitializeComponent();
        _shellReadySignal = shellReadySignal;
        _timingController = timingController;

        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _pollTimer.Tick += OnPollTick;

        PreviewKeyDown += (_, _) => DismissImmediately();
        PreviewMouseDown += (_, _) => DismissImmediately();
    }

    public void Begin(AppSettings settings)
    {
        try
        {
            var contentType = ContentTypeResolver.FromFilePath(settings.ContentFilePath!);
            LoadContent(settings.ContentFilePath!, contentType, settings.AudioEnabled, settings.AudioFilePath);
        }
        catch
        {
            // Fail open: a missing/corrupt asset must never trap the user at logon.
            Application.Current.Shutdown();
            return;
        }

        _timingController.Start();
        _pollTimer.Start();
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        var decision = _timingController.Evaluate(_shellReadySignal.IsReady());
        switch (decision)
        {
            case DismissDecision.DismissClean:
                _pollTimer.Stop();
                Application.Current.Shutdown();
                break;
            case DismissDecision.DismissTimeout:
                _pollTimer.Stop();
                FadeOutAndClose();
                break;
        }
    }

    private void DismissImmediately()
    {
        _pollTimer.Stop();
        Application.Current.Shutdown();
    }

    private void FadeOutAndClose()
    {
        var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(400));
        fadeOut.Completed += (_, _) => Application.Current.Shutdown();
        BeginAnimation(OpacityProperty, fadeOut);
    }

    public void LoadContent(string contentFilePath, ContentType contentType, bool audioEnabled, string? audioFilePath)
    {
        switch (contentType)
        {
            case ContentType.Video:
                VideoElement.Source = new Uri(contentFilePath, UriKind.Absolute);
                VideoElement.Visibility = Visibility.Visible;
                VideoElement.MediaEnded += (_, _) =>
                {
                    VideoElement.Position = TimeSpan.Zero;
                    VideoElement.Play();
                };
                VideoElement.Volume = audioEnabled ? 1.0 : 0.0;
                VideoElement.Play();
                break;

            case ContentType.Gif:
                var gifImage = new BitmapImage(new Uri(contentFilePath, UriKind.Absolute));
                ImageBehavior.SetAnimatedSource(ImageElement, gifImage);
                ImageBehavior.SetRepeatBehavior(ImageElement, RepeatBehavior.Forever);
                ImageElement.Visibility = Visibility.Visible;
                PlayAudioFileIfEnabled(audioEnabled, audioFilePath);
                break;

            case ContentType.Image:
                ImageElement.Source = new BitmapImage(new Uri(contentFilePath, UriKind.Absolute));
                ImageElement.Visibility = Visibility.Visible;
                PlayAudioFileIfEnabled(audioEnabled, audioFilePath);
                break;

            default:
                throw new NotSupportedException($"Unsupported content type: {contentType}");
        }
    }

    private void PlayAudioFileIfEnabled(bool audioEnabled, string? audioFilePath)
    {
        if (!audioEnabled || string.IsNullOrEmpty(audioFilePath))
        {
            return;
        }

        AudioElement.Source = new Uri(audioFilePath, UriKind.Absolute);
        AudioElement.Visibility = Visibility.Visible;
        AudioElement.MediaEnded += (_, _) =>
        {
            AudioElement.Position = TimeSpan.Zero;
            AudioElement.Play();
        };
        AudioElement.Play();
    }
}
```

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build`
Expected: build succeeds. (Full behavioral verification — real dismiss/timeout/fail-open behavior — happens in Task 12 once there's an entry point that constructs this window.)

- [ ] **Step 4: Commit**

```
git add -A
git commit -m "feat: wire overlay dismiss, timeout fade-out, and fail-open handling"
```

---

## Task 10: ConfiguratorViewModel

**Files:**
- Create: `src/WindowsBootX.Core/ConfiguratorViewModel.cs`
- Test: `tests/WindowsBootX.Core.Tests/ConfiguratorViewModelTests.cs`

**Interfaces:**
- Consumes: `ISettingsStore`, `AppSettings`, `ContentType`, `ContentTypeResolver`, `PlaybackMode` (Tasks 2/3), `ILogonTaskManager` (Task 7).
- Produces: `class ConfiguratorViewModel(ISettingsStore settingsStore, ILogonTaskManager logonTaskManager) : INotifyPropertyChanged` with bindable properties `ContentFilePath`, `ContentType`, `AudioEnabled`, `AudioFilePath`, `PlaybackMode`, `IsEveryBootSelected`, `IsEverySignInSelected`, `Enabled`, `IsAudioFilePickerVisible`, `CanInstall`, and methods `SaveSettings()`, `Install(string executablePath)`, `Uninstall()`, `bool IsInstalled()` — used by Task 11.

- [ ] **Step 1: Write the failing tests**

Create `tests/WindowsBootX.Core.Tests/ConfiguratorViewModelTests.cs`:
```csharp
using WindowsBootX.Core;
using Xunit;

namespace WindowsBootX.Core.Tests;

public class ConfiguratorViewModelTests
{
    private class FakeSettingsStore : ISettingsStore
    {
        public AppSettings Stored { get; set; } = new AppSettings();
        public AppSettings Load() => Stored;
        public void Save(AppSettings settings) => Stored = settings;
    }

    private class FakeLogonTaskManager : ILogonTaskManager
    {
        public bool Installed { get; private set; }
        public PlaybackMode? LastInstalledMode { get; private set; }

        public void Install(PlaybackMode mode, string executablePath)
        {
            Installed = true;
            LastInstalledMode = mode;
        }

        public void Uninstall() => Installed = false;

        public bool IsInstalled() => Installed;
    }

    private static string CreateTempFile(string extension)
    {
        var dir = Path.Combine(Path.GetTempPath(), "WindowsBootXTests_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "clip" + extension);
        File.WriteAllText(path, "fake");
        return path;
    }

    [Fact]
    public void CanInstall_NoContentFile_ReturnsFalse()
    {
        var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), new FakeLogonTaskManager());

        Assert.False(viewModel.CanInstall);
    }

    [Fact]
    public void SettingContentFilePath_UpdatesContentTypeFromExtension()
    {
        var filePath = CreateTempFile(".mp4");
        try
        {
            var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), new FakeLogonTaskManager())
            {
                ContentFilePath = filePath
            };

            Assert.Equal(ContentType.Video, viewModel.ContentType);
            Assert.True(viewModel.CanInstall);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true);
        }
    }

    [Fact]
    public void IsAudioFilePickerVisible_VideoContent_AudioEnabled_ReturnsFalse()
    {
        var filePath = CreateTempFile(".mp4");
        try
        {
            var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), new FakeLogonTaskManager())
            {
                ContentFilePath = filePath,
                AudioEnabled = true
            };

            Assert.False(viewModel.IsAudioFilePickerVisible);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true);
        }
    }

    [Fact]
    public void IsAudioFilePickerVisible_GifContent_AudioEnabled_ReturnsTrue()
    {
        var filePath = CreateTempFile(".gif");
        try
        {
            var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), new FakeLogonTaskManager())
            {
                ContentFilePath = filePath,
                AudioEnabled = true
            };

            Assert.True(viewModel.IsAudioFilePickerVisible);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true);
        }
    }

    [Fact]
    public void IsEveryBootSelected_SetTrue_SetsPlaybackModeToEveryBoot()
    {
        var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), new FakeLogonTaskManager())
        {
            IsEverySignInSelected = true
        };

        viewModel.IsEveryBootSelected = true;

        Assert.Equal(PlaybackMode.EveryBoot, viewModel.PlaybackMode);
        Assert.False(viewModel.IsEverySignInSelected);
    }

    [Fact]
    public void Install_CallsLogonTaskManagerAndSetsEnabled()
    {
        var filePath = CreateTempFile(".mp4");
        var logonTaskManager = new FakeLogonTaskManager();
        try
        {
            var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), logonTaskManager)
            {
                ContentFilePath = filePath,
                IsEverySignInSelected = true
            };

            viewModel.Install(@"C:\fake\WindowsBootX.exe");

            Assert.True(logonTaskManager.Installed);
            Assert.Equal(PlaybackMode.EverySignIn, logonTaskManager.LastInstalledMode);
            Assert.True(viewModel.Enabled);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(filePath)!, recursive: true);
        }
    }

    [Fact]
    public void Uninstall_CallsLogonTaskManagerAndClearsEnabled()
    {
        var logonTaskManager = new FakeLogonTaskManager();
        var viewModel = new ConfiguratorViewModel(new FakeSettingsStore(), logonTaskManager)
        {
            Enabled = true
        };

        viewModel.Uninstall();

        Assert.False(logonTaskManager.Installed);
        Assert.False(viewModel.Enabled);
    }
}
```

- [ ] **Step 2: Run tests, verify they fail to compile**

Run: `dotnet test --filter ConfiguratorViewModelTests`
Expected: build error — `ConfiguratorViewModel` does not exist.

- [ ] **Step 3: Implement ConfiguratorViewModel**

Create `src/WindowsBootX.Core/ConfiguratorViewModel.cs`:
```csharp
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowsBootX.Core;

public class ConfiguratorViewModel : INotifyPropertyChanged
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILogonTaskManager _logonTaskManager;
    private readonly AppSettings _settings;

    public ConfiguratorViewModel(ISettingsStore settingsStore, ILogonTaskManager logonTaskManager)
    {
        _settingsStore = settingsStore;
        _logonTaskManager = logonTaskManager;
        _settings = _settingsStore.Load();
    }

    public string? ContentFilePath
    {
        get => _settings.ContentFilePath;
        set
        {
            if (_settings.ContentFilePath == value) return;

            if (!string.IsNullOrEmpty(value))
            {
                // The Configurator's file picker restricts the dialog filter to
                // supported extensions (Task 11), so this should never throw
                // in practice — it's an invariant, not a user-facing error path.
                _settings.ContentType = ContentTypeResolver.FromFilePath(value);
            }

            _settings.ContentFilePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ContentType));
            OnPropertyChanged(nameof(IsAudioFilePickerVisible));
            OnPropertyChanged(nameof(CanInstall));
        }
    }

    public ContentType ContentType => _settings.ContentType;

    public bool AudioEnabled
    {
        get => _settings.AudioEnabled;
        set
        {
            if (_settings.AudioEnabled == value) return;
            _settings.AudioEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsAudioFilePickerVisible));
        }
    }

    public string? AudioFilePath
    {
        get => _settings.AudioFilePath;
        set
        {
            if (_settings.AudioFilePath == value) return;
            _settings.AudioFilePath = value;
            OnPropertyChanged();
        }
    }

    public PlaybackMode PlaybackMode
    {
        get => _settings.PlaybackMode;
        set
        {
            if (_settings.PlaybackMode == value) return;
            _settings.PlaybackMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsEveryBootSelected));
            OnPropertyChanged(nameof(IsEverySignInSelected));
        }
    }

    public bool IsEveryBootSelected
    {
        get => PlaybackMode == PlaybackMode.EveryBoot;
        set { if (value) PlaybackMode = PlaybackMode.EveryBoot; }
    }

    public bool IsEverySignInSelected
    {
        get => PlaybackMode == PlaybackMode.EverySignIn;
        set { if (value) PlaybackMode = PlaybackMode.EverySignIn; }
    }

    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            if (_settings.Enabled == value) return;
            _settings.Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool IsAudioFilePickerVisible => AudioEnabled && ContentType != ContentType.Video;

    public bool CanInstall => !string.IsNullOrEmpty(ContentFilePath) && File.Exists(ContentFilePath);

    public void SaveSettings() => _settingsStore.Save(_settings);

    public void Install(string executablePath)
    {
        SaveSettings();
        _logonTaskManager.Install(PlaybackMode, executablePath);
        Enabled = true;
        SaveSettings();
    }

    public void Uninstall()
    {
        _logonTaskManager.Uninstall();
        Enabled = false;
        SaveSettings();
    }

    public bool IsInstalled() => _logonTaskManager.IsInstalled();

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

- [ ] **Step 4: Run tests, verify they pass**

Run: `dotnet test --filter ConfiguratorViewModelTests`
Expected: 7 passed.

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "feat: add ConfiguratorViewModel with settings binding and install/uninstall"
```

---

## Task 11: ConfiguratorWindow — Layout, Binding, and Install/Uninstall/Preview Wiring

**Files:**
- Create: `src/WindowsBootX/Configurator/ConfiguratorWindow.xaml`
- Create: `src/WindowsBootX/Configurator/ConfiguratorWindow.xaml.cs`

**Interfaces:**
- Consumes: `ConfiguratorViewModel`, `SettingsStore`, `LogonTaskManager` (Core, Tasks 3/7/10).
- Produces: `class ConfiguratorWindow : Window` with a parameterless constructor wiring real dependencies — used by Task 12.

- [ ] **Step 1: Create the window XAML**

Create `src/WindowsBootX/Configurator/ConfiguratorWindow.xaml`:
```xml
<Window x:Class="WindowsBootX.Configurator.ConfiguratorWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="WindowsBootX" Height="360" Width="480"
        ResizeMode="NoResize" WindowStartupLocation="CenterScreen">
    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVisibilityConverter" />
    </Window.Resources>
    <StackPanel Margin="16">
        <TextBlock Text="Animation file" FontWeight="Bold" Margin="0,0,0,4" />
        <DockPanel Margin="0,0,0,12">
            <Button DockPanel.Dock="Right" Content="Browse…" Width="80" Click="OnBrowseContentClick" />
            <TextBox Text="{Binding ContentFilePath, Mode=OneWay}" IsReadOnly="True" Margin="0,0,8,0" />
        </DockPanel>

        <CheckBox Content="Play audio" IsChecked="{Binding AudioEnabled}" Margin="0,0,0,8" />

        <DockPanel Margin="0,0,0,12" Visibility="{Binding IsAudioFilePickerVisible, Converter={StaticResource BoolToVisibilityConverter}}">
            <Button DockPanel.Dock="Right" Content="Browse…" Width="80" Click="OnBrowseAudioClick" />
            <TextBox Text="{Binding AudioFilePath, Mode=OneWay}" IsReadOnly="True" Margin="0,0,8,0" />
        </DockPanel>

        <TextBlock Text="When should it play?" FontWeight="Bold" Margin="0,0,0,4" />
        <RadioButton Content="Every boot (once per restart)" GroupName="Frequency"
                     IsChecked="{Binding IsEveryBootSelected}" Margin="0,0,0,4" />
        <RadioButton Content="Every sign-in (including unlock)" GroupName="Frequency"
                     IsChecked="{Binding IsEverySignInSelected}" Margin="0,0,0,12" />

        <StackPanel Orientation="Horizontal" HorizontalAlignment="Right">
            <Button Content="Preview" Width="90" Margin="0,0,8,0" Click="OnPreviewClick" />
            <Button Content="Install" Width="90" Margin="0,0,8,0" IsEnabled="{Binding CanInstall}" Click="OnInstallClick" />
            <Button Content="Uninstall" Width="90" Click="OnUninstallClick" />
        </StackPanel>
    </StackPanel>
</Window>
```

- [ ] **Step 2: Implement the code-behind**

Create `src/WindowsBootX/Configurator/ConfiguratorWindow.xaml.cs`:
```csharp
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using WindowsBootX.Core;

namespace WindowsBootX.Configurator;

public partial class ConfiguratorWindow : Window
{
    private readonly ConfiguratorViewModel _viewModel;

    public ConfiguratorWindow()
    {
        InitializeComponent();
        _viewModel = new ConfiguratorViewModel(
            new SettingsStore(SettingsStore.DefaultFilePath),
            new LogonTaskManager());
        DataContext = _viewModel;
    }

    private void OnBrowseContentClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an animation file",
            Filter = "Supported files|*.mp4;*.wmv;*.avi;*.mov;*.mkv;*.gif;*.png;*.jpg;*.jpeg;*.bmp"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.ContentFilePath = dialog.FileName;
        }
    }

    private void OnBrowseAudioClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an audio file",
            Filter = "Audio files|*.mp3;*.wav"
        };

        if (dialog.ShowDialog() == true)
        {
            _viewModel.AudioFilePath = dialog.FileName;
        }
    }

    private void OnInstallClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Install(Environment.ProcessPath!);
        MessageBox.Show(this, "WindowsBootX will now play at sign-in.", "Installed",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnUninstallClick(object sender, RoutedEventArgs e)
    {
        _viewModel.Uninstall();
        MessageBox.Show(this, "WindowsBootX has been removed from sign-in.", "Uninstalled",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SaveSettings();
        Process.Start(Environment.ProcessPath!, "--play");
    }
}
```

- [ ] **Step 3: Verify the project builds**

Run: `dotnet build`
Expected: build succeeds. (End-to-end behavioral verification happens in Task 12, once `App.xaml.cs` can actually construct this window.)

- [ ] **Step 4: Commit**

```
git add -A
git commit -m "feat: add ConfiguratorWindow with content/audio pickers and install/uninstall/preview"
```

---

## Task 12: App Entry Dispatch (First Full End-to-End)

**Files:**
- Modify: `src/WindowsBootX/App.xaml.cs`

**Interfaces:**
- Consumes: `LaunchModeResolver`, `LaunchMode`, `SettingsStore`, `PlayerGate`, `SafeModeDetector`, `BootSessionTracker`, `WmiBootIdProvider`, `DismissTimingController`, `SystemClock`, `LogonTaskManager` (Core); `OverlayWindow`, `ShellReadySignal` (Player); `ConfiguratorWindow` (Configurator).
- Produces: a fully wired `App.OnStartup` that dispatches between Player mode (`--play`), an uninstall-only mode (`--unregister`, used by the installer's uninstaller), and the Configurator.

- [ ] **Step 1: Replace App.xaml.cs**

Replace the full contents of `src/WindowsBootX/App.xaml.cs`:
```csharp
using System.Diagnostics;
using System.Linq;
using System.Windows;
using WindowsBootX.Configurator;
using WindowsBootX.Core;
using WindowsBootX.Player;

namespace WindowsBootX;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => string.Equals(a, "--unregister", StringComparison.OrdinalIgnoreCase)))
        {
            new LogonTaskManager().Uninstall();
            Shutdown();
            return;
        }

        var mode = LaunchModeResolver.Resolve(e.Args);
        if (mode == LaunchMode.Player)
        {
            RunPlayerMode();
        }
        else
        {
            RunConfiguratorMode();
        }
    }

    private void RunPlayerMode()
    {
        // Briefly run above-normal priority to win the race against the
        // rest of the shell starting up; the process exits shortly after
        // dismissal, so there's no need to lower it back down.
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;

        var settingsStore = new SettingsStore(SettingsStore.DefaultFilePath);
        var settings = settingsStore.Load();

        var markerPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsBootX",
            "last-boot.marker");
        var bootSessionTracker = new BootSessionTracker(new WmiBootIdProvider(), markerPath);
        var safeModeDetector = new SafeModeDetector();

        if (!PlayerGate.ShouldPlay(settings, safeModeDetector, bootSessionTracker))
        {
            Shutdown();
            return;
        }

        if (settings.PlaybackMode == PlaybackMode.EveryBoot)
        {
            bootSessionTracker.MarkPlayed();
        }

        var timingController = new DismissTimingController(
            new SystemClock(),
            TimeSpan.FromMilliseconds(settings.MinDisplayMilliseconds),
            TimeSpan.FromMilliseconds(settings.MaxDisplayMilliseconds));

        var overlay = new OverlayWindow(new ShellReadySignal(), timingController);
        overlay.Show();
        overlay.Begin(settings);
    }

    private void RunConfiguratorMode()
    {
        var window = new ConfiguratorWindow();
        window.Show();
    }
}
```

- [ ] **Step 2: Run the full test suite and build**

Run: `dotnet build && dotnet test`
Expected: build succeeds, all tests pass.

- [ ] **Step 3: Manual verification — Configurator path**

Run:
```
dotnet run --project src/WindowsBootX
```
Expected: the Configurator window opens (no `--play` arg). Click "Browse…" and pick a short `.mp4` or `.gif` test file, check "Play audio" if you have a paired file, choose a frequency, click "Install", confirm the success dialog, then click "Preview" — a fullscreen black overlay should appear playing your content, dismissible by any keypress or click.

- [ ] **Step 4: Manual verification — Player path directly**

Run:
```
dotnet run --project src/WindowsBootX -- --play
```
Expected: since Task 11's Install already ran, this should show the same fullscreen overlay (no Configurator window), and dismiss on its own once `Shell_TrayWnd` is found (should be near-instant on an already-running desktop) or on any keypress/click.

- [ ] **Step 5: Manual verification — fail-open and Safe Mode**

Temporarily set `ContentFilePath` in `%LOCALAPPDATA%\WindowsBootX\settings.json` to a nonexistent file path, then run `dotnet run --project src/WindowsBootX -- --play` again — expected: the process exits immediately with no window and no error dialog. Restore the valid path afterward. (Safe Mode itself is verified in Task 15's full checklist, since it requires an actual Safe Mode boot.)

- [ ] **Step 6: Commit**

```
git add -A
git commit -m "feat: wire App entry dispatch across Player, Configurator, and unregister modes"
```

---

## Task 13: Publish Profile for Self-Contained Single-File Build

**Files:**
- Modify: `src/WindowsBootX/WindowsBootX.csproj`

**Interfaces:**
- Produces: a `dotnet publish` output that is a single `WindowsBootX.exe` with no external .NET runtime dependency.

- [ ] **Step 1: Add publish properties**

Edit `src/WindowsBootX/WindowsBootX.csproj`, adding to the existing `<PropertyGroup>`:
```xml
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <SelfContained>true</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

Note: intentionally **not** setting `PublishTrimmed` — WPF's reflection-heavy XAML loading makes IL trimming unreliable and can produce runtime `XamlParseException`s that don't show up until you hit the specific trimmed-away code path. Self-contained + single-file + compression already gets a single ~70-100MB exe with zero external dependency, which is the property that actually matters here (a stranger can double-click it and it just works).

- [ ] **Step 2: Publish and verify a single file is produced**

```
dotnet publish src/WindowsBootX/WindowsBootX.csproj -c Release
```

Verify:
```
ls src/WindowsBootX/bin/Release/net10.0-windows/win-x64/publish/
```
Expected: `WindowsBootX.exe` (and possibly a `.pdb`) — no other `.dll` files required alongside it to run.

- [ ] **Step 3: Run the published exe directly**

```
& "src/WindowsBootX/bin/Release/net10.0-windows/win-x64/publish/WindowsBootX.exe"
```
Expected: the Configurator window opens, same as `dotnet run` in Task 12.

- [ ] **Step 4: Commit**

```
git add -A
git commit -m "build: configure self-contained single-file publish for WindowsBootX"
```

---

## Task 14: Inno Setup Installer Script

**Files:**
- Create: `installer/WindowsBootX.iss`

**Interfaces:**
- Consumes: the publish output from Task 13 (`src/WindowsBootX/bin/Release/net10.0-windows/win-x64/publish/WindowsBootX.exe`).
- Produces: `WindowsBootX-Setup.exe`, a per-user installer requiring no administrator privileges.

- [ ] **Step 1: Install Inno Setup**

```
winget install JRSoftware.InnoSetup
```

- [ ] **Step 2: Write the installer script**

Create `installer/WindowsBootX.iss`:
```ini
; WindowsBootX Inno Setup script — per-user install, no administrator
; privileges required. The installer only places the exe and a shortcut;
; the app registers/unregisters its own logon Scheduled Task itself
; (see ConfiguratorWindow's Install/Uninstall buttons).

#define MyAppName "WindowsBootX"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "WindowsBootX Contributors"
#define MyAppExeName "WindowsBootX.exe"

[Setup]
AppId={{6E1B6C6E-6B7B-4E9B-9E7F-6B0B7B6E6B7B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
OutputBaseFilename=WindowsBootX-Setup
OutputDir=..\installer\output
Compression=lzma2
SolidCompression=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Files]
Source: "..\src\WindowsBootX\bin\Release\net10.0-windows\win-x64\publish\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Best-effort cleanup: removes the logon Scheduled Task on uninstall even
; if the user didn't click "Uninstall" inside the app first, so nothing
; is left behind trying to launch a now-deleted exe at next sign-in.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--unregister"; Flags: runhidden skipifdoesntexist waituntilterminated
```

- [ ] **Step 3: Build the installer**

Open `installer/WindowsBootX.iss` in the Inno Setup Compiler (or run `iscc installer\WindowsBootX.iss` from a terminal with Inno Setup on PATH) after Task 13's publish step has produced `WindowsBootX.exe`.

Verify: `installer/output/WindowsBootX-Setup.exe` is produced.

- [ ] **Step 4: Manual verification — install/uninstall cycle**

Run `WindowsBootX-Setup.exe` as your normal (non-admin) user. Confirm no UAC prompt appears, the app installs under `%LOCALAPPDATA%\Programs\WindowsBootX`, and a Start Menu shortcut is created. Launch it, install the logon task via the Configurator, then uninstall via Control Panel/Settings → Apps and confirm (via `taskschd.msc`) that the `WindowsBootX\PlayAnimation` scheduled task is gone afterward.

- [ ] **Step 5: Commit**

```
git add -A
git commit -m "build: add per-user Inno Setup installer script"
```

---

## Task 15: README Finalization & Full Manual End-to-End Verification

**Files:**
- Modify: `README.md`

**Interfaces:**
- None — documentation and manual verification only.

- [ ] **Step 1: Finalize the README**

Replace the full contents of `README.md`:
```markdown
# WindowsBootX

A free, open-source Windows app that plays a custom animation (video, GIF,
or image) as a fullscreen overlay immediately after you sign in — covering
the gap before your desktop is ready, like an OEM boot animation. It never
touches BIOS, UEFI, the Windows Boot Manager, or any system file: it works
entirely through a per-user Windows Scheduled Task that launches the app
in a minimal "player" mode at sign-in.

## Features

- Supports video (`.mp4`, `.wmv`, `.avi`, `.mov`, `.mkv`), GIF, and static
  image content, with optional audio (embedded for video, a separate file
  for GIF/image).
- Two frequency modes: **Every boot** (plays once per restart) or
  **Every sign-in** (includes lock-screen unlock and sleep/hibernate wake).
- Any keypress or click instantly dismisses the overlay.
- Automatically skips itself in Windows Safe Mode.
- Fails open: a missing or corrupt animation file exits silently rather
  than blocking your sign-in.
- No administrator rights required to install, use, or uninstall.

## Installing

Download `WindowsBootX-Setup.exe` from the Releases page and run it as
your normal user account — no elevation prompt. It installs to
`%LOCALAPPDATA%\Programs\WindowsBootX`.

**Note:** this build is unsigned. Windows SmartScreen may show an
"Unknown Publisher" warning on first run — this is expected for a free,
community-built tool without a paid code-signing certificate. Click
"More info" → "Run anyway" if you trust the source you downloaded it from.

## Using it

1. Launch WindowsBootX from the Start Menu.
2. Click **Browse…** and pick your animation file.
3. Optionally enable audio and pick an audio file (for GIF/image content).
4. Choose **Every boot** or **Every sign-in**.
5. Click **Preview** to see it fullscreen before committing.
6. Click **Install**.

To stop it from playing at sign-in, open WindowsBootX again and click
**Uninstall** — or just uninstall the app itself, which cleans up the
scheduled task automatically.

## Building from source

Requires the .NET 10 SDK.

```
dotnet build
dotnet test
dotnet publish src/WindowsBootX/WindowsBootX.csproj -c Release
```

The published, self-contained single-file exe lands in
`src/WindowsBootX/bin/Release/net10.0-windows/win-x64/publish/`.

## How it works

WindowsBootX registers a per-user Windows Scheduled Task (visible under
`WindowsBootX\PlayAnimation` in Task Scheduler) that launches the app with
a `--play` flag at sign-in (and, in "Every sign-in" mode, also on
workstation unlock). That minimal player mode renders your chosen content
fullscreen, waits for the desktop shell to become ready (clamped between a
minimum and maximum wait so it never flashes or hangs), and dismisses
itself. It never runs with administrator privileges and never touches
anything outside your own user profile.

## License

MIT — see [LICENSE](LICENSE).
```

- [ ] **Step 2: Commit the README**

```
git add -A
git commit -m "docs: finalize README with usage, build, and design notes"
```

- [ ] **Step 3: Full manual end-to-end verification checklist**

Work through each of these on a real machine (not just `dotnet run`) using the installed build from Task 14. Check off each as it passes; investigate and fix (looping back to the relevant earlier task) before considering the plan complete.

- [ ] **Real reboot, "Every boot" mode:** Install with "Every boot" selected, restart the machine, sign in. The animation plays once. Sign out and back in again (same boot) — it does **not** play a second time.
- [ ] **Real reboot, "Every sign-in" mode:** Switch to "Every sign-in", sign out and back in — it plays every time, including a second consecutive sign-in without a restart.
- [ ] **Lock/unlock:** With "Every sign-in" active, lock the screen (Win+L) and unlock — the animation plays.
- [ ] **Sleep/wake:** Put the machine to sleep and wake it (with the lock screen showing on wake) — the animation plays under "Every sign-in" mode.
- [ ] **Fast user switching:** With "Every boot" active and two user accounts, sign in as User A (plays), switch to User B without restarting (plays for B's own installed instance, since this is per-user), switch back to A (does not play again — same boot).
- [ ] **Instant dismiss:** During playback, press any key or click — the overlay closes immediately.
- [ ] **Timeout fade-out:** Temporarily set `MaxDisplayMilliseconds` very low (e.g. `500`) in `settings.json` and sign in — confirm the overlay fades out cleanly even though the shell likely isn't fully ready yet, revealing the desktop underneath with no error. Restore the normal value afterward.
- [ ] **Fail-open on bad asset:** Point `ContentFilePath` at a deleted file, sign in — no overlay appears, no error dialog, sign-in proceeds normally. Restore afterward.
- [ ] **Safe Mode:** Boot into Safe Mode (`msconfig` → Boot → Safe boot, or hold Shift while restarting → Troubleshoot → Advanced options → Startup Settings) with the task installed — sign in and confirm no overlay appears. Revert the Safe Mode boot setting afterward.
- [ ] **Uninstall cleanliness:** Uninstall the app via Settings → Apps. Confirm via `taskschd.msc` that the `WindowsBootX` task folder is gone, and confirm the next sign-in shows no overlay.
- [ ] **No elevation anywhere:** Confirm neither installer, app launch, Install/Uninstall buttons, nor the Scheduled Task itself ever trigger a UAC prompt.

- [ ] **Step 4: Final commit**

```
git add -A
git commit -m "chore: complete v1 manual end-to-end verification"
```
