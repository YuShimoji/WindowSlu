using System.Collections.ObjectModel;
using System.IO;
using WindowSlu.Models;

namespace WindowSlu.Services;

public class PresetService
{
    private readonly string _presetFilePath;
    private readonly WindowService _windowService;
    private readonly GroupingService _groupingService;
    public ObservableCollection<WindowPreset> Presets { get; } = new();
    public string? LastError { get; private set; }
    public WindowOperationResult LastResult { get; private set; } = new();

    public PresetService(WindowService windowService, GroupingService groupingService, string? presetFilePath = null)
    {
        _windowService = windowService;
        _groupingService = groupingService;
        _presetFilePath = presetFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSlu", "presets.json");
        LoadPresets();
    }

    public void LoadPresets()
    {
        var data = JsonFileStore.Load<PresetData>(_presetFilePath, out var error);
        LastError = error;
        if (data == null) return;
        Presets.Clear();
        foreach (var preset in data.Presets ?? new()) Presets.Add(preset);
        _groupingService.RestoreManualGroups(data.ManualGroups ?? new());
    }

    public bool SavePresets()
    {
        bool saved = JsonFileStore.Save(_presetFilePath, new PresetData
        {
            Presets = Presets.ToList(), ManualGroups = _groupingService.GetManualGroupsData()
        }, out var error);
        LastError = error;
        return saved;
    }

    public WindowPreset CreatePreset(string name)
    {
        var preset = new WindowPreset { Name = name, Width = 800, Height = 600, Opacity = 100, IsTopMost = false };
        Presets.Add(preset);
        SavePresets();
        return preset;
    }

    public bool DeletePreset(string presetId)
    {
        var preset = GetPresetById(presetId);
        if (preset == null) return false;
        Presets.Remove(preset);
        return SavePresets();
    }

    public void UpdatePreset(WindowPreset preset) { preset.ModifiedAt = DateTime.Now; SavePresets(); }
    public WindowPreset? GetPresetById(string presetId) => Presets.FirstOrDefault(p => p.Id == presetId);

    public bool MatchesTargetFilter(WindowPreset preset, WindowInfo window) =>
        (string.IsNullOrWhiteSpace(preset.TargetProcessName) ||
         string.Equals(window.ProcessName, preset.TargetProcessName, StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(preset.TargetGroupId) ||
         string.Equals(window.GroupId, preset.TargetGroupId, StringComparison.OrdinalIgnoreCase));

    public bool MatchesTargetFilter(WindowPreset preset, WindowGroup group) =>
        (string.IsNullOrWhiteSpace(preset.TargetGroupId) || string.Equals(group.Id, preset.TargetGroupId, StringComparison.OrdinalIgnoreCase)) &&
        group.Windows.Any(w => MatchesTargetFilter(preset, w));

    // Used by the Apply button, tray menu and preset hotkeys. Selection narrows, never bypasses, the filter.
    public WindowOperationResult ApplyPreset(WindowPreset preset, IEnumerable<WindowGroup> groups,
        WindowGroup? selectedGroup = null, WindowInfo? selectedWindow = null)
    {
        var result = new WindowOperationResult();
        var seen = new HashSet<IntPtr>();
        var targets = selectedGroup == null ? groups.ToList() : new List<WindowGroup> { selectedGroup };
        if (selectedWindow != null)
        {
            if (MatchesTargetFilter(preset, selectedWindow)) ApplyWindows(preset, new[] { selectedWindow }, result, seen);
        }
        else
        {
            foreach (var group in targets)
                ApplyWindows(preset, group.Windows.Where(w => MatchesTargetFilter(preset, w)).ToList(), result, seen);
        }
        if (seen.Count == 0) { result.Unmatched = 1; result.Details.Add("No windows matched the selection and target filter."); }
        LastResult = result;
        return result;
    }

    public void ApplyPresetToGroup(WindowPreset preset, WindowGroup group) => ApplyPreset(preset, new[] { group });
    public void ApplyPresetToWindow(WindowPreset preset, WindowInfo window) => ApplyPreset(preset, Array.Empty<WindowGroup>(), selectedWindow: window);
    public int ApplyPresetWithFilter(WindowPreset preset, IEnumerable<WindowGroup> groups) => ApplyPreset(preset, groups).Succeeded;

    private void ApplyWindows(WindowPreset preset, IEnumerable<WindowInfo> candidates, WindowOperationResult result, HashSet<IntPtr> seen)
    {
        var windows = candidates.Where(w => seen.Add(w.Handle)).ToList();
        if (windows.Count == 0) return;
        int x = preset.CascadeStartX ?? _windowService.GetWindowPosition(windows[0].Handle).Left;
        int y = preset.CascadeStartY ?? _windowService.GetWindowPosition(windows[0].Handle).Top;
        foreach (var window in windows)
        {
            // Validate the entire request before changing this window.
            if (preset.Width is <= 0 || preset.Height is <= 0 || preset.Opacity is < 0 or > 100)
            {
                result.Failed++; result.Details.Add($"Invalid dimensions or opacity: {window.Title}"); continue;
            }
            bool success = true;
            if (preset.Opacity.HasValue)
            {
                bool changed = _windowService.SetTransparency(window.Handle, preset.Opacity.Value);
                success &= changed;
                if (changed) window.Opacity = _windowService.GetTransparency(window.Handle);
            }
            if (preset.IsTopMost.HasValue)
            {
                bool changed = _windowService.SetTopMost(window.Handle, preset.IsTopMost.Value);
                success &= changed;
                if (changed) window.IsTopMost = _windowService.IsTopMost(window.Handle);
            }
            if (preset.EnableCascade || preset.Width.HasValue || preset.Height.HasValue)
            {
                var current = _windowService.GetWindowPosition(window.Handle);
                int width = preset.Width ?? current.Width, height = preset.Height ?? current.Height;
                bool moved = preset.EnableCascade
                    ? _windowService.SetWindowPosition(window.Handle, x, y, width, height)
                    : _windowService.SetWindowSize(window.Handle, width, height);
                success &= moved;
                if (moved) _windowService.UpdateWindowPositionInfo(window);
            }
            if (success) result.Succeeded++;
            else { result.Failed++; result.Details.Add($"Could not apply all settings: {window.Title}"); }
            x += preset.CascadeDirection switch { CascadeDirection.BottomLeft or CascadeDirection.TopLeft => -preset.CascadeOffsetX, CascadeDirection.Down => 0, _ => preset.CascadeOffsetX };
            y += preset.CascadeDirection switch { CascadeDirection.TopLeft or CascadeDirection.TopRight => -preset.CascadeOffsetY, CascadeDirection.Right => 0, _ => preset.CascadeOffsetY };
        }
    }

    public WindowPreset CreatePresetFromWindow(WindowInfo window, string presetName)
    {
        _windowService.UpdateWindowPositionInfo(window);
        var preset = new WindowPreset
        {
            Name = presetName, TargetProcessName = window.ProcessName, Width = window.Width, Height = window.Height,
            Opacity = _windowService.GetTransparency(window.Handle), IsTopMost = _windowService.IsTopMost(window.Handle)
        };
        Presets.Add(preset); SavePresets(); return preset;
    }

    public WindowPreset CreatePresetFromGroup(WindowGroup group, string presetName)
    {
        var window = group.Windows.FirstOrDefault();
        if (window == null) throw new ArgumentException("The group has no windows.", nameof(group));
        var preset = CreatePresetFromWindow(window, presetName);
        preset.TargetProcessName = group.Type == GroupType.AutoByProcess ? group.ProcessNameFilter : null;
        preset.TargetGroupId = group.Id;
        preset.EnableCascade = true;
        SavePresets(); return preset;
    }
}
