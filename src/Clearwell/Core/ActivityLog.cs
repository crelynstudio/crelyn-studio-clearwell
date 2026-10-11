using System.Text.Json;

namespace Clearwell.Core;

public record ActivityRecord(DateTime TimeUtc, string Area, string Outcome, int? ErrorCode);

public static class ActivityLog
{
    public static List<ActivityRecord> Read(string folder)
    {
        var path = Path.Combine(folder, "activity.json");
        if (!File.Exists(path)) return [];
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Activity log is too large.");
        var entries = JsonSerializer.Deserialize<List<ActivityRecord>>(File.ReadAllText(path)) ?? [];
        if (entries.Count > 200 || entries.Any(e => e == null || e.Area is not ("home" or "cleanup" or "cleanup-review" or "performance" or "privacy" or "apps" or "power" or "history" or "about" or "tools" or "search") || e.Outcome is not ("completed" or "stopped" or "failed" or "inspect")))
            throw new InvalidDataException("Unrecognized activity data.");
        return entries;
    }
    public static void Add(string folder, string area, string outcome, int? errorCode = null)
    {
        var entries = Read(folder);
        entries.Add(new(DateTime.UtcNow, area, outcome, errorCode));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "activity.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(entries.TakeLast(200)));
        File.Move(path + ".tmp", path, true);
    }
    public static string Diagnostics(string folder) => JsonSerializer.Serialize(new
    {
        Version = VersionInfo.Current,
        Windows = Environment.OSVersion.Version.ToString(),
        Architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
        Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Activity = Read(folder)
    }, new JsonSerializerOptions { WriteIndented = true });
}
