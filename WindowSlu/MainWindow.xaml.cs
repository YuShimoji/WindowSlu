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
        private readonly DispatcherTimer _updateTimer;
        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();
        private WindowInfo? _selectedWindowInfo;
        private readonly WindowService _windowService;
        private readonly ThemeService _themeService;
        private Services.Theme _currentTheme = Services.Theme.Dark;
        private IntPtr _hwnd;
        private HwndSource? _hwndSource;
        private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? MyNotifyIcon;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _windowService = new WindowService();
            _themeService = new ThemeService();
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (s, e) => await RefreshWindowList();
            
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            try
            {
                MyNotifyIcon = (Hardcodet.Wpf.TaskbarNotification.TaskbarIcon)FindResource("NotifyIcon");
                System.Diagnostics.Debug.WriteLine("[DEBUG] NotifyIcon loaded successfully.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Failed to load NotifyIcon: {ex.Message}");
            }
            await RefreshWindowList();
            _updateTimer.Start();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _updateTimer?.Stop();
            _hwndSource?.Dispose();
            MyNotifyIcon?.Dispose();
        }

        private async Task RefreshWindowList()
        {
            var newWindows = await Task.Run(() => _windowService.GetAllWindows());
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Found {newWindows.Count} windows.");

            // The UI update needs to happen on the UI thread.
            // Using Application.Current.Dispatcher to be explicit.
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var currentHandles = new HashSet<IntPtr>(Windows.Select(w => w.Handle));
                var newHandles = new HashSet<IntPtr>(newWindows.Select(w => w.Handle));

                foreach (var window in newWindows)
                {
                    var existing = Windows.FirstOrDefault(w => w.Handle == window.Handle);
                    if (existing == null)
                    {
                        window.Opacity = _windowService.GetTransparency(window.Handle);
                        window.IsTopMost = (WindowService.GetWindowLong(window.Handle, -20) & 0x8) != 0;
                        Windows.Add(window);
                    }
                    else
                    {
                        existing.Title = window.Title;
                        bool isActuallyTopMost = (WindowService.GetWindowLong(window.Handle, -20) & 0x8) != 0;
                        if (existing.IsTopMost != isActuallyTopMost)
                        {
                            existing.IsTopMost = isActuallyTopMost;
                        }
                        int currentOpacity = _windowService.GetTransparency(window.Handle);
                        if (existing.Opacity != currentOpacity)
                        {
                            existing.Opacity = currentOpacity;
                        }
                    }
                }

                var removedWindows = Windows.Where(w => !newHandles.Contains(w.Handle)).ToList();
                foreach (var removed in removedWindows)
                {
                    Windows.Remove(removed);
                }
            });
        }
        
        private void SetTransparency(IntPtr hWnd, int percent)
        {
            if (hWnd == IntPtr.Zero) return;
            _windowService.SetTransparency(hWnd, percent);
            var windowInfo = Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (windowInfo != null) windowInfo.Opacity = percent;
        }

        private void ToggleTopMost(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            _windowService.ToggleTopMost(hWnd);
            var windowInfo = Windows.FirstOrDefault(w => w.Handle == hWnd);
            if (windowInfo != null) windowInfo.IsTopMost = !windowInfo.IsTopMost;
        }

        // --- Event Handlers ---
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
        private void CloseButton_Click(object sender, RoutedEventArgs e) { Close(); }
        private void WindowListView_SelectionChanged(object sender, SelectionChangedEventArgs e) { _selectedWindowInfo = WindowListView.SelectedItem as WindowInfo; }
        private void PinButton_Click(object sender, RoutedEventArgs e) { if (sender is Button btn && btn.DataContext is WindowInfo info) ToggleTopMost(info.Handle); }
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) 
        { 
            if (sender is Slider slider && slider.DataContext is WindowInfo info) 
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Slider changed for '{info.Title}' to {e.NewValue}");
                SetTransparency(info.Handle, (int)e.NewValue); 
            }
        }
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e) { _currentTheme = _currentTheme == Services.Theme.Dark ? Services.Theme.Light : Services.Theme.Dark; _themeService.ApplyTheme(_currentTheme); }
        
        // --- Tray Icon ---
        private void TrayButton_Click(object sender, RoutedEventArgs e) { if(MyNotifyIcon != null) { Hide(); MyNotifyIcon.Visibility = Visibility.Visible; } }
        private void ShowWindow() { if(MyNotifyIcon != null) { Show(); WindowState = WindowState.Normal; Activate(); MyNotifyIcon.Visibility = Visibility.Collapsed; } }
        private void NotifyIcon_DoubleClick(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ShowMenuItem_Click(object sender, RoutedEventArgs e) { ShowWindow(); }
        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) { Close(); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
    }
} 