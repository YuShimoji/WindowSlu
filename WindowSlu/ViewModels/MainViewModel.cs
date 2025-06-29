using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowSlu.Models;
using WindowSlu.Services;
using System.Windows.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Windows;

namespace WindowSlu.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        public WindowService WindowService { get; }
        private readonly MainWindow _mainWindow;
        private readonly DispatcherTimer _updateTimer;

        private string _statusText = "Ready";
        public string StatusText
        {
            get => _statusText;
            set
            {
                if (_statusText != value)
                {
                    _statusText = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();

        public MainViewModel(SettingsService settingsService, WindowService windowService, MainWindow mainWindow)
        {
            _settingsService = settingsService;
            WindowService = windowService;
            _mainWindow = mainWindow;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (s, e) => await RefreshWindowList();
        }

        public void Start()
        {
            _updateTimer.Start();
        }

        public void Stop()
        {
            _updateTimer.Stop();
        }

        public async Task RefreshWindowList()
        {
            var newWindows = await Task.Run(() => WindowService.GetAllWindows());
            
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var currentHandles = new HashSet<IntPtr>(Windows.Select(w => w.Handle));
                var newHandles = new HashSet<IntPtr>(newWindows.Select(w => w.Handle));

                foreach (var window in newWindows)
                {
                    var existing = Windows.FirstOrDefault(w => w.Handle == window.Handle);
                    if (existing == null)
                    {
                        window.Opacity = WindowService.GetTransparency(window.Handle);
                        window.IsTopMost = WindowService.IsTopMost(window.Handle);
                        window.IsClickThrough = WindowService.IsClickThrough(window.Handle);
                        Windows.Add(window);
                    }
                    else
                    {
                        existing.Title = window.Title;
                        bool isActuallyTopMost = WindowService.IsTopMost(window.Handle);
                        if (existing.IsTopMost != isActuallyTopMost)
                        {
                            existing.IsTopMost = isActuallyTopMost;
                        }

                        bool isActuallyClickThrough = WindowService.IsClickThrough(window.Handle);
                        if (existing.IsClickThrough != isActuallyClickThrough)
                        {
                            existing.IsClickThrough = isActuallyClickThrough;
                        }
                    }
                }

                var windowsToRemove = Windows.Where(w => !newHandles.Contains(w.Handle)).ToList();
                foreach (var window in windowsToRemove)
                {
                    Windows.Remove(window);
                }
            });
        }

        public void UpdateWindowOpacity(int newOpacity)
        {
            // This is a placeholder. 
            // The original implementation likely called _mainWindow.ShowTransparencyIndicator(newOpacity);
            _mainWindow.ShowTransparencyIndicator(newOpacity);
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 