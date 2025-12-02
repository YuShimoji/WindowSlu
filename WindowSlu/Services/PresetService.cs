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
    /// プリセットの管理と適用を行うサービス
    /// </summary>
    public class PresetService
    {
        private readonly string _presetFilePath;
        private readonly WindowService _windowService;
        private readonly GroupingService _groupingService;

        /// <summary>
        /// 登録されたプリセットのコレクション
        /// </summary>
        public ObservableCollection<WindowPreset> Presets { get; } = new ObservableCollection<WindowPreset>();

        public PresetService(WindowService windowService, GroupingService groupingService)
        {
            _windowService = windowService;
            _groupingService = groupingService;
            
            string appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowSlu");
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }
            
            _presetFilePath = Path.Combine(appDataPath, "presets.json");
            LoadPresets();
        }

        /// <summary>
        /// プリセットをファイルから読み込む
        /// </summary>
        public void LoadPresets()
        {
            try
            {
                if (File.Exists(_presetFilePath))
                {
                    string json = File.ReadAllText(_presetFilePath);
                    var data = JsonSerializer.Deserialize<PresetData>(json);
                    
                    if (data?.Presets != null)
                    {
                        Presets.Clear();
                        foreach (var preset in data.Presets)
                        {
                            Presets.Add(preset);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load presets: {ex.Message}");
            }
        }

        /// <summary>
        /// プリセットをファイルに保存
        /// </summary>
        public void SavePresets()
        {
            try
            {
                var data = new PresetData
                {
                    Presets = Presets.ToList(),
                    ManualGroups = GetManualGroupsData()
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(data, options);
                File.WriteAllText(_presetFilePath, json);
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save presets: {ex.Message}");
            }
        }

        /// <summary>
        /// 新しいプリセットを作成
        /// </summary>
        public WindowPreset CreatePreset(string name)
        {
            var preset = new WindowPreset
            {
                Name = name,
                Width = 800,
                Height = 600,
                Opacity = 100,
                IsTopMost = false,
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now
            };
            
            Presets.Add(preset);
            SavePresets();
            return preset;
        }

        /// <summary>
        /// プリセットを削除
        /// </summary>
        public bool DeletePreset(string presetId)
        {
            var preset = Presets.FirstOrDefault(p => p.Id == presetId);
            if (preset != null)
            {
                Presets.Remove(preset);
                SavePresets();
                return true;
            }
            return false;
        }

        /// <summary>
        /// プリセットを更新
        /// </summary>
        public void UpdatePreset(WindowPreset preset)
        {
            preset.ModifiedAt = DateTime.Now;
            SavePresets();
        }

        /// <summary>
        /// IDでプリセットを取得
        /// </summary>
        public WindowPreset? GetPresetById(string presetId)
        {
            return Presets.FirstOrDefault(p => p.Id == presetId);
        }

        /// <summary>
        /// プリセットを指定のウィンドウグループに適用
        /// </summary>
        public void ApplyPresetToGroup(WindowPreset preset, WindowGroup group)
        {
            if (group.Windows.Count == 0) return;

            // 開始位置を決定
            int startX = preset.CascadeStartX ?? group.Windows[0].Left;
            int startY = preset.CascadeStartY ?? group.Windows[0].Top;
            int currentX = startX;
            int currentY = startY;

            // オフセット方向を決定
            int offsetX = GetDirectionOffsetX(preset.CascadeDirection, preset.CascadeOffsetX);
            int offsetY = GetDirectionOffsetY(preset.CascadeDirection, preset.CascadeOffsetY);

            foreach (var window in group.Windows)
            {
                // 透明度を適用
                if (preset.Opacity.HasValue)
                {
                    _windowService.SetTransparency(window.Handle, preset.Opacity.Value);
                    window.Opacity = preset.Opacity.Value;
                }

                // TopMostを適用
                if (preset.IsTopMost.HasValue)
                {
                    if (preset.IsTopMost.Value != window.IsTopMost)
                    {
                        _windowService.ToggleTopMost(window.Handle);
                        window.IsTopMost = preset.IsTopMost.Value;
                    }
                }

                // サイズと位置を適用
                int newWidth = preset.Width ?? window.Width;
                int newHeight = preset.Height ?? window.Height;

                if (preset.EnableCascade)
                {
                    _windowService.SetWindowPosition(window.Handle, currentX, currentY, newWidth, newHeight);
                    window.Left = currentX;
                    window.Top = currentY;
                    window.Width = newWidth;
                    window.Height = newHeight;

                    currentX += offsetX;
                    currentY += offsetY;
                }
                else if (preset.Width.HasValue || preset.Height.HasValue)
                {
                    _windowService.SetWindowSize(window.Handle, newWidth, newHeight);
                    window.Width = newWidth;
                    window.Height = newHeight;
                }
            }
        }

        /// <summary>
        /// プリセットを単一ウィンドウに適用
        /// </summary>
        public void ApplyPresetToWindow(WindowPreset preset, WindowInfo window)
        {
            // 透明度を適用
            if (preset.Opacity.HasValue)
            {
                _windowService.SetTransparency(window.Handle, preset.Opacity.Value);
                window.Opacity = preset.Opacity.Value;
            }

            // TopMostを適用
            if (preset.IsTopMost.HasValue)
            {
                if (preset.IsTopMost.Value != window.IsTopMost)
                {
                    _windowService.ToggleTopMost(window.Handle);
                    window.IsTopMost = preset.IsTopMost.Value;
                }
            }

            // サイズを適用
            if (preset.Width.HasValue || preset.Height.HasValue)
            {
                int newWidth = preset.Width ?? window.Width;
                int newHeight = preset.Height ?? window.Height;
                _windowService.SetWindowSize(window.Handle, newWidth, newHeight);
                window.Width = newWidth;
                window.Height = newHeight;
            }
        }

        /// <summary>
        /// 現在のウィンドウ設定からプリセットを作成
        /// </summary>
        public WindowPreset CreatePresetFromWindow(WindowInfo window, string presetName)
        {
            var preset = new WindowPreset
            {
                Name = presetName,
                TargetProcessName = window.ProcessName,
                Width = window.Width,
                Height = window.Height,
                Opacity = window.Opacity,
                IsTopMost = window.IsTopMost
            };

            Presets.Add(preset);
            SavePresets();
            return preset;
        }

        /// <summary>
        /// 現在のグループ設定からプリセットを作成
        /// </summary>
        public WindowPreset CreatePresetFromGroup(WindowGroup group, string presetName)
        {
            var firstWindow = group.Windows.FirstOrDefault();
            var preset = new WindowPreset
            {
                Name = presetName,
                TargetProcessName = group.ProcessNameFilter,
                TargetGroupId = group.Id,
                Width = firstWindow?.Width,
                Height = firstWindow?.Height,
                Opacity = firstWindow?.Opacity,
                IsTopMost = firstWindow?.IsTopMost,
                EnableCascade = true,
                CascadeOffsetX = 30,
                CascadeOffsetY = 30,
                CascadeDirection = CascadeDirection.BottomRight
            };

            Presets.Add(preset);
            SavePresets();
            return preset;
        }

        private int GetDirectionOffsetX(CascadeDirection direction, int offset)
        {
            return direction switch
            {
                CascadeDirection.BottomRight => offset,
                CascadeDirection.TopRight => offset,
                CascadeDirection.BottomLeft => -offset,
                CascadeDirection.TopLeft => -offset,
                CascadeDirection.Right => offset,
                CascadeDirection.Down => 0,
                _ => offset
            };
        }

        private int GetDirectionOffsetY(CascadeDirection direction, int offset)
        {
            return direction switch
            {
                CascadeDirection.BottomRight => offset,
                CascadeDirection.TopRight => -offset,
                CascadeDirection.BottomLeft => offset,
                CascadeDirection.TopLeft => -offset,
                CascadeDirection.Right => 0,
                CascadeDirection.Down => offset,
                _ => offset
            };
        }

        private List<ManualGroupData> GetManualGroupsData()
        {
            return _groupingService.Groups
                .Where(g => g.Type == GroupType.Manual)
                .Select(g => new ManualGroupData
                {
                    Id = g.Id,
                    Name = g.Name,
                    ProcessNames = g.Windows.Select(w => w.ProcessName).Distinct().ToList()
                })
                .ToList();
        }
    }
}
