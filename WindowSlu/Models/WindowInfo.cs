using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace WindowSlu.Models
{
    public class WindowInfo : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isTopMost;
        private int _opacity = 100;
        private bool _isActive;
        private bool _isClickThrough;
        private ImageSource? _icon;
        private string? _groupId;
        private int _left;
        private int _top;
        private int _width;
        private int _height;
        private string _monitorId = string.Empty;

        public IntPtr Handle { get; set; }
        
        public string Title
        {
            get => _title;
            set => SetField(ref _title, value);
        }

        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        
        public bool IsTopMost
        {
            get => _isTopMost;
            set => SetField(ref _isTopMost, value);
        }

        public int Opacity
        {
            get => _opacity;
            set => SetField(ref _opacity, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetField(ref _isActive, value);
        }

        public bool IsClickThrough
        {
            get => _isClickThrough;
            set => SetField(ref _isClickThrough, value);
        }

        public ImageSource? Icon
        {
            get => _icon;
            set => SetField(ref _icon, value);
        }

        /// <summary>
        /// 所属グループID
        /// </summary>
        public string? GroupId
        {
            get => _groupId;
            set => SetField(ref _groupId, value);
        }

        /// <summary>
        /// ウィンドウのX座標
        /// </summary>
        public int Left
        {
            get => _left;
            set => SetField(ref _left, value);
        }

        /// <summary>
        /// ウィンドウのY座標
        /// </summary>
        public int Top
        {
            get => _top;
            set => SetField(ref _top, value);
        }

        /// <summary>
        /// ウィンドウの幅
        /// </summary>
        public int Width
        {
            get => _width;
            set => SetField(ref _width, value);
        }

        /// <summary>
        /// ウィンドウの高さ
        /// </summary>
        public int Height
        {
            get => _height;
            set => SetField(ref _height, value);
        }

        /// <summary>
        /// 所属モニター識別子
        /// </summary>
        public string MonitorId
        {
            get => _monitorId;
            set => SetField(ref _monitorId, value);
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
} 