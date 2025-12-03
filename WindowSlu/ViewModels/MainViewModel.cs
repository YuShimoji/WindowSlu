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
        public GroupingService GroupingService { get; }
        public PresetService PresetService { get; }
        public LinkedDragService LinkedDragService { get; }
        public SettingsService SettingsService => _settingsService;
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

        private WindowGroup? _selectedGroup;
        public WindowGroup? SelectedGroup
        {
            get => _selectedGroup;
            set
            {
                if (_selectedGroup != value)
                {
                    _selectedGroup = value;
                    OnPropertyChanged();
                }
            }
        }

        private WindowInfo? _selectedWindow;
        public WindowInfo? SelectedWindow
        {
            get => _selectedWindow;
            set
            {
                if (_selectedWindow != value)
                {
                    _selectedWindow = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// フラットなウィンドウリスト（後方互換性のため維持）
        /// </summary>
        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();

        private string _windowFilterText = string.Empty;
        /// <summary>
        /// ウィンドウフィルタ用のテキスト
        /// </summary>
        public string WindowFilterText
        {
            get => _windowFilterText;
            set
            {
                if (_windowFilterText != value)
                {
                    _windowFilterText = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FilteredWindows));
                }
            }
        }

        /// <summary>
        /// フィルタ適用後のウィンドウリスト
        /// </summary>
        public IEnumerable<WindowInfo> FilteredWindows
        {
            get
            {
                if (string.IsNullOrWhiteSpace(WindowFilterText))
                    return Windows;

                var filter = WindowFilterText.ToLowerInvariant();
                return Windows.Where(w =>
                    (w.Title?.ToLowerInvariant().Contains(filter) ?? false) ||
                    (w.ProcessName?.ToLowerInvariant().Contains(filter) ?? false));
            }
        }

        /// <summary>
        /// グループ化されたウィンドウリスト
        /// </summary>
        public ObservableCollection<WindowGroup> WindowGroups => GroupingService.Groups;

        public int BulkOpacityPercent
        {
            get => _settingsService.Settings.BulkOpacityPercent;
            set
            {
                if (_settingsService.Settings.BulkOpacityPercent != value)
                {
                    _settingsService.Settings.BulkOpacityPercent = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainViewModel(SettingsService settingsService, WindowService windowService, MainWindow mainWindow)
        {
            _settingsService = settingsService;
            WindowService = windowService;
            GroupingService = new GroupingService();
            PresetService = new PresetService(windowService, GroupingService);
            LinkedDragService = new LinkedDragService(GroupingService, windowService);
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
            LinkedDragService?.Dispose();
        }

        public async Task RefreshWindowList()
        {
            var newWindows = await Task.Run(() => WindowService.GetAllWindows());
            var foregroundWindow = await Task.Run(() => WindowService.GetForegroundWindow());
            
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
                        WindowService.UpdateWindowPositionInfo(window);
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
                        
                        // 位置情報を更新
                        WindowService.UpdateWindowPositionInfo(existing);
                    }
                }

                var windowsToRemove = Windows.Where(w => !newHandles.Contains(w.Handle)).ToList();
                foreach (var window in windowsToRemove)
                {
                    Windows.Remove(window);
                }

                // アクティブウィンドウを設定
                foreach (var window in Windows)
                {
                    window.IsActive = window.Handle == foregroundWindow;
                }

                // グループ化を更新
                GroupingService.UpdateGroups(Windows);
                OnPropertyChanged(nameof(WindowGroups));
                OnPropertyChanged(nameof(FilteredWindows));
            });
        }

        /// <summary>
        /// 初回のグループ化を実行
        /// </summary>
        public void InitializeGroups()
        {
            GroupingService.GroupByProcess(Windows);
            OnPropertyChanged(nameof(WindowGroups));
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