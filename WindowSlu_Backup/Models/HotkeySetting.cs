namespace WindowSlu
{
    public class HotkeySetting
    {
        public string Keys { get; set; } = string.Empty;
        public HotkeyAction Action { get; set; } = HotkeyAction.None;
        public int Parameter { get; set; }

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