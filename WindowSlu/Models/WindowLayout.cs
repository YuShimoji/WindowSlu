using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowSlu.Models
{
    /// <summary>
    /// 保存されたウィンドウレイアウトを表すモデル
    /// </summary>
    public class WindowLayout : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "New Layout";
        private string _description = string.Empty;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _modifiedAt = DateTime.Now;
        private List<WindowLayoutEntry> _entries = new List<WindowLayoutEntry>();

        /// <summary>
        /// レイアウトID
        /// </summary>
        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// レイアウト名
        /// </summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// レイアウトの説明
        /// </summary>
        public string Description
        {
            get => _description;
            set { if (_description != value) { _description = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// 作成日時
        /// </summary>
        public DateTime CreatedAt
        {
            get => _createdAt;
            set { if (_createdAt != value) { _createdAt = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// 更新日時
        /// </summary>
        public DateTime ModifiedAt
        {
            get => _modifiedAt;
            set { if (_modifiedAt != value) { _modifiedAt = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// ウィンドウ配置エントリのリスト
        /// </summary>
        public List<WindowLayoutEntry> Entries
        {
            get => _entries;
            set { if (_entries != value) { _entries = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// レイアウト内の個別ウィンドウエントリ
    /// プロセス名とタイトルパターンでウィンドウを特定し、位置・サイズを復元
    /// </summary>
    public class WindowLayoutEntry
    {
        /// <summary>
        /// ウィンドウを特定するためのプロセス名
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// ウィンドウタイトル（部分一致で使用）
        /// </summary>
        public string? TitlePattern { get; set; }

        /// <summary>
        /// ウィンドウ左端のX座標
        /// </summary>
        public int Left { get; set; }

        /// <summary>
        /// ウィンドウ上端のY座標
        /// </summary>
        public int Top { get; set; }

        /// <summary>
        /// ウィンドウ幅
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// ウィンドウ高さ
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// 透明度（0-100）
        /// </summary>
        public int? Opacity { get; set; }

        /// <summary>
        /// 最前面表示フラグ
        /// </summary>
        public bool? IsTopMost { get; set; }

        /// <summary>
        /// モニター識別子（マルチモニター対応）
        /// </summary>
        public string MonitorId { get; set; } = string.Empty;
    }

    /// <summary>
    /// レイアウトの保存データ
    /// </summary>
    public class LayoutData
    {
        public List<WindowLayout> Layouts { get; set; } = new List<WindowLayout>();
    }
}
