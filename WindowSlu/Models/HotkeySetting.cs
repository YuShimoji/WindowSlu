using System.Text.Json.Serialization;
using System.Windows.Input;

namespace WindowSlu.Models
{
    public class HotkeySetting
    {
        public HotkeyAction Action { get; set; }
        public string Keys { get; set; } = string.Empty;
        public int Parameter { get; set; }
        public bool IsEnabled { get; set; } = true;
        public Key Key { get; set; }
        public ModifierKeys Modifiers { get; set; }

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