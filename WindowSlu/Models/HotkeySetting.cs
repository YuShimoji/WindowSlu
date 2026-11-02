using System.Text.Json.Serialization;
using System.Windows.Input;

namespace WindowSlu.Models
{
    public class HotkeySetting
    {
        public HotkeyAction Action { get; set; }
        private string _keys = string.Empty;
        public string Keys
        {
            get
            {
                if (Key == Key.None) return _keys;
                var parts = new List<string>();
                if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
                if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
                if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
                if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
                parts.Add(Key.ToString());
                return string.Join("+", parts);
            }
            set => _keys = value;
        }
        public int Parameter { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }
        
        /// <summary>
        /// ApplyPresetアクション用のプリセットID
        /// </summary>
        public string? PresetId { get; set; }

        public HotkeySetting()
        {
        }

        public HotkeySetting(string keys, HotkeyAction action, int parameter = 0)
        {
            Keys = keys;
            Action = action;
            Parameter = parameter;
        }
    }
} 