namespace WindowSlu.Services
{
    /// <summary>Boundary around the operating-system hotkey registration calls.</summary>
    public interface IHotkeyPlatform
    {
        bool RegisterHotKey(IntPtr windowHandle, int id, uint modifiers, uint virtualKey);
        bool UnregisterHotKey(IntPtr windowHandle, int id);
        int GetLastError();
    }
}
