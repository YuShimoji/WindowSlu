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
            _updateTimer.Tick += (s, e) => RefreshWindowList();
            
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _hwnd = new WindowInteropHelper(this).EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(_hwnd);
            RefreshWindowList();
            _updateTimer.Start();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _updateTimer?.Stop();
            _hwndSource?.Dispose();
            MyNotifyIcon?.Dispose();
        }

        private void RefreshWindowList()
        {
            var newWindows = _windowService.GetAllWindows();
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
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }
    }
} 