using System.Text.Json;
using System.Windows.Controls;
using Clearwell.Core;
using Microsoft.Win32;

namespace Clearwell;

public partial class MainWindow
{
    private int historyPage;
    private void HistoryDetails()
    {
        var actions = new WrapPanel();
        actions.Children.Add(Action(L("Archive undone records", "Geri alınmış kayıtları arşivle"), () => _ = Work(L("Archiving history…", "Geçmiş arşivleniyor…"), _ => { store.ArchiveUndone(); return Task.CompletedTask; })));
        actions.Children.Add(Action(L("Export support report", "Destek raporunu dışa aktar"), ExportDiagnostics));
        Page.Children.Add(actions);
        Section(L("Local activity · Last 200 operations", "Yerel etkinlik · Son 200 işlem"));
        Notice(L("This log records operation areas, outcomes and error codes only. It has no file paths or installer output. Entries are not undo commands and are never uploaded automatically.", "Bu günlük yalnızca işlem alanını, sonucunu ve hata kodunu kaydeder. Dosya yolu veya kurucu çıktısı içermez. Kayıtlar geri alma komutu değildir ve otomatik yüklenmez."));
        try
        {
            foreach (var item in ActivityLog.Read(store.Folder).AsEnumerable().Reverse())
            {
                var area = Routes.FirstOrDefault(r => r.Id == item.Area);
                var outcome = item.Outcome switch { "completed" => L("Completed", "Tamamlandı"), "stopped" => L("Stopped / check actual state", "Durduruldu / gerçek durumu kontrol edin"), "failed" => L("Needs attention", "İnceleme gerekiyor"), _ => L("Unverified / inspect result", "Doğrulanmadı / sonucu inceleyin") };
                Page.Children.Add(Row(area.Id == null ? L("Operation", "İşlem") : L(area.En, area.Tr), $"{item.TimeUtc.ToLocalTime():g} · {outcome}" + (item.ErrorCode is int code ? $" · 0x{code:X8}" : "")));
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { Notice(L("Activity log is unreadable. Recovery history is independent.", "Etkinlik günlüğü okunamıyor. Kurtarma geçmişi bağımsızdır."), warning: true); }
    }
    private void ExportDiagnostics()
    {
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "Clearwell-support.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, ActivityLog.Diagnostics(store.Folder)); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException) { _ = Inform(L("Export failed", "Aktarım başarısız"), e.Message); }
    }
    private void History()
    {
        historyPage = Math.Clamp(historyPage, 0, Math.Max(0, (store.Changes.Count - 1) / 200));
        Heading(L("Every change has a story.", "Her değişikliğin bir kaydı var."), L("Original settings stay on this device. Undo the latest related change first.", "Özgün ayarlar bu cihazda kalır. İlgili son değişikliği önce geri alın."));
        Notice(L("History covers settings and power plans only. File deletions and application actions cannot be undone here. A pending entry may indicate an interrupted operation; undo checks the current value first.", "Geçmiş yalnızca ayarları ve güç planlarını kapsar. Dosya silme ve uygulama işlemleri buradan geri alınamaz. Bekleyen kayıt kesilen işlemi gösterebilir; geri alma önce mevcut değeri kontrol eder."));
        if (store.Changes.Count == 0)
        {
            Section(L("Nothing changed yet.", "Henüz değişiklik yok.")); Page.Children.Add(Text(L("When you apply a Windows setting or power plan, its recovery record appears here.", "Windows ayarı veya güç planı uyguladığınızda kurtarma kaydı burada görünür."), 14, true));
        }
        if (store.Changes.Count > 200)
        {
            Page.Children.Add(Text(L($"Recovery page {historyPage + 1} · 200 records per page", $"Kurtarma sayfası {historyPage + 1} · Sayfada 200 kayıt"), 13));
            if (historyPage > 0) Page.Children.Add(Action(L("Newer records", "Daha yeni kayıtlar"), () => { historyPage--; Navigate("history"); }));
            if ((historyPage + 1) * 200 < store.Changes.Count) Page.Children.Add(Action(L("Older records", "Daha eski kayıtlar"), () => { historyPage++; Navigate("history"); }));
        }
        foreach (var change in store.Changes.AsEnumerable().Reverse().Skip(historyPage * 200).Take(200))
        {
            var setting = SystemSettings.All.FirstOrDefault(s => s.Id == change.Target);
            var title = change.Kind == "power" ? L("Power plan", "Güç planı") : setting == null ? L("Unknown setting", "Bilinmeyen ayar") : L(setting.En, setting.Tr);
            var state = change.State switch { "applied" => L("Applied", "Uygulandı"), "undone" => L("Undone", "Geri alındı"), _ => L("Pending / interrupted", "Bekliyor / kesildi") };
            var action = Action(L("Undo", "Geri al"), () => _ = Undo(change)); action.IsEnabled = change.State is "applied" or "pending" && !busy && !store.Damaged && store.IsLatest(change);
            Page.Children.Add(Row(title, $"{change.Time.ToLocalTime():g} · {state}\n{change.Before ?? L("Windows default", "Windows varsayılanı")} → {change.After}", action));
        }
        HistoryDetails();
    }
    private async Task Undo(ChangeRecord change)
    {
        if (!await Confirm(L("Restore the original value?", "Özgün değer geri yüklensin mi?"), L("Undo restores the value recorded before this change. If another program has changed it since, Clearwell refuses to overwrite it. Sign-out or restart may be needed.", "Geri alma, işlem öncesi kaydedilen değeri yükler. Başka bir program değeri değiştirdiyse Clearwell üzerine yazmayı reddeder. Oturumu kapatma veya yeniden başlatma gerekebilir."), L("Restore value", "Değeri geri yükle"))) return;
        await Work(L("Restoring original value…", "Özgün değer geri yükleniyor…"), async token =>
        {
            if (change.Kind == "power") { await WindowsTools.UndoPower(change, store, token); powerPlans = null; }
            else SystemSettings.Undo(change, store);
        });
    }
}
