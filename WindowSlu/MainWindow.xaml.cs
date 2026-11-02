using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Windows.Media;
using WindowSlu.Models;
using WindowSlu.Services;
using WindowSlu.ViewModels;
using Button = System.Windows.Controls.Button;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WindowSlu
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private WindowInfo? _selectedWindowInfo;
        private readonly ThemeService _themeService;
        private readonly SettingsService _settingsService;
        private HotkeyService? _hotkeyService;
        private GlobalMouseHookService? _globalMouseHookService;
        private Services.Theme _currentTheme = Services.Theme.Dark;
        private IntPtr _hwnd;

        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? MyNotifyIcon;
        private ContextMenu? _contextMenu;
        private MenuItem? _showMenuItem;
        private MenuItem? _exitMenuItem;
        private readonly MainViewModel _viewModel;
        private DispatcherTimer? _indicatorTimer;

        private readonly bool _enableSystemHooks;
        public MainWindow() : this(null, true) { }

        public MainWindow(string? dataDirectory, bool enableSystemHooks)
        {
            _enableSystemHooks = enableSystemHooks;
            LoggingService.LogInfo("MainWindow constructor called.");
            InitializeComponent();
            InitializeIndicatorTimer();
            
            var windowService = new WindowService();
            _themeService = new ThemeService();
            _settingsService = new SettingsService(dataDirectory == null ? null : System.IO.Path.Combine(dataDirectory, "settings.json"));
            _viewModel = new MainViewModel(_settingsService, windowService, this, dataDirectory);
            this.DataContext = _viewModel;

            // Tests can render the UI without installing system input hooks.
            if (enableSystemHooks)
            try
            {
                _globalMouseHookService = new GlobalMouseHookService();
                _globalMouseHookService.GlobalMouseWheelEvent += (sender, e) =>
                {
                    try
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (e.WindowHandle == IntPtr.Zero)
                            {
                                return;
                            }

                            // メインウィンドウ自体は対象外
                            if (e.WindowHandle == _hwnd)
                            {
                                return;
                            }

                            // 管理対象のウィンドウか確認
                            var windowInfo = _viewModel.Windows.FirstOrDefault(w => w.Handle == e.WindowHandle);
                            if (windowInfo == null)
                            {
                                return;
                            }

                            if (!_settingsService.Settings.MouseWheelOpacityEnabled) return;
                            int currentOpacity = _viewModel.WindowService.GetTransparency(e.WindowHandle);

                            const int step = 10;
                            int direction = e.Delta > 0 ? 1 : -1;
                            int newOpacity = Math.Max(0, Math.Min(100, currentOpacity + direction * step));

                            if (newOpacity == currentOpacity)
                            {
                                e.Handled = true;
                                return;
                            }

                            if (!SetTransparency(e.WindowHandle, newOpacity)) return;
                            ShowTransparencyIndicator(windowInfo.Opacity);
                            _viewModel.StatusText = $"Opacity: {windowInfo.Opacity}% (Ctrl+MouseWheel)";

                            // 他アプリのズームなど既定動作を抑制
                            e.Handled = true;
                        });
                    }
                    catch (Exception ex)
                    {
                        LoggingService.LogError($"Error handling global mouse wheel event: {ex.Message}");
                    }
                };
                LoggingService.LogInfo("GlobalMouseHookService initialized successfully.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to initialize GlobalMouseHookService: {ex.Message}");
            }

            // Apply saved theme
            if (Enum.TryParse<Services.Theme>(_settingsService.Settings.Theme, out var savedTheme))
            {
                _currentTheme = savedTheme;
                _themeService.ApplyTheme(_currentTheme);
            }

            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void InitializeIndicatorTimer()
        {
            _indicatorTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _indicatorTimer.Tick += (s, e) =>
            {
                if(TransparencyIndicator != null)
                {
                    TransparencyIndicator.Visibility = Visibility.Collapsed;
                }
                _indicatorTimer.Stop();
            };
        }

        public void ShowTransparencyIndicator(int newOpacity)
        {
            if (TransparencyIndicator != null && TransparencyValueText != null)
            {
                TransparencyValueText.Text = $"{newOpacity}%";
                TransparencyIndicator.Visibility = Visibility.Visible;
                _indicatorTimer?.Stop();
                _indicatorTimer?.Start();
            }
        }

        private void HandleHotkey(HotkeySetting setting)
        {
            var action = setting.Action;
            var parameter = setting.Parameter;

            // プリセット適用アクションは別処理
            if (action == HotkeyAction.ApplyPreset)
            {
                HandleApplyPresetHotkey(setting.PresetId);
                return;
            }

            IntPtr foregroundWindow = WindowService.GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                LoggingService.LogInfo("No foreground window found.");
                return;
            }

            if (foregroundWindow == _hwnd)
            {
                LoggingService.LogInfo("Skipping hotkey action on main application window to prevent unintended side effects.");
                return;
            }

            LoggingService.LogInfo($"Operating on foreground window: {foregroundWindow}");

            switch (action)
            {
                case HotkeyAction.IncreaseOpacity:
                    {
                        int currentOpacity = _viewModel.WindowService.GetTransparency(foregroundWindow);
                        int newOpacity = Math.Min(100, currentOpacity + parameter);
                        SetTransparency(foregroundWindow, newOpacity);
                        ShowTransparencyIndicator(newOpacity);
                        break;
                    }
                case HotkeyAction.DecreaseOpacity:
                    {
                        int currentOpacity = _viewModel.WindowService.GetTransparency(foregroundWindow);
                        int newOpacity = Math.Max(0, currentOpacity - parameter);
                        SetTransparency(foregroundWindow, newOpacity);
                        ShowTransparencyIndicator(newOpacity);
                        break;
                    }
                case HotkeyAction.SetOpacity:
                    {
                        SetTransparency(foregroundWindow, parameter);
                        ShowTransparencyIndicator(parameter);
                        break;
                    }
                case HotkeyAction.ToggleTopMost:
                    _viewModel.WindowService.ToggleTopMost(foregroundWindow);
                    var topMostInfo = _viewModel.Windows.FirstOrDefault(w => w.Handle == foregroundWindow);
                    if (topMostInfo != null) topMostInfo.IsTopMost = _viewModel.WindowService.IsTopMost(foregroundWindow);
                    break;
                case HotkeyAction.ToggleClickThrough:
                    _viewModel.WindowService.ToggleClickThrough(foregroundWindow);
                    var clickThroughInfo = _viewModel.Windows.FirstOrDefault(w => w.Handle == foregroundWindow);
                    if (clickThroughInfo != null) clickThroughInfo.IsClickThrough = _viewModel.WindowService.IsClickThrough(foregroundWindow);
                    break;
                case HotkeyAction.SetAllTo80:
                    SetAllWindowsOpacity(80);
                    break;
            }
        }

        /// <summary>
        /// すべてのプリセットのホットキーを登録
        /// </summary>
        private void RegisterAllPresetHotkeys()
        {
            if (_hotkeyService == null) return;
            var errors = _hotkeyService.LastRegistrationErrors.ToList();
            foreach (var preset in _viewModel.PresetService.Presets)
                if (!string.IsNullOrWhiteSpace(preset.HotKey) && !_hotkeyService.RegisterPresetHotkey(preset.Id, preset.HotKey))
                    errors.AddRange(_hotkeyService.LastRegistrationErrors);
            _viewModel.StatusText = errors.Count == 0 ? "Hotkeys enabled" : string.Join("; ", errors);
        }

        /// <summary>
        /// プリセット適用ホットキーの処理
        /// </summary>
        private void HandleApplyPresetHotkey(string? presetId)
        {
            var preset = presetId == null ? null : _viewModel.PresetService.GetPresetById(presetId);
            if (preset == null) { _viewModel.StatusText = "Preset not found"; return; }
            ShowOperationResult($"Preset '{preset.Name}'", _viewModel.PresetService.ApplyPreset(preset, _viewModel.WindowGroups));
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("MainWindow_Loaded event triggered.");
            
            // Restore window position and size with validation
            RestoreWindowPosition();

            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            if (_enableSystemHooks && _settingsService.Settings.HotkeysEnabled)
            {
                _hotkeyService = new HotkeyService(_hwnd, _settingsService, HandleHotkey);
                
                // 既存プリセットのホットキーを登録
                RegisterAllPresetHotkeys();
            }
            
            // Add a hook to receive window messages
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(WndProc);
            }
            
            try
            {
                MyNotifyIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("NotifyIcon");
                if (MyNotifyIcon != null)
                {
                    _contextMenu = new ContextMenu();
                    
                    // Show
                    _showMenuItem = new MenuItem { Header = "Show WindowSlu" };
                    _showMenuItem.Click += ShowMenuItem_Click;
                    _contextMenu.Items.Add(_showMenuItem);
                    
                    _contextMenu.Items.Add(new Separator());
                    
                    // Quick Opacity
                    var opacityMenu = new MenuItem { Header = "Set All Opacity" };
                    var opacity100 = new MenuItem { Header = "100% (Opaque)" };
                    opacity100.Click += (s, e) => SetAllWindowsOpacity(100);
                    var opacity80 = new MenuItem { Header = "80%" };
                    opacity80.Click += (s, e) => SetAllWindowsOpacity(80);
                    var opacity50 = new MenuItem { Header = "50%" };
                    opacity50.Click += (s, e) => SetAllWindowsOpacity(50);
                    opacityMenu.Items.Add(opacity100);
                    opacityMenu.Items.Add(opacity80);
                    opacityMenu.Items.Add(opacity50);
                    _contextMenu.Items.Add(opacityMenu);
                    
                    // Presets submenu (dynamically populated)
                    var presetsMenu = new MenuItem { Header = "Apply Preset" };
                    presetsMenu.SubmenuOpened += (s, e) => RefreshPresetsSubmenu(presetsMenu);
                    _contextMenu.Items.Add(presetsMenu);
                    
                    // Layouts submenu (dynamically populated)
                    var layoutsMenu = new MenuItem { Header = "Restore Layout" };
                    layoutsMenu.SubmenuOpened += (s, e) => RefreshLayoutsSubmenu(layoutsMenu);
                    _contextMenu.Items.Add(layoutsMenu);
                    
                    _contextMenu.Items.Add(new Separator());
                    
                    // Exit
                    _exitMenuItem = new MenuItem { Header = "Exit" };
                    _exitMenuItem.Click += ExitMenuItem_Click;
                    _contextMenu.Items.Add(_exitMenuItem);
                    
                    MyNotifyIcon.ContextMenu = _contextMenu;
                }
            }
            catch (ResourceReferenceKeyNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Failed to load NotifyIcon from resources. It might be created later. Details: {ex.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] An unexpected error occurred with NotifyIcon: {ex.Message}");
            }
            
            await _viewModel.RefreshWindowList();
            _viewModel.InitializeGroups();
            _viewModel.Start();
            var loadErrors = new[] { _settingsService.LastError, _viewModel.PresetService.LastError, _viewModel.LayoutService.LastError }.Where(error => error != null);
            if (loadErrors.Any()) _viewModel.StatusText = string.Join("; ", loadErrors);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            LoggingService.LogInfo("Main window closing. Disposing resources.");
            _hotkeyService?.Dispose();
            _globalMouseHookService?.Dispose();

            // Save window position and size
            if (double.IsFinite(this.Left)) _settingsService.Settings.WindowLeft = this.Left;
            if (double.IsFinite(this.Top)) _settingsService.Settings.WindowTop = this.Top;
            if (double.IsFinite(this.Width)) _settingsService.Settings.WindowWidth = this.Width;
            if (double.IsFinite(this.Height)) _settingsService.Settings.WindowHeight = this.Height;

            // Save current theme
            _settingsService.Settings.Theme = _currentTheme.ToString();
            
            if (_viewModel.PresetService.LastError == null && !_viewModel.PresetService.SavePresets())
                MessageBox.Show(_viewModel.PresetService.LastError, "Groups save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            if (!_settingsService.SaveSettings()) MessageBox.Show(_settingsService.LastError, "Settings save failed", MessageBoxButton.OK, MessageBoxImage.Warning);

            _viewModel.Stop();
            MyNotifyIcon?.Dispose();
        }
        
        private bool SetTransparency(IntPtr hWnd, int percent)
        {
            bool success = _viewModel.WindowService.SetTransparency(hWnd, percent);
            var window = _viewModel.Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (window != null) window.Opacity = _viewModel.WindowService.GetTransparency(hWnd);
            if (!success) _viewModel.StatusText = "Could not change opacity; the actual value has been reloaded.";
            return success;
        }

        private void ToggleTopMost(IntPtr hWnd)
        {
            bool success = _viewModel.WindowService.ToggleTopMost(hWnd);
            var window = _viewModel.Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (window != null) window.IsTopMost = _viewModel.WindowService.IsTopMost(hWnd);
            if (!success) _viewModel.StatusText = "Could not change Always on Top.";
        }

        /// <summary>
        /// ウィンドウ位置を復元し、画面外の場合は中央に配置する
        /// </summary>
        private void RestoreWindowPosition()
        {
            double left = _settingsService.Settings.WindowLeft;
            double top = _settingsService.Settings.WindowTop;
            double width = _settingsService.Settings.WindowWidth;
            double height = _settingsService.Settings.WindowHeight;

            // サイズの検証（最小サイズを確保）
            if (width < 400) width = 800;
            if (height < 300) height = 450;

            // 画面の作業領域を取得
            var workArea = SystemParameters.WorkArea;

            // 位置の検証（画面内に収まるか確認）
            bool isPositionValid = 
                left >= workArea.Left - 100 && 
                left < workArea.Right - 100 &&
                top >= workArea.Top - 50 && 
                top < workArea.Bottom - 100;

            if (!isPositionValid)
            {
                // 画面外の場合は中央に配置
                left = (workArea.Width - width) / 2 + workArea.Left;
                top = (workArea.Height - height) / 2 + workArea.Top;
                LoggingService.LogInfo($"Window position was off-screen. Resetting to center: ({left}, {top})");
            }

            this.Left = left;
            this.Top = top;
            this.Width = width;
            this.Height = height;

            LoggingService.LogInfo($"Window position restored: Left={left}, Top={top}, Width={width}, Height={height}");
        }

        private IEnumerable<WindowInfo> GetBulkTargetWindows()
        {
            if (_viewModel.SelectedGroup != null)
            {
                return _viewModel.SelectedGroup.Windows;
            }

            if (_selectedWindowInfo != null)
            {
                var group = _viewModel.WindowGroups.FirstOrDefault(g => g.Windows.Contains(_selectedWindowInfo));
                if (group != null)
                {
                    return group.Windows;
                }

                return new[] { _selectedWindowInfo };
            }

            return _viewModel.Windows;
        }

        // --- Event Handlers ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private void WindowListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedWindowInfo = (sender as ListView)?.SelectedItem as WindowInfo;
            _viewModel.SelectedWindow = _selectedWindowInfo;
            if (_selectedWindowInfo != null) _viewModel.SelectedGroup = null;
        }
        
        private void WindowTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is WindowInfo windowInfo)
            {
                _selectedWindowInfo = windowInfo;
                _viewModel.SelectedWindow = windowInfo;
                _viewModel.SelectedGroup = null;
            }
            else if (e.NewValue is WindowGroup group)
            {
                _selectedWindowInfo = null;
                _viewModel.SelectedWindow = null;
                _viewModel.SelectedGroup = group;
                GroupNameTextBox.Text = group.Name;
            }
        }

        private void CascadeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WindowGroup group)
                ShowOperationResult("Cascade", _viewModel.PresetService.ApplyPreset(
                    new WindowPreset { EnableCascade = true }, new[] { group }));
        }
        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && toggleButton.DataContext is WindowInfo info)
            {
                _viewModel.WindowService.ToggleTopMost(info.Handle);
                info.IsTopMost = _viewModel.WindowService.IsTopMost(info.Handle);
            }
        }
        private void ClickThroughButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Primitives.ToggleButton toggleButton && toggleButton.DataContext is WindowInfo info)
            {
                _viewModel.WindowService.ToggleClickThrough(info.Handle);
                info.IsClickThrough = _viewModel.WindowService.IsClickThrough(info.Handle);
            }
        }
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) 
        { 
            if (sender is Slider slider && (slider.IsMouseCaptureWithin || slider.IsKeyboardFocusWithin) && slider.DataContext is WindowInfo info)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Slider changed for '{info.Title}' to {e.NewValue}");
                SetTransparency(info.Handle, (int)e.NewValue); 
            }
        }
        private void WindowWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.IsMouseCaptureWithin && slider.DataContext is WindowInfo info)
            {
                int newWidth = (int)e.NewValue;
                int height = info.Height > 0 ? info.Height : 600;
                if (_viewModel.WindowService.SetWindowSize(info.Handle, newWidth, height)) _viewModel.WindowService.UpdateWindowPositionInfo(info);
                else _viewModel.StatusText = "Could not resize window.";
            }
        }

        private void WindowHeightSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && slider.IsMouseCaptureWithin && slider.DataContext is WindowInfo info)
            {
                int newHeight = (int)e.NewValue;
                int width = info.Width > 0 ? info.Width : 800;
                if (_viewModel.WindowService.SetWindowSize(info.Handle, width, newHeight)) _viewModel.WindowService.UpdateWindowPositionInfo(info);
                else _viewModel.StatusText = "Could not resize window.";
            }
        }

        // --- Group Bulk Control Event Handlers ---
        private void ApplyGroupOpacity(WindowGroup group, int opacity)
        {
            var result = new WindowOperationResult();
            foreach (var window in group.Windows)
                if (SetTransparency(window.Handle, opacity)) result.Succeeded++; else result.Failed++;
            ShowOperationResult($"Group '{group.Name}' opacity", result);
        }

        private void GroupOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider && (slider.IsMouseCaptureWithin || slider.IsKeyboardFocusWithin) && slider.Tag is WindowGroup group)
                ApplyGroupOpacity(group, (int)e.NewValue);
        }

        private void GroupResetOpacity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WindowGroup group) ApplyGroupOpacity(group, 100);
        }

        private void GroupSizeApply_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WindowGroup group)
            {
                // ボタンの親要素からTextBoxを探す
                var parent = button.Parent as StackPanel;
                if (parent == null) return;

                int width = 800;
                int height = 600;

                foreach (var child in parent.Children)
                {
                    if (child is TextBox textBox)
                    {
                        if (textBox.Name == "GroupWidthTextBox" && int.TryParse(textBox.Text, out int w))
                        {
                            width = w;
                        }
                        else if (textBox.Name == "GroupHeightTextBox" && int.TryParse(textBox.Text, out int h))
                        {
                            height = h;
                        }
                    }
                }

                var result = new WindowOperationResult();
                foreach (var window in group.Windows)
                {
                    if (_viewModel.WindowService.SetWindowSize(window.Handle, width, height))
                    { _viewModel.WindowService.UpdateWindowPositionInfo(window); result.Succeeded++; }
                    else result.Failed++;
                }
                ShowOperationResult("Group resize", result);
            }
        }

        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) 
        {
            _currentTheme = _currentTheme == Services.Theme.Dark ? Services.Theme.Light : Services.Theme.Dark; 
            _themeService.ApplyTheme(_currentTheme);
        }

        // --- Drag & Drop Event Handlers ---
        private void WindowItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is Grid grid && grid.Tag is WindowInfo windowInfo)
            {
                // スライダーやToggleButton上ではD&Dを開始しない
                var source = e.OriginalSource as DependencyObject;
                while (source != null && source != grid)
                {
                    if (source is Slider || source is System.Windows.Controls.Primitives.ToggleButton || 
                        source is System.Windows.Controls.Primitives.Thumb || source is Button ||
                        source is TextBox)
                    {
                        return; // コントロール操作中はD&Dしない
                    }
                    source = VisualTreeHelper.GetParent(source);
                }
                
                DragDrop.DoDragDrop(grid, windowInfo, DragDropEffects.Move);
            }
        }

        private void GroupHeader_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(WindowInfo)))
            {
                e.Effects = DragDropEffects.Move;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void GroupHeader_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(WindowInfo)) is WindowInfo window && sender is FrameworkElement element && element.Tag is WindowGroup group)
            {
                _viewModel.GroupingService.AddToGroup(group, window);
                _viewModel.StatusText = _viewModel.PresetService.SavePresets()
                    ? $"Moved '{window.Title}' to '{group.Name}'" : _viewModel.PresetService.LastError!;
            }
            e.Handled = true;
        }
        
        // --- Tray Icon ---
        private void TrayButton_Click(object sender, RoutedEventArgs e) { if(MyNotifyIcon != null) { Hide(); MyNotifyIcon.Visibility = Visibility.Visible; } }
        private void ShowWindow() { if(MyNotifyIcon != null) { Show(); WindowState = WindowState.Normal; Activate(); MyNotifyIcon.Visibility = Visibility.Collapsed; } }
        private void NotifyIcon_DoubleClick(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ShowMenuItem_Click(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) { Close(); }

        private void SetAllWindowsOpacity(int opacity)
        {
            var result = new WindowOperationResult();
            foreach (var window in _viewModel.Windows.ToList())
            {
                if (SetTransparency(window.Handle, opacity)) result.Succeeded++;
                else result.Failed++;
            }
            ShowOperationResult($"Opacity {opacity}%", result);
        }

        private void RefreshPresetsSubmenu(MenuItem presetsMenu)
        {
            presetsMenu.Items.Clear();
            if (_viewModel.PresetService.Presets.Count == 0)
            {
                presetsMenu.Items.Add(new MenuItem { Header = "(No presets)", IsEnabled = false });
                return;
            }
            foreach (var preset in _viewModel.PresetService.Presets)
            {
                var item = new MenuItem { Header = preset.Name, Tag = preset };
                item.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is WindowPreset p)
                    {
                        ShowOperationResult($"Preset '{p.Name}'", _viewModel.PresetService.ApplyPreset(p, _viewModel.WindowGroups));
                    }
                };
                presetsMenu.Items.Add(item);
            }
        }

        private void RefreshLayoutsSubmenu(MenuItem layoutsMenu)
        {
            layoutsMenu.Items.Clear();
            if (_viewModel.LayoutService.Layouts.Count == 0)
            {
                layoutsMenu.Items.Add(new MenuItem { Header = "(No layouts)", IsEnabled = false });
                return;
            }
            foreach (var layout in _viewModel.LayoutService.Layouts)
            {
                var item = new MenuItem { Header = layout.Name, Tag = layout };
                item.Click += (s, e) =>
                {
                    if (s is MenuItem mi && mi.Tag is WindowLayout l)
                    {
                        _viewModel.LayoutService.RestoreLayout(l, _viewModel.Windows);
                        ShowOperationResult($"Layout '{l.Name}'", _viewModel.LayoutService.LastRestoreResult);
                    }
                };
                layoutsMenu.Items.Add(item);
            }
        }

        private void LightTheme_Click(object sender, RoutedEventArgs e)
        {
            _currentTheme = Services.Theme.Light;
            _themeService.ApplyTheme(_currentTheme);
        }

        private void DarkTheme_Click(object sender, RoutedEventArgs e)
        {
            _currentTheme = Services.Theme.Dark;
            _themeService.ApplyTheme(_currentTheme);
        }

        // --- Preset Event Handlers ---
        private void ApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is WindowPreset preset)
                ShowOperationResult($"Preset '{preset.Name}'", _viewModel.PresetService.ApplyPreset(
                    preset, _viewModel.WindowGroups, _viewModel.SelectedGroup, _selectedWindowInfo));
        }

        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = _viewModel.PresetService.CreatePreset($"Preset {_viewModel.PresetService.Presets.Count + 1}");
            PresetComboBox.SelectedItem = preset;
            _viewModel.StatusText = _viewModel.PresetService.LastError ?? $"Created new preset: {preset.Name}";
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is WindowPreset preset)
            {
                string name = preset.Name;
                // ホットキーを解除
                _hotkeyService?.UnregisterPresetHotkey(preset.Id);
                _viewModel.PresetService.DeletePreset(preset.Id);
                _viewModel.StatusText = _viewModel.PresetService.LastError ?? $"Deleted preset: {name}";
            }
        }

        /// <summary>
        /// ホットキー入力用のキー入力ハンドラ
        /// </summary>
        private void HotKeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.TextBox textBox) return;
            if (PresetComboBox.SelectedItem is not WindowPreset preset) return;

            e.Handled = true;

            var key = e.Key == Key.System ? e.SystemKey : e.Key;

            // 修飾キーのみの場合は無視
            if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftAlt || key == Key.RightAlt ||
                key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
            {
                return;
            }

            // Escapeでクリア
            if (key == Key.Escape)
            {
                preset.HotKey = null;
                _hotkeyService?.UnregisterPresetHotkey(preset.Id);
                _viewModel.StatusText = _viewModel.PresetService.SavePresets() ? "Hotkey cleared and saved" : _viewModel.PresetService.LastError!;
                return;
            }

            // ホットキー文字列を構築
            var parts = new List<string>();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(key.ToString());

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) parts.Insert(0, "Win");
            var hotKeyString = string.Join("+", parts);
            TrySetPresetHotkey(preset, hotKeyString);
            textBox.Text = preset.HotKey ?? string.Empty;
        }

        private bool TrySetPresetHotkey(WindowPreset preset, string hotkey)
        {
            if (_hotkeyService != null && !_hotkeyService.RegisterPresetHotkey(preset.Id, hotkey))
            {
                _viewModel.StatusText = string.Join("; ", _hotkeyService.LastRegistrationErrors);
                return false;
            }
            preset.HotKey = hotkey;
            bool saved = _viewModel.PresetService.SavePresets();
            _viewModel.StatusText = saved
                ? $"Hotkey '{hotkey}' saved" + (_hotkeyService == null ? " (enable Hotkeys in Settings to activate)" : "")
                : _viewModel.PresetService.LastError!;
            return saved;
        }

        private void ClearPresetGroup_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is WindowPreset preset) preset.TargetGroupId = null;
        }

        private void SavePresets_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StatusText = _viewModel.PresetService.SavePresets() ? "Presets and groups saved" : _viewModel.PresetService.LastError!;
        }

        // --- Layout Event Handlers ---
        private void RestoreLayout_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutComboBox.SelectedItem is WindowLayout layout)
            {
                _viewModel.LayoutService.RestoreLayout(layout, _viewModel.Windows);
                ShowOperationResult($"Layout '{layout.Name}'", _viewModel.LayoutService.LastRestoreResult);
            }
            else
            {
                _viewModel.StatusText = "Please select a layout to restore";
            }
        }

        private void CaptureLayout_Click(object sender, RoutedEventArgs e)
        {
            string layoutName = $"Layout {_viewModel.LayoutService.Layouts.Count + 1}";
            var layout = _viewModel.LayoutService.CaptureCurrentLayout(layoutName, _viewModel.Windows);
            LayoutComboBox.SelectedItem = layout;
            _viewModel.StatusText = _viewModel.LayoutService.LastError ?? $"Captured '{layout.Name}' ({layout.Entries.Count} windows)";
        }

        private void DeleteLayout_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutComboBox.SelectedItem is WindowLayout layout)
            {
                string name = layout.Name;
                _viewModel.LayoutService.DeleteLayout(layout.Id);
                _viewModel.StatusText = _viewModel.LayoutService.LastError ?? $"Deleted layout: {name}";
            }
        }

        private void UpdateLayout_Click(object sender, RoutedEventArgs e)
        {
            if (LayoutComboBox.SelectedItem is WindowLayout layout)
            {
                _viewModel.LayoutService.UpdateLayout(layout, _viewModel.Windows);
                _viewModel.StatusText = _viewModel.LayoutService.LastError ?? $"Updated layout '{layout.Name}'";
            }
            else
            {
                _viewModel.StatusText = "Please select a layout to update";
            }
        }

        private void SaveLayouts_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StatusText = _viewModel.LayoutService.SaveLayouts() ? "Layouts saved" : _viewModel.LayoutService.LastError!;
        }


        private void ShowOperationResult(string action, WindowOperationResult result)
        {
            _viewModel.StatusText = $"{action}: {result.Summary}";
            _viewModel.OperationDetails = string.Join(Environment.NewLine, result.Details);
        }

        private void NewGroup_Click(object sender, RoutedEventArgs e)
        {
            string name = GroupNameTextBox.Text.Trim();
            if (name.Length == 0) { _viewModel.StatusText = "Enter a group name."; return; }
            var windows = _selectedWindowInfo == null ? Array.Empty<WindowInfo>() : new[] { _selectedWindowInfo };
            var group = _viewModel.GroupingService.CreateManualGroup(name, windows);
            _viewModel.SelectedGroup = group;
            _viewModel.SelectedWindow = null; _selectedWindowInfo = null;
            _viewModel.StatusText = _viewModel.PresetService.SavePresets() ? $"Created group '{name}'" : _viewModel.PresetService.LastError!;
        }

        private void RenameGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = _viewModel.SelectedGroup;
            string name = GroupNameTextBox.Text.Trim();
            if (group?.Type != GroupType.Manual || name.Length == 0) { _viewModel.StatusText = "Select a manual group and enter its new name."; return; }
            _viewModel.GroupingService.RenameGroup(group, name);
            _viewModel.StatusText = _viewModel.PresetService.SavePresets() ? $"Renamed group to '{name}'" : _viewModel.PresetService.LastError!;
        }

        private void DeleteGroup_Click(object sender, RoutedEventArgs e)
        {
            var group = _viewModel.SelectedGroup;
            if (group?.Type != GroupType.Manual) { _viewModel.StatusText = "Select a manual group to delete."; return; }
            _viewModel.GroupingService.DeleteGroup(group);
            _viewModel.SelectedGroup = null;
            _viewModel.GroupingService.UpdateGroups(_viewModel.Windows);
            _viewModel.StatusText = _viewModel.PresetService.SavePresets() ? "Group deleted; windows returned to automatic groups." : _viewModel.PresetService.LastError!;
        }

        private void CapturePreset_Click(object sender, RoutedEventArgs e)
        {
            WindowPreset preset;
            if (_viewModel.SelectedGroup is WindowGroup group && group.Windows.Count > 0)
                preset = _viewModel.PresetService.CreatePresetFromGroup(group, group.Name + " preset");
            else if (_selectedWindowInfo != null)
                preset = _viewModel.PresetService.CreatePresetFromWindow(_selectedWindowInfo, _selectedWindowInfo.ProcessName + " preset");
            else { _viewModel.StatusText = "Select a window or non-empty group in Windows first."; return; }
            PresetComboBox.SelectedItem = preset;
            _viewModel.StatusText = _viewModel.PresetService.LastError ?? $"Captured preset '{preset.Name}'";
        }

        private void SavePreferences_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.StatusText = _settingsService.SaveSettings() ? "Preferences saved" : _settingsService.LastError!;
        }

        // --- Hotkey Event Handlers ---
        private void EditHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeySetting setting)
            {
                // シンプルな編集ダイアログ
                var dialog = new Window
                {
                    Title = $"Edit Hotkey for {setting.Action}",
                    Width = 300,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Background = Background,
                    Foreground = Foreground
                };

                var stackPanel = new StackPanel { Margin = new Thickness(10) };
                var instructionText = new TextBlock
                {
                    Text = "Press the key combination you want to assign:",
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(instructionText);

                var keyTextBox = new TextBox
                {
                    IsReadOnly = true,
                    Text = setting.Keys,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                stackPanel.Children.Add(keyTextBox);

                var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                var okButton = new Button { Content = "OK", Width = 60, Margin = new Thickness(5, 0, 0, 0) };
                var cancelButton = new Button { Content = "Cancel", Width = 60, Margin = new Thickness(5, 0, 0, 0) };
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;

                KeyEventHandler keyDownHandler = null!;
                keyDownHandler = (s, args) =>
                {
                    var modifiers = Keyboard.Modifiers;
                    var key = args.Key;
                    if (key == Key.System) key = args.SystemKey; // Alt+Tab etc.

                    // 修飾キー単体は無視
                    if (key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt)
                    {
                        return;
                    }

                    // 修飾キーなしのホットキーは無効化（a など単独キーで OS 全体をブロックしないため）
                    if (modifiers == ModifierKeys.None)
                    {
                        _viewModel.StatusText = "Hotkeys must include Ctrl, Alt, or Shift.";
                        return;
                    }

                    var keyString = "";
                    if ((modifiers & ModifierKeys.Control) != 0) keyString += "Ctrl+";
                    if ((modifiers & ModifierKeys.Alt) != 0) keyString += "Alt+";
                    if ((modifiers & ModifierKeys.Shift) != 0) keyString += "Shift+";
                    keyString += key.ToString();

                    keyTextBox.Text = keyString;
                    setting.Keys = keyString;
                    setting.Modifiers = modifiers;
                    setting.Key = key;
                };

                keyTextBox.KeyDown += keyDownHandler;

                okButton.Click += (s, args) =>
                {
                    dialog.Close();
                };

                cancelButton.Click += (s, args) =>
                {
                    dialog.Close();
                };

                dialog.ShowDialog();

                keyTextBox.KeyDown -= keyDownHandler;
            }
        }

        private void SaveHotkeys_Click(object sender, RoutedEventArgs e)
        {
            HotkeySettingsList.Items.Refresh();
            if (!_settingsService.SaveSettings()) { _viewModel.StatusText = _settingsService.LastError!; return; }
            if (_enableSystemHooks && _settingsService.Settings.HotkeysEnabled)
            {
                if (_hotkeyService == null)
                {
                    _hotkeyService = new HotkeyService(_hwnd, _settingsService, HandleHotkey);
                    RegisterAllPresetHotkeys();
                }
                else
                {
                    _hotkeyService.ReloadSettings();
                    _viewModel.StatusText = _hotkeyService.LastRegistrationErrors.Count == 0
                        ? "Hotkeys enabled and saved" : string.Join("; ", _hotkeyService.LastRegistrationErrors);
                }
            }
            else
            {
                _hotkeyService?.Dispose(); _hotkeyService = null;
                _viewModel.StatusText = "Hotkeys disabled and saved";
            }
        }

        private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.HotkeySettings = new ApplicationSettings().HotkeySettings;
            HotkeySettingsList.ItemsSource = _settingsService.Settings.HotkeySettings;
            SaveHotkeys_Click(sender, e);
        }

        // --- Bulk Window Operations ---
        private void SetAllTo80Percent_Click(object sender, RoutedEventArgs e)
        {
            ApplyBulkOpacity(80);
        }

        private void SetAllTo100Percent_Click(object sender, RoutedEventArgs e) => ApplyBulkOpacity(100);

        private void ApplyBulkOpacity(int opacity)
        {
            var result = new WindowOperationResult();
            foreach (var window in GetBulkTargetWindows().ToList())
                if (SetTransparency(window.Handle, opacity)) result.Succeeded++; else result.Failed++;
            ShowOperationResult($"Selected targets opacity {opacity}%", result);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                _hotkeyService?.HandleHotkeyMessage(hotkeyId);
                handled = true;
            }
            return IntPtr.Zero;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
    }
} 