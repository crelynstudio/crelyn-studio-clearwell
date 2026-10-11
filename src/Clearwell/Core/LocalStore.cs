using System.Text.Json;
using Microsoft.Win32;

namespace Clearwell.Core;

public class Preferences
{
    public string Language { get; set; } = "en";
    public bool Dark { get; set; } = true;
    public bool Motion { get; set; } = true;
    public int RetentionDays { get; set; } = 7;
}
public class ChangeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Time { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = "registry";
    public string Target { get; set; } = "";
    public string? Before { get; set; }
    public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;
    public string After { get; set; } = "";
    public string State { get; set; } = "pending";
}

public sealed class LocalStore
{
    public string Folder { get; }
    public Preferences Preferences { get; }
    public List<ChangeRecord> Changes { get; }
    public bool PreferencesDamaged { get; private set; }
    public bool Damaged { get; private set; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LocalStore(string folder)
    {
        Folder = folder;
        Preferences = Load<Preferences>("preferences.json") ?? new();
        Changes = LoadChanges();
        if (Changes.Any(c => c == null ||
            c.State is not ("pending" or "applied" or "undone") ||
            (c.Kind == "registry" ? !SystemSettings.All.Any(s => s.Id == c.Target && c.After == s.Desired.ToString(System.Globalization.CultureInfo.InvariantCulture)) || c.ValueKind is not (RegistryValueKind.DWord or RegistryValueKind.String) || (c.ValueKind == RegistryValueKind.DWord && c.Before != null && !int.TryParse(c.Before, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _)) :
            c.Kind != "power" || !Guid.TryParse(c.Before, out _) || !Guid.TryParse(c.After, out _) || c.Target != c.After)))
        { Damaged = true; Changes = []; }
        if (Preferences.Language is not ("en" or "tr")) Preferences.Language = "en";
        if (Preferences.RetentionDays is not (1 or 7 or 30)) Preferences.RetentionDays = 7;
    }
    private sealed record RecoveryFile(int SchemaVersion, List<ChangeRecord> Records);
    private List<ChangeRecord> LoadChanges()
    {
        var path = Path.Combine(Folder, "changes.json");
        if (!File.Exists(path)) return [];
        try
        {
            if (new FileInfo(path).Length > 64_000_000) throw new InvalidDataException();
            var json = File.ReadAllText(path);
            if (json.TrimStart().StartsWith('[')) return JsonSerializer.Deserialize<List<ChangeRecord>>(json) ?? throw new InvalidDataException();
            var file = JsonSerializer.Deserialize<RecoveryFile>(json) ?? throw new InvalidDataException();
            if (file.SchemaVersion != 1 || file.Records == null) throw new InvalidDataException();
            return file.Records;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { Damaged = true; return []; }
    }
    private T? Load<T>(string name)
    {
        var path = Path.Combine(Folder, name);
        if (!File.Exists(path)) return default;
        try
        {
            if (new FileInfo(path).Length > 4_000_000) throw new InvalidDataException();
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        { PreferencesDamaged = true; return default; }
    }
    public void SavePreferences()
    {
        if (PreferencesDamaged && File.Exists(Path.Combine(Folder, "preferences.json")))
            File.Copy(Path.Combine(Folder, "preferences.json"), Path.Combine(Folder, "preferences-damaged-" + Guid.NewGuid().ToString("N") + ".json"));
        Write("preferences.json", Preferences);
        PreferencesDamaged = false;
    }
    public void SaveChanges()
    {
        if (Damaged) throw new InvalidDataException("Local recovery data is damaged.");
        Write("changes.json", new RecoveryFile(1, Changes));
    }
    private void Write<T>(string name, T value)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, name);
        var temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(stream, value, Options); stream.Flush(flushToDisk: true); }
        File.Move(temp, path, true);
    }
    public void Add(ChangeRecord record)
    {
        Changes.Add(record);
        try { SaveChanges(); }
        catch { Changes.Remove(record); throw; }
    }
    public int ArchiveUndone()
    {
        if (Damaged) throw new InvalidDataException("Recovery data is damaged.");
        var records = Changes.Where(c => c.State == "undone").ToList();
        if (records.Count == 0) return 0;
        Write("changes-archive-" + Guid.NewGuid().ToString("N") + ".json", new RecoveryFile(1, records));
        var original = Changes.ToArray();
        Changes.RemoveAll(c => c.State == "undone");
        try { SaveChanges(); }
        catch { Changes.Clear(); Changes.AddRange(original); throw; }
        return records.Count;
    }
    public bool IsLatest(ChangeRecord record) => ReferenceEquals(Changes.LastOrDefault(c => c.State is "pending" or "applied" && c.Kind == record.Kind && (c.Kind == "power" || c.Target == record.Target)), record);
}
