using System.IO;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public sealed class WindowServicesTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "WindowSlu.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath(string name) => Path.Combine(_directory, name);
    private static WindowInfo W(int handle, string title, string process = "editor") => new()
    { Handle = new(handle), Title = title, ProcessName = process, Width = 800, Height = 600, Opacity = 100 };
    private static WindowLayoutEntry E(string title, int x = 100) => new()
    { ProcessName = "editor", TitlePattern = title, Left = x, Top = 100, Width = 800, Height = 600 };

    [Fact]
    public void RestoreReservesExactMatchesBeforeFallbackAndNeverReusesAWindow()
    {
        var native = new FakeWindows();
        var service = new LayoutService(native, FilePath("layouts.json"));
        var layout = new WindowLayout { Entries = new() { E("missing", 300), E("exact", 500) } };
        Assert.Equal(1, service.RestoreLayout(layout, new[] { W(1, "exact") }));
        Assert.Equal((new IntPtr(1), 500), native.Moves.Single());
        Assert.Equal(1, service.LastRestoreResult.Unmatched);
    }

    [Fact]
    public void RestoreSameProcessWindowsOneToOneRegardlessOfCurrentOrder()
    {
        var native = new FakeWindows();
        var service = new LayoutService(native, FilePath("layouts.json"));
        Assert.Equal(2, service.RestoreLayout(new() { Entries = new() { E("one", 100), E("two", 200) } },
            new[] { W(2, "two"), W(1, "one"), W(1, "one") }));
        Assert.Equal(2, native.Moves.Select(m => m.Handle).Distinct().Count());
        Assert.Contains((new IntPtr(1), 100), native.Moves);
        Assert.Contains((new IntPtr(2), 200), native.Moves);
    }

    [Fact]
    public void RestoreSkipsCompetingPartialMatchesInsteadOfChoosingByEntryOrder()
    {
        var native = new FakeWindows();
        var service = new LayoutService(native, FilePath("layouts.json"));
        Assert.Equal(0, service.RestoreLayout(new() { Entries = new() { E("alpha", 100), E("beta", 200) } }, new[] { W(1, "alpha beta") }));
        Assert.Equal(2, service.LastRestoreResult.Ambiguous);
        Assert.Empty(native.Moves);
    }

    [Fact]
    public void RestoreSkipsAmbiguousSameTitleWindows()
    {
        var native = new FakeWindows();
        var service = new LayoutService(native, FilePath("layouts.json"));
        Assert.Equal(0, service.RestoreLayout(new() { Entries = new() { E("same"), E("same") } }, new[] { W(1, "same"), W(2, "same") }));
        Assert.Equal(2, service.LastRestoreResult.Ambiguous);
        Assert.Empty(native.Moves);
    }

    [Fact]
    public void RestoreDoesNotCountFailedOrInvalidOperationsAsSuccess()
    {
        var native = new FakeWindows { FailMove = true };
        var service = new LayoutService(native, FilePath("layouts.json"));
        var window = W(1, "one");
        Assert.Equal(0, service.RestoreLayout(new() { Entries = new() { E("one", 400) } }, new[] { window }));
        Assert.Equal(1, service.LastRestoreResult.Failed);
        Assert.Equal(0, window.Left);
        var invalid = E("one"); invalid.Width = -1;
        native.Moves.Clear();
        Assert.Equal(0, service.RestoreLayout(new() { Entries = new() { invalid } }, new[] { window }));
        Assert.Empty(native.Moves);
    }

    [Fact]
    public void MissingMonitorRestoresInsidePrimaryWorkAreaAndFitsOversizedWindows()
    {
        var entry = E("one", 4000); entry.Top = -1000; entry.Width = 5000; entry.Height = 3000; entry.MonitorId = "removed";
        var rect = LayoutService.FitToMonitor(entry, new[] { new MonitorInfo
        { DeviceName = "primary", IsPrimary = true, WorkArea = new() { Left = -1920, Top = 0, Right = 0, Bottom = 1040 } } });
        Assert.Equal(-1920, rect.Left); Assert.Equal(0, rect.Top);
        Assert.Equal(1920, rect.Width); Assert.Equal(1040, rect.Height);
    }

    [Fact]
    public void SelectionCannotBypassPresetProcessOrGroupFilter()
    {
        var native = new FakeWindows(); var groups = new GroupingService();
        var editor = W(1, "one"); var browser = W(2, "two", "browser");
        groups.GroupByProcess(new[] { editor, browser });
        var service = new PresetService(native, groups, FilePath("presets.json"));
        var preset = new WindowPreset { TargetProcessName = "editor", Opacity = 80 };
        Assert.Equal(0, service.ApplyPreset(preset, groups.Groups, selectedWindow: browser).Succeeded);
        Assert.Equal(0, service.ApplyPreset(preset, groups.Groups, groups.GetGroupByProcessName("browser")).Succeeded);
        Assert.Empty(native.OpacityChanges);
        preset.TargetGroupId = "missing-id";
        Assert.Equal(0, service.ApplyPreset(preset, groups.Groups, selectedWindow: editor).Succeeded);
    }

    [Fact]
    public void PresetFiltersIndividualMembersOfMixedManualGroup()
    {
        var native = new FakeWindows(); var groups = new GroupingService();
        var editor = W(1, "one"); var browser = W(2, "two", "browser");
        var group = groups.CreateManualGroup("Mixed", new[] { editor, browser });
        var service = new PresetService(native, groups, FilePath("presets.json"));
        var preset = new WindowPreset { TargetProcessName = "editor", TargetGroupId = group.Id, Opacity = 80 };
        Assert.Equal(1, service.ApplyPreset(preset, groups.Groups).Succeeded);
        Assert.Single(native.OpacityChanges); Assert.Equal(80, editor.Opacity); Assert.Equal(100, browser.Opacity);
    }

    [Fact]
    public void PresetFailureLeavesModelAtActualOpacityAndReportsFailure()
    {
        var native = new FakeWindows { FailOpacity = true }; var groups = new GroupingService();
        var window = W(1, "one"); groups.GroupByProcess(new[] { window });
        var service = new PresetService(native, groups, FilePath("presets.json"));
        var result = service.ApplyPreset(new() { Opacity = 80 }, groups.Groups);
        Assert.Equal(0, result.Succeeded); Assert.Equal(1, result.Failed); Assert.Equal(100, window.Opacity);
    }

    [Fact]
    public void PresetsAndManualMembershipSurviveSaveAndNewServiceInstances()
    {
        var native = new FakeWindows(); var groups = new GroupingService();
        var group = groups.CreateManualGroup("Work", new[] { W(1, "one") });
        var original = new PresetService(native, groups, FilePath("presets.json"));
        var preset = original.CreatePreset("Work preset"); preset.TargetGroupId = group.Id;
        Assert.True(original.SavePresets());
        var restoredGroups = new GroupingService();
        var restored = new PresetService(native, restoredGroups, FilePath("presets.json"));
        restoredGroups.UpdateGroups(new[] { W(10, "one"), W(11, "other") });
        Assert.Equal(group.Id, restored.Presets.Single().TargetGroupId);
        Assert.Equal("one", restoredGroups.GetGroupById(group.Id)!.Windows.Single().Title);
    }

    [Fact]
    public void AtomicSaveKeepsPreviousVersionAndCorruptLoadKeepsOriginal()
    {
        string path = FilePath("settings.json");
        Assert.True(JsonFileStore.Save(path, new ApplicationSettings { BulkOpacityPercent = 70 }, out _));
        Assert.True(JsonFileStore.Save(path, new ApplicationSettings { BulkOpacityPercent = 80 }, out _));
        Assert.Equal(70, JsonFileStore.Load<ApplicationSettings>(path + ".bak", out _)!.BulkOpacityPercent);
        File.WriteAllText(path, "broken-json");
        var settings = new SettingsService(path);
        Assert.NotNull(settings.LastError); Assert.Equal(70, settings.Settings.BulkOpacityPercent);
        Assert.Equal("broken-json", File.ReadAllText(path));
        Assert.NotEmpty(Directory.GetFiles(_directory, "*.corrupt-*"));
    }

    [Fact]
    public void SaveFailureIsReturnedWithoutReplacingOriginal()
    {
        string path = FilePath("settings.json");
        var settings = new SettingsService(path);
        string original = File.ReadAllText(path);
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            settings.Settings.BulkOpacityPercent = 20;
            Assert.False(settings.SaveSettings()); Assert.NotNull(settings.LastError);
            Assert.Equal(original, File.ReadAllText(path));
        }
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void InvalidSettingsReturnSaveErrorInsteadOfThrowingOrReplacingFile()
    {
        var settings = new SettingsService(FilePath("invalid-settings.json"));
        settings.Settings.WindowLeft = double.NaN;
        Assert.False(settings.SaveSettings());
        Assert.NotNull(settings.LastError);
        Assert.True(double.IsFinite(JsonFileStore.Load<ApplicationSettings>(FilePath("invalid-settings.json"), out _)!.WindowLeft));
    }

    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}

internal sealed class FakeWindows : WindowService
{
    public bool FailMove { get; set; }
    public bool FailOpacity { get; set; }
    public List<(IntPtr Handle, int X)> Moves { get; } = new();
    public Dictionary<IntPtr, int> OpacityChanges { get; } = new();
    private readonly Dictionary<IntPtr, RECT> _positions = new();
    private readonly Dictionary<IntPtr, bool> _top = new();
    public override RECT GetWindowPosition(IntPtr handle) => _positions.GetValueOrDefault(handle,
        new RECT { Left = 0, Top = 0, Right = 800, Bottom = 600 });
    public override bool SetWindowPosition(IntPtr handle, int x, int y, int width, int height)
    {
        Moves.Add((handle, x));
        if (FailMove) return false;
        _positions[handle] = new() { Left = x, Top = y, Right = x + width, Bottom = y + height }; return true;
    }
    public override bool SetWindowSize(IntPtr handle, int width, int height) => SetWindowPosition(handle, 0, 0, width, height);
    public override int GetTransparency(IntPtr handle) => OpacityChanges.GetValueOrDefault(handle, 100);
    public override bool SetTransparency(IntPtr handle, int opacity)
    { if (FailOpacity) return false; OpacityChanges[handle] = opacity; return true; }
    public override bool SetTopMost(IntPtr handle, bool enabled) { _top[handle] = enabled; return true; }
    public override bool IsTopMost(IntPtr handle) => _top.GetValueOrDefault(handle);
    public override string GetMonitorId(IntPtr handle) => "primary";
    public override List<MonitorInfo> GetAllMonitors() => new();
}
