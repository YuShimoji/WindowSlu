using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    /// <summary>
    /// ウィンドウのグループ化を管理するサービス
    /// </summary>
    public class GroupingService
    {
        private readonly ObservableCollection<WindowGroup> _groups = new ObservableCollection<WindowGroup>();
        private readonly Dictionary<string, WindowGroup> _manualGroups = new Dictionary<string, WindowGroup>();

        /// <summary>
        /// 全グループのコレクション
        /// </summary>
        public ObservableCollection<WindowGroup> Groups => _groups;

        /// <summary>
        /// プロセス名でウィンドウを自動グループ化
        /// </summary>
        /// <param name="windows">グループ化するウィンドウのコレクション</param>
        /// <returns>グループ化されたコレクション</returns>
        public ObservableCollection<WindowGroup> GroupByProcess(IEnumerable<WindowInfo> windows)
        {
            var windowList = windows.ToList();
            
            // プロセス名でグループ化
            var processGroups = windowList
                .GroupBy(w => w.ProcessName)
                .OrderBy(g => g.Key);

            // 既存のAutoByProcessグループをクリア（Manualは保持）
            var autoGroups = _groups.Where(g => g.Type == GroupType.AutoByProcess).ToList();
            foreach (var group in autoGroups)
            {
                _groups.Remove(group);
            }

            foreach (var processGroup in processGroups)
            {
                var group = new WindowGroup
                {
                    Name = processGroup.Key,
                    Type = GroupType.AutoByProcess,
                    ProcessNameFilter = processGroup.Key
                };

                foreach (var window in processGroup)
                {
                    // 手動グループに属していないウィンドウのみ追加
                    if (string.IsNullOrEmpty(window.GroupId) || !_manualGroups.ContainsKey(window.GroupId))
                    {
                        window.GroupId = group.Id;
                        group.Windows.Add(window);
                    }
                }

                // ウィンドウが1つ以上あるグループのみ追加
                if (group.Windows.Count > 0)
                {
                    // 先頭ウィンドウからアイコンを取得
                    group.GroupIcon = group.Windows.FirstOrDefault()?.Icon;
                    _groups.Add(group);
                }
            }

            return _groups;
        }

        /// <summary>
        /// 既存のグループ構造を維持しながらウィンドウリストを更新
        /// </summary>
        /// <param name="newWindows">新しいウィンドウリスト</param>
        public void UpdateGroups(IEnumerable<WindowInfo> newWindows)
        {
            var windowList = newWindows.ToList();
            var newHandles = new HashSet<IntPtr>(windowList.Select(w => w.Handle));

            // 各グループから閉じられたウィンドウを削除
            foreach (var group in _groups.ToList())
            {
                var windowsToRemove = group.Windows
                    .Where(w => !newHandles.Contains(w.Handle))
                    .ToList();

                foreach (var window in windowsToRemove)
                {
                    group.Windows.Remove(window);
                }

                // 空になったAutoグループは削除
                if (group.Windows.Count == 0 && group.Type == GroupType.AutoByProcess)
                {
                    _groups.Remove(group);
                }
            }

            // 新しいウィンドウを適切なグループに追加
            var existingHandles = _groups
                .SelectMany(g => g.Windows)
                .Select(w => w.Handle)
                .ToHashSet();

            foreach (var window in windowList.Where(w => !existingHandles.Contains(w.Handle)))
            {
                // 同じプロセス名のAutoByProcessグループを探す
                var existingGroup = _groups.FirstOrDefault(g => 
                    g.Type == GroupType.AutoByProcess && 
                    g.ProcessNameFilter == window.ProcessName);

                if (existingGroup != null)
                {
                    window.GroupId = existingGroup.Id;
                    existingGroup.Windows.Add(window);
                }
                else
                {
                    // 新しいグループを作成
                    var newGroup = new WindowGroup
                    {
                        Name = window.ProcessName,
                        Type = GroupType.AutoByProcess,
                        ProcessNameFilter = window.ProcessName,
                        GroupIcon = window.Icon
                    };
                    window.GroupId = newGroup.Id;
                    newGroup.Windows.Add(window);
                    _groups.Add(newGroup);
                }
            }

            // 既存ウィンドウの情報を更新
            foreach (var group in _groups)
            {
                foreach (var existingWindow in group.Windows)
                {
                    var newWindow = windowList.FirstOrDefault(w => w.Handle == existingWindow.Handle);
                    if (newWindow != null)
                    {
                        existingWindow.Title = newWindow.Title;
                        existingWindow.IsTopMost = newWindow.IsTopMost;
                        existingWindow.IsClickThrough = newWindow.IsClickThrough;
                        // Opacity, Left, Top, Width, Height は WindowService から別途更新
                    }
                }
            }
        }

        /// <summary>
        /// 手動グループを作成
        /// </summary>
        /// <param name="name">グループ名</param>
        /// <param name="windows">グループに追加するウィンドウ</param>
        /// <returns>作成されたグループ</returns>
        public WindowGroup CreateManualGroup(string name, IEnumerable<WindowInfo>? windows = null)
        {
            var group = new WindowGroup
            {
                Name = name,
                Type = GroupType.Manual
            };

            if (windows != null)
            {
                foreach (var window in windows)
                {
                    AddToGroup(group, window);
                }
            }

            _manualGroups[group.Id] = group;
            _groups.Insert(0, group); // 手動グループは先頭に表示
            return group;
        }

        /// <summary>
        /// グループにウィンドウを追加
        /// </summary>
        /// <param name="group">対象グループ</param>
        /// <param name="window">追加するウィンドウ</param>
        public void AddToGroup(WindowGroup group, WindowInfo window)
        {
            // 既存のグループから削除
            if (!string.IsNullOrEmpty(window.GroupId))
            {
                var oldGroup = _groups.FirstOrDefault(g => g.Id == window.GroupId);
                oldGroup?.Windows.Remove(window);
            }

            window.GroupId = group.Id;
            if (!group.Windows.Contains(window))
            {
                group.Windows.Add(window);
            }

            // アイコンが未設定なら設定
            if (group.GroupIcon == null && window.Icon != null)
            {
                group.GroupIcon = window.Icon;
            }
        }

        /// <summary>
        /// グループからウィンドウを削除
        /// </summary>
        /// <param name="group">対象グループ</param>
        /// <param name="window">削除するウィンドウ</param>
        public void RemoveFromGroup(WindowGroup group, WindowInfo window)
        {
            group.Windows.Remove(window);
            window.GroupId = null;

            // 空になった手動グループは削除しない（ユーザーが明示的に削除する）
            // 空になったAutoグループは削除
            if (group.Windows.Count == 0 && group.Type == GroupType.AutoByProcess)
            {
                _groups.Remove(group);
            }
        }

        /// <summary>
        /// グループを削除
        /// </summary>
        /// <param name="group">削除するグループ</param>
        public void DeleteGroup(WindowGroup group)
        {
            foreach (var window in group.Windows)
            {
                window.GroupId = null;
            }
            group.Windows.Clear();
            _groups.Remove(group);
            _manualGroups.Remove(group.Id);
        }

        /// <summary>
        /// IDでグループを取得
        /// </summary>
        /// <param name="groupId">グループID</param>
        /// <returns>グループ（見つからない場合はnull）</returns>
        public WindowGroup? GetGroupById(string groupId)
        {
            return _groups.FirstOrDefault(g => g.Id == groupId);
        }

        /// <summary>
        /// プロセス名でグループを取得
        /// </summary>
        /// <param name="processName">プロセス名</param>
        /// <returns>グループ（見つからない場合はnull）</returns>
        public WindowGroup? GetGroupByProcessName(string processName)
        {
            return _groups.FirstOrDefault(g => 
                g.Type == GroupType.AutoByProcess && 
                g.ProcessNameFilter == processName);
        }

        /// <summary>
        /// グループをクリア
        /// </summary>
        public void ClearGroups()
        {
            foreach (var group in _groups)
            {
                foreach (var window in group.Windows)
                {
                    window.GroupId = null;
                }
            }
            _groups.Clear();
            _manualGroups.Clear();
        }
    }
}
