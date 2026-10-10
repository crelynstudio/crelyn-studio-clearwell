using Microsoft.Win32;

namespace Clearwell.Core;

public record SettingDefinition(string Id, string Group, string En, string Tr, string DescriptionEn, string DescriptionTr, string Key, string Value, int Desired);

public static class SystemSettings
{
    private const string Current = @"Software\Microsoft\Windows\CurrentVersion\";
    public static IReadOnlyList<SettingDefinition> All { get; } = [
        new("capture", "performance", "Turn off game capture", "Oyun kaydını kapat", "Disables Game DVR capture for this user. Windows game recording will be unavailable; restart may be needed.", "Bu kullanıcı için Game DVR kaydını kapatır. Windows oyun kaydı kullanılamaz; yeniden başlatma gerekebilir.", Current + "GameDVR", "AppCaptureEnabled", 0),
        new("background-capture", "performance", "Turn off Game DVR background support", "Game DVR arka plan desteğini kapat", "Disables the current user's Game DVR preference. Pair with game capture above. Does not increase FPS by a guaranteed amount.", "Geçerli kullanıcının Game DVR tercihini kapatır. Yukarıdaki oyun kaydıyla birlikte kullanılabilir. Belirli bir FPS artışı garanti etmez.", @"System\GameConfigStore", "GameDVR_Enabled", 0),
        new("visual-effects", "performance", "Prefer performance for visual effects", "Görsel efektlerde performansı tercih et", "Sets Windows' best-performance preference. Sign out and back in; individual effects can still be managed in Windows.", "Windows'un en iyi performans tercihini ayarlar. Oturumu kapatıp açın; tekil efektleri Windows'tan yönetebilirsiniz.", Current + @"Explorer\VisualEffects", "VisualFXSetting", 2),
        new("advertising", "privacy", "Turn off advertising ID", "Reklam kimliğini kapat", "Stops apps from using this user's Windows advertising ID. Does not block all ads or network tracking.", "Uygulamaların bu kullanıcının Windows reklam kimliğini kullanmasını kapatır. Tüm reklamları veya ağ takibini engellemez.", Current + "AdvertisingInfo", "Enabled", 0),
        new("tailored", "privacy", "Turn off tailored experiences", "Kişiselleştirilmiş deneyimleri kapat", "Disables this user's diagnostic-data personalization preference. Does not turn off required diagnostics.", "Bu kullanıcının tanılama verisine dayalı kişiselleştirme tercihini kapatır. Zorunlu tanılamayı kapatmaz.", Current + "Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0),
        new("suggestions", "privacy", "Turn off suggested app content", "Önerilen uygulama içeriğini kapat", "Disables the current user's subscribed app-suggestion preference. Availability varies by Windows version; some recommendations have separate controls.", "Geçerli kullanıcının abonelikli uygulama önerisi tercihini kapatır. Windows sürümüne göre değişir; bazı önerilerin ayrı ayarları vardır.", Current + "ContentDeliveryManager", "SubscribedContent-338388Enabled", 0)
    ];

    public static string? Read(SettingDefinition setting)
    {
        using var key = Registry.CurrentUser.OpenSubKey(setting.Key);
        var value = key?.GetValue(setting.Value, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value == null ? null : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
    }
    public static void Apply(SettingDefinition setting, LocalStore store)
    {
        if (!All.Contains(setting)) throw new ArgumentException("Unknown setting.");
        using var existing = Registry.CurrentUser.OpenSubKey(setting.Key);
        var original = Read(setting);
        if (original == setting.Desired.ToString()) return;
        var kind = original == null ? RegistryValueKind.DWord : existing!.GetValueKind(setting.Value);
        if (kind is not (RegistryValueKind.DWord or RegistryValueKind.String)) throw new InvalidDataException("Unsupported registry type.");
        var change = new ChangeRecord { Target = setting.Id, Before = original, ValueKind = kind, After = setting.Desired.ToString() };
        store.Add(change);
        using var key = Registry.CurrentUser.CreateSubKey(setting.Key, true);
        key.SetValue(setting.Value, setting.Desired, RegistryValueKind.DWord);
        if (Read(setting) != change.After) throw new IOException("Saved preference verification failed.");
        change.State = "applied";
        store.SaveChanges();
    }
    public static void Undo(ChangeRecord change, LocalStore store)
    {
        if (store.Damaged) throw new InvalidDataException("Recovery data is damaged.");
        if (change.Kind != "registry" || change.State is not ("applied" or "pending")) throw new InvalidOperationException();
        var setting = All.Single(s => s.Id == change.Target);
        if (change.After != setting.Desired.ToString() || Read(setting) != change.After)
            throw new InvalidOperationException("The value has changed outside Clearwell.");
        using var key = Registry.CurrentUser.CreateSubKey(setting.Key, true);
        if (change.Before == null) key.DeleteValue(setting.Value, false);
        else if (change.ValueKind == RegistryValueKind.DWord && int.TryParse(change.Before, out var value))
            key.SetValue(setting.Value, value, RegistryValueKind.DWord);
        else if (change.ValueKind == RegistryValueKind.String) key.SetValue(setting.Value, change.Before, RegistryValueKind.String);
        else throw new InvalidDataException();
        change.State = "undone";
        store.SaveChanges();
    }
}
