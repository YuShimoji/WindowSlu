using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WindowSlu.Controls;
using WindowSlu.Models;
using WindowSlu.Services;
using WindowSlu.ViewModels;

namespace WindowSlu.Tests;

internal static class UiFollowupScenarios
{
    public static void Run(MainWindow window, WindowService native, WindowInfo first, WindowInfo second, WindowInfo outsider, WindowGroup group, App app)
    {
        var vm = (MainViewModel)window.DataContext;
        var tabs = (TabControl)window.FindName("MainTabs"); tabs.SelectedIndex = 0;
        var list = (ListView)window.FindName("AllWindowsListView"); list.SelectedItem = first; Pump(window);
        var width = Visuals<IntegerEditor>(window).Single(c => ReferenceEquals(c.DataContext, first) && Equals(c.Tag, "Width"));
        var text = (TextBox)width.FindName("NumberBox");
        var set = (Button)width.FindName("SetButton");
        int untouched = native.GetWindowPosition(second.Handle).Width;
        int before = native.GetWindowPosition(first.Handle).Width;
        foreach (string invalid in new[] { "", "0", "8x", "20000" })
        {
            text.Text = invalid; Assert.Equal(before, native.GetWindowPosition(first.Handle).Width);
            set.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(before, native.GetWindowPosition(first.Handle).Width);
            Assert.Equal(Visibility.Visible, ((TextBlock)width.FindName("ErrorCaption")).Visibility);
        }
        text.Focus(); text.Text = "850";
        var old = native.GetWindowPosition(first.Handle);
        native.SetWindowPosition(first.Handle, old.Left, old.Top, 805, old.Height);
        var refreshing = vm.RefreshWindowList(); Wait(refreshing, window);
        Assert.Equal("850", text.Text); Assert.True(text.IsKeyboardFocusWithin);
        Assert.Equal(805, native.GetWindowPosition(first.Handle).Width);
        set.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(850, native.GetWindowPosition(first.Handle).Width);
        Assert.Equal(untouched, native.GetWindowPosition(second.Handle).Width);
        Assert.Equal("alpha", ((TextBox)window.FindName("WindowFilterTextBox")).Text);

        var step = (ComboBox)width.FindName("StepPicker"); step.SelectedIndex = 2; // 50px
        Assert.Equal(850, native.GetWindowPosition(first.Handle).Width);
        ((Button)width.FindName("PlusButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(900, native.GetWindowPosition(first.Handle).Width);
        ((Button)width.FindName("MinusButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(850, native.GetWindowPosition(first.Handle).Width);
        text.Text = "4040"; set.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(4040, native.GetWindowPosition(first.Handle).Width);
        step.SelectedIndex = 0;
        ((Slider)width.FindName("FineAdjustment")).Focus(); Slider.IncreaseSmall.Execute(null, (Slider)width.FindName("FineAdjustment"));
        Assert.Equal(4041, native.GetWindowPosition(first.Handle).Width); // fine adjustment also works above the old 3000px maximum
        Common(width, 1280); Assert.Equal(1280, native.GetWindowPosition(first.Handle).Width);
        step.SelectedIndex = 0; Pump(window);
        var slider = (Slider)width.FindName("FineAdjustment");
        var thumb = Visuals<Thumb>(slider).Single();
        slider.Focus();
        int beforeDrag = native.GetWindowPosition(first.Handle).Width;
        thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        thumb.RaiseEvent(new DragDeltaEventArgs(10, 0) { RoutedEvent = Thumb.DragDeltaEvent });
        thumb.RaiseEvent(new DragCompletedEventArgs(10, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
        int afterDrag = native.GetWindowPosition(first.Handle).Width;
        Assert.InRange(afterDrag - beforeDrag, 1, 15);
        Assert.Equal(afterDrag.ToString(), text.Text);
        Assert.Equal(untouched, native.GetWindowPosition(second.Handle).Width);

        var opacity = Visuals<IntegerEditor>(window).Single(c => ReferenceEquals(c.DataContext, first) && Equals(c.Tag, "Opacity"));
        Common(opacity, 80); Assert.InRange(native.GetTransparency(first.Handle), 79, 81);
        Assert.Contains("mixed", group.OpacitySummary);
        int outsideOpacity = native.GetTransparency(outsider.Handle);
        var groupEditor = Visuals<IntegerEditor>(window).Single(c => ReferenceEquals(c.DataContext, group));
        Common(groupEditor, 25);
        Assert.All(new[] { first, second }, item => Assert.InRange(native.GetTransparency(item.Handle), 24, 26));
        Assert.DoesNotContain("mixed", group.OpacitySummary); Assert.Equal(outsideOpacity, native.GetTransparency(outsider.Handle));
        var bulk = (IntegerEditor)window.FindName("BulkOpacityEditor");
        Common(bulk, 50); Assert.Equal(50, vm.BulkOpacityPercent);
        Assert.InRange(native.GetTransparency(first.Handle), 24, 26); // staging alone never applies
        ((Button)window.FindName("ApplyOpacityButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.InRange(native.GetTransparency(first.Handle), 49, 51); Assert.InRange(native.GetTransparency(second.Handle), 24, 26);
        tabs.SelectedIndex = 1; Pump(window);
        var presetEditor = Visuals<IntegerEditor>(window).Single(c => c.Label == "Preset opacity");
        Common(presetEditor, 75); Assert.Equal(75, vm.SelectedPreset!.Opacity);
        Assert.InRange(native.GetTransparency(first.Handle), 49, 51);

        string? directory = Environment.GetEnvironmentVariable("WINDOWSLU_FOLLOWUP_RENDER_DIR");
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "native-measurements.json"), JsonSerializer.Serialize(new
            {
                screenDragPixels = 10, beforeDrag, afterDrag, actualSizeDelta = afterDrag - beforeDrag,
                fineMinimum = slider.Minimum, fineMaximum = slider.Maximum, sliderWidth = slider.ActualWidth,
                numberEntry = 850, roundedStep = 50, commonSize = 1280,
                secondUnchanged = native.GetWindowPosition(second.Handle).Width == untouched,
                outsiderOpacityUnchanged = native.GetTransparency(outsider.Handle) == outsideOpacity
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        foreach (var theme in new[] { WindowSlu.Theme.Dark, WindowSlu.Theme.Light })
        {
            app.ChangeTheme(theme); Pump(window);
            PaletteFixture(app, window, theme, directory);
            // The actual extra hotkey Window uses the same App-level input palette.
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            Exception? dialogError = null;
            timer.Tick += (_, _) =>
            {
                var dialog = app.Windows.OfType<Window>().FirstOrDefault(w => w.Title.StartsWith("Edit Hotkey for"));
                if (dialog == null) return;
                try
                {
                    var field = Visuals<TextBox>(dialog).Single(); Assert.NotEmpty(field.Text); Assert.True(field.IsReadOnly);
                    Assert.True(Contrast(field.Foreground, field.Background) >= 4.5);
                    if (directory != null) Capture((FrameworkElement)dialog.Content, Path.Combine(directory, $"hotkey-{theme}-fixture.png"));
                }
                catch (Exception ex) { dialogError = ex; }
                finally { timer.Stop(); dialog.Close(); }
            };
            timer.Start();
            typeof(MainWindow).GetMethod("EditHotkey_Click", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window,
                new object[] { new Button { Tag = vm.SettingsService.Settings.HotkeySettings.First() }, new RoutedEventArgs() });
            if (dialogError != null) throw dialogError;
            tabs.SelectedIndex = 1; Pump(window);
            if (directory != null) Capture((FrameworkElement)window.Content, Path.Combine(directory, $"525-Presets-{theme}-fixture.png"));
        }
        app.ChangeTheme(WindowSlu.Theme.Dark); tabs.SelectedIndex = 0;
        if (directory != null)
        foreach (int size in new[] { 525, 800 })
        {
            window.Width = size; window.Height = 850; Pump(window);
            list.SelectedItem = null; list.SelectedItem = first; Pump(window);
            Capture((FrameworkElement)window.Content, Path.Combine(directory, $"{size}-Windows-fixture.png"));
            window.Height = 700; Pump(window);
            list.SelectedItem = null; list.SelectedItem = first; Pump(window);
            Capture((FrameworkElement)window.Content, Path.Combine(directory, $"{size}x700-Windows-fixture.png"));
            foreach (var editor in Visuals<IntegerEditor>(window).Where(c => ReferenceEquals(c.DataContext, first)).ToList())
            {
                var number = (TextBox)editor.FindName("NumberBox"); Assert.True(number.Focus()); Pump(window);
                var viewport = Visuals<ScrollViewer>((TreeView)window.FindName("WindowTreeView")).First();
                var bounds = number.TransformToAncestor(viewport).TransformBounds(new Rect(number.RenderSize));
                Assert.True(bounds.Top >= -1 && bounds.Bottom <= viewport.ActualHeight + 1, $"Exact input {editor.Label} must be fully reachable at 700px height: {bounds}, viewport {viewport.ActualHeight}.");
            }
            ((TextBox)window.FindName("WindowFilterTextBox")).Focus();
            var treeScroll = Visuals<ScrollViewer>((TreeView)window.FindName("WindowTreeView")).First(); treeScroll.ScrollToTop(); Pump(window);
            Capture((FrameworkElement)window.Content, Path.Combine(directory, $"{size}x700-Group-mixed-fixture.png"));
        }
        window.Width = 525; window.Height = 700; Pump(window);
    }

    private static void PaletteFixture(App app, Window owner, WindowSlu.Theme theme, string? directory)
    {
        var fields = new[] { new TextBox { Text = "Editable sample 850 px" }, new TextBox { Text = "Read only Ctrl+Alt+Up", IsReadOnly = true },
            new TextBox { Text = "Disabled sample 75%", IsEnabled = false }, new TextBox { Text = "Selected text sample" } };
        var combo = new ComboBox { ItemsSource = new[] { "Candidate sample 80%", "Candidate sample 100%" }, SelectedIndex = 0 };
        var content = new StackPanel { Margin = new Thickness(12) };
        content.SetResourceReference(Panel.BackgroundProperty, "BackgroundColor");
        foreach (var field in fields) { field.Margin = new Thickness(0, 4, 0, 4); content.Children.Add(field); }
        content.Children.Add(combo);
        var disabledCombo = new ComboBox { ItemsSource = new[] { "Disabled candidate sample" }, SelectedIndex = 0, IsEnabled = false };
        var readOnlyCombo = new ComboBox { ItemsSource = new[] { "Read only candidate sample" }, SelectedIndex = 0, IsEditable = true, IsReadOnly = true };
        content.Children.Add(disabledCombo); content.Children.Add(readOnlyCombo);
        var valueEditor = new IntegerEditor { Label = "Width", Unit = "px", Minimum = 200, Maximum = 3000, Value = 800 };
        content.Children.Add(valueEditor);
        var palette = new Window { Title = "WindowSlu input palette verification", Width = 520, Height = 470, Owner = owner, Content = content };
        try
        {
            palette.Show(); Pump(palette); fields[3].Focus(); fields[3].SelectAll(); Pump(palette);
            foreach (var field in fields) Assert.True(Contrast(field.Foreground, field.Background) >= (field.IsEnabled ? 4.5 : 3));
            Assert.True(Contrast(disabledCombo.Foreground, disabledCombo.Background) >= 3);
            Assert.True(Contrast(readOnlyCombo.Foreground, readOnlyCombo.Background) >= 4.5);
            Assert.True(Contrast(fields[3].SelectionTextBrush, fields[3].SelectionBrush) >= 4.5);
            AssertSelectedTextInk(fields[3]);
            // Default data templates must inherit the paired control color, not an App TextBlock style.
            var selectedText = Visuals<TextBlock>(combo).Single(t => t.Text == "Candidate sample 80%");
            Assert.True(Contrast(selectedText.Foreground, combo.Background) >= 4.5);
            if (directory != null) Capture((FrameworkElement)palette.Content, Path.Combine(directory, $"palette-{theme}-fixture.png"));
            combo.IsDropDownOpen = true; Pump(palette);
            var item = (ComboBoxItem)combo.ItemContainerGenerator.ContainerFromIndex(0)!;
            Assert.True(Contrast(item.Foreground, item.Background) >= 4.5);
            Assert.True(Contrast(Visuals<TextBlock>(item).First().Foreground, item.Background) >= 4.5);
            var popup = (Popup)combo.Template.FindName("PART_Popup", combo);
            if (directory != null) Capture((FrameworkElement)popup.Child, Path.Combine(directory, $"candidates-{theme}-fixture.png"));
            combo.IsDropDownOpen = false;
            ((Button)valueEditor.FindName("ValuesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var values = ((Button)valueEditor.FindName("ValuesButton")).ContextMenu;
            values.UpdateLayout();
            foreach (var valueItem in values.Items.OfType<MenuItem>()) Assert.True(Contrast(valueItem.Foreground, valueItem.Background) >= 4.5);
            var highlight = (MenuItem)values.Items[0]; highlight.Focus(); Pump(palette);
            Assert.True(highlight.IsHighlighted); Assert.True(Contrast(highlight.Foreground, highlight.Background) >= 4.5);
            Assert.True(Contrast(Visuals<TextBlock>(highlight).First(t => t.Text.Contains("640")).Foreground, highlight.Background) >= 4.5);
            if (directory != null) Capture(values, Path.Combine(directory, $"values-highlight-{theme}-fixture.png"));
            values.IsOpen = false;
        }
        finally { palette.Close(); }
    }
    private static void AssertSelectedTextInk(TextBox field)
    {
        int width = (int)Math.Ceiling(field.ActualWidth), height = (int)Math.Ceiling(field.ActualHeight);
        var image = Snapshot(field, width, height);
        var pixels = new byte[width * height * 4]; image.CopyPixels(pixels, width * 4, 0);
        var ink = ((SolidColorBrush)field.SelectionTextBrush).Color; int count = 0;
        for (int y = 5; y < height - 5; y++)
        for (int x = 10; x < Math.Min(180, width - 10); x++)
        {
            int i = (y * width + x) * 4;
            if (pixels[i + 3] > 240 && Math.Abs(pixels[i] - ink.B) + Math.Abs(pixels[i + 1] - ink.G) + Math.Abs(pixels[i + 2] - ink.R) < 35) count++;
        }
        AppContext.TryGetSwitch("Switch.System.Windows.Controls.Text.UseAdornerForTextboxSelectionRendering", out bool adorner);
        Assert.True(count > 5, $"Selected text glyph pixels={count}, switch={adorner}, default opacity={TextBoxBase.SelectionOpacityProperty.GetMetadata(typeof(TextBox)).DefaultValue}, active={field.IsSelectionActive}, focus={field.IsKeyboardFocusWithin}.");
    }
    private static double Contrast(Brush foreground, Brush background)
    {
        static double L(Color c)
        {
            static double Channel(byte value) { double v = value / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
            return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
        }
        double a = L(((SolidColorBrush)foreground).Color), b = L(((SolidColorBrush)background).Color);
        return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
    private static void Common(IntegerEditor editor, int value)
    {
        ((Button)editor.FindName("ValuesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menu = ((Button)editor.FindName("ValuesButton")).ContextMenu;
        menu.Items.OfType<MenuItem>().Single(i => Equals(i.Tag, value)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.IsOpen = false;
    }
    private static void Capture(FrameworkElement content, string path)
    {
        content.UpdateLayout(); int width = (int)Math.Ceiling(content.ActualWidth), height = (int)Math.Ceiling(content.ActualHeight);
        var bitmap = Snapshot(content, width, height);
        using var file = File.Create(path); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(file);
    }
    private static RenderTargetBitmap Snapshot(Visual visual, int width, int height)
    {
        var drawing = new DrawingVisual();
        using (var context = drawing.RenderOpen()) context.DrawRectangle(new VisualBrush(visual), null, new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(drawing); return bitmap;
    }
    private static void Pump(Window window) { window.UpdateLayout(); window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle); }
    private static void Wait(Task task, Window window) { while (!task.IsCompleted) { Pump(window); Thread.Sleep(1); } task.GetAwaiter().GetResult(); }
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T item) yield return item;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
}
