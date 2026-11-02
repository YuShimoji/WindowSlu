using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WindowSlu.Models;

namespace WindowSlu.Services
{
    /// <summary>ウィンドウのグループ化を管理するサービス。</summary>
    public class GroupingService
    {
        private readonly ObservableCollection<WindowGroup> _groups = new ObservableCollection<WindowGroup>();
        private readonly Dictionary<string, WindowGroup> _manualGroups = new Dictionary<string, WindowGroup>();
        // Each persisted member slot retains its live handle until that window closes.
        private readonly Dictionary<string, List<ManualMemberSlot>> _manualMembers = new Dictionary<string, List<ManualMemberSlot>>();

        private sealed class ManualMemberSlot
        {
            public ManualGroupMemberData Member { get; set; } = new ManualGroupMemberData();
            public IntPtr? LiveHandle { get; set; }
        }

        public ObservableCollection<WindowGroup> Groups => _groups;

        public ObservableCollection<WindowGroup> GroupByProcess(IEnumerable<WindowInfo> windows)
        {
            var windowList = windows.GroupBy(w => w.Handle).Select(g => g.First()).ToList();
            var manual = _groups.Where(g => g.Type == GroupType.Manual).ToList();
            foreach (var group in _groups.Where(g => g.Type == GroupType.AutoByProcess).ToList()) _groups.Remove(group);

            ApplySavedManualMembership(windowList);
            foreach (var processGroup in windowList.GroupBy(w => w.ProcessName, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                var normalized = processGroup.Key.ToLowerInvariant();
                var group = new WindowGroup
                {
                    Id = "auto:" + normalized,
                    Name = processGroup.Key,
                    Type = GroupType.AutoByProcess,
                    ProcessNameFilter = processGroup.Key
                };
                foreach (var window in processGroup)
                {
                    if (window.GroupId == null || !_manualGroups.ContainsKey(window.GroupId))
                    {
                        window.GroupId = group.Id;
                        if (!group.Windows.Any(w => w.Handle == window.Handle)) group.Windows.Add(window);
                    }
                }
                if (group.Windows.Count > 0)
                {
                    group.GroupIcon = group.Windows.FirstOrDefault()?.Icon;
                    _groups.Add(group);
                }
            }
            // Keep manual groups at the head even when GroupByProcess is called after restoration.
            foreach (var group in manual)
            {
                var index = _groups.IndexOf(group);
                if (index >= 0) _groups.RemoveAt(index);
                _groups.Insert(_groups.Count(g => g.Type == GroupType.Manual), group);
            }
            return _groups;
        }

        public void UpdateGroups(IEnumerable<WindowInfo> newWindows)
        {
            var windowList = newWindows.GroupBy(w => w.Handle).Select(g => g.First()).ToList();
            ApplySavedManualMembership(windowList);
            var byHandle = windowList.GroupBy(w => w.Handle).ToDictionary(g => g.Key, g => g.First());

            foreach (var group in _groups.ToList())
            {
                foreach (var old in group.Windows.Where(w => !byHandle.ContainsKey(w.Handle)).ToList()) group.Windows.Remove(old);
                if (group.Windows.Count == 0 && group.Type == GroupType.AutoByProcess) _groups.Remove(group);
            }

            var assigned = new HashSet<IntPtr>();
            foreach (var group in _groups)
            {
                foreach (var stale in group.Windows.ToList())
                {
                    if (!byHandle.TryGetValue(stale.Handle, out var current)) continue;
                    if (!ReferenceEquals(stale, current))
                    {
                        var index = group.Windows.IndexOf(stale);
                        group.Windows[index] = current;
                    }
                    current.GroupId = group.Id;
                    assigned.Add(current.Handle);
                }
            }

            foreach (var window in windowList)
            {
                if (!assigned.Add(window.Handle)) continue;
                var group = _groups.FirstOrDefault(g => g.Type == GroupType.AutoByProcess &&
                    string.Equals(g.ProcessNameFilter, window.ProcessName, StringComparison.OrdinalIgnoreCase));
                if (group == null)
                {
                    group = new WindowGroup
                    {
                        Id = "auto:" + window.ProcessName.ToLowerInvariant(), Name = window.ProcessName,
                        Type = GroupType.AutoByProcess, ProcessNameFilter = window.ProcessName, GroupIcon = window.Icon
                    };
                    _groups.Add(group);
                }
                window.GroupId = group.Id;
                group.Windows.Add(window);
            }

            foreach (var group in _groups)
                foreach (var existing in group.Windows)
                    if (byHandle.TryGetValue(existing.Handle, out var current))
                    {
                        existing.Title = current.Title;
                        existing.IsTopMost = current.IsTopMost;
                        existing.IsClickThrough = current.IsClickThrough;
                    }
        }

        public WindowGroup CreateManualGroup(string name, IEnumerable<WindowInfo>? windows = null)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("グループ名を入力してください。", nameof(name));
            var group = new WindowGroup { Name = name.Trim(), Type = GroupType.Manual };
            _manualGroups[group.Id] = group;
            _manualMembers[group.Id] = new List<ManualMemberSlot>();
            _groups.Insert(0, group);
            if (windows != null) foreach (var window in windows) AddToGroup(group, window);
            return group;
        }

        public bool RenameGroup(WindowGroup group, string name)
        {
            if (group == null || group.Type != GroupType.Manual || !_manualGroups.ContainsKey(group.Id) || string.IsNullOrWhiteSpace(name)) return false;
            group.Name = name.Trim();
            return true;
        }

        public List<ManualGroupData> GetManualGroupsData()
        {
            return _manualGroups.Values.Select(g =>
            {
                var members = _manualMembers.TryGetValue(g.Id, out var saved) ? saved : new List<ManualMemberSlot>();
                return new ManualGroupData
                {
                    Id = g.Id,
                    Name = g.Name,
                    Members = CloneMembers(members.Select(slot => slot.Member)),
                    ProcessNames = members.Select(slot => slot.Member.ProcessName).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                };
            }).ToList();
        }

        public void RestoreManualGroups(IEnumerable<ManualGroupData> groups)
        {
            foreach (var old in _groups.Where(g => g.Type == GroupType.Manual).ToList())
            {
                foreach (var w in old.Windows) w.GroupId = null;
                _groups.Remove(old);
            }
            _manualGroups.Clear();
            _manualMembers.Clear();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var data in groups ?? Enumerable.Empty<ManualGroupData>())
            {
                if (string.IsNullOrWhiteSpace(data.Id) || string.IsNullOrWhiteSpace(data.Name) || !seenIds.Add(data.Id)) continue;
                var group = new WindowGroup { Id = data.Id, Name = data.Name, Type = GroupType.Manual };
                _manualGroups[group.Id] = group;
                _groups.Insert(_manualGroups.Count - 1, group);
                var members = data.Members != null && data.Members.Count > 0
                    ? CloneMembers(data.Members)
                    : (data.ProcessNames ?? new List<string>()).Select(p => new ManualGroupMemberData { ProcessName = p, Title = string.Empty }).ToList();
                _manualMembers[group.Id] = members.Select(member => new ManualMemberSlot { Member = member }).ToList();
            }
        }

        private void ApplySavedManualMembership(List<WindowInfo> windows)
        {
            var liveByHandle = windows.GroupBy(w => w.Handle).ToDictionary(g => g.Key, g => g.First());
            var claimed = new HashSet<IntPtr>();

            // While the app is running, a live handle is the strongest identity. Keep its
            // manual group across title changes and refresh the persisted process/title key.
            foreach (var group in _manualGroups.Values)
            {
                var slots = _manualMembers[group.Id];
                foreach (var existing in group.Windows.ToList())
                {
                    if (!liveByHandle.TryGetValue(existing.Handle, out var current))
                    {
                        group.Windows.Remove(existing);
                        continue;
                    }

                    if (!ReferenceEquals(existing, current))
                    {
                        var index = group.Windows.IndexOf(existing);
                        group.Windows[index] = current;
                    }
                    current.GroupId = group.Id;
                    claimed.Add(current.Handle);
                    var slot = slots.FirstOrDefault(s => s.LiveHandle == current.Handle);
                    slot ??= slots.FirstOrDefault(s => s.LiveHandle == null &&
                        string.Equals(s.Member.ProcessName, existing.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                        (s.Member.Title.Length == 0 || string.Equals(s.Member.Title, existing.Title, StringComparison.Ordinal)));
                    slot ??= new ManualMemberSlot();
                    if (!slots.Contains(slot)) slots.Add(slot);
                    slot.LiveHandle = current.Handle;
                    slot.Member.ProcessName = current.ProcessName;
                    slot.Member.Title = current.Title;
                }

                foreach (var slot in slots)
                    if (slot.LiveHandle.HasValue && !liveByHandle.ContainsKey(slot.LiveHandle.Value)) slot.LiveHandle = null;
            }

            foreach (var group in _manualGroups.Values)
            {
                var slots = _manualMembers[group.Id];
                foreach (var slot in slots.Where(s => s.LiveHandle == null))
                {
                    var match = windows.FirstOrDefault(w => !claimed.Contains(w.Handle) &&
                        string.Equals(w.ProcessName, slot.Member.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                        (slot.Member.Title.Length == 0 || string.Equals(w.Title, slot.Member.Title, StringComparison.Ordinal)));
                    if (match == null) continue;
                    claimed.Add(match.Handle);
                    RemoveFromAnyGroup(match);
                    match.GroupId = group.Id;
                    if (!group.Windows.Any(w => w.Handle == match.Handle)) group.Windows.Add(match);
                    slot.LiveHandle = match.Handle;
                    slot.Member.ProcessName = match.ProcessName;
                    slot.Member.Title = match.Title;
                }
            }
        }

        private void RemoveFromAnyGroup(WindowInfo window)
        {
            foreach (var group in _groups)
                foreach (var existing in group.Windows.Where(w => w.Handle == window.Handle).ToList()) group.Windows.Remove(existing);
        }

        public void AddToGroup(WindowGroup group, WindowInfo window)
        {
            var alreadyInTarget = group.Windows.Any(w => w.Handle == window.Handle);
            // Remove the old saved identity so a refresh cannot undo an explicit move.
            if (window.GroupId != null && window.GroupId != group.Id && _manualMembers.TryGetValue(window.GroupId, out var oldMembers))
                RemoveSlot(oldMembers, window);
            RemoveFromAnyGroup(window);
            window.GroupId = group.Id;
            if (!group.Windows.Any(w => w.Handle == window.Handle)) group.Windows.Add(window);
            if (_manualGroups.ContainsKey(group.Id))
            {
                var targetSlots = _manualMembers[group.Id];
                var existing = targetSlots.FirstOrDefault(slot => slot.LiveHandle == window.Handle);
                if (existing != null)
                {
                    existing.Member.ProcessName = window.ProcessName;
                    existing.Member.Title = window.Title;
                }
                else if (!alreadyInTarget)
                    targetSlots.Add(NewSlot(window));
            }
            if (group.GroupIcon == null && window.Icon != null) group.GroupIcon = window.Icon;
        }

        public void RemoveFromGroup(WindowGroup group, WindowInfo window)
        {
            group.Windows.Remove(window);
            if (_manualMembers.TryGetValue(group.Id, out var members)) RemoveSlot(members, window);
            window.GroupId = null;
            if (group.Windows.Count == 0 && group.Type == GroupType.AutoByProcess) _groups.Remove(group);
        }

        public void DeleteGroup(WindowGroup group)
        {
            foreach (var window in group.Windows) window.GroupId = null;
            group.Windows.Clear();
            _groups.Remove(group);
            _manualGroups.Remove(group.Id);
            _manualMembers.Remove(group.Id);
        }

        public WindowGroup? GetGroupById(string groupId) => _groups.FirstOrDefault(g => g.Id == groupId);

        public WindowGroup? GetGroupByProcessName(string processName) => _groups.FirstOrDefault(g =>
            g.Type == GroupType.AutoByProcess && string.Equals(g.ProcessNameFilter, processName, StringComparison.OrdinalIgnoreCase));

        public void ClearGroups()
        {
            foreach (var group in _groups)
                foreach (var window in group.Windows) window.GroupId = null;
            _groups.Clear();
            _manualGroups.Clear();
            _manualMembers.Clear();
        }

        private static ManualMemberSlot NewSlot(WindowInfo window) => new()
        {
            Member = new ManualGroupMemberData { ProcessName = window.ProcessName, Title = window.Title },
            LiveHandle = window.Handle
        };

        private static bool RemoveSlot(List<ManualMemberSlot> list, WindowInfo window)
        {
            var index = list.FindIndex(slot => slot.LiveHandle == window.Handle);
            if (index < 0) index = list.FindIndex(slot => string.Equals(slot.Member.ProcessName, window.ProcessName, StringComparison.OrdinalIgnoreCase) && string.Equals(slot.Member.Title, window.Title, StringComparison.Ordinal));
            if (index < 0) return false;
            list.RemoveAt(index);
            return true;
        }
        private static List<ManualGroupMemberData> CloneMembers(IEnumerable<ManualGroupMemberData> members) => members.Select(m => new ManualGroupMemberData { ProcessName = m.ProcessName ?? string.Empty, Title = m.Title ?? string.Empty }).ToList();
    }
}
