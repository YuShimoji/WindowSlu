using System.Text.Json.Serialization;
using System.Collections.Generic;
using WindowSlu.Models;
using System.Windows.Input;

namespace WindowSlu.Models
{
    public class ApplicationSettings
    {
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "Dark";

        [JsonPropertyName("window_left")]
        public double WindowLeft { get; set; } = 100;

        [JsonPropertyName("window_top")]
        public double WindowTop { get; set; } = 100;

        [JsonPropertyName("window_width")]
        public double WindowWidth { get; set; } = 525;

        [JsonPropertyName("window_height")]
        public double WindowHeight { get; set; } = 600;

        [JsonPropertyName("bulk_opacity_percent")]
        public int BulkOpacityPercent { get; set; } = 80;

        [JsonPropertyName("hotkeys_enabled")]
        public bool HotkeysEnabled { get; set; } = false;

        [JsonPropertyName("hotkey_settings")]
        public List<HotkeySetting> HotkeySettings { get; set; } = new List<HotkeySetting>
        {
            new HotkeySetting { Action = HotkeyAction.IncreaseOpacity,   Key = Key.Up,    Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 5,  IsEnabled = true },
            new HotkeySetting { Action = HotkeyAction.DecreaseOpacity,   Key = Key.Down,  Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 5,  IsEnabled = true },
            new HotkeySetting { Action = HotkeyAction.ToggleTopMost,     Key = Key.T,     Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 0,  IsEnabled = true },
            new HotkeySetting { Action = HotkeyAction.ToggleClickThrough,Key = Key.C,     Modifiers = ModifierKeys.Control | ModifierKeys.Alt, Parameter = 0,  IsEnabled = true },
            new HotkeySetting { Action = HotkeyAction.SetAllTo80,        Key = Key.A,     Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Parameter = 80, IsEnabled = true }
        };
    }
} 