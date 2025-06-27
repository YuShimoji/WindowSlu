using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WindowSlu.Models;
using WindowSlu.Services;

namespace WindowSlu.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly SettingsService _settingsService;
        private readonly MainWindow _mainWindow;

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

        public ObservableCollection<WindowInfo> Windows { get; } = new ObservableCollection<WindowInfo>();

        public MainViewModel(SettingsService settingsService, MainWindow mainWindow)
        {
            _settingsService = settingsService;
            _mainWindow = mainWindow;
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