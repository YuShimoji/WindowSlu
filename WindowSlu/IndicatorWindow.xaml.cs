using System;
using System.Windows;
using System.Windows.Threading;

namespace WindowSlu
{
    public partial class IndicatorWindow : Window
    {
        public IndicatorWindow(int opacity)
        {
            InitializeComponent();
            OpacityText.Text = $"{opacity}%";
            Loaded += IndicatorWindow_Loaded;
        }

        private void IndicatorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            timer.Tick += (s, args) =>
            {
                timer.Stop();
                this.Close();
            };
            timer.Start();
        }
    }
} 