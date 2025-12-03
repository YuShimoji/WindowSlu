using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    /// <summary>
    /// ウィンドウレイアウトの保存・復元を管理するサービス
    /// </summary>
    public class LayoutService
    {
        private readonly WindowService _windowService;
        private readonly string _layoutFilePath;
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// 保存済みレイアウトのリスト
        /// </summary>
        public ObservableCollection<WindowLayout> Layouts { get; } = new ObservableCollection<WindowLayout>();

        public LayoutService(WindowService windowService)
        {
            _windowService = windowService;
            _layoutFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowSlu",
                "layouts.json"
            );

            LoadLayouts();
        }

        /// <summary>
        /// レイアウトをファイルから読み込み
        /// </summary>
        public void LoadLayouts()
        {
            try
            {
                if (File.Exists(_layoutFilePath))
                {
                    var json = File.ReadAllText(_layoutFilePath);
                    var data = JsonSerializer.Deserialize<LayoutData>(json);
                    if (data?.Layouts != null)
                    {
                        Layouts.Clear();
                        foreach (var layout in data.Layouts)
                        {
                            Layouts.Add(layout);
                        }
                    }
                    LoggingService.LogInfo($"Loaded {Layouts.Count} layouts from file.");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load layouts: {ex.Message}");
            }
        }

        /// <summary>
        /// レイアウトをファイルに保存
        /// </summary>
        public void SaveLayouts()
        {
            try
            {
                var directory = Path.GetDirectoryName(_layoutFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new LayoutData { Layouts = Layouts.ToList() };
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                File.WriteAllText(_layoutFilePath, json);
                LoggingService.LogInfo($"Saved {Layouts.Count} layouts to file.");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save layouts: {ex.Message}");
            }
        }

        /// <summary>
        /// 新しいレイアウトを作成
        /// </summary>
        public WindowLayout CreateLayout(string name)
        {
            var layout = new WindowLayout
            {
                Name = name,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };
            Layouts.Add(layout);
            SaveLayouts();
            return layout;
        }

        /// <summary>
        /// 現在のウィンドウ配置からレイアウトを作成
        /// </summary>
        public WindowLayout CaptureCurrentLayout(string name, IEnumerable<WindowInfo> windows)
        {
            var layout = new WindowLayout
            {
                Name = name,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };

            foreach (var window in windows)
            {
                // ウィンドウの現在位置を取得
                _windowService.UpdateWindowPositionInfo(window);

                var entry = new WindowLayoutEntry
                {
                    ProcessName = window.ProcessName,
                    TitlePattern = window.Title,
                    Left = window.Left,
                    Top = window.Top,
                    Width = window.Width,
                    Height = window.Height,
                    Opacity = window.Opacity,
                    IsTopMost = window.IsTopMost,
                    MonitorId = window.MonitorId
                };
                layout.Entries.Add(entry);
            }

            Layouts.Add(layout);
            SaveLayouts();
            LoggingService.LogInfo($"Captured layout '{name}' with {layout.Entries.Count} windows.");
            return layout;
        }

        /// <summary>
        /// レイアウトを削除
        /// </summary>
        public bool DeleteLayout(string layoutId)
        {
            var layout = Layouts.FirstOrDefault(l => l.Id == layoutId);
            if (layout != null)
            {
                Layouts.Remove(layout);
                SaveLayouts();
                LoggingService.LogInfo($"Deleted layout: {layout.Name}");
                return true;
            }
            return false;
        }

        /// <summary>
        /// レイアウトを復元
        /// </summary>
        public int RestoreLayout(WindowLayout layout, IEnumerable<WindowInfo> currentWindows)
        {
            int restoredCount = 0;
            var windowsList = currentWindows.ToList();

            foreach (var entry in layout.Entries)
            {
                // プロセス名とタイトルでウィンドウを検索
                var matchingWindow = FindMatchingWindow(entry, windowsList);
                if (matchingWindow != null)
                {
                    // 位置とサイズを復元
                    _windowService.SetWindowPosition(
                        matchingWindow.Handle,
                        entry.Left,
                        entry.Top,
                        entry.Width,
                        entry.Height
                    );
                    matchingWindow.Left = entry.Left;
                    matchingWindow.Top = entry.Top;
                    matchingWindow.Width = entry.Width;
                    matchingWindow.Height = entry.Height;

                    // 透明度を復元
                    if (entry.Opacity.HasValue)
                    {
                        _windowService.SetTransparency(matchingWindow.Handle, entry.Opacity.Value);
                        matchingWindow.Opacity = entry.Opacity.Value;
                    }

                    // TopMostを復元
                    if (entry.IsTopMost.HasValue && matchingWindow.IsTopMost != entry.IsTopMost.Value)
                    {
                        _windowService.ToggleTopMost(matchingWindow.Handle);
                        matchingWindow.IsTopMost = entry.IsTopMost.Value;
                    }

                    restoredCount++;
                    LoggingService.LogInfo($"Restored window: {matchingWindow.Title} to ({entry.Left}, {entry.Top})");
                }
            }

            LoggingService.LogInfo($"Restored {restoredCount} of {layout.Entries.Count} windows from layout '{layout.Name}'");
            return restoredCount;
        }

        /// <summary>
        /// レイアウトエントリに一致するウィンドウを検索
        /// </summary>
        private WindowInfo? FindMatchingWindow(WindowLayoutEntry entry, List<WindowInfo> windows)
        {
            // まずプロセス名とタイトルの完全一致を試みる
            var exactMatch = windows.FirstOrDefault(w =>
                string.Equals(w.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(w.Title, entry.TitlePattern, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null) return exactMatch;

            // 次にプロセス名一致 + タイトル部分一致
            if (!string.IsNullOrWhiteSpace(entry.TitlePattern))
            {
                var partialMatch = windows.FirstOrDefault(w =>
                    string.Equals(w.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                    (w.Title?.Contains(entry.TitlePattern, StringComparison.OrdinalIgnoreCase) ?? false));

                if (partialMatch != null) return partialMatch;
            }

            // 最後にプロセス名のみで検索
            return windows.FirstOrDefault(w =>
                string.Equals(w.ProcessName, entry.ProcessName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 現在のウィンドウ配置でレイアウトを更新
        /// </summary>
        public void UpdateLayout(WindowLayout layout, IEnumerable<WindowInfo> windows)
        {
            layout.Entries.Clear();

            foreach (var window in windows)
            {
                _windowService.UpdateWindowPositionInfo(window);

                var entry = new WindowLayoutEntry
                {
                    ProcessName = window.ProcessName,
                    TitlePattern = window.Title,
                    Left = window.Left,
                    Top = window.Top,
                    Width = window.Width,
                    Height = window.Height,
                    Opacity = window.Opacity,
                    IsTopMost = window.IsTopMost,
                    MonitorId = window.MonitorId
                };
                layout.Entries.Add(entry);
            }

            layout.ModifiedAt = DateTime.Now;
            SaveLayouts();
            LoggingService.LogInfo($"Updated layout '{layout.Name}' with {layout.Entries.Count} windows.");
        }
    }
}
