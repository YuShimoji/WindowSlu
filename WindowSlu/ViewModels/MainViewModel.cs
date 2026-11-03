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
        public LayoutService LayoutService { get; }
        public SplitArrangementService SplitArrangementService { get; }
        public SettingsService SettingsService => _settingsService;
        private readonly MainWindow _mainWindow;
        private readonly DispatcherTimer _updateTimer;

        public string OperationDetails { get => _operationDetails; set { _operationDetails = value; OnPropertyChanged(); } }
        private string _operationDetails = string.Empty;
        private bool _refreshing;

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
                    NotifySplitTargetChanged();
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
                    NotifySplitTargetChanged();
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

        public IReadOnlyList<WindowInfo> GetSplitTargets()
        {
            if (SelectedGroup != null) return SelectedGroup.Windows.ToList();
            if (SelectedWindow == null) return Array.Empty<WindowInfo>();
            var group = WindowGroups.FirstOrDefault(g => g.Windows.Any(w => w.Handle == SelectedWindow.Handle));
            return group?.Windows.ToList() ?? new List<WindowInfo> { SelectedWindow };
        }

        public bool CanSplitSelectedGroup => GetSplitTargets().Where(w => w.Handle != IntPtr.Zero).Select(w => w.Handle).Distinct().Count() >= 2;
        public string SplitTargetDescription => CanSplitSelectedGroup
            ? $"Arrange {GetSplitTargets().Count} windows on the first window's monitor."
            : "Select a group with at least two windows, or a window belonging to that group.";

        public void NotifySplitTargetChanged()
        {
            NotifyOperationTargetsChanged();
            OnPropertyChanged(nameof(CanSplitSelectedGroup));
            OnPropertyChanged(nameof(SplitTargetDescription));
        }

        private bool _applyToAllWindows;
        public bool ApplyToAllWindows
        {
            get => _applyToAllWindows;
            set { _applyToAllWindows = value; OnPropertyChanged(); NotifyOperationTargetsChanged(); }
        }
        public IReadOnlyList<WindowInfo> GetOpacityTargets() => ApplyToAllWindows ? Windows.ToList()
            : SelectedWindow != null ? new[] { SelectedWindow }
            : SelectedGroup != null ? SelectedGroup.Windows.ToList() : Array.Empty<WindowInfo>();
        public string SelectionDescription => SelectedWindow != null ? $"Window '{SelectedWindow.Title}' (1)"
            : SelectedGroup != null ? $"Group '{SelectedGroup.Name}' ({SelectedGroup.Windows.Count})" : "No selection";
        public string OpacityTargetDescription => ApplyToAllWindows ? $"All managed windows ({Windows.Count})" : SelectionDescription;
        public string ResetAllDescription => $"Reset all managed windows ({Windows.Count}) to 100%";
        public bool CanApplyOpacity => GetOpacityTargets().Count > 0;
        private WindowPreset? _selectedPreset;
        public WindowPreset? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (_selectedPreset != null) _selectedPreset.PropertyChanged -= PresetChanged;
                _selectedPreset = value;
                if (value != null) value.PropertyChanged += PresetChanged;
                OnPropertyChanged(); NotifyOperationTargetsChanged();
                StatusText = value == null ? "Select a preset to apply." : "Preset selected. Check targets before Apply.";
                OperationDetails = string.Empty;
            }
        }
        private void PresetChanged(object? sender, PropertyChangedEventArgs e) => NotifyOperationTargetsChanged();
        public string PresetTargetDescription
        {
            get
            {
                if (SelectedPreset == null) return "Select a preset to see targets and conditions.";
                var candidates = SelectedWindow != null ? new[] { SelectedWindow }
                    : SelectedGroup != null ? SelectedGroup.Windows.AsEnumerable() : WindowGroups.SelectMany(g => g.Windows);
                int count = candidates.Where(w => PresetService.MatchesTargetFilter(SelectedPreset, w)).Select(w => w.Handle).Distinct().Count();
                string group = string.IsNullOrWhiteSpace(SelectedPreset.TargetGroupId) ? "any group"
                    : WindowGroups.FirstOrDefault(g => g.Id == SelectedPreset.TargetGroupId)?.Name ?? SelectedPreset.TargetGroupId;
                return $"{(SelectedWindow == null && SelectedGroup == null ? "All managed windows" : SelectionDescription)}; process: {(string.IsNullOrWhiteSpace(SelectedPreset.TargetProcessName) ? "any" : SelectedPreset.TargetProcessName)}; group: {group}; matches: {count}";
            }
        }
        public void NotifyOperationTargetsChanged()
        {
            OnPropertyChanged(nameof(OpacityTargetDescription)); OnPropertyChanged(nameof(CanApplyOpacity));
            OnPropertyChanged(nameof(PresetTargetDescription)); OnPropertyChanged(nameof(ResetAllDescription));
        }
        public bool IsReadingWindowState { get; private set; }

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

        public MainViewModel(SettingsService settingsService, WindowService windowService, MainWindow mainWindow, string? dataDirectory = null, bool enableSystemHooks = true)
        {
            _settingsService = settingsService;
            WindowService = windowService;
            GroupingService = new GroupingService();
            PresetService = new PresetService(windowService, GroupingService, dataDirectory == null ? null : System.IO.Path.Combine(dataDirectory, "presets.json"));
            LinkedDragService = new LinkedDragService(GroupingService, windowService, enableHook: enableSystemHooks);
            LayoutService = new LayoutService(windowService, dataDirectory == null ? null : System.IO.Path.Combine(dataDirectory, "layouts.json"));
            SplitArrangementService = new SplitArrangementService(windowService);
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
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                var newWindows = await Task.Run(() => WindowService.GetAllWindows());
                var foregroundWindow = await Task.Run(() => WindowService.GetForegroundWindow());

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    IsReadingWindowState = true;
                    try
                    {
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
                                existing.Opacity = WindowService.GetTransparency(window.Handle);
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

                        if (SelectedWindow != null && !newHandles.Contains(SelectedWindow.Handle)) SelectedWindow = null;

                        // アクティブウィンドウを設定
                        foreach (var window in Windows)
                        {
                            window.IsActive = window.Handle == foregroundWindow;
                        }

                        // グループ化を更新
                        GroupingService.UpdateGroups(Windows);
                        OnPropertyChanged(nameof(WindowGroups));
                        OnPropertyChanged(nameof(FilteredWindows));
                        NotifySplitTargetChanged();
                    }
                    finally { IsReadingWindowState = false; }
                });
            }
            finally { _refreshing = false; }
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
