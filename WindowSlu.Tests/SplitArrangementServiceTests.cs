using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public sealed class SplitArrangementServiceTests
{
    private static WindowInfo W(int id, string monitor = "m") => new() { Handle = new IntPtr(id), Title = $"w{id}", MonitorId = monitor };
    private static WindowService.RECT R(int l, int t, int r, int b) => new() { Left = l, Top = t, Right = r, Bottom = b };

    [Fact]
    public void TwoWindowsUseExactHalfPartitionsForBothDirections()
    {
        var columns = SplitArrangementService.CalculateRectangles(R(-5, -3, 0, 1), 2, SplitDirection.SideBySide);
        Assert.Equal(-5, columns[0].Left); Assert.Equal(-3, columns[0].Right); Assert.Equal(-3, columns[1].Left); Assert.Equal(0, columns[1].Right);
        var rows = SplitArrangementService.CalculateRectangles(R(-5, -3, 0, 1), 2, SplitDirection.Stacked);
        Assert.Equal(-3, rows[0].Top); Assert.Equal(-1, rows[0].Bottom); Assert.Equal(-1, rows[1].Top); Assert.Equal(1, rows[1].Bottom);
    }
    [Fact]
    public void ThreeWayRemainderHasNoGapsOrOverlapIncludingNegativeCoordinates()
    {
        var area = R(-10, -7, 0, 2);
        var columns = SplitArrangementService.CalculateRectangles(area, 3, SplitDirection.SideBySide);
        Assert.Equal(new[] { -10, -7, -4, 0 }, columns.Select(x => x.Left).Append(columns[^1].Right));
        var rows = SplitArrangementService.CalculateRectangles(area, 3, SplitDirection.Stacked);
        Assert.Equal(new[] { -7, -4, -1, 2 }, rows.Select(x => x.Top).Append(rows[^1].Bottom));
    }

    [Fact]
    public void UsesFirstWindowMonitorWorkAreaAndUpdatesModelsFromActualPositions()
    {
        var native = new Fake { Monitors = new() { new() { DeviceName = "m", WorkArea = R(-100, 10, 101, 111), Bounds = R(-100, 0, 101, 120) } } };
        var items = new[] { W(1), W(2), W(3) };
        var result = new SplitArrangementService(native).Arrange(items, SplitDirection.SideBySide);
        Assert.Equal(3, result.Succeeded); Assert.Equal(0, result.Failed);
        Assert.Equal(new[] { -100, -33, 34 }, native.Moves.Select(x => x.X));
        Assert.Equal(101, items[2].Left + items[2].Width);
    }

    [Fact]
    public void FallsBackFromMissingMonitorToMonitorAtFirstWindowThenPrimaryThenFirstValid()
    {
        var native = new Fake { Monitors = new() { new() { DeviceName = "else", Bounds = R(100, 0, 200, 100), WorkArea = R(100, 0, 200, 100) }, new() { DeviceName = "located", Bounds = R(-100, 0, 0, 100), WorkArea = R(-100, 10, 0, 100) } } };
        native.Positions[new IntPtr(1)] = R(-50, 10, -20, 50);
        var result = new SplitArrangementService(native).Arrange(new[] { W(1, "gone"), W(2) }, SplitDirection.Stacked);
        Assert.Equal(2, result.Succeeded); Assert.Equal(-100, native.Moves[0].X);
    }

    [Fact]
    public void UsesCurrentMonitorInsteadOfStaleWindowInfoMonitorId()
    {
        var native = Ready();
        native.Monitors.Add(new() { DeviceName = "current", Bounds = R(200, 0, 300, 100), WorkArea = R(210, 0, 300, 100) });
        native.CurrentMonitorIds[new IntPtr(1)] = "current";
        var result = new SplitArrangementService(native).Arrange(new[] { W(1, "m"), W(2) }, SplitDirection.SideBySide);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(210, native.Moves[0].X);
    }

    [Fact]
    public void InvalidPrimaryWorkAreaFallsBackToAnotherValidMonitor()
    {
        var native = new Fake
        {
            Monitors = new()
            {
                new() { DeviceName = "primary", IsPrimary = true, Bounds = R(0, 0, 100, 100), WorkArea = R(0, 0, 0, 100) },
                new() { DeviceName = "valid", Bounds = R(100, 0, 200, 100), WorkArea = R(110, 10, 200, 100) }
            }
        };
        native.CurrentMonitorIds[new IntPtr(1)] = "removed";
        native.Positions[new IntPtr(1)] = R(300, 300, 310, 310);
        var result = new SplitArrangementService(native).Arrange(new[] { W(1, "removed"), W(2) }, SplitDirection.SideBySide);
        Assert.Equal(2, result.Succeeded);
        Assert.Equal(110, native.Moves[0].X);
    }

    [Fact]
    public void ZeroOrOneAndInvalidAreasDoNotMoveWindows()
    {
        var native = new Fake { Monitors = new() { new() { DeviceName = "m", WorkArea = R(0, 0, 1, 50) } } };
        var svc = new SplitArrangementService(native);
        Assert.Equal(1, svc.Arrange(Array.Empty<WindowInfo>(), SplitDirection.SideBySide).Unmatched);
        Assert.Equal(1, svc.Arrange(new[] { W(1) }, SplitDirection.SideBySide).Unmatched);
        Assert.Equal(2, svc.Arrange(new[] { W(1), W(2) }, SplitDirection.SideBySide).Failed);
        native.Monitors[0].WorkArea = R(0, 0, 0, 50);
        Assert.Equal(2, svc.Arrange(new[] { W(1), W(2) }, SplitDirection.SideBySide).Failed);
        Assert.Empty(native.Moves);
    }

    [Fact]
    public void EmptyAndDuplicateHandlesAreNotCountedAsSuccessfulMoves()
    {
        var native = Ready(); var a = W(1); var b = W(2);
        var result = new SplitArrangementService(native).Arrange(new[] { W(0), a, a, b }, SplitDirection.SideBySide);
        Assert.Equal(2, result.Succeeded); Assert.Equal(2, result.Failed); Assert.Equal(2, native.Moves.Count);
    }

    [Fact]
    public void PrepareFailureAndMidSequenceMoveFailureDoNotStopRemainingWindows()
    {
        var native = Ready(); native.FailPrepare.Add(new IntPtr(1)); native.FailMove.Add(new IntPtr(2));
        var result = new SplitArrangementService(native).Arrange(new[] { W(1), W(2), W(3) }, SplitDirection.SideBySide);
        Assert.Equal(1, result.Succeeded); Assert.Equal(2, result.Failed);
        Assert.DoesNotContain(native.Moves, m => m.Handle == new IntPtr(1));
        Assert.Contains(native.Moves, m => m.Handle == new IntPtr(3));
    }

    [Fact]
    public void ReadsActualRectAndFailsWhenWindowConstraintsPreventRequestedSize()
    {
        var native = Ready(); native.Clamp.Add(new IntPtr(1)); var item = W(1);
        var result = new SplitArrangementService(native).Arrange(new[] { item, W(2) }, SplitDirection.SideBySide);
        Assert.Equal(1, result.Succeeded); Assert.Equal(1, result.Failed);
        Assert.Equal(native.GetWindowPosition(item.Handle).Width, item.Width);
        Assert.NotEmpty(result.Details);
    }

    private static Fake Ready() => new() { Monitors = new() { new() { DeviceName = "m", Bounds = R(0, 0, 100, 100), WorkArea = R(0, 0, 100, 100), IsPrimary = true } } };

    private sealed class Fake : WindowService
    {
        public List<MonitorInfo> Monitors { get; set; } = new();
        public Dictionary<IntPtr, RECT> Positions { get; } = new();
        public Dictionary<IntPtr, string> CurrentMonitorIds { get; } = new();
        public HashSet<IntPtr> FailPrepare { get; } = new();
        public HashSet<IntPtr> FailMove { get; } = new();
        public HashSet<IntPtr> Clamp { get; } = new();
        public List<(IntPtr Handle, int X)> Moves { get; } = new();
        public IEnumerable<(IntPtr Handle, int X)> OrderedMoves => Moves;
        public override List<MonitorInfo> GetAllMonitors() => Monitors;
        public override string GetMonitorId(IntPtr handle) => CurrentMonitorIds.GetValueOrDefault(handle, "m");
        public override bool PrepareForArrangement(IntPtr handle) => !FailPrepare.Contains(handle);
        public override RECT GetWindowPosition(IntPtr handle) => Positions.GetValueOrDefault(handle, R(0, 0, 10, 10));
        public override bool SetWindowPosition(IntPtr handle, int x, int y, int width, int height)
        {
            Moves.Add((handle, x));
            if (FailMove.Contains(handle)) return false;
            Positions[handle] = Clamp.Contains(handle) ? R(x, y, x + Math.Max(1, width - 1), y + height) : R(x, y, x + width, y + height);
            return true;
        }
    }
}


