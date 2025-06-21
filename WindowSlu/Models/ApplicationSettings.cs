using System.Text.Json.Serialization;
using System.Collections.Generic;
using WindowSlu.Models;

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
        public double WindowWidth { get; set; } = 800;

        [JsonPropertyName("window_height")]
        public double WindowHeight { get; set; } = 450;

        [JsonPropertyName("hotkey_settings")]
        public List<HotkeySetting> HotkeySettings { get; set; } = new List<HotkeySetting>
        {
            new HotkeySetting("Ctrl+Alt+Up", HotkeyAction.IncreaseOpacity, 10),
            new HotkeySetting("Ctrl+Alt+Down", HotkeyAction.DecreaseOpacity, 10),
            new HotkeySetting("Ctrl+Alt+0", HotkeyAction.SetOpacity, 100),
            new HotkeySetting("Ctrl+Alt+T", HotkeyAction.ToggleTopMost),
            new HotkeySetting("Ctrl+Alt+C", HotkeyAction.ToggleClickThrough)
        };
    }
} 