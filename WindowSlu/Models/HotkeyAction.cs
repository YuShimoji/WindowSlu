namespace WindowSlu.Models
{
    public enum HotkeyAction
    {
        None,
        SetOpacity,
        IncreaseOpacity,
        DecreaseOpacity,
        ToggleTopMost,
        ToggleClickThrough,
        SetAllTo80,
        ApplyPreset // プリセット適用（ParameterにプリセットインデックスまたはPresetIdを使用）
    }
} 