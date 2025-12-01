using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WindowSlu.Models
{
    /// <summary>
    /// ウィンドウグループを表すモデルクラス
    /// </summary>
    public class WindowGroup : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private string _name = string.Empty;
        private GroupType _type = GroupType.AutoByProcess;
        private string? _processNameFilter;
        private bool _isExpanded = true;
        private ImageSource? _groupIcon;
        private bool _isSelected;
        private bool _linkedDragEnabled = false;

        /// <summary>
        /// グループの一意識別子 (GUID)
        /// </summary>
        public string Id
        {
            get => _id;
            set => SetField(ref _id, value);
        }

        /// <summary>
        /// グループの表示名
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        /// <summary>
        /// グループの種類（自動/手動）
        /// </summary>
        public GroupType Type
        {
            get => _type;
            set => SetField(ref _type, value);
        }

        /// <summary>
        /// 自動グループの場合のプロセス名フィルタ
        /// </summary>
        public string? ProcessNameFilter
        {
            get => _processNameFilter;
            set => SetField(ref _processNameFilter, value);
        }

        /// <summary>
        /// グループに属するウィンドウのコレクション
        /// </summary>
        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();

        /// <summary>
        /// TreeViewでの展開状態
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetField(ref _isExpanded, value);
        }

        /// <summary>
        /// グループのアイコン（先頭ウィンドウから取得）
        /// </summary>
        public ImageSource? GroupIcon
        {
            get => _groupIcon;
            set => SetField(ref _groupIcon, value);
        }

        /// <summary>
        /// グループが選択されているかどうか
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        /// <summary>
        /// このグループで連動ドラッグを有効にするかどうか
        /// </summary>
        public bool LinkedDragEnabled
        {
            get => _linkedDragEnabled;
            set => SetField(ref _linkedDragEnabled, value);
        }

        /// <summary>
        /// グループ内のウィンドウ数を取得
        /// </summary>
        public int WindowCount => Windows.Count;

        /// <summary>
        /// 表示用のグループヘッダーテキスト
        /// </summary>
        public string DisplayHeader => $"{Name} ({WindowCount} windows)";

        public WindowGroup()
        {
            _id = Guid.NewGuid().ToString();
            Windows.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(WindowCount));
                OnPropertyChanged(nameof(DisplayHeader));
                
                // 先頭ウィンドウからアイコンを取得
                if (Windows.Count > 0 && GroupIcon == null)
                {
                    GroupIcon = Windows[0].Icon;
                }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// グループの種類
    /// </summary>
    public enum GroupType
    {
        /// <summary>
        /// プロセス名による自動グループ
        /// </summary>
        AutoByProcess,

        /// <summary>
        /// ユーザー定義の手動グループ
        /// </summary>
        Manual
    }
}
