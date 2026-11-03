using System.Runtime.ExceptionServices;
using System.Windows.Threading;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

[Collection("Native desktop")]
public class NativeSplitArrangementTests
{
    [Fact]
    public void SplitMovesOnlyFixtureWindowsAndLinkedDragResumesAfterArrangement()
    {
        RunSta(() =>
        {
            using var a = new NativeTestWindow();
            using var b = new NativeTestWindow();
            using var c = new NativeTestWindow();
            using var outsider = new NativeTestWindow();
            var native = new TestMonitorWindows();
            var windows = new[] { a, b, c }.Select((fixture, index) => new WindowInfo
            { Handle = fixture.Handle, Title = "Native split " + index, ProcessName = "fixture", IncludeInLinkedDrag = true }).ToList();
            foreach (var window in windows)
                Assert.True(native.SetWindowPosition(window.Handle, -30000, -30000, 500, 400));
            Assert.True(native.SetWindowPosition(outsider.Handle, -31000, -31000, 100, 100));
            var outsideBefore = native.GetWindowPosition(outsider.Handle);
            Assert.True(native.SetTransparency(a.Handle, 80));
            var grouping = new GroupingService();
            var group = grouping.CreateManualGroup("Native fixtures", windows);
            group.LinkedDragEnabled = true;
            using var linked = new LinkedDragService(grouping, native);
            foreach (var window in windows)
                Assert.True(native.SetWindowPosition(window.Handle, -29998, -30000, 500, 400));
            Pump(TimeSpan.FromMilliseconds(150));

            var split = new SplitArrangementService(native);
            foreach (var direction in new[] { SplitDirection.SideBySide, SplitDirection.Stacked })
            {
                using (linked.SuspendForArrangement(windows.Select(w => w.Handle)))
                {
                    var result = split.Arrange(windows, direction);
                    Assert.Equal(3, result.Succeeded);
                    Assert.Equal(0, result.Failed);
                }
                // Give actual WinEvents and the linked-drag timer time to run after scope disposal.
                Pump(TimeSpan.FromMilliseconds(350));
                var expected = SplitArrangementService.CalculateRectangles(TestMonitorWindows.Area, 3, direction);
                for (int i = 0; i < windows.Count; i++) AssertRect(expected[i], native.GetWindowPosition(windows[i].Handle));
                AssertRect(outsideBefore, native.GetWindowPosition(outsider.Handle));
                Assert.Equal(80, native.GetTransparency(a.Handle));
                Assert.True(group.LinkedDragEnabled);
            }

            // Prove that a real native move is observed and followers still move after suppression.
            var before = windows.Select(w => native.GetWindowPosition(w.Handle)).ToList();
            var leader = before[0];
            Assert.True(native.SetWindowPosition(a.Handle, leader.Left + 10, leader.Top + 5, leader.Width, leader.Height));
            Pump(TimeSpan.FromMilliseconds(400));
            for (int i = 1; i < windows.Count; i++)
            {
                var actual = native.GetWindowPosition(windows[i].Handle);
                Assert.Equal(before[i].Left + 10, actual.Left);
                Assert.Equal(before[i].Top + 5, actual.Top);
            }
        });
    }

    [Fact]
    public void NativePreparationRestoresMinimizedFixtureAndRejectsInvalidHandle()
    {
        RunSta(() =>
        {
            using var fixture = new NativeTestWindow();
            var native = new WindowService();
            Assert.False(native.PrepareForArrangement(IntPtr.Zero));
            Assert.True(native.SetWindowPosition(fixture.Handle, -30000, -30000, 500, 400));
            fixture.Minimize();
            Assert.True(fixture.IsMinimized);
            Assert.True(native.PrepareForArrangement(fixture.Handle));
            Assert.False(fixture.IsMinimized);
        });
    }

    private sealed class TestMonitorWindows : WindowService
    {
        // A virtual work area keeps our real fixture HWNDs outside the user's visible desktop.
        public static readonly RECT Area = new() { Left = -30000, Top = -30000, Right = -28997, Bottom = -29397 };
        public override string GetMonitorId(IntPtr handle) => "isolated-native-fixtures";
        public override List<MonitorInfo> GetAllMonitors() => new() { new()
        { DeviceName = "isolated-native-fixtures", IsPrimary = true, WorkArea = Area, Bounds = Area } };
    }

    private static void AssertRect(WindowService.RECT expected, WindowService.RECT actual)
    {
        Assert.Equal(expected.Left, actual.Left); Assert.Equal(expected.Top, actual.Top);
        Assert.Equal(expected.Right, actual.Right); Assert.Equal(expected.Bottom, actual.Bottom);
    }

    private static void Pump(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame);
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Native fixture test did not finish.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
