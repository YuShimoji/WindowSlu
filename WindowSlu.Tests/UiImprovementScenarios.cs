using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowSlu.Models;
using WindowSlu.Services;
using WindowSlu.ViewModels;

namespace WindowSlu.Tests;

// Shares the existing STA Application; every operation targets only dedicated native HWNDs and temp JSON.
internal static class UiImprovementScenarios
{
    public static void Run(App app)
    {
        using var a = new NativeTestWindow(); using var b = new NativeTestWindow(); using var c = new NativeTestWindow();
        string directory = Path.Combine(Path.GetTempPath(), "WindowSlu.UiImprovement", Guid.NewGuid().ToString("N"));
        var service = new IsolatedWindows();
        var window = new MainWindow(directory, false, service) { Title = "WindowSlu isolated UI verification", Width = 525, Height = 700 };
        try
        {
            var vm = (MainViewModel)window.DataContext;
            var first = new WindowInfo { Handle = a.Handle, Title = "Search target alpha", ProcessName = "fixture" };
            var second = new WindowInfo { Handle = b.Handle, Title = "Group beta", ProcessName = "fixture" };
            var outside = new WindowInfo { Handle = c.Handle, Title = "Outside gamma", ProcessName = "other" };
            foreach (var item in new[] { first, second, outside }) { vm.Windows.Add(item); service.Items.Add(item); service.UpdateWindowPositionInfo(item); }
            var group = vm.GroupingService.CreateManualGroup("Test pair", new[] { first, second });
            vm.GroupingService.UpdateGroups(vm.Windows);
            for (int i = 0; i < 8; i++) vm.WindowGroups.Insert(0, new WindowGroup { Name = $"Decoy {i}", IsExpanded = false });
            vm.NotifySplitTargetChanged();
            window.Show(); Pump(window);
            var tabs = (TabControl)window.FindName("MainTabs");
            var list = (ListView)window.FindName("AllWindowsListView");
            var apply = (Button)window.FindName("ApplyOpacityButton");
            Assert.False(apply.IsEnabled); // no implicit all-windows fallback
            Invoke(window, "SetAllTo80Percent_Click");
            Assert.Equal(100, service.GetTransparency(a.Handle));
            list.SelectedItem = first; Pump(window);
            vm.BulkOpacityPercent = 37; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.InRange(service.GetTransparency(a.Handle), 36, 38);
            Assert.Equal(100, service.GetTransparency(b.Handle)); Assert.Equal(100, service.GetTransparency(c.Handle));
            Assert.Contains(first.Title, vm.OpacityTargetDescription);
            list.SelectedItem = null; vm.SelectedWindow = null; vm.SelectedGroup = group;
            vm.BulkOpacityPercent = 63; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.InRange(service.GetTransparency(a.Handle), 62, 64); Assert.InRange(service.GetTransparency(b.Handle), 62, 64);
            Assert.Equal(100, service.GetTransparency(c.Handle));
            vm.ApplyToAllWindows = true; vm.BulkOpacityPercent = 45; apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.All(service.Items, item => Assert.InRange(service.GetTransparency(item.Handle), 44, 46));
            vm.ApplyToAllWindows = false; vm.SelectedGroup = null; list.SelectedItem = first;
            ((Button)window.FindName("ResetAllOpacityButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.All(service.Items, item => Assert.Equal(100, service.GetTransparency(item.Handle)));

            // Real template contract: selected tab's input/button peers must be exposed.
            Assert.NotNull(tabs.Template.FindName("PART_SelectedContentHost", tabs));
            var windowsPeer = UIElementAutomationPeer.CreatePeerForElement((TabItem)tabs.SelectedItem)!;
            var peerNames = PeerNames(windowsPeer).ToList();
            Assert.Contains("Apply displayed opacity to described targets", peerNames);

            // Search -> existing controls, preserving search focus and selected row across refresh.
            var search = (TextBox)window.FindName("WindowFilterTextBox");
            search.Text = "alpha"; search.Focus(); list.SelectedItem = null; list.SelectedItem = first; Pump(window);
            Assert.True(group.IsExpanded); Assert.Same(first, list.SelectedItem); Assert.True(search.IsKeyboardFocusWithin);
            var tree = (TreeView)window.FindName("WindowTreeView");
            var parent = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromItem(group)!;
            var row = (TreeViewItem)parent.ItemContainerGenerator.ContainerFromItem(first)!;
            Assert.NotNull(row); Assert.True(row.IsVisible);
            var thumb = Visuals<System.Windows.Controls.Primitives.Thumb>((Slider)window.FindName("BulkOpacitySlider")).First();
            Assert.True(thumb.ActualWidth >= 28 && thumb.ActualHeight >= 28);
            Assert.NotNull(thumb.InputHitTest(new Point(1, 1))); // transparent padding, outside the 14px visible grip
            var expander = (System.Windows.Controls.Primitives.ToggleButton)parent.Template.FindName("Expander", parent);
            Assert.True(expander.ActualWidth >= 26 && expander.ActualHeight >= 28);
            Assert.NotNull(expander.InputHitTest(new Point(1, 1)));

            var controlsViewport = Visuals<ScrollViewer>(tree).First();
            Assert.True(controlsViewport.VerticalOffset > 0, "Search must scroll to a target below the initial viewport");
            var rowBounds = row.TransformToAncestor(tree).TransformBounds(new Rect(row.RenderSize));
            Assert.InRange(rowBounds.Top, 0, tree.ActualHeight - row.ActualHeight + 1);
            var opacity = Visuals<Slider>(row).Single(s => AutomationProperties.GetName(s) == "Window opacity percent");
            opacity.Focus(); opacity.Value = 58; Pump(window);
            Assert.InRange(service.GetTransparency(a.Handle), 57, 59); Assert.Equal(100, service.GetTransparency(b.Handle));
            var width = Visuals<Slider>(row).Single(s => AutomationProperties.GetName(s) == "Window width");
            var height = Visuals<Slider>(row).Single(s => AutomationProperties.GetName(s) == "Window height");
            int oldWidth = first.Width; Assert.True(width.Focus(), "Width slider must receive keyboard focus");
            Slider.IncreaseSmall.Execute(null, width);
            Assert.True(service.GetWindowPosition(a.Handle).Width > oldWidth, $"width before={oldWidth} slider={width.Value} actual={service.GetWindowPosition(a.Handle).Width} focus={width.IsKeyboardFocusWithin} calls={service.ResizeCalls} status={vm.StatusText}");
            int oldHeight = first.Height; height.Focus();
            Slider.IncreaseSmall.Execute(null, height);
            Assert.True(service.GetWindowPosition(a.Handle).Height > oldHeight);
            int calls = service.ResizeCalls;
            service.UpdateWindowPositionInfo(first); Pump(window);
            Assert.Equal(calls, service.ResizeCalls);
            service.RejectResize = true; width.Focus(); width.Value += 20;
            Assert.Contains("Could not resize", vm.StatusText); Assert.Equal(service.GetWindowPosition(a.Handle).Width, first.Width);
            Assert.Equal(first.Width, width.Value); service.RejectResize = false;
            // A background enumeration must not suppress real input while awaiting the native query.
            service.BlockEnumeration = true;
            var refresh = vm.RefreshWindowList();
            Assert.True(service.EnumerationStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(vm.IsReadingWindowState);
            int beforePendingInput = service.GetWindowPosition(a.Handle).Width;
            Slider.IncreaseSmall.Execute(null, width);
            Assert.True(service.GetWindowPosition(a.Handle).Width > beforePendingInput);
            service.ContinueEnumeration.Set(); refresh.GetAwaiterWithDispatcher(window);
            service.BlockEnumeration = false;
            search.Focus(); vm.RefreshWindowList().GetAwaiterWithDispatcher(window);
            Assert.Equal("alpha", search.Text); Assert.Same(first, list.SelectedItem); Assert.True(search.IsKeyboardFocusWithin);

            tabs.SelectedIndex = 1; Pump(window);
            Invoke(window, "ApplyPreset_Click"); Assert.Contains("Select a preset", vm.StatusText); Assert.Empty(vm.OperationDetails);
            var preset = vm.PresetService.CreatePreset("Fixture filtered preset");
            preset.Width = null; preset.Height = null; preset.IsTopMost = null; preset.Opacity = 71;
            preset.TargetProcessName = "fixture"; preset.TargetGroupId = group.Id; preset.HotKey = "Ctrl+Alt+F11";
            var picker = (ComboBox)window.FindName("PresetComboBox"); picker.SelectedItem = preset;
            Assert.Contains("matches: 1", vm.PresetTargetDescription);
            var hotkeyEditor = (TextBox)window.FindName("HotKeyTextBox");
            hotkeyEditor.Focus();
            var tabInput = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(hotkeyEditor), 0, Key.Tab) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            hotkeyEditor.RaiseEvent(tabInput);
            Assert.False(tabInput.Handled); Assert.Equal("Ctrl+Alt+F11", preset.HotKey);
            Assert.True(hotkeyEditor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
            Assert.False(hotkeyEditor.IsKeyboardFocusWithin);

            Invoke(window, "ApplyPreset_Click"); Assert.InRange(service.GetTransparency(a.Handle), 70, 72);
            Assert.Equal(100, service.GetTransparency(b.Handle)); // selection narrows the group filter
            list.SelectedItem = null; vm.SelectedWindow = null; vm.SelectedGroup = null;
            Assert.Contains("matches: 2", vm.PresetTargetDescription);
            Invoke(window, "ApplyPreset_Click"); Assert.InRange(service.GetTransparency(b.Handle), 70, 72); Assert.Equal(100, service.GetTransparency(c.Handle));
            preset.TargetProcessName = "missing"; Assert.Contains("matches: 0", vm.PresetTargetDescription);
            Invoke(window, "ApplyPreset_Click"); Assert.Equal(1, vm.PresetService.LastResult.Unmatched);
            preset.TargetProcessName = "fixture";
            var platform = new TestHotkeys();
            var hotkeys = new HotkeyService(a.Handle, vm.SettingsService, null, platform);
            typeof(MainWindow).GetField("_hotkeyService", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, hotkeys);
            Assert.True(hotkeys.RegisterPresetHotkey(preset.Id, preset.HotKey!));
            int registered = platform.Active.Count;
            Invoke(window, "DeletePreset_Click"); Assert.DoesNotContain(preset, vm.PresetService.Presets);
            Assert.Equal(registered - 1, platform.Active.Count);
            Assert.Contains(preset.Name, ((Button)window.FindName("UndoPresetButton")).Content.ToString());
            ((Button)window.FindName("UndoPresetButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Same(preset, picker.SelectedItem);
            Assert.Equal(registered, platform.Active.Count);
            var reloadedPreset = new PresetService(service, new GroupingService(), Path.Combine(directory, "presets.json")).GetPresetById(preset.Id)!;
            Assert.Equal(preset.HotKey, reloadedPreset.HotKey); Assert.Equal(group.Id, reloadedPreset.TargetGroupId);
            Assert.True(File.Exists(Path.Combine(directory, "presets.json.bak")));

            tabs.SelectedIndex = 2; Pump(window);
            Invoke(window, "CaptureLayout_Click");
            var layout = (WindowLayout)((ComboBox)window.FindName("LayoutComboBox")).SelectedItem;
            var captured = service.GetWindowPosition(a.Handle);
            service.SetWindowPosition(a.Handle, 210, 220, 510, 410);
            Invoke(window, "RestoreLayout_Click"); Assert.Equal(captured.Width, service.GetWindowPosition(a.Handle).Width);
            Invoke(window, "DeleteLayout_Click"); Assert.DoesNotContain(layout, vm.LayoutService.Layouts);
            ((Button)window.FindName("UndoLayoutButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(layout.Id, Assert.Single(new LayoutService(service, Path.Combine(directory, "layouts.json")).Layouts).Id);
            Assert.Same(layout, ((ComboBox)window.FindName("LayoutComboBox")).SelectedItem);

            list.SelectedItem = first;
            string? output = Environment.GetEnvironmentVariable("WINDOWSLU_IMPROVEMENT_RENDER_DIR");
            if (output != null)
            {
                Directory.CreateDirectory(output);
                for (int size = 0; size < 2; size++)
                for (int tab = 0; tab < 5; tab++)
                {
                    window.Width = size == 0 ? 525 : 800; window.Height = 700; tabs.SelectedIndex = tab; Pump(window);
                    string name = ((TabItem)tabs.SelectedItem).Header.ToString()!;
                    var content = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)window.Width, 700, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
                    using var file = File.Create(Path.Combine(output, $"{(int)window.Width}-{name}.png"));
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                    File.WriteAllLines(Path.Combine(output, $"{(int)window.Width}-{name}-peers.txt"), PeerNames(UIElementAutomationPeer.CreatePeerForElement((TabItem)tabs.SelectedItem)!));
                }
                foreach (var theme in new[] { WindowSlu.Theme.Dark, WindowSlu.Theme.Light })
                {
                    app.ChangeTheme(theme); tabs.SelectedIndex = 1; window.Width = 525; Pump(window);
                    var bitmap = new RenderTargetBitmap(525, 700, 96, 96, PixelFormats.Pbgra32); bitmap.Render((FrameworkElement)window.Content);
                    using var file = File.Create(Path.Combine(output, $"525-Presets-{theme}.png"));
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
                    picker.IsDropDownOpen = true; Pump(window);
                    var selectedItem = (ComboBoxItem)picker.ItemContainerGenerator.ContainerFromItem(preset)!;
                    var text = Visuals<TextBlock>(selectedItem).First();
                    Assert.Equal(System.Windows.Media.Color.FromRgb(32, 32, 32), ((SolidColorBrush)text.Foreground).Color);
                    picker.IsDropDownOpen = false;
                }
                app.ChangeTheme(WindowSlu.Theme.Dark);
                tabs.SelectedIndex = 0; window.Width = 525; Pump(window);
                // Optional bounded visible host for Computer Use; a stop file avoids modifying user apps.
                if (Environment.GetEnvironmentVariable("WINDOWSLU_HOLD_UI") == "1")
                {
                    File.WriteAllText(Path.Combine(output, "host-ready.txt"), directory);
                    var until = DateTime.UtcNow.AddMinutes(20);
                    var frame = new DispatcherFrame();
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                    timer.Tick += (_, _) => { if (File.Exists(Path.Combine(output, "stop-host.txt")) || DateTime.UtcNow >= until) frame.Continue = false; };
                    timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
                }
            }
        }
        finally { window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    private static void Invoke(MainWindow window, string method) => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
    private static void Pump(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void GetAwaiterWithDispatcher(this Task task, Window window) { while (!task.IsCompleted) { Pump(window); Thread.Sleep(1); } task.GetAwaiter().GetResult(); }
    private static IEnumerable<string> PeerNames(AutomationPeer peer)
    {
        yield return peer.GetName();
        foreach (var child in peer.GetChildren() ?? new List<AutomationPeer>()) foreach (var name in PeerNames(child)) yield return name;
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var item2 in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return item2;
    }
    private sealed class IsolatedWindows : WindowService
    {
        public ObservableCollection<WindowInfo> Items { get; } = new();
        public bool BlockEnumeration { get; set; }
        public ManualResetEventSlim EnumerationStarted { get; } = new();
        public ManualResetEventSlim ContinueEnumeration { get; } = new();
        public override ObservableCollection<WindowInfo> GetAllWindows()
        {
            if (BlockEnumeration) { EnumerationStarted.Set(); if (!ContinueEnumeration.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException(); }
            return new(Items);
        }
        public int ResizeCalls { get; private set; }
        public bool RejectResize { get; set; }
        public override bool SetWindowSize(IntPtr handle, int width, int height) { ResizeCalls++; return !RejectResize && base.SetWindowSize(handle, width, height); }
    }
    private sealed class TestHotkeys : IHotkeyPlatform
    {
        public HashSet<int> Active { get; } = new();
        public bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey) => Active.Add(id);
        public bool UnregisterHotKey(IntPtr windowHandle, int id) => Active.Remove(id);
        public int GetLastError() => 0;
    }
}
