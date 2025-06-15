using System.Windows;

namespace WindowSlu
{
    public partial class ResetSettingsDialog : Window
    {
        public bool ResetConfirmed { get; private set; }

        public ResetSettingsDialog()
        {
            InitializeComponent();
            ResetConfirmed = false;
        }

        private void YesButton_Click(object sender, RoutedEventArgs e)
        {
            ResetConfirmed = true;
            DialogResult = true;
            Close();
        }

        private void NoButton_Click(object sender, RoutedEventArgs e)
        {
            ResetConfirmed = false;
            DialogResult = false;
            Close();
        }
    }
} 