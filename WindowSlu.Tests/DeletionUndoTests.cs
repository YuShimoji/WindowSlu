using System.IO;
using WindowSlu.Services;

namespace WindowSlu.Tests;

public class DeletionUndoTests
{
    [Fact]
    public void ConsecutivePresetDeletesUndoInOrderAndPersistIdentityAndHotkeys()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowSlu.UndoTests", Guid.NewGuid().ToString("N"));
        try
        {
            string path = Path.Combine(directory, "presets.json");
            var service = new PresetService(new WindowService(), new GroupingService(), path);
            var first = service.CreatePreset("First"); var second = service.CreatePreset("Second");
            first.HotKey = "Ctrl+Alt+F11"; first.TargetProcessName = "fixture"; service.SavePresets();
            Assert.True(service.DeletePreset(first.Id)); Assert.True(service.DeletePreset(second.Id));
            Assert.Contains("Second", service.UndoDescription);
            Assert.True(service.UndoDelete(out var restoredSecond)); Assert.Same(second, restoredSecond);
            Assert.Contains("First", service.UndoDescription);
            Assert.True(service.UndoDelete(out var restoredFirst)); Assert.Same(first, restoredFirst);
            var restored = new PresetService(new WindowService(), new GroupingService(), path).Presets;
            Assert.Equal(new[] { first.Id, second.Id }, restored.Select(p => p.Id));
            Assert.Equal(first.HotKey, restored[0].HotKey); Assert.Equal("fixture", restored[0].TargetProcessName);
            Assert.False(service.CanUndoDeletion); Assert.True(File.Exists(path + ".bak"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void SaveFailureRollsBackDeletionAndFailedUndoRemainsRetryable()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowSlu.UndoTests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "layouts.json");
            var service = new LayoutService(new WindowService(), path);
            var first = service.CreateLayout("First"); var second = service.CreateLayout("Second");
            // A directory at the JSON destination creates a deterministic IO failure, without permissions changes.
            File.Move(path, path + ".retained"); Directory.CreateDirectory(path);
            Assert.False(service.DeleteLayout(first.Id)); Assert.Equal(new[] { first, second }, service.Layouts);
            Assert.NotNull(service.LastError); Assert.False(service.CanUndoDeletion);
            Directory.Delete(path); File.Move(path + ".retained", path);
            Assert.True(service.DeleteLayout(first.Id)); Assert.True(service.DeleteLayout(second.Id));
            File.Move(path, path + ".retained"); Directory.CreateDirectory(path);
            Assert.False(service.UndoDelete(out _)); Assert.Empty(service.Layouts); Assert.Contains("Second", service.UndoDescription);
            Assert.NotNull(service.LastError);
            Directory.Delete(path); File.Move(path + ".retained", path);
            Assert.True(service.UndoDelete(out _)); Assert.True(service.UndoDelete(out _));
            Assert.Equal(new[] { first.Id, second.Id }, new LayoutService(new WindowService(), path).Layouts.Select(l => l.Id));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
