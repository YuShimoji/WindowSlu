using System.Runtime.InteropServices;
using WindowSlu.Models;

namespace WindowSlu.Services;

public sealed class SplitArrangementService
{
    private readonly WindowService _windows;

    public SplitArrangementService(WindowService windows) => _windows = windows ?? throw new ArgumentNullException(nameof(windows));

    public WindowOperationResult Arrange(IEnumerable<WindowInfo> windows, SplitDirection direction)
    {
        var result = new WindowOperationResult();
        if (!Enum.IsDefined(direction))
        {
            result.Failed++;
            result.Details.Add("Unknown split direction.");
            return result;
        }

        var unique = new List<WindowInfo>();
        var seen = new HashSet<IntPtr>();
        foreach (var window in windows ?? Enumerable.Empty<WindowInfo>())
        {
            if (window is null || window.Handle == IntPtr.Zero || !seen.Add(window.Handle))
            {
                result.Failed++;
                result.Details.Add("Skipped an empty or duplicate window handle.");
                continue;
            }
            unique.Add(window);
        }

        if (unique.Count < 2)
        {
            result.Unmatched++;
            result.Details.Add(unique.Count == 0 ? "At least two valid windows are required; none were available." : "At least two valid windows are required; one was available.");
            return result;
        }

        var monitors = _windows.GetAllMonitors();
        var first = unique[0];
        var currentMonitorId = _windows.GetMonitorId(first.Handle);
        var monitor = monitors.FirstOrDefault(m => Valid(m.WorkArea) && !string.IsNullOrWhiteSpace(currentMonitorId) && string.Equals(m.DeviceName, currentMonitorId, StringComparison.OrdinalIgnoreCase));
        if (monitor is null)
        {
            var firstRect = _windows.GetWindowPosition(first.Handle);
            monitor = monitors.FirstOrDefault(m => Valid(m.WorkArea) && Contains(m.Bounds, firstRect.Left, firstRect.Top));
        }
        monitor ??= monitors.FirstOrDefault(m => m.IsPrimary && Valid(m.WorkArea));
        monitor ??= monitors.FirstOrDefault(m => Valid(m.WorkArea));
        if (monitor is null || !Valid(monitor.WorkArea))
        {
            result.Failed += unique.Count;
            result.Details.Add("No monitor with a valid work area is available.");
            return result;
        }

        var rectangles = CalculateRectangles(monitor.WorkArea, unique.Count, direction);
        if (rectangles.Count != unique.Count || rectangles.Any(r => r.Width <= 0 || r.Height <= 0))
        {
            result.Failed += unique.Count;
            result.Details.Add("The monitor work area is too small for the requested split.");
            return result;
        }

        for (int i = 0; i < unique.Count; i++)
        {
            var window = unique[i];
            var target = rectangles[i];
            if (!_windows.PrepareForArrangement(window.Handle))
            {
                result.Failed++;
                result.Details.Add($"{Describe(window)} could not be restored for arrangement.");
                continue;
            }

            bool moved = _windows.SetWindowPosition(window.Handle, target.Left, target.Top, target.Width, target.Height);
            var actual = _windows.GetWindowPosition(window.Handle);
            window.Left = actual.Left;
            window.Top = actual.Top;
            window.Width = actual.Width;
            window.Height = actual.Height;
            window.MonitorId = _windows.GetMonitorId(window.Handle);

            if (moved && Same(actual, target)) result.Succeeded++;
            else
            {
                result.Failed++;
                result.Details.Add($"{Describe(window)} did not reach its assigned rectangle (expected {Format(target)}, actual {Format(actual)}).");
            }
        }
        return result;
    }

    public static IReadOnlyList<WindowService.RECT> CalculateRectangles(WindowService.RECT workArea, int count, SplitDirection direction)
    {
        if (count <= 0 || !Valid(workArea) || !Enum.IsDefined(direction)) return Array.Empty<WindowService.RECT>();
        int extent = direction == SplitDirection.SideBySide ? workArea.Width : workArea.Height;
        if (extent / (long)count <= 0) return Array.Empty<WindowService.RECT>();
        var rects = new List<WindowService.RECT>(count);
        for (int i = 0; i < count; i++)
        {
            if (direction == SplitDirection.SideBySide)
            {
                int left = Boundary(workArea.Left, workArea.Right, i, count);
                int right = Boundary(workArea.Left, workArea.Right, i + 1, count);
                rects.Add(new() { Left = left, Top = workArea.Top, Right = right, Bottom = workArea.Bottom });
            }
            else
            {
                int top = Boundary(workArea.Top, workArea.Bottom, i, count);
                int bottom = Boundary(workArea.Top, workArea.Bottom, i + 1, count);
                rects.Add(new() { Left = workArea.Left, Top = top, Right = workArea.Right, Bottom = bottom });
            }
        }
        return rects;
    }

    private static int Boundary(int start, int end, int i, int count) => (int)(start + ((long)(end - (long)start) * i / count));
    private static bool Valid(WindowService.RECT r) => r.Right > r.Left && r.Bottom > r.Top;
    private static bool Contains(WindowService.RECT r, int x, int y) => x >= r.Left && x < r.Right && y >= r.Top && y < r.Bottom;
    private static bool Same(WindowService.RECT a, WindowService.RECT b) => a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
    private static string Format(WindowService.RECT r) => $"({r.Left},{r.Top},{r.Right},{r.Bottom})";
    private static string Describe(WindowInfo w) => string.IsNullOrWhiteSpace(w.Title) ? $"Window {w.Handle}" : $"'{w.Title}'";
}

