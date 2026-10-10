using System.Text.Json;
using Microsoft.Win32;

namespace Clearwell.Core;

public class Preferences
{
    public string Language { get; set; } = "en";
    public bool Dark { get; set; } = true;
    public bool Motion { get; set; } = true;
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
    public bool Damaged { get; private set; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public LocalStore(string folder)
    {
        Folder = folder;
        Preferences = Load<Preferences>("preferences.json") ?? new();
        Changes = Load<List<ChangeRecord>>("changes.json") ?? [];
        if (Changes.Count > 10000 || Changes.Any(c => c == null ||
            c.State is not ("pending" or "applied" or "undone") ||
            (c.Kind == "registry" ? !SystemSettings.All.Any(s => s.Id == c.Target) || c.ValueKind is not (RegistryValueKind.DWord or RegistryValueKind.String) :
            c.Kind != "power" || !Guid.TryParse(c.Before, out _) || !Guid.TryParse(c.After, out _) || c.Target != c.After)))
        { Damaged = true; Changes = []; }
        if (Preferences.Language is not ("en" or "tr")) Preferences.Language = "en";
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
        { Damaged = true; return default; }
    }
    public void SavePreferences() => Write("preferences.json", Preferences);
    public void SaveChanges()
    {
        if (Damaged) throw new InvalidDataException("Local recovery data is damaged.");
        Write("changes.json", Changes);
    }
    private void Write<T>(string name, T value)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, name);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
    public void Add(ChangeRecord record)
    {
        Changes.Add(record);
        try { SaveChanges(); }
        catch { Changes.Remove(record); throw; }
    }
}
