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
            _viewModel.Start();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
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
            // ... existing code ...
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