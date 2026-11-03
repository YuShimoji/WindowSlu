using System.Runtime.CompilerServices;
using WindowSlu.Models;

namespace WindowSlu.Tests;

public class GroupOpacityLifetimeTests
{
    [Fact]
    public void SummaryUpdatesButAbandonedGroupDoesNotRemainRootedBySurvivingWindow()
    {
        var window = new WindowInfo { Opacity = 80 };
        var weak = SubscribeAndAbandon(window);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Assert.False(weak.IsAlive);
        window.Opacity = 75; // A stale listener must not affect a surviving window either.
        GC.KeepAlive(window);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SubscribeAndAbandon(WindowInfo window)
    {
        var group = new WindowGroup(); group.Windows.Add(window);
        window.Opacity = 65; Assert.Contains("65%", group.OpacitySummary);
        return new WeakReference(group);
    }
}
