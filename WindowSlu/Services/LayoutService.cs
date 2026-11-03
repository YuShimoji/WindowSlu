using System.Collections.ObjectModel;
using System.IO;
using WindowSlu.Models;

namespace WindowSlu.Services;

public class LayoutService : System.ComponentModel.INotifyPropertyChanged
{
    private readonly WindowService _windowService;
    private readonly string _layoutFilePath;
    public ObservableCollection<WindowLayout> Layouts { get; } = new();
    public string? LastError { get; private set; }
    public WindowOperationResult LastRestoreResult { get; private set; } = new();

    public LayoutService(WindowService windowService, string? layoutFilePath = null)
    {
        _windowService = windowService;
        _layoutFilePath = layoutFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WindowSlu", "layouts.json");
        LoadLayouts();
    }

    public void LoadLayouts()
    {
        var data = JsonFileStore.Load<LayoutData>(_layoutFilePath, out var error);
        LastError = error;
        if (data == null) return;
        Layouts.Clear();
        foreach (var layout in data.Layouts ?? new()) Layouts.Add(layout);
    }

    public bool SaveLayouts()
    {
        bool saved = JsonFileStore.Save(_layoutFilePath, new LayoutData { Layouts = Layouts.ToList() }, out var error);
        LastError = error; return saved;
    }

    public WindowLayout CreateLayout(string name)
    {
        var layout = new WindowLayout { Name = name };
        Layouts.Add(layout); SaveLayouts(); return layout;
    }

    private List<WindowLayoutEntry> CaptureEntries(IEnumerable<WindowInfo> windows) => windows.Select(window =>
    {
        _windowService.UpdateWindowPositionInfo(window);
        return new WindowLayoutEntry
        {
            ProcessName = window.ProcessName, TitlePattern = window.Title,
            Left = window.Left, Top = window.Top, Width = window.Width, Height = window.Height,
            Opacity = _windowService.GetTransparency(window.Handle), IsTopMost = _windowService.IsTopMost(window.Handle),
            MonitorId = window.MonitorId
        };
    }).ToList();

    public WindowLayout CaptureCurrentLayout(string name, IEnumerable<WindowInfo> windows)
    {
        var layout = new WindowLayout { Name = name, Entries = CaptureEntries(windows) };
        Layouts.Add(layout); SaveLayouts(); return layout;
    }

    private readonly Stack<(WindowLayout Item, int Index)> _deleted = new();
    public bool CanUndoDeletion => _deleted.Count > 0;
    public string UndoDescription => CanUndoDeletion ? $"Undo delete '{_deleted.Peek().Item.Name}'" : "Nothing to undo";
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void NotifyUndo()
    {
        PropertyChanged?.Invoke(this, new(nameof(CanUndoDeletion)));
        PropertyChanged?.Invoke(this, new(nameof(UndoDescription)));
    }
    public bool DeleteLayout(string id)
    {
        var item = Layouts.FirstOrDefault(l => l.Id == id);
        if (item == null) return false;
        int index = Layouts.IndexOf(item);
        Layouts.RemoveAt(index);
        if (!SaveLayouts()) { Layouts.Insert(index, item); return false; }
        _deleted.Push((item, index)); NotifyUndo(); return true;
    }
    public bool UndoDelete(out WindowLayout? restored)
    {
        restored = null;
        if (!CanUndoDeletion) return false;
        var entry = _deleted.Peek();
        Layouts.Insert(Math.Min(entry.Index, Layouts.Count), entry.Item);
        if (!SaveLayouts()) { Layouts.Remove(entry.Item); return false; }
        restored = entry.Item; _deleted.Pop(); NotifyUndo(); return true;
    }

    public void UpdateLayout(WindowLayout layout, IEnumerable<WindowInfo> windows)
    {
        layout.Entries = CaptureEntries(windows);
        layout.ModifiedAt = DateTime.Now;
        SaveLayouts();
    }

    public int RestoreLayout(WindowLayout layout, IEnumerable<WindowInfo> currentWindows)
    {
        var result = new WindowOperationResult();
        LastRestoreResult = result;
        var available = currentWindows.GroupBy(w => w.Handle).Select(g => g.First()).ToList();
        var assignments = new Dictionary<WindowLayoutEntry, WindowInfo>();
        var pending = layout.Entries.Distinct().ToList();
        var ambiguous = new HashSet<WindowLayoutEntry>();
        void MatchStage(Func<WindowLayoutEntry, WindowInfo, bool> matches)
        {
            var candidates = pending.ToDictionary(entry => entry, entry => available.Where(w => matches(entry, w)).ToList());
            var claimCounts = candidates.Values.SelectMany(list => list).GroupBy(w => w.Handle).ToDictionary(g => g.Key, g => g.Count());
            var blockedHandles = new HashSet<IntPtr>();
            foreach (var (entry, possible) in candidates)
            {
                if (possible.Count > 1 || possible.Any(w => claimCounts[w.Handle] > 1))
                {
                    ambiguous.Add(entry); pending.Remove(entry);
                    foreach (var window in possible) blockedHandles.Add(window.Handle);
                }
                else if (possible.Count == 1)
                { assignments[entry] = possible[0]; pending.Remove(entry); available.Remove(possible[0]); }
            }
            // Do not let a weaker match consume a window already involved in an ambiguity.
            available.RemoveAll(w => blockedHandles.Contains(w.Handle));
        }
        MatchStage((entry, w) => SameProcess(entry, w) && string.Equals(w.Title, entry.TitlePattern, StringComparison.OrdinalIgnoreCase));
        MatchStage((entry, w) => SameProcess(entry, w) &&
            (string.IsNullOrWhiteSpace(entry.TitlePattern) || w.Title.Contains(entry.TitlePattern, StringComparison.OrdinalIgnoreCase)));
        MatchStage((entry, w) => SameProcess(entry, w) &&
            layout.Entries.Count(e => string.Equals(e.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase)) == 1);
        foreach (var entry in ambiguous)
        { result.Ambiguous++; result.Details.Add($"Ambiguous: {entry.ProcessName} / {entry.TitlePattern}"); }
        foreach (var entry in pending)
        {
            if (available.Count(w => SameProcess(entry, w)) > 1)
            { result.Ambiguous++; result.Details.Add($"Ambiguous: {entry.ProcessName} / {entry.TitlePattern}"); }
            else { result.Unmatched++; result.Details.Add($"Not found: {entry.ProcessName} / {entry.TitlePattern}"); }
        }
        var monitors = _windowService.GetAllMonitors();
        foreach (var (entry, window) in assignments)
        {
            if (entry.Width <= 0 || entry.Height <= 0 || entry.Opacity is < 0 or > 100)
            { result.Failed++; result.Details.Add($"Invalid saved dimensions or opacity: {entry.TitlePattern}"); continue; }
            var bounds = FitToMonitor(entry, monitors);
            bool success = _windowService.SetWindowPosition(window.Handle, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            if (success) _windowService.UpdateWindowPositionInfo(window);
            if (entry.Opacity.HasValue)
            {
                bool changed = _windowService.SetTransparency(window.Handle, entry.Opacity.Value);
                success &= changed;
                if (changed) window.Opacity = _windowService.GetTransparency(window.Handle);
            }
            if (entry.IsTopMost.HasValue)
            {
                bool changed = _windowService.SetTopMost(window.Handle, entry.IsTopMost.Value);
                success &= changed;
                if (changed) window.IsTopMost = _windowService.IsTopMost(window.Handle);
            }
            if (success) result.Succeeded++;
            else { result.Failed++; result.Details.Add($"Could not restore all settings: {window.Title}"); }
        }
        return result.Succeeded;
    }

    private static bool SameProcess(WindowLayoutEntry entry, WindowInfo window) =>
        string.Equals(entry.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase);

    public static WindowService.RECT FitToMonitor(WindowLayoutEntry entry, IReadOnlyList<MonitorInfo> monitors)
    {
        var monitor = monitors.FirstOrDefault(m => string.Equals(m.DeviceName, entry.MonitorId, StringComparison.OrdinalIgnoreCase))
            ?? monitors.FirstOrDefault(m => entry.Left >= m.WorkArea.Left && entry.Left < m.WorkArea.Right && entry.Top >= m.WorkArea.Top && entry.Top < m.WorkArea.Bottom)
            ?? monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
        if (monitor == null) return new() { Left = entry.Left, Top = entry.Top, Right = entry.Left + entry.Width, Bottom = entry.Top + entry.Height };
        var area = monitor.WorkArea;
        int width = Math.Clamp(entry.Width, 1, Math.Max(1, area.Width));
        int height = Math.Clamp(entry.Height, 1, Math.Max(1, area.Height));
        int x = Math.Clamp(entry.Left, area.Left, Math.Max(area.Left, area.Right - width));
        int y = Math.Clamp(entry.Top, area.Top, Math.Max(area.Top, area.Bottom - height));
        return new() { Left = x, Top = y, Right = x + width, Bottom = y + height };
    }
}
