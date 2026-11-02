using System.Reflection;
using System.Windows;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public class LinkedDragArrangementTests
{
    private static readonly MethodInfo HandleMove = typeof(LinkedDragService).GetMethod("HandleWindowMove", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void ArrangementSuppressionRefreshesBaselineAndPreservesFlags()
    {
        using var fixture = new Fixture();
        using (fixture.Service.SuspendForArrangement(new[] { fixture.A, fixture.B }))
        {
            fixture.Native.Move(fixture.A, 300, 300);
            HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        }
        fixture.Native.Move(fixture.A, 400, 400);
        HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        Assert.Equal(new Point(100, 100), fixture.Native.Position(fixture.B));
        Assert.True(fixture.Group.LinkedDragEnabled);
        Assert.True(fixture.Group.Windows.All(w => w.IncludeInLinkedDrag));
    }

    [Fact]
    public void ScopeIsNestedExceptionSafeAndIdempotent()
    {
        using var fixture = new Fixture();
        IDisposable? outer = null;
        try
        {
            outer = fixture.Service.SuspendForArrangement(new[] { fixture.A });
            using (fixture.Service.SuspendForArrangement(new[] { fixture.A }))
            {
                fixture.Native.Move(fixture.A, 200, 200);
                HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
            }
        }
        finally { outer?.Dispose(); outer?.Dispose(); }
        fixture.Native.Move(fixture.A, 350, 350);
        HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        Assert.Equal(new Point(100, 100), fixture.Native.Position(fixture.B));
    }

    [Fact]
    public void ArrangementClearsQueuedMovesAndOtherGroupStillTracks()
    {
        using var fixture = new Fixture();
        fixture.Native.Move(fixture.A, 120, 100);
        HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        using (fixture.Service.SuspendForArrangement(new[] { fixture.B })) { }
        fixture.Native.Move(fixture.C, 150, 100);
        HandleMove.Invoke(fixture.Service, new object[] { fixture.C });
        typeof(LinkedDragService).GetMethod("ProcessPendingMoves", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Service, new object?[] { null });
        Assert.Equal(new Point(100, 100), fixture.Native.Position(fixture.B));
        Assert.Equal(new Point(150, 100), fixture.Native.Position(fixture.D));
    }

    [Fact]
    public void NormalLinkedMoveResumesAfterSuppressionWindow()
    {
        using var fixture = new Fixture();
        using (fixture.Service.SuspendForArrangement(new[] { fixture.A, fixture.B }))
        {
            fixture.Native.Move(fixture.A, 120, 100);
            HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        }
        Thread.Sleep(220);
        fixture.Native.Move(fixture.A, 130, 100);
        HandleMove.Invoke(fixture.Service, new object[] { fixture.A });
        typeof(LinkedDragService).GetMethod("ProcessPendingMoves", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture.Service, new object?[] { null });
        Assert.Equal(new Point(110, 100), fixture.Native.Position(fixture.B));
    }

    private sealed class Fixture : IDisposable
    {
        public readonly IntPtr A = new(1), B = new(2), C = new(3), D = new(4);
        public readonly GroupingService Grouping = new();
        public readonly FakeWindowService Native = new();
        public readonly WindowGroup Group = new() { LinkedDragEnabled = true };
        public readonly LinkedDragService Service;
        public Fixture()
        {
            foreach (var h in new[] { A, B, C, D }) Native.Move(h, 100, 100);
            foreach (var h in new[] { A, B, C, D }) Group.Windows.Add(new WindowInfo { Handle = h, IncludeInLinkedDrag = true });
            Grouping.Groups.Add(Group);
            Service = new LinkedDragService(Grouping, Native, enableHook: false);
            foreach (var h in new[] { A, B, C, D }) HandleMove.Invoke(Service, new object[] { h });
            Group.Windows.Remove(Group.Windows[2]); Group.Windows.Remove(Group.Windows[2]);
            var second = new WindowGroup { LinkedDragEnabled = true };
            second.Windows.Add(new WindowInfo { Handle = C, IncludeInLinkedDrag = true });
            second.Windows.Add(new WindowInfo { Handle = D, IncludeInLinkedDrag = true });
            Grouping.Groups.Add(second);
        }
        public void Dispose() => Service.Dispose();
    }

    private sealed class FakeWindowService : WindowService
    {
        private readonly Dictionary<IntPtr, Point> _positions = new();
        public void Move(IntPtr h, int x, int y) => _positions[h] = new Point(x, y);
        public Point Position(IntPtr h) => _positions[h];
        public override RECT GetWindowPosition(IntPtr h)
        {
            var p = _positions[h];
            return new RECT { Left = (int)p.X, Top = (int)p.Y, Right = (int)p.X + 100, Bottom = (int)p.Y + 100 };
        }
        public override bool SetWindowPosition(IntPtr h, int x, int y, int width, int height) { Move(h, x, y); return true; }
    }
}
