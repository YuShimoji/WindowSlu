using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowSlu.Models
{
    /// <summary>
    /// ウィンドウプリセットを表すモデル
    /// サイズ、透明度、位置、TopMost、カスケード設定を保持
    /// </summary>
    public class WindowPreset : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name = "New Preset";
        private string _description = string.Empty;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _modifiedAt = DateTime.Now;

        // ターゲット条件
        private string? _targetProcessName;
        private string? _targetGroupId;

        // ウィンドウ設定
        private int? _width;
        private int? _height;
        private int? _opacity;
        private bool? _isTopMost;

        // カスケード設定
        private bool _enableCascade = false;
        private int _cascadeOffsetX = 30;
        private int _cascadeOffsetY = 30;
        private CascadeDirection _cascadeDirection = CascadeDirection.BottomRight;
        private int? _cascadeStartX;
        private int? _cascadeStartY;

        /// <summary>
        /// プリセットID
        /// </summary>
        public string Id
        {
            get => _id;
            set { if (_id != value) { _id = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// プリセット名
        /// </summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// プリセットの説明
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
        /// 対象プロセス名（nullの場合は条件なし）
        /// </summary>
        public string? TargetProcessName
        {
            get => _targetProcessName;
            set { if (_targetProcessName != value) { _targetProcessName = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// 対象グループID（nullの場合は条件なし）
        /// </summary>
        public string? TargetGroupId
        {
            get => _targetGroupId;
            set { if (_targetGroupId != value) { _targetGroupId = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// ウィンドウ幅（nullの場合は変更しない）
        /// </summary>
        public int? Width
        {
            get => _width;
            set { if (_width != value) { _width = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// ウィンドウ高さ（nullの場合は変更しない）
        /// </summary>
        public int? Height
        {
            get => _height;
            set { if (_height != value) { _height = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// 透明度（0-100、nullの場合は変更しない）
        /// </summary>
        public int? Opacity
        {
            get => _opacity;
            set { if (_opacity != value) { _opacity = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// 最前面表示（nullの場合は変更しない）
        /// </summary>
        public bool? IsTopMost
        {
            get => _isTopMost;
            set { if (_isTopMost != value) { _isTopMost = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケード配置を有効にする
        /// </summary>
        public bool EnableCascade
        {
            get => _enableCascade;
            set { if (_enableCascade != value) { _enableCascade = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケードX方向オフセット（ピクセル）
        /// </summary>
        public int CascadeOffsetX
        {
            get => _cascadeOffsetX;
            set { if (_cascadeOffsetX != value) { _cascadeOffsetX = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケードY方向オフセット（ピクセル）
        /// </summary>
        public int CascadeOffsetY
        {
            get => _cascadeOffsetY;
            set { if (_cascadeOffsetY != value) { _cascadeOffsetY = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケード方向
        /// </summary>
        public CascadeDirection CascadeDirection
        {
            get => _cascadeDirection;
            set { if (_cascadeDirection != value) { _cascadeDirection = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケード開始X座標（nullの場合は先頭ウィンドウの位置を使用）
        /// </summary>
        public int? CascadeStartX
        {
            get => _cascadeStartX;
            set { if (_cascadeStartX != value) { _cascadeStartX = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// カスケード開始Y座標（nullの場合は先頭ウィンドウの位置を使用）
        /// </summary>
        public int? CascadeStartY
        {
            get => _cascadeStartY;
            set { if (_cascadeStartY != value) { _cascadeStartY = value; OnPropertyChanged(); _modifiedAt = DateTime.Now; } }
        }

        /// <summary>
        /// プリセットのクローンを作成
        /// </summary>
        public WindowPreset Clone()
        {
            return new WindowPreset
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{Name} (Copy)",
                Description = Description,
                TargetProcessName = TargetProcessName,
                TargetGroupId = TargetGroupId,
                Width = Width,
                Height = Height,
                Opacity = Opacity,
                IsTopMost = IsTopMost,
                EnableCascade = EnableCascade,
                CascadeOffsetX = CascadeOffsetX,
                CascadeOffsetY = CascadeOffsetY,
                CascadeDirection = CascadeDirection,
                CascadeStartX = CascadeStartX,
                CascadeStartY = CascadeStartY
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// カスケード配置の方向
    /// </summary>
    public enum CascadeDirection
    {
        /// <summary>右下方向（標準）</summary>
        BottomRight,
        /// <summary>右上方向</summary>
        TopRight,
        /// <summary>左下方向</summary>
        BottomLeft,
        /// <summary>左上方向</summary>
        TopLeft,
        /// <summary>右方向のみ</summary>
        Right,
        /// <summary>下方向のみ</summary>
        Down
    }

    /// <summary>
    /// プリセットとグループの保存データ
    /// </summary>
    public class PresetData
    {
        public List<WindowPreset> Presets { get; set; } = new List<WindowPreset>();
        public List<ManualGroupData> ManualGroups { get; set; } = new List<ManualGroupData>();
    }

    /// <summary>
    /// 手動グループの保存データ
    /// </summary>
    public class ManualGroupData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public List<string> ProcessNames { get; set; } = new List<string>();
    }
}
