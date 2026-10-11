using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Clearwell.Core;
using Microsoft.Win32;
using Path = System.IO.Path;

namespace Clearwell;

public partial class MainWindow
{
    private readonly HashSet<string> excludedFiles = new(StringComparer.OrdinalIgnoreCase);
    private sealed class ReviewFile(FileCandidate file, string category, Action<bool> changed)
    {
        private bool selected = true;
        public bool Selected { get => selected; set { selected = value; changed(value); } }
        public string FileName => Path.GetFileName(file.Path);
        public string FullPath => App.CaptureFolder == null ? file.Path : file.Path.Replace(App.CaptureFolder, "%TEST_FIXTURE%", StringComparison.OrdinalIgnoreCase);
        public string Size => Bytes(file.Length);
        public DateTime Modified => new DateTime(file.LastWriteTicks, DateTimeKind.Utc).ToLocalTime();
        public string Category => category;
    }

    private void ReviewCleanup()
    {
        Heading(L("Review individual files", "Dosyaları tek tek inceleyin"), L("Uncheck files to keep. Filtering does not change selection. Paths stay on this PC.", "Korunacak dosyaların seçimini kaldırın. Filtreleme seçimi değiştirmez. Yollar bu bilgisayarda kalır."));
        Page.Children.Add(Action(L("Back to categories", "Kategorilere dön"), () => Navigate("cleanup")));
        if (scans == null) { Notice(L("Scan files first.", "Önce dosyaları tarayın.")); return; }
        var candidates = scans.Where(s => selectedRoots.Contains(s.Root.Id)).ToArray();
        var count = Text("", 14);
        void Update() => count.Text = L($"Selected: {candidates.Sum(s => s.Files.Count(f => !excludedFiles.Contains(f.Path))):N0} files", $"Seçili: {candidates.Sum(s => s.Files.Count(f => !excludedFiles.Contains(f.Path))):N0} dosya");
        var rows = candidates.SelectMany(s => s.Files.Select(f => new ReviewFile(f, (s.Root.Advanced ? L("Advanced · ", "Gelişmiş · ") : "") + L(s.Root.En, s.Root.Tr), value =>
        { if (value) excludedFiles.Remove(f.Path); else excludedFiles.Add(f.Path); Update(); }) { Selected = !excludedFiles.Contains(f.Path) })).ToList();
        var view = new ListCollectionView(rows);
        var filter = new TextBox { Margin = new Thickness(0, 6, 0, 12) };
        Name(filter, L("Filter files by name or path", "Dosyaları ad veya yola göre filtrele"));
        filter.ToolTip = L("Filter by file name or full path", "Dosya adı veya tam yola göre filtrele");
        filter.TextChanged += (_, _) => { view.Filter = item => ((ReviewFile)item).FullPath.Contains(filter.Text, StringComparison.OrdinalIgnoreCase); };
        Page.Children.Add(Text(L("Filter by name or path", "Ad veya yola göre filtrele"), 13, true)); Page.Children.Add(filter);
        var grid = new DataGrid { ItemsSource = view, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, Height = 320,
            EnableRowVirtualization = true, EnableColumnVirtualization = true, RowHeaderWidth = 0, RowHeight = 36, ColumnHeaderHeight = 34, SelectionMode = DataGridSelectionMode.Single, IsEnabled = !busy,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        grid.SetResourceReference(BackgroundProperty, "Surface"); grid.SetResourceReference(ForegroundProperty, "Ink");
        grid.SetResourceReference(DataGrid.RowBackgroundProperty, "Surface");
        var headerStyle = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        headerStyle.Setters.Add(new Setter(BackgroundProperty, FindResource("Raised"))); headerStyle.Setters.Add(new Setter(ForegroundProperty, FindResource("Ink")));
        grid.ColumnHeaderStyle = headerStyle;
        var cellStyle = new Style(typeof(DataGridCell));
        cellStyle.Setters.Add(new Setter(BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        cellStyle.Setters.Add(new Setter(ForegroundProperty, FindResource("Ink")));
        var selected = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selected.Setters.Add(new Setter(BackgroundProperty, FindResource("AccentSoft"))); selected.Setters.Add(new Setter(ForegroundProperty, FindResource("Ink")));
        cellStyle.Triggers.Add(selected); grid.CellStyle = cellStyle;
        grid.Columns.Add(new DataGridCheckBoxColumn { Header = L("Delete", "Sil"), Binding = new Binding("Selected") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 65 });
        foreach (var column in new[] { ("FileName", L("File", "Dosya"), 160.0), ("FullPath", L("Full path", "Tam yol"), 330.0), ("Size", L("Size", "Boyut"), 85.0), ("Modified", L("Modified", "Değiştirilme"), 160.0), ("Category", L("Category / risk", "Kategori / risk"), 180.0) })
            grid.Columns.Add(new DataGridTextColumn { Header = column.Item2, Binding = new Binding(column.Item1), Width = column.Item3, IsReadOnly = true });
        Name(grid, L("Cleanup candidates. Toggle Delete with the Space key; scroll for full paths.", "Temizlik adayları. Sil seçimini boşluk tuşuyla değiştirin; tam yollar için kaydırın."));
        Page.Children.Add(grid); Update(); Page.Children.Add(count);
        var actions = new WrapPanel();
        foreach (var select in new[] { true, false })
            actions.Children.Add(Action(select ? L("Select filtered", "Filtrelenenleri seç") : L("Keep filtered", "Filtrelenenleri koru"), () =>
            { foreach (ReviewFile row in view) row.Selected = select; grid.Items.Refresh(); }));
        actions.Children.Add(Action(L("Export review CSV", "İncelemeyi CSV aktar"), () =>
        {
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            var dialog = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "Clearwell-cleanup-review.csv" };
            if (dialog.ShowDialog(this) != true) return;
            try { File.WriteAllText(dialog.FileName, CleanupService.ReviewCsv(candidates, excludedFiles), new System.Text.UTF8Encoding(true)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { _ = Inform(L("Export failed", "Aktarım başarısız"), e.Message); }
        }));
        actions.Children.Add(Action(L("Review permanent deletion", "Kalıcı silmeyi incele"), () => { grid.CommitEdit(DataGridEditingUnit.Cell, true); _ = CleanAsync(); }, true));
        Page.Children.Add(actions);
        Notice(L("Permanent deletion has no undo. Advanced categories may contain useful caches or crash evidence. CSV contains full paths; review before sharing.", "Kalıcı silmenin geri alması yoktur. Gelişmiş kategoriler yararlı önbellek veya çökme kanıtları içerebilir. CSV tam yollar içerir; paylaşmadan önce inceleyin."), warning: true);
    }
    private void Cleanup()
    {
        Heading(L("Cleanup workspace", "Temizlik çalışma alanı"), L("Scan known locations, compare categories and review what will be removed.", "Bilinen konumları tarayın, kategorileri karşılaştırın ve silinecekleri inceleyin."));
        var actions = new WrapPanel();
        actions.Children.Add(Action(scans == null ? L("Scan files", "Dosyaları tara") : L("Scan again", "Yeniden tara"), () => _ = ScanAsync(), true));
        if (scans != null) actions.Children.Add(Action(L("Review individual files", "Dosyaları tek tek incele"), () => Navigate("cleanup-review")));
        var retention = new ComboBox { Width = 180, Margin = new Thickness(4, 6, 0, 6), IsEnabled = !busy };
        foreach (var days in new[] { 1, 7, 30 }) retention.Items.Add(new ComboBoxItem { Content = L($"Keep last {days} days", $"Son {days} günü koru"), Tag = days });
        retention.SelectedIndex = store.Preferences.RetentionDays == 30 ? 2 : store.Preferences.RetentionDays == 7 ? 1 : 0;
        Name(retention, L("Minimum file age", "En az dosya yaşı"));
        retention.SelectionChanged += (_, _) => { store.Preferences.RetentionDays = (int)((ComboBoxItem)retention.SelectedItem).Tag; scans = null; SavePreferences(); Navigate("cleanup"); };
        actions.Children.Add(retention);
        Page.Children.Add(actions);
        if (scans == null) Notice(L($"Scanning is read-only. Files changed within the last {store.Preferences.RetentionDays} days are excluded.", $"Tarama yalnızca okuma yapar. Son {store.Preferences.RetentionDays} günde değişen dosyalar hariç tutulur."));
        else Notice(L($"Scan complete · {Bytes(scans.Sum(s => s.Bytes))} across {scans.Sum(s => s.Files.Count):N0} eligible files. This is a snapshot, not a promise of recoverable space.", $"Tarama tamamlandı · {scans.Sum(s => s.Files.Count):N0} uygun dosyada {Bytes(scans.Sum(s => s.Bytes))}. Bu bir anlık görüntüdür; kazanılacak alan garantisi değildir."), success: true);
        if (scans?.Any(s => s.Truncated) == true) Notice(L("Partial scan: the safety limit was reached. Totals cover only the inspected subset; deletion requires an additional confirmation.", "Kısmi tarama: güvenlik sınırına ulaşıldı. Toplamlar yalnızca incelenen alt kümeyi kapsar; silme ek onay gerektirir."), warning: true);
        var categories = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var selectionLabel = Text("", 16);
        void UpdateCleanupSelection()
        {
            var files = scans?.Where(s => selectedRoots.Contains(s.Root.Id)).SelectMany(s => s.Files).Where(f => !excludedFiles.Contains(f.Path)).ToArray();
            selectionLabel.Text = files == null ? L("No scan yet", "Henüz tarama yok") : L($"Selected: {Bytes(files.Sum(f => f.Length))} · {files.Length:N0} files", $"Seçili: {Bytes(files.Sum(f => f.Length))} · {files.Length:N0} dosya");
        }
        foreach (var root in CleanupService.Roots())
        {
            var scan = scans?.FirstOrDefault(s => s.Root.Id == root.Id);
            var check = new CheckBox { Content = L(root.En, root.Tr), FontSize = 16, FontWeight = FontWeights.SemiBold, IsChecked = selectedRoots.Contains(root.Id), VerticalAlignment = VerticalAlignment.Center, IsEnabled = !busy };
            Name(check, L(root.En, root.Tr));
            check.Checked += (_, _) => { selectedRoots.Add(root.Id); UpdateCleanupSelection(); }; check.Unchecked += (_, _) => { selectedRoots.Remove(root.Id); UpdateCleanupSelection(); };
            var displayPath = root.Path.Replace(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "%LOCALAPPDATA%", StringComparison.OrdinalIgnoreCase).Replace(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "%WINDIR%", StringComparison.OrdinalIgnoreCase);
            var detail = L(root.DetailEn, root.DetailTr) + "\n" + displayPath;
            if (scan != null && scan.Skipped > 0) detail += L($"\n{scan.Skipped} inaccessible or linked items skipped.", $"\n{scan.Skipped} erişilemeyen veya bağlantılı öğe atlandı.");
            if (scan != null) foreach (var reason in scan.Reasons)
                detail += "\n" + (reason.Key switch { "access" => L("Access denied", "Erişim reddedildi"), "locked" => L("Locked", "Kilitli"), "link" => L("Link / outside scope", "Bağlantı / kapsam dışı"), _ => L("I/O error", "Girdi/çıktı hatası") }) + $": {reason.Value:N0}";
            if (scan?.Truncated == true) detail += L("\nScan limit reached; results are partial.", "\nTarama sınırına ulaşıldı; sonuçlar kısmi.");
            var body = new StackPanel(); body.Children.Add(check);
            var amount = Text(scan == null ? L("Awaiting scan", "Tarama bekleniyor") : Bytes(scan.Bytes), 26); amount.FontWeight = FontWeights.SemiBold; amount.Margin = new Thickness(0, 16, 0, 4); body.Children.Add(amount);
            body.Children.Add(Text(scan == null ? L("Read-only discovery", "Salt okunur keşif") : L($"{scan.Files.Count:N0} eligible files", $"{scan.Files.Count:N0} uygun dosya"), 12, true));
            body.Children.Add(Text(detail, 12, true));
            var panel = Panel(body); panel.Margin = new Thickness(0, 8, 16, 8); categories.Children.Add(panel);
        }
        Page.Children.Add(categories); UpdateCleanupSelection(); selectionLabel.Margin = new Thickness(0, 18, 0, 10); Page.Children.Add(selectionLabel);
        Notice(L("Deletion is permanent and cannot be undone from Change history. Files in use, links and changed files are skipped. Prefetch and crash dumps are optional.", "Silme kalıcıdır; Değişiklik geçmişinden geri alınamaz. Kullanılan, bağlantılı ve değişmiş dosyalar atlanır. Prefetch ve çökme dökümleri isteğe bağlıdır."), warning: true);
        if (!IsAdmin) Page.Children.Add(Action(L("Restart as administrator", "Yönetici olarak yeniden başlat"), () => _ = RestartAdmin()));
    }
    private async Task ScanAsync()
    {
        await Work(L("Scanning temporary locations…", "Geçici konumlar taranıyor…"), async token =>
        {
            scans = null; excludedFiles.Clear();
            scans = await Task.Run(() => CleanupService.Roots().Select(root => CleanupService.Scan(root, token, retentionDays: store.Preferences.RetentionDays)).ToList(), token);
        });
    }
    private async Task CleanAsync()
    {
        if (scans == null || busy) return;
        var selection = scans.Where(s => selectedRoots.Contains(s.Root.Id)).Select(s => s with { Files = s.Files.Where(f => !excludedFiles.Contains(f.Path)).ToList() }).Where(s => s.Files.Count > 0).ToList();
        if (selection.Count == 0) { await Inform(L("Nothing selected", "Seçili dosya yok"), L("Scan and select a category containing eligible files.", "Tarama yapın ve uygun dosya içeren bir kategori seçin.")); return; }
        var summary = string.Join("\n", selection.Select(s => $"{L(s.Root.En, s.Root.Tr)} · {Bytes(s.Bytes)} · {s.Files.Count:N0} {L("files", "dosya")}"));
        if (selection.Any(s => s.Truncated) && !await Confirm(L("Partial scan: continue?", "Kısmi tarama: devam edilsin mi?"), L("The 100,000-entry safety limit was reached. Only the reviewed subset will be deleted; totals do not describe the whole location.", "100.000 öğe güvenlik sınırına ulaşıldı. Yalnızca incelenen alt küme silinecek; toplamlar tüm konumu kapsamaz."), L("Review partial deletion", "Kısmi silmeyi incele"))) return;
        if (!await Confirm(L("Review permanent deletion", "Kalıcı silmeyi inceleyin"), summary + "\n\n" + L("These files will be permanently deleted. There is no undo. Applications may rebuild caches. Close other applications before continuing.", "Bu dosyalar kalıcı olarak silinecek. Geri alma yoktur. Uygulamalar önbellekleri yeniden oluşturabilir. Devam etmeden önce diğer uygulamaları kapatın."), L("Delete selected files", "Seçilen dosyaları sil"), destructive: true)) return;
        long bytes = 0; var deleted = 0; var skipped = 0;
        await Work(L("Cleaning selected files…", "Seçilen dosyalar temizleniyor…"), async token =>
        {
            foreach (var scan in selection)
            {
                var result = await Task.Run(() => CleanupService.Delete(scan, token), token);
                bytes += result.Bytes; deleted += result.Deleted; skipped += result.Skipped;
            }
            scans = null;
        }, () => L($"Deleted {deleted:N0} files · {Bytes(bytes)} · Skipped {skipped:N0} items.", $"{deleted:N0} dosya silindi · {Bytes(bytes)} · {skipped:N0} öğe atlandı."));
        scans = null;
        if (route == "cleanup") Navigate(route);
    }
}
