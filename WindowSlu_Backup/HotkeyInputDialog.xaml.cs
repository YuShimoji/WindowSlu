using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace WindowSlu
{
    public partial class HotkeyInputDialog : Window
    {
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        public uint SelectedModifiers { get; private set; } = MOD_NONE;
        public uint SelectedVirtualKey { get; private set; } = 0;
        public HotkeyAction SelectedAction { get; private set; } = HotkeyAction.None;

        public HotkeyInputDialog(HotkeyAction currentAction = HotkeyAction.None, uint currentModifiers = MOD_NONE, uint currentKey = 0)
        {
            InitializeComponent();
            LoadActions();

            // Set initial values if provided (for editing)
            SelectedAction = currentAction;
            ActionComboBox.SelectedItem = currentAction;

            SelectedModifiers = currentModifiers;
            CtrlCheckBox.IsChecked = (currentModifiers & MOD_CONTROL) != 0;
            AltCheckBox.IsChecked = (currentModifiers & MOD_ALT) != 0;
            ShiftCheckBox.IsChecked = (currentModifiers & MOD_SHIFT) != 0;
            WinCheckBox.IsChecked = (currentModifiers & MOD_WIN) != 0;

            SelectedVirtualKey = currentKey;
            if (currentKey != 0)
            {
                Key key = KeyInterop.KeyFromVirtualKey((int)currentKey);
                KeyTextBox.Text = key != Key.None ? key.ToString() : $"0x{currentKey:X2}";
            }
            else
            {
                KeyTextBox.Text = "キーを選択 (押下してください)";
            }
        }

        private void LoadActions()
        {
            ActionComboBox.ItemsSource = Enum.GetValues(typeof(HotkeyAction));
            ActionComboBox.SelectedItem = HotkeyAction.None;
        }

        private void KeyTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true; // Prevent further processing of the key press

            // Get the virtual key code
            SelectedVirtualKey = (uint)KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key);

            // Update the TextBox
            KeyTextBox.Text = (e.Key == Key.System ? e.SystemKey : e.Key).ToString();

            // Modifiers are handled by CheckBoxes, PreviewKeyDown only for the main key.
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update SelectedModifiers from CheckBoxes
            SelectedModifiers = MOD_NONE;
            if (CtrlCheckBox.IsChecked == true) SelectedModifiers |= MOD_CONTROL;
            if (AltCheckBox.IsChecked == true) SelectedModifiers |= MOD_ALT;
            if (ShiftCheckBox.IsChecked == true) SelectedModifiers |= MOD_SHIFT;
            if (WinCheckBox.IsChecked == true) SelectedModifiers |= MOD_WIN;

            // Update SelectedAction from ComboBox
            if (ActionComboBox.SelectedItem is HotkeyAction action)
            {
                SelectedAction = action;
            }
            else
            {
                // Handle case where no action is selected, or set a default
                SelectedAction = HotkeyAction.None;
            }

            // Basic validation
            if (SelectedVirtualKey == 0)
            {
                System.Windows.MessageBox.Show("キーが選択されていません。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (SelectedModifiers == MOD_NONE && SelectedVirtualKey != 0) // Allow "None" modifier for single key hotkeys if desired
            {
                // This might be acceptable depending on requirements.
                // If modifiers are mandatory, add a check here.
            }
            if (SelectedAction == HotkeyAction.None)
            {
                System.Windows.MessageBox.Show("アクションが選択されていません。", "入力エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            this.DialogResult = true;
            this.Close();
        }
    }
} 