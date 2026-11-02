using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowSlu.Models;
using WindowSlu.Services;
using WindowSlu.ViewModels;

namespace WindowSlu.Tests;

public class UiAndNativeTests
{
    [Fact]
    public void RealWin32OperationsAffectOnlyTheHiddenTestWindow()
    {
        RunSta(() =>
        {
            using var source = new NativeTestWindow();
            var windows = new WindowService();
            Assert.True(windows.SetWindowPosition(source.Handle, 123, 145, 640, 480));
            var position = windows.GetWindowPosition(source.Handle);
            Assert.Equal(123, position.Left); Assert.Equal(145, position.Top);
            Assert.Equal(640, position.Width); Assert.Equal(480, position.Height);
            Assert.True(windows.SetTransparency(source.Handle, 80));
            Assert.InRange(windows.GetTransparency(source.Handle), 79, 81);
            Assert.True(windows.SetTransparency(source.Handle, 100));
            Assert.Equal(100, windows.GetTransparency(source.Handle));
            Assert.True(windows.SetWindowPosition(source.Handle, -32000, -32000, 100, 100));
            source.ShowWithoutActivation();
            // Windows can accept the API call without applying the requested Z order.
            // The service must report the actual outcome, including this failure case.
            bool madeTopMost = windows.SetTopMost(source.Handle, true);
            Assert.Equal(windows.IsTopMost(source.Handle), madeTopMost);
            bool madeNormal = windows.SetTopMost(source.Handle, false);
            Assert.Equal(!windows.IsTopMost(source.Handle), madeNormal);
            Assert.False(windows.SetTransparency(source.Handle, 101));
            Assert.False(windows.SetWindowPosition(source.Handle, 0, 0, -1, 100));
        });
    }

    [Fact]
    public void MainWindowLoadsAndGroupControlsPersistAcrossRestartWithoutSystemInputHooks()
    {
        RunSta(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "WindowSlu.UiTests", Guid.NewGuid().ToString("N"));
            var app = new App();
            app.InitializeComponent();
            app.ChangeTheme(WindowSlu.Theme.Dark);
            MainWindow? window = null;
            try
            {
                window = new MainWindow(directory, false);
                var viewModel = (MainViewModel)window.DataContext;
                Assert.All(viewModel.SettingsService.Settings.HotkeySettings, key => Assert.NotEmpty(key.Keys));
                var name = (TextBox)window.FindName("GroupNameTextBox");
                name.Text = "Test group";
                Invoke(window, "NewGroup_Click");
                var group = Assert.Single(viewModel.WindowGroups);
                Assert.Equal("Test group", group.Name);
                name.Text = "Renamed";
                Invoke(window, "RenameGroup_Click");
                Assert.Equal("Renamed", group.Name);
                var restoredGroups = new GroupingService();
                _ = new PresetService(new WindowService(), restoredGroups, Path.Combine(directory, "presets.json"));
                Assert.Equal(group.Id, Assert.Single(restoredGroups.Groups).Id);
                Assert.Equal("Renamed", Assert.Single(restoredGroups.Groups).Name);

                var preset = viewModel.PresetService.CreatePreset("Shortcut test");
                preset.HotKey = "Ctrl+F1";
                var rejectingService = new HotkeyService(IntPtr.Zero, viewModel.SettingsService, null, new RejectingPlatform());
                typeof(MainWindow).GetField("_hotkeyService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, rejectingService);
                var assigned = (bool)typeof(MainWindow).GetMethod("TrySetPresetHotkey", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, new object[] { preset, "Ctrl+F2" })!;
                Assert.False(assigned);
                Assert.Equal("Ctrl+F1", preset.HotKey);
                Assert.Contains("Failed to register", viewModel.StatusText);

                var selectedWindow = new WindowInfo { Handle = new IntPtr(100), ProcessName = "test", Title = "Selection test" };
                var list = new ListView(); list.Items.Add(selectedWindow); list.SelectedItem = selectedWindow;
                typeof(MainWindow).GetMethod("WindowListView_SelectionChanged", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(window, new object[] { list, new SelectionChangedEventArgs(SelectorSelectionChangedEvent(), Array.Empty<object>(), new object[] { selectedWindow }) });
                Assert.Null(viewModel.SelectedGroup);
                Assert.Same(selectedWindow, viewModel.SelectedWindow);
                viewModel.SelectedGroup = group;

                // Render all changed surfaces to catch missing resources and invalid WPF layout.
                var content = (FrameworkElement)window.Content;
                string? renderDirectory = Environment.GetEnvironmentVariable("WINDOWSLU_RENDER_DIR");
                foreach (var header in new[] { "Windows", "Presets", "Layouts", "Settings", "Help" })
                {
                    var tabs = FindVisual<TabControl>(content).First();
                    tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, header));
                    content.Measure(new Size(525, 700)); content.Arrange(new Rect(0, 0, 525, 700)); content.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(525, 700, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(content);
                    if (renderDirectory != null)
                    {
                        Directory.CreateDirectory(renderDirectory);
                        using var file = File.Create(Path.Combine(renderDirectory, header + ".png"));
                        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                    }
                }
                Invoke(window, "DeleteGroup_Click");
                Assert.Empty(viewModel.WindowGroups);
                _ = new PresetService(new WindowService(), restoredGroups, Path.Combine(directory, "presets.json"));
                Assert.Empty(restoredGroups.Groups);
                viewModel.Stop();
            }
            finally
            {
                window?.Close(); app.Shutdown();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        });
    }

    private static RoutedEvent SelectorSelectionChangedEvent() => System.Windows.Controls.Primitives.Selector.SelectionChangedEvent;

    private sealed class RejectingPlatform : IHotkeyPlatform
    {
        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey) => false;
        public bool UnregisterHotKey(IntPtr windowHandle, int id) => true;
        public int GetLastError() => 1409;
    }

    private static void Invoke(MainWindow window, string method) => typeof(MainWindow)
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, new object[] { window, new RoutedEventArgs() });

    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var descendant in FindVisual<T>(VisualTreeHelper.GetChild(root, i))) yield return descendant;
    }

    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "STA test did not finish.");
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}

internal sealed class NativeTestWindow : IDisposable
{
    public IntPtr Handle { get; } = CreateWindowEx(0, "STATIC", "WindowSlu isolated native test", unchecked((int)0x80000000),
        0, 0, 800, 600, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateWindowExW", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int exStyle, string className, string title, int style, int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(IntPtr window, int index);
    public int ExtendedStyle => GetWindowLong(Handle, -20);
    public void ShowWithoutActivation() => ShowWindow(Handle, 4);
    public void Dispose() { if (Handle != IntPtr.Zero) DestroyWindow(Handle); }
}
