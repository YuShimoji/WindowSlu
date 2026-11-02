using System;
using System.Linq;
using System.Text.Json;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public class GroupingServiceTests
{
    private static WindowInfo W(long handle, string process, string title) => new() { Handle = new IntPtr(handle), ProcessName = process, Title = title };

    [Fact]
    public void AutomaticGroupIdIsStableAcrossRegenerationAndCase()
    {
        var service = new GroupingService();
        service.GroupByProcess(new[] { W(1, "CODE", "one") });
        var firstId = service.GetGroupByProcessName("CODE")!.Id;
        service.GroupByProcess(new[] { W(2, "code", "two") });
        Assert.Equal(firstId, service.GetGroupByProcessName("code")!.Id);
        Assert.Equal("auto:code", firstId);
    }

    [Fact]
    public void RestoresManualGroupAndExactMembersAfterJsonRoundTrip()
    {
        var original = new GroupingService();
        var group = original.CreateManualGroup("Work", new[] { W(1, "code", "Editor"), W(2, "code", "Terminal") });
        var json = JsonSerializer.Serialize(original.GetManualGroupsData());
        var data = JsonSerializer.Deserialize<System.Collections.Generic.List<ManualGroupData>>(json)!;

        var restored = new GroupingService();
        restored.RestoreManualGroups(data);
        restored.UpdateGroups(new[] { W(10, "code", "Editor"), W(11, "code", "Terminal"), W(12, "code", "Other") });
        var restoredGroup = restored.GetGroupById(group.Id)!;

        Assert.Equal("Work", restoredGroup.Name);
        Assert.Equal(new[] { "Editor", "Terminal" }, restoredGroup.Windows.Select(w => w.Title).OrderBy(t => t));
        Assert.Equal("auto:code", restored.GetGroupByProcessName("code")!.Id);
    }

    [Fact]
    public void MatchesSameProcessByTitleAndAssignsDuplicateTitlesOneToOne()
    {
        var service = new GroupingService();
        var group = service.CreateManualGroup("Duplicates", new[] { W(1, "app", "same"), W(2, "app", "same"), W(3, "app", "other") });
        var data = JsonSerializer.Deserialize<System.Collections.Generic.List<ManualGroupData>>(JsonSerializer.Serialize(service.GetManualGroupsData()))!;
        var restored = new GroupingService();
        restored.RestoreManualGroups(data);
        restored.UpdateGroups(new[] { W(10, "app", "same"), W(11, "app", "same"), W(12, "app", "other"), W(13, "app", "unrelated") });
        var actual = restored.GetGroupById(group.Id)!.Windows;
        Assert.Equal(3, actual.Count);
        Assert.Equal(3, actual.Select(w => w.Handle).Distinct().Count());
        Assert.DoesNotContain(actual, w => w.Title == "unrelated");
    }

    [Fact]
    public void ExplicitMoveSurvivesRefreshAndClosedMemberCanReturn()
    {
        var service = new GroupingService();
        var original = W(1, "app", "remember me");
        service.GroupByProcess(new[] { original });
        var manual = service.CreateManualGroup("Pinned", new[] { original });
        service.UpdateGroups(Array.Empty<WindowInfo>());
        service.UpdateGroups(new[] { W(2, "app", "remember me") });
        Assert.Single(manual.Windows);

        var second = service.CreateManualGroup("Second");
        var moved = manual.Windows.Single();
        service.AddToGroup(second, moved);
        service.UpdateGroups(new[] { W(3, "app", "remember me") });
        Assert.Empty(manual.Windows);
        Assert.Single(second.Windows);
    }

    [Fact]
    public void RejectsWhitespaceNameAndSupportsRename()
    {
        var service = new GroupingService();
        Assert.Throws<ArgumentException>(() => service.CreateManualGroup("  "));
        var group = service.CreateManualGroup("Before");
        Assert.True(service.RenameGroup(group, "After"));
        Assert.Equal("After", group.Name);
        Assert.False(service.RenameGroup(group, " "));
    }

    [Fact]
    public void LegacyProcessNamesRestoreAndDuplicateHandlesStayInOneGroup()
    {
        var service = new GroupingService();
        var legacy = new ManualGroupData
        {
            Id = "legacy-id", Name = "Legacy", ProcessNames = new() { "app" }, Members = new()
        };
        service.RestoreManualGroups(new[] { legacy });
        var duplicateHandle = W(22, "app", "any title");
        service.UpdateGroups(new[] { duplicateHandle, W(22, "app", "any title") });

        Assert.Equal("legacy-id", duplicateHandle.GroupId);
        Assert.Single(service.Groups.SelectMany(g => g.Windows), w => w.Handle == duplicateHandle.Handle);
    }

    [Fact]
    public void LiveHandleKeepsManualMembershipAcrossTitleChangeAndPersistsNewTitle()
    {
        var service = new GroupingService();
        var original = W(31, "app", "old title");
        var manual = service.CreateManualGroup("Pinned", new[] { original });

        original.Title = "new title";
        service.UpdateGroups(new[] { original });
        Assert.Equal(manual.Id, service.Groups.SelectMany(g => g.Windows).Single(w => w.Handle == new IntPtr(31)).GroupId);
        var data = service.GetManualGroupsData();
        Assert.Equal("new title", Assert.Single(data.Single().Members).Title);

        var restarted = new GroupingService();
        restarted.RestoreManualGroups(data);
        restarted.UpdateGroups(new[] { W(32, "app", "new title") });
        Assert.Equal(manual.Id, restarted.Groups.SelectMany(g => g.Windows).Single(w => w.Handle == new IntPtr(32)).GroupId);
    }

    [Fact]
    public void OneSavedSlotDoesNotClaimAnotherSameTitleLiveHandle()
    {
        var service = new GroupingService();
        var manual = service.CreateManualGroup("Single", new[] { W(61, "app", "same") });
        var live = W(61, "app", "same");
        var other = W(62, "app", "same");

        service.UpdateGroups(new[] { live, other });

        Assert.Single(manual.Windows);
        Assert.Contains(manual.Windows, w => w.Handle == live.Handle);
        Assert.DoesNotContain(manual.Windows, w => w.Handle == other.Handle);
    }

    [Fact]
    public void ClosedLiveSlotCanBeReplacedWithoutMovingTheOtherSlot()
    {
        var service = new GroupingService();
        var manual = service.CreateManualGroup("Pair", new[] { W(71, "app", "same"), W(72, "app", "same") });
        var retained = W(72, "app", "same");
        var replacement = W(73, "app", "same");

        service.UpdateGroups(new[] { retained, replacement });

        Assert.Equal(2, manual.Windows.Count);
        Assert.Contains(manual.Windows, w => w.Handle == retained.Handle);
        Assert.Contains(manual.Windows, w => w.Handle == replacement.Handle);
    }

    [Fact]
    public void MovingAfterTitleChangeIsNotPulledBackAndDuplicateRestoreIdsAreIgnored()
    {
        var service = new GroupingService();
        var original = W(41, "app", "old title");
        var first = service.CreateManualGroup("First", new[] { original });
        service.UpdateGroups(new[] { W(41, "app", "new title") });
        var second = service.CreateManualGroup("Second");
        service.AddToGroup(second, first.Windows.Single());
        service.UpdateGroups(new[] { W(42, "app", "new title") });
        Assert.Empty(first.Windows);
        Assert.Single(second.Windows);

        var duplicated = new ManualGroupData[]
        {
            new() { Id = "same", Name = "First copy" },
            new() { Id = "same", Name = "Second copy" }
        };
        var restored = new GroupingService();
        restored.RestoreManualGroups(duplicated);
        Assert.Single(restored.Groups);
        Assert.Equal("First copy", restored.Groups[0].Name);
    }

    [Fact]
    public void ManualRestoreMatchesProcessNameWithoutCaseDifference()
    {
        var service = new GroupingService();
        service.RestoreManualGroups(new[]
        {
            new ManualGroupData
            {
                Id = "case-id", Name = "Case", Members = new() { new ManualGroupMemberData { ProcessName = "APP", Title = "Editor" } }
            }
        });
        var window = W(51, "app", "Editor");
        service.UpdateGroups(new[] { window });
        Assert.Equal("case-id", window.GroupId);
        Assert.Equal("app", Assert.Single(service.GetManualGroupsData().Single().Members).ProcessName);
    }

    [Fact]
    public void RemoveAndClearRemovePersistedSlotsAndDuplicateInputsStayUnique()
    {
        var service = new GroupingService();
        var window = W(81, "app", "remove me");
        var manual = service.CreateManualGroup("Manual", new[] { window });
        service.UpdateGroups(new[] { window, window });
        Assert.Single(manual.Windows);

        service.RemoveFromGroup(manual, window);
        service.UpdateGroups(new[] { W(82, "app", "remove me") });
        Assert.Empty(manual.Windows);
        Assert.Empty(service.GetManualGroupsData().Single().Members);

        service.ClearGroups();
        Assert.Empty(service.Groups);
        Assert.Empty(service.GetManualGroupsData());
    }
}
