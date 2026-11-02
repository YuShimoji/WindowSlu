using System.IO;
using System.Text.Json;

namespace WindowSlu.Services;

/// <summary>Replace JSON atomically and retain the previous file.</summary>
public static class JsonFileStore
{
    public static T? Load<T>(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path)) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
                ?? throw new JsonException("The file contains null instead of settings.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            error = $"Could not read {Path.GetFileName(path)}: {ex.Message}";
            try { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false); }
            catch (Exception copyError) when (copyError is IOException or UnauthorizedAccessException) { }
            try
            {
                if (File.Exists(path + ".bak"))
                {
                    var backup = JsonSerializer.Deserialize<T>(File.ReadAllText(path + ".bak"));
                    if (backup != null) { error += " Loaded the previous backup."; return backup; }
                }
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException or JsonException) { }
            return default;
        }
    }

    public static bool Save<T>(string path, T data, out string? error)
    {
        error = null;
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak", true);
            else File.Move(temporary, path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            error = $"Could not save {Path.GetFileName(path)}: {ex.Message}";
            LoggingService.LogError(error);
            return false;
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
