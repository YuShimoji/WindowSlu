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
        private Services.Theme _currentTheme = Services.Theme.Dark;
        private IntPtr _hwnd;
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? MyNotifyIcon;
        private ContextMenu? _contextMenu;
        private MenuItem? _showMenuItem;
        private MenuItem? _exitMenuItem;
        private readonly MainViewModel _viewModel;
        private DispatcherTimer? _indicatorTimer;

        public MainWindow()
        {
            LoggingService.LogInfo("MainWindow constructor called.");
            InitializeComponent();
            InitializeIndicatorTimer();
            
            var windowService = new WindowService();
            _themeService = new ThemeService();
            _settingsService = new SettingsService();
            _viewModel = new MainViewModel(_settingsService, windowService, this);
            this.DataContext = _viewModel;

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

        private void HandleHotkey(HotkeyAction action, int parameter)
        {
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
                        _viewModel.WindowService.SetTransparency(foregroundWindow, newOpacity);
                        ShowTransparencyIndicator(newOpacity);
                        break;
                    }
                case HotkeyAction.DecreaseOpacity:
                    {
                        int currentOpacity = _viewModel.WindowService.GetTransparency(foregroundWindow);
                        int newOpacity = Math.Max(0, currentOpacity - parameter);
                        _viewModel.WindowService.SetTransparency(foregroundWindow, newOpacity);
                        ShowTransparencyIndicator(newOpacity);
                        break;
                    }
                case HotkeyAction.SetOpacity:
                    {
                        _viewModel.WindowService.SetTransparency(foregroundWindow, parameter);
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
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.StatusText = "全ウィンドウの透明度を80%に設定中...";
                        Task.Run(() =>
                        {
                            var windowsToChange = viewModel.Windows.ToList();
                            foreach (var window in windowsToChange)
                            {
                                if (window.Handle != _hwnd)
                                {
                                    // Set transparency in the background
                                    _viewModel.WindowService.SetTransparency(window.Handle, 80);
                                    
                                    // Update UI on the main thread
                                    Application.Current.Dispatcher.Invoke(() => window.Opacity = 80);
                                }
                            }
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                viewModel.StatusText = "全ウィンドウの透明度設定が完了しました。";
                            });
                        });
                    }
                    break;
            }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoggingService.LogInfo("MainWindow_Loaded event triggered.");
            // Restore window position and size
            this.Left = _settingsService.Settings.WindowLeft;
            this.Top = _settingsService.Settings.WindowTop;
            this.Width = _settingsService.Settings.WindowWidth;
            this.Height = _settingsService.Settings.WindowHeight;

            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            _hotkeyService = new HotkeyService(_hwnd, _settingsService, HandleHotkey);
            
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
                    _showMenuItem = new MenuItem { Header = "Show" };
                    _showMenuItem.Click += ShowMenuItem_Click;
                    _exitMenuItem = new MenuItem { Header = "Exit" };
                    _exitMenuItem.Click += ExitMenuItem_Click;
                    _contextMenu.Items.Add(_showMenuItem);
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
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            LoggingService.LogInfo("Main window closing. Disposing resources.");
            _hotkeyService?.Dispose();

            // Save window position and size
            _settingsService.Settings.WindowLeft = this.Left;
            _settingsService.Settings.WindowTop = this.Top;
            _settingsService.Settings.WindowWidth = this.Width;
            _settingsService.Settings.WindowHeight = this.Height;

            // Save current theme
            _settingsService.Settings.Theme = _currentTheme.ToString();
            
            _settingsService.SaveSettings();

            _viewModel.Stop();
            MyNotifyIcon?.Dispose();
        }
        
        private void SetTransparency(IntPtr hWnd, int percent)
        {
            if (hWnd == IntPtr.Zero) return;
            _viewModel.WindowService.SetTransparency(hWnd, percent);
            var windowInfo = _viewModel.Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (windowInfo != null) windowInfo.Opacity = percent;
        }

        private void ToggleTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            _viewModel.WindowService.ToggleTopMost(hWnd);
            var windowInfo = _viewModel.Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (windowInfo != null) windowInfo.IsTopMost = !windowInfo.IsTopMost;
        }

        // --- Event Handlers ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private void WindowListView_SelectionChanged(object sender, SelectionChangedEventArgs e) { _selectedWindowInfo = (sender as ListView)?.SelectedItem as WindowInfo; }
        
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
            }
        }

        private void CascadeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is WindowGroup group)
            {
                // 現在は簡易実装：グループ内のウィンドウをカスケード配置
                int offsetX = 30;
                int offsetY = 30;
                int startX = 100;
                int startY = 100;

                // 先頭ウィンドウの位置を基準にする
                if (group.Windows.Count > 0)
                {
                    var leadWindow = group.Windows[0];
                    startX = leadWindow.Left;
                    startY = leadWindow.Top;
                }

                int currentX = startX;
                int currentY = startY;

                foreach (var window in group.Windows)
                {
                    _viewModel.WindowService.SetWindowPosition(
                        window.Handle, 
                        currentX, 
                        currentY, 
                        window.Width > 0 ? window.Width : 800, 
                        window.Height > 0 ? window.Height : 600);
                    
                    window.Left = currentX;
                    window.Top = currentY;
                    
                    currentX += offsetX;
                    currentY += offsetY;
                }

                _viewModel.StatusText = $"Cascaded {group.Windows.Count} windows in '{group.Name}'";
            }
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
            if (sender is Slider slider && slider.DataContext is WindowInfo info) 
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Slider changed for '{info.Title}' to {e.NewValue}");
                SetTransparency(info.Handle, (int)e.NewValue); 
            }
        }
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) 
        {
            _currentTheme = _currentTheme == Services.Theme.Dark ? Services.Theme.Light : Services.Theme.Dark; 
            _themeService.ApplyTheme(_currentTheme);
        }
        
        // --- Tray Icon ---
        private void TrayButton_Click(object sender, RoutedEventArgs e) { if(MyNotifyIcon != null) { Hide(); MyNotifyIcon.Visibility = Visibility.Visible; } }
        private void ShowWindow() { if(MyNotifyIcon != null) { Show(); WindowState = WindowState.Normal; Activate(); MyNotifyIcon.Visibility = Visibility.Collapsed; } }
        private void NotifyIcon_DoubleClick(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ShowMenuItem_Click(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) { Close(); }

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
            {
                // 選択されたグループにプリセットを適用
                if (_viewModel.SelectedGroup != null)
                {
                    _viewModel.PresetService.ApplyPresetToGroup(preset, _viewModel.SelectedGroup);
                    _viewModel.StatusText = $"Applied preset '{preset.Name}' to group '{_viewModel.SelectedGroup.Name}'";
                }
                else if (_selectedWindowInfo != null)
                {
                    _viewModel.PresetService.ApplyPresetToWindow(preset, _selectedWindowInfo);
                    _viewModel.StatusText = $"Applied preset '{preset.Name}' to window";
                }
                else
                {
                    // 全グループに適用
                    foreach (var group in _viewModel.WindowGroups)
                    {
                        _viewModel.PresetService.ApplyPresetToGroup(preset, group);
                    }
                    _viewModel.StatusText = $"Applied preset '{preset.Name}' to all windows";
                }
            }
        }

        private void NewPreset_Click(object sender, RoutedEventArgs e)
        {
            var preset = _viewModel.PresetService.CreatePreset($"Preset {_viewModel.PresetService.Presets.Count + 1}");
            PresetComboBox.SelectedItem = preset;
            _viewModel.StatusText = $"Created new preset: {preset.Name}";
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (PresetComboBox.SelectedItem is WindowPreset preset)
            {
                string name = preset.Name;
                _viewModel.PresetService.DeletePreset(preset.Id);
                _viewModel.StatusText = $"Deleted preset: {name}";
            }
        }

        private void SavePresets_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PresetService.SavePresets();
            _viewModel.StatusText = "Presets saved";
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

                    // 修飾キーなしのキーだけを無視
                    if (modifiers == ModifierKeys.None && (key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt))
                        return;

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
            _viewModel.SettingsService.SaveSettings();
            _hotkeyService?.ReloadSettings();
            _viewModel.StatusText = "Hotkeys saved";
        }

        private void ResetHotkeys_Click(object sender, RoutedEventArgs e)
        {
            // デフォルトホットキーを設定
            var defaultHotkeys = new List<HotkeySetting>
            {
                new HotkeySetting("Ctrl+Alt+Up", HotkeyAction.IncreaseOpacity, 10),
                new HotkeySetting("Ctrl+Alt+Down", HotkeyAction.DecreaseOpacity, 10),
                new HotkeySetting("Ctrl+Alt+T", HotkeyAction.ToggleTopMost),
                new HotkeySetting("Ctrl+Alt+Shift+A", HotkeyAction.SetAllTo80, 80)
            };

            _viewModel.SettingsService.Settings.HotkeySettings = defaultHotkeys;
            HotkeySettingsList.ItemsSource = defaultHotkeys; // UI更新
            _viewModel.StatusText = "Hotkeys reset to defaults";
        }

        // --- Bulk Window Operations ---
        private void SetAllTo80Percent_Click(object sender, RoutedEventArgs e)
        {
            foreach (var window in _viewModel.Windows)
            {
                _viewModel.WindowService.SetTransparency(window.Handle, 80);
                window.Opacity = 80;
            }
            _viewModel.StatusText = "Set all windows to 80% opacity";
        }

        private void SetAllTo100Percent_Click(object sender, RoutedEventArgs e)
        {
            foreach (var window in _viewModel.Windows)
            {
                _viewModel.WindowService.SetTransparency(window.Handle, 100);
                window.Opacity = 100;
            }
            _viewModel.StatusText = "Set all windows to 100% opacity";
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