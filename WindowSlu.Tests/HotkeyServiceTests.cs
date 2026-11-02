using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public sealed class HotkeyServiceTests : IDisposable
{
    private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WindowSlu.HotkeyTests", Guid.NewGuid().ToString("N"));
    private SettingsService CreateSettings() => new(System.IO.Path.Combine(_directory, "settings.json"));

    [Fact]
    public void ReloadRegistersRegularAndPresetHotkeys()
    {
        var settings = CreateSettings();
        var platform = new FakePlatform();
        using var service = new HotkeyService(IntPtr.Zero, settings, null, platform);
        Assert.True(service.RegisterPresetHotkey("preset-1", "Ctrl+Alt+F1"));
        Assert.Equal(6, platform.Active.Count);

        service.ReloadSettings();

        Assert.Equal(6, platform.Active.Count);
        Assert.Contains(platform.Active.Values, key => key == (uint)KeyInterop.VirtualKeyFromKey(Key.F1));
        Assert.Empty(service.LastRegistrationErrors);
    }

    [Fact]
    public void ConflictReturnsFalseAndPublishesKeyAndReason()
    {
        var settings = CreateSettings();
        var platform = new FakePlatform();
        using var service = new HotkeyService(IntPtr.Zero, settings, null, platform);
        platform.FailNext = true;

        Assert.False(service.RegisterPresetHotkey("busy", "Ctrl+Alt+F2"));

        var error = Assert.Single(service.LastRegistrationErrors);
        Assert.Contains("F2", error);
        Assert.Contains("Win32Error: 1409", error);
    }

    [Fact]
    public void FailedReplacementKeepsOldAssignmentAndRegistration()
    {
        var settings = CreateSettings();
        var platform = new FakePlatform();
        using var service = new HotkeyService(IntPtr.Zero, settings, null, platform);
        Assert.True(service.RegisterPresetHotkey("preset", "Ctrl+Alt+F3"));
        var previousId = platform.Active.Single(pair => pair.Value == (uint)KeyInterop.VirtualKeyFromKey(Key.F3)).Key;
        platform.FailNext = true;

        Assert.False(service.RegisterPresetHotkey("preset", "Ctrl+Alt+F4"));

        Assert.Contains(previousId, platform.Active.Keys);
        Assert.True(service.RegisterPresetHotkey("preset", "Ctrl+Alt+F3"));
    }

    [Fact]
    public void UnregisterAndReenableDoNotDuplicateRegistrations()
    {
        var settings = CreateSettings();
        var platform = new FakePlatform();
        using var service = new HotkeyService(IntPtr.Zero, settings, null, platform);
        Assert.True(service.RegisterPresetHotkey("preset", "Ctrl+Alt+F5"));
        Assert.Equal(6, platform.Active.Count);

        service.UnregisterPresetHotkey("preset");
        Assert.Equal(5, platform.Active.Count);
        Assert.True(service.RegisterPresetHotkey("preset", "Ctrl+Alt+F5"));
        service.ReloadSettings();
        Assert.Equal(6, platform.Active.Count);
    }

    [Fact]
    public void SameAssignmentRetriesAfterReloadRegistrationFailure()
    {
        var settings = CreateSettings();
        var platform = new FakePlatform();
        using var service = new HotkeyService(IntPtr.Zero, settings, null, platform);
        Assert.True(service.RegisterPresetHotkey("retry", "Ctrl+Alt+F6"));
        platform.FailVirtualKey = (uint)KeyInterop.VirtualKeyFromKey(Key.F6);

        service.ReloadSettings();
        Assert.Contains(service.LastRegistrationErrors, error => error.Contains("F6", StringComparison.Ordinal));

        Assert.True(service.RegisterPresetHotkey("retry", "Ctrl+Alt+F6"));
        Assert.Contains(platform.Active.Values, key => key == (uint)KeyInterop.VirtualKeyFromKey(Key.F6));
        Assert.Empty(service.LastRegistrationErrors);
    }

    [Fact]
    public void NativePlatformRegistersAndUnregistersThreadHotkey()
    {
        var settings = CreateSettings();
        settings.Settings.HotkeySettings.Clear();
        Assert.True(settings.SaveSettings());

        using var service = new HotkeyService(IntPtr.Zero, settings, null);
        var registered = service.RegisterPresetHotkey("native-smoke", "Ctrl+Alt+Shift+Win+F24");

        Assert.True(registered, string.Join(Environment.NewLine, service.LastRegistrationErrors));
        service.UnregisterPresetHotkey("native-smoke");
        Assert.Empty(service.LastRegistrationErrors);
    }

    public void Dispose() => System.IO.Directory.Delete(_directory, true);

    private sealed class FakePlatform : IHotkeyPlatform
    {
        private readonly Dictionary<int, uint> _active = new();
        public IReadOnlyDictionary<int, uint> Active => _active;
        public bool FailNext { get; set; }
        public uint? FailVirtualKey { get; set; }
        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey)
        {
            if (FailNext) { FailNext = false; return false; }
            if (FailVirtualKey == virtualKey) { FailVirtualKey = null; return false; }
            if (_active.Values.Contains(virtualKey)) return false;
            _active.Add(id, virtualKey);
            return true;
        }
        public bool UnregisterHotKey(IntPtr windowHandle, int id) => _active.Remove(id);
        public int GetLastError() => 1409;
    }
}
