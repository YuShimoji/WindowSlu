using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WindowSlu.Models
{
    public class WindowInfo : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private bool _isTopMost;
        private int _opacity = 100;
        private bool _isActive;
        private bool _isClickThrough;

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