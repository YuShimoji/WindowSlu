using System.Windows;
using System.Windows.Threading;
using System;

namespace WindowSlu
{
    public partial class IndicatorWindow : Window
    {
        private DispatcherTimer _fadeoutTimer;

        public IndicatorWindow()
        {
            InitializeComponent();
            _fadeoutTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Display for 1 second
            };
            _fadeoutTimer.Tick += (s, e) => 
            {
                Hide();
                _fadeoutTimer.Stop();
            };
        }

        public void ShowIndicator(int percentage)
        {
            PercentageText.Text = $"{percentage}%";
            this.Visibility = Visibility.Visible;

            // Restart the timer
            _fadeoutTimer.Stop();
            _fadeoutTimer.Start();
        }
    }
}