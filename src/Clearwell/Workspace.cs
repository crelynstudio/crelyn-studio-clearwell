using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clearwell.Core;
using Path = System.IO.Path;

namespace Clearwell;

public partial class MainWindow
{
    private SystemSnapshot? system;
    private bool systemReadFailed;
    private string appFilter = "all", appQuery = "", searchQuery = "";
    private bool installedView;
    private readonly HashSet<string> selectedPackages = [];
    private static readonly (string Id, string En, string Tr)[] Routes = [
        ("home", "System overview", "Sistem özeti"), ("cleanup", "Cleanup", "Temizlik"),
        ("performance", "Performance", "Performans"), ("apps", "Applications", "Uygulamalar"),
        ("privacy", "Privacy", "Gizlilik"), ("power", "Power plans", "Güç planları"),
        ("tools", "Windows tools", "Windows araçları"), ("history", "Change history", "Değişiklik geçmişi")
    ];
    private string CategoryLabel(string id) => id switch
    {
        "web" => L("Browsers", "Tarayıcılar"), "media" => L("Media & capture", "Medya ve yakalama"),
        "dev" => L("Development", "Geliştirme"), "communication" => L("Communication", "İletişim"),
        "productivity" => L("Productivity", "Üretkenlik"), "games" => L("Games", "Oyunlar"),
        "utilities" => L("Utilities", "Yardımcı araçlar"), "runtimes" => L("Runtimes", "Çalışma zamanları"), _ => L("All apps", "Tüm uygulamalar")
    };
    private Border Panel(UIElement content, bool accent = false)
    {
        var border = new Border { Child = content, Padding = new Thickness(22), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 0, 18) };
        border.SetResourceReference(Border.BackgroundProperty, accent ? "AccentSoft" : "Surface");
        border.SetResourceReference(Border.BorderBrushProperty, "Line"); return border;
    }
    private Grid Columns(UIElement left, UIElement right, double rightWidth = 0)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = rightWidth == 0 ? new GridLength(0.85, GridUnitType.Star) : new GridLength(rightWidth) });
        grid.Children.Add(left); Grid.SetColumn(right, 2); grid.Children.Add(right); return grid;
    }
    private void PageHeader(string title, string description, UIElement? action = null)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 22) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { Margin = new Thickness(0, 0, 18, 0) };
        var heading = Text(title, 28); heading.FontWeight = FontWeights.SemiBold; labels.Children.Add(heading); labels.Children.Add(Text(description, 13, true)); row.Children.Add(labels);
        if (action != null) { Grid.SetColumn(action, 1); row.Children.Add(action); } Page.Children.Add(row);
    }
    private TextBlock PanelTitle(string title)
    { var text = Text(title, 17); text.FontWeight = FontWeights.SemiBold; text.Margin = new Thickness(0, 0, 0, 18); return text; }
    private UIElement Meter(double percentage, string name)
    {
        var grid = new Grid { Height = 6, Margin = new Thickness(0, 5, 0, 12) };
        var ratio = Math.Clamp(percentage, 0, 100);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(ratio, 0.001), GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(100 - ratio, 0.001), GridUnitType.Star) });
        var track = new Border { CornerRadius = new CornerRadius(3) }; track.SetResourceReference(Border.BackgroundProperty, "Raised"); Grid.SetColumnSpan(track, 2); grid.Children.Add(track);
        var fill = new Border { CornerRadius = new CornerRadius(3), Visibility = ratio == 0 ? Visibility.Hidden : Visibility.Visible }; fill.SetResourceReference(Border.BackgroundProperty, "Accent"); grid.Children.Add(fill);
        Name(grid, name + $" {ratio:0}%"); return grid;
    }
    private void HardwareLine(StackPanel panel, string label, string value, string icon)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var picture = Icon(icon, 20); picture.VerticalAlignment = VerticalAlignment.Top; picture.Margin = new Thickness(0, 3, 14, 0); picture.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "LinkInk"); grid.Children.Add(picture);
        var text = new StackPanel(); text.Children.Add(Text(label, 12, true)); var name = Text(string.IsNullOrWhiteSpace(value) ? L("Not reported by Windows", "Windows tarafından bildirilmedi") : value, 14); name.FontWeight = FontWeights.SemiBold; text.Children.Add(name);
        Grid.SetColumn(text, 1); grid.Children.Add(text); panel.Children.Add(grid);
    }
    private async Task RefreshSystem()
    { await Work(L("Reading device information…", "Cihaz bilgileri okunuyor…"), async token => { system = await Task.Run(SystemSnapshot.Read, token); systemReadFailed = false; }); }
    private UIElement MemoryChart(double percentage)
    {
        var ratio = Math.Clamp(percentage, 0, 100);
        var chart = new Grid { Width = 144, Height = 144, Margin = new Thickness(0, 4, 24, 4) };
        var track = new System.Windows.Shapes.Ellipse { Margin = new Thickness(8), StrokeThickness = 10 };
        track.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Raised"); chart.Children.Add(track);
        if (ratio > 0)
        {
            var angle = Math.Min(ratio, 99.999) * Math.PI / 50 - Math.PI / 2;
            var geometry = new StreamGeometry();
            using (var draw = geometry.Open())
            {
                draw.BeginFigure(new Point(72, 8), false, false);
                draw.ArcTo(new Point(72 + 64 * Math.Cos(angle), 72 + 64 * Math.Sin(angle)), new Size(64, 64), 0, ratio > 50, SweepDirection.Clockwise, true, false);
            }
            geometry.Freeze();
            var arc = new System.Windows.Shapes.Path { Data = geometry, StrokeThickness = 10, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
            arc.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Accent"); chart.Children.Add(arc);
        }
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var value = Text(L($"{ratio:0}%", $"%{ratio:0}"), 30); value.FontWeight = FontWeights.SemiBold; value.HorizontalAlignment = HorizontalAlignment.Center; value.Margin = new Thickness(0);
        labels.Children.Add(value); var caption = Text(L("in use", "kullanımda"), 12, true); caption.HorizontalAlignment = HorizontalAlignment.Center; caption.Margin = new Thickness(0); labels.Children.Add(caption); chart.Children.Add(labels);
        Name(chart, L($"Physical memory: {ratio:0}% used", $"Fiziksel bellek: %{ratio:0} kullanımda"));
        return chart;
    }
    private void Dashboard()
    {
        PageHeader(L("System overview", "Sistem özeti"), system == null ? L("Reading your device…", "Cihazınız okunuyor…") : L($"Your device · Updated {system.CapturedAt.LocalDateTime:t}", $"Cihazınız · Güncelleme {system.CapturedAt.LocalDateTime:t}"), Action(L("Refresh snapshot", "Bilgileri yenile"), () => _ = RefreshSystem()));
        if (systemReadFailed) Notice(L("Device information could not be read. Use Refresh to retry; maintenance pages remain available.", "Cihaz bilgileri okunamadı. Yenile ile tekrar deneyin; bakım sayfaları kullanılabilir."));
        var memory = new StackPanel(); memory.Children.Add(PanelTitle(L("Physical memory", "Fiziksel bellek")));
        var storage = new StackPanel(); storage.Children.Add(PanelTitle(L("Storage", "Depolama")));
        if (system != null)
        {
            if (system.MemoryTotal > 0)
            {
                var memoryRow = new Grid(); memoryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); memoryRow.ColumnDefinitions.Add(new ColumnDefinition());
                memoryRow.Children.Add(MemoryChart(system.MemoryUsedPercent));
                var details = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                details.Children.Add(Text(L("In use", "Kullanımda"), 12, true));
                var used = Text(Bytes((long)(system.MemoryTotal - Math.Min(system.MemoryAvailable, system.MemoryTotal))), 21); used.FontWeight = FontWeights.SemiBold; details.Children.Add(used);
                details.Children.Add(Text(L($"{Bytes((long)system.MemoryAvailable)} available", $"{Bytes((long)system.MemoryAvailable)} kullanılabilir"), 13, true));
                details.Children.Add(Text(L($"{Bytes((long)system.MemoryTotal)} total", $"{Bytes((long)system.MemoryTotal)} toplam"), 12, true));
                Grid.SetColumn(details, 1); memoryRow.Children.Add(details);
                var distribution = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var distributionTitle = Text(L("Memory breakdown", "Bellek dağılımı"), 13); distributionTitle.FontWeight = FontWeights.SemiBold; distribution.Children.Add(distributionTitle);
                distribution.Children.Add(Meter(system.MemoryUsedPercent, L("Physical memory in use", "Kullanılan fiziksel bellek")));
                var legend = new Grid(); legend.ColumnDefinitions.Add(new ColumnDefinition()); legend.ColumnDefinitions.Add(new ColumnDefinition());
                var inUse = Text(L($"In use · {system.MemoryUsedPercent:0}%", $"Kullanımda · %{system.MemoryUsedPercent:0}"), 12, true);
                var available = Text(L($"Available · {100 - system.MemoryUsedPercent:0}%", $"Kullanılabilir · %{100 - system.MemoryUsedPercent:0}"), 12, true); available.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(available, 1); legend.Children.Add(inUse); legend.Children.Add(available); distribution.Children.Add(legend);
                distribution.Children.Add(Text(L("Available memory can be used by apps and Windows. Values update when you refresh the snapshot.", "Kullanılabilir bellek, uygulamalar ve Windows tarafından kullanılabilir. Değerler bilgileri yenilediğinizde güncellenir."), 12, true));
                var memoryLayout = new Grid();
                memoryLayout.ColumnDefinitions.Add(new ColumnDefinition()); memoryLayout.ColumnDefinitions.Add(new ColumnDefinition());
                memoryLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); memoryLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                memoryLayout.Children.Add(memoryRow); memoryLayout.Children.Add(distribution);
                memoryLayout.SizeChanged += (_, _) =>
                {
                    var stacked = memoryLayout.ActualWidth < 650;
                    Grid.SetColumnSpan(memoryRow, stacked ? 2 : 1); Grid.SetColumnSpan(distribution, stacked ? 2 : 1);
                    Grid.SetColumn(distribution, stacked ? 0 : 1); Grid.SetRow(distribution, stacked ? 1 : 0);
                    distribution.Margin = stacked ? new Thickness(0, 18, 0, 0) : new Thickness(24, 0, 0, 0);
                };
                memory.Children.Add(memoryLayout);
            }
            else memory.Children.Add(Text(L("Memory information is unavailable.", "Bellek bilgisi kullanılamıyor."), 13, true));
            foreach (var drive in system.Drives)
            {
                var line = new Grid(); line.ColumnDefinitions.Add(new ColumnDefinition()); line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                line.Children.Add(Text(drive.Name + "  ·  " + drive.Format, 14));
                var percent = Text(L($"{drive.UsedPercent:0}% used", $"%{drive.UsedPercent:0} kullanımda"), 12, true); Grid.SetColumn(percent, 1); line.Children.Add(percent); storage.Children.Add(line);
                storage.Children.Add(Meter(drive.UsedPercent, drive.Name + L(" usage", " kullanımı")));
                storage.Children.Add(Text(L($"{Bytes(drive.Free)} free of {Bytes(drive.Total)}", $"{Bytes(drive.Total)} alanın {Bytes(drive.Free)} kadarı boş"), 12, true));
            }
            if (system.Drives.Count == 0) storage.Children.Add(Text(L("No accessible fixed volumes.", "Erişilebilir sabit birim bulunamadı."), 13, true));
        }
        else { memory.Children.Add(Text(L("Reading physical memory…", "Fiziksel bellek okunuyor…"), 13, true)); storage.Children.Add(Text(L("Reading fixed volumes…", "Sabit birimler okunuyor…"), 13, true)); }
        var memoryPanel = Panel(memory); memoryPanel.VerticalAlignment = VerticalAlignment.Top;
        var storagePanel = Panel(storage); var resources = Columns(memoryPanel, storagePanel);
        resources.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); resources.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resources.SizeChanged += (_, _) =>
        {
            var narrow = resources.ActualWidth < 800;
            Grid.SetColumnSpan(memoryPanel, narrow ? 3 : 1); Grid.SetColumnSpan(storagePanel, narrow ? 3 : 1);
            Grid.SetColumn(storagePanel, narrow ? 0 : 2); Grid.SetRow(storagePanel, narrow ? 1 : 0);
        };
        Page.Children.Add(resources);
        var hardware = new StackPanel(); hardware.Children.Add(PanelTitle(L("Device details", "Cihaz ayrıntıları")));
        var left = new StackPanel(); var right = new StackPanel();
        HardwareLine(left, L("Processor", "İşlemci"), system == null ? "…" : system.Processor, "cpu");
        HardwareLine(left, L("Graphics", "Grafik"), system == null ? "…" : system.Graphics, "display");
        HardwareLine(right, L("Device model", "Cihaz modeli"), system == null ? "…" : system.Model, "device");
        HardwareLine(right, L("Windows", "Windows"), system == null ? "…" : system.Windows, "apps");
        hardware.Children.Add(Columns(left, right));
        hardware.Children.Add(Text(system == null ? "…" : L($"{system.LogicalProcessors} logical processors · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} · Uptime {(int)system.Uptime.TotalDays}d {system.Uptime.Hours}h {system.Uptime.Minutes}m", $"{system.LogicalProcessors} mantıksal işlemci · {System.Runtime.InteropServices.RuntimeInformation.OSArchitecture} · Çalışma süresi {(int)system.Uptime.TotalDays}g {system.Uptime.Hours}sa {system.Uptime.Minutes}dk"), 12, true));
        Page.Children.Add(Panel(hardware));
        Page.Children.Add(PanelTitle(L("Maintenance", "Bakım")));
        Page.Children.Add(Row(L("Temporary files", "Geçici dosyalar"), scans == null ? L("Scan Temp, Prefetch and crash dumps. Review files before deleting.", "Temp, Prefetch ve çökme dökümlerini tarayın. Silmeden önce dosyaları inceleyin.") : L($"Latest scan: {Bytes(scans.Sum(s => s.Bytes))} · {scans.Sum(s => s.Files.Count):N0} eligible files", $"Son tarama: {Bytes(scans.Sum(s => s.Bytes))} · {scans.Sum(s => s.Files.Count):N0} uygun dosya"), Action(L("Scan files", "Dosyaları tara"), () => { Navigate("cleanup"); _ = ScanAsync(); }, true)));
        Page.Children.Add(Row(L("Application library", "Uygulama kütüphanesi"), L($"{WindowsTools.Catalog.Count} applications · Build your install queue", $"{WindowsTools.Catalog.Count} uygulama · Kurulum sıranızı oluşturun"), Action(L("Browse apps", "Uygulamaları gör"), () => Navigate("apps"))));
        Page.Children.Add(Row(L("Windows preferences", "Windows tercihleri"), L($"{SystemSettings.All.Count} explained changes · Original values saved for undo", $"{SystemSettings.All.Count} açıklanmış değişiklik · Geri almak için özgün değerler saklanır"), Action(L("Review settings", "Ayarları incele"), () => Navigate("performance"))));
        Page.Children.Add(Text(L("Snapshot refreshes on request. No background monitoring.", "Anlık görüntü isteğinizle yenilenir. Arka plan izlemesi yoktur."), 12, true));
    }
    private void CatalogWorkspace()
    {
        var settings = Action(L("Windows app settings", "Windows uygulama ayarları"), () => Open("ms-settings:appsfeatures"));
        settings.Style = (Style)FindResource("StudioLink");
        PageHeader(L("Application library", "Uygulama kütüphanesi"), L($"{WindowsTools.Catalog.Count} applications · Choose your tools. Install when you're ready.", $"{WindowsTools.Catalog.Count} uygulama · Araçlarınızı seçin. Hazır olduğunuzda kurun."), settings);
        var tabs = new WrapPanel { Margin = new Thickness(0, 0, 0, 22) };
        Button Tab(string label, bool active, Action callback)
        {
            var button = Action(label, callback); button.Style = (Style)FindResource("Tab"); button.Margin = new Thickness(0, 0, 24, 0);
            if (active) { button.SetResourceReference(BorderBrushProperty, "Accent"); button.SetResourceReference(ForegroundProperty, "LinkInk"); }
            return button;
        }
        tabs.Children.Add(Tab(L("Discover", "Keşfet"), !installedView, () => { installedView = false; Navigate("apps"); }));
        tabs.Children.Add(Tab(L("Installed apps", "Kurulu uygulamalar"), installedView, () =>
        {
            installedView = true;
            _ = Work(L("Reading installed applications…", "Kurulu uygulamalar okunuyor…"), async token => installed = await Task.Run(WindowsTools.InstalledApps, token));
        }));
        Page.Children.Add(tabs);
        if (installedView)
        {
            Page.Children.Add(Action(L("Include current-user Store / MSIX apps", "Geçerli kullanıcı Store / MSIX uygulamalarını ekle"), () => _ = Work(L("Reading Store inventory…", "Store envanteri okunuyor…"), async token =>
            { var desktop = await Task.Run(WindowsTools.InstalledApps, token); var storeApps = await WindowsTools.StoreApps(token); installed = desktop.Concat(storeApps).OrderBy(a => a.Name).ToList(); })));
            Page.Children.Add(Text(L("Desktop applications reported by Windows. Store apps may only appear in Windows Settings.", "Windows tarafından bildirilen masaüstü uygulamaları. Store uygulamaları yalnızca Windows Ayarları'nda görünebilir."), 13, true));
            if (installed == null) Page.Children.Add(Text(busy ? L("Reading applications…", "Uygulamalar okunuyor…") : L("The application list could not be read. Select Installed apps to retry.", "Uygulama listesi okunamadı. Tekrar denemek için Kurulu uygulamalar'ı seçin."), 14, true));
            else
            {
                Section(L($"{installed.Count(a => a.Source == "Desktop")} desktop applications · {installed.Count(a => a.Source != "Desktop")} MSIX / Appx", $"{installed.Count(a => a.Source == "Desktop")} masaüstü uygulaması · {installed.Count(a => a.Source != "Desktop")} MSIX / Appx"));
                if (installed.Count == 0) Page.Children.Add(Text(L("No desktop applications were reported.", "Masaüstü uygulaması bildirilmedi."), 14, true));
                foreach (var app in installed) Page.Children.Add(Row(app.Name, app.Version + " · " + app.Publisher + " · " + (app.Source == "Desktop" ? L("Desktop", "Masaüstü") : app.Source)));
            }
            return;
        }
        var filters = new WrapPanel { Margin = new Thickness(0, 0, 0, 18) };
        foreach (var category in new[] { "all" }.Concat(WindowsTools.Catalog.Select(p => p.Category).Distinct()))
        {
            var chip = Action(CategoryLabel(category), () => { appFilter = category; Navigate("apps"); }); chip.Style = (Style)FindResource("StudioLink"); chip.Padding = new Thickness(12, 7, 12, 7); chip.Margin = new Thickness(0, 0, 6, 6); chip.MinHeight = 34;
            if (category == appFilter) { chip.SetResourceReference(BackgroundProperty, "AccentSoft"); chip.SetResourceReference(ForegroundProperty, "LinkInk"); } filters.Children.Add(chip);
        }
        Page.Children.Add(filters);
        var main = new StackPanel();
        main.Children.Add(Text(L("Find an application · Name or package ID", "Uygulama bul · Ad veya paket kimliği"), 12, true));
        var search = new TextBox { Text = appQuery, MaxLength = 100, Margin = new Thickness(0, 0, 0, 16) }; Name(search, L("Filter applications by name or ID", "Uygulamaları ada veya kimliğe göre filtrele")); main.Children.Add(search);
        var cards = new UniformGrid { Columns = 3 }; cards.SizeChanged += (_, _) => cards.Columns = cards.ActualWidth >= 780 ? 3 : cards.ActualWidth >= 520 ? 2 : 1;
        main.Children.Add(cards);
        var summary = new StackPanel(); summary.Children.Add(PanelTitle(L("Install queue", "Kurulum sırası")));
        var count = Text("", 22); count.FontWeight = FontWeights.SemiBold; summary.Children.Add(count);
        var names = Text("", 13, true); summary.Children.Add(names);
        var install = Action(L("Review & install", "İncele ve kur"), () => _ = InstallSelection(), true); install.HorizontalAlignment = HorizontalAlignment.Stretch; install.Margin = new Thickness(0, 12, 0, 4); summary.Children.Add(install);
        var clear = Action(L("Clear selection", "Seçimi temizle"), () => { selectedPackages.Clear(); Navigate("apps"); }); clear.HorizontalAlignment = HorizontalAlignment.Stretch; clear.Margin = new Thickness(0, 4, 0, 16); summary.Children.Add(clear);
        summary.Children.Add(Text(L("WinGet · Internet required", "WinGet · İnternet gerekir"), 13));
        summary.Children.Add(Text(L("Publisher installers may request administrator access. Installing accepts package and source agreements. App actions have no automatic undo.", "Yayıncı kurucuları yönetici izni isteyebilir. Kurulum, paket ve kaynak sözleşmelerini kabul eder. Uygulama işlemlerinin otomatik geri alması yoktur."), 12, true));
        var compact = new StackPanel(); var compactActions = new WrapPanel();
        var compactCount = Text("", 16); compactCount.VerticalAlignment = VerticalAlignment.Center; compactCount.Margin = new Thickness(0, 0, 18, 0); compactActions.Children.Add(compactCount);
        var compactInstall = Action(L("Review & install", "İncele ve kur"), () => _ = InstallSelection(), true); compactActions.Children.Add(compactInstall);
        var compactClear = Action(L("Clear selection", "Seçimi temizle"), () => { selectedPackages.Clear(); Navigate("apps"); }); compactActions.Children.Add(compactClear);
        compact.Children.Add(compactActions); var compactNames = Text("", 12, true); compact.Children.Add(compactNames);
        compact.Children.Add(Text(L("WinGet · Internet required · App actions have no automatic undo", "WinGet · İnternet gerekir · Uygulama işlemlerinde otomatik geri alma yok"), 12, true));
        var compactPanel = Panel(compact); compactPanel.Padding = new Thickness(16); compactPanel.Visibility = Visibility.Collapsed;
        var winget = WindowsTools.FindWinget() != null;
        void UpdateSelection()
        {
            count.Text = L($"{selectedPackages.Count} selected", $"{selectedPackages.Count} seçili");
            names.Text = selectedPackages.Count == 0 ? L("Choose applications from the library.", "Kütüphaneden uygulamaları seçin.") : string.Join("\n", WindowsTools.Catalog.Where(p => selectedPackages.Contains(p.Id)).Select(p => p.Name));
            install.IsEnabled = winget && !busy && selectedPackages.Count > 0; clear.IsEnabled = !busy && selectedPackages.Count > 0;
            compactCount.Text = L("Install queue · ", "Kurulum sırası · ") + count.Text; compactNames.Text = selectedPackages.Count == 0 ? "" : names.Text.Replace("\n", " · ");
            compactNames.Visibility = selectedPackages.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            compactInstall.IsEnabled = install.IsEnabled; compactClear.IsEnabled = clear.IsEnabled;
        }
        void RenderCatalog()
        {
            cards.Children.Clear();
            foreach (var package in WindowsTools.Catalog.Where(p => (appFilter == "all" || p.Category == appFilter) && (p.Name.Contains(appQuery, StringComparison.OrdinalIgnoreCase) || p.Id.Contains(appQuery, StringComparison.OrdinalIgnoreCase))))
            {
                var body = new StackPanel();
                var checkbox = new CheckBox { Content = package.Name, IsChecked = selectedPackages.Contains(package.Id), IsEnabled = !busy, FontWeight = FontWeights.SemiBold, FontSize = 15, Margin = new Thickness(-7, -3, 0, 12) }; Name(checkbox, L("Select ", "Seç: ") + package.Name);
                body.Children.Add(checkbox); body.Children.Add(Text(L(package.DescriptionEn, package.DescriptionTr), 13, true));
                var id = Text(package.Id, 11, true); id.TextTrimming = TextTrimming.CharacterEllipsis; id.TextWrapping = TextWrapping.NoWrap; body.Children.Add(id);
                body.Children.Add(Text(CategoryLabel(package.Category), 11, true));
                var card = Panel(body); card.Padding = new Thickness(18); card.Margin = new Thickness(0, 0, 12, 12); card.MinHeight = 144; card.BorderThickness = new Thickness(1);
                void UpdateCard() => card.SetResourceReference(Border.BorderBrushProperty, selectedPackages.Contains(package.Id) ? "Accent" : "Line");
                checkbox.Checked += (_, _) => { selectedPackages.Add(package.Id); UpdateCard(); UpdateSelection(); };
                checkbox.Unchecked += (_, _) => { selectedPackages.Remove(package.Id); UpdateCard(); UpdateSelection(); }; UpdateCard(); cards.Children.Add(card);
            }
            if (cards.Children.Count == 0) cards.Children.Add(Text(L("No matching applications. Clear the search or change the category.", "Eşleşen uygulama yok. Aramayı temizleyin veya kategoriyi değiştirin."), 14, true));
        }
        search.TextChanged += (_, _) => { appQuery = search.Text.Trim(); RenderCatalog(); }; RenderCatalog(); UpdateSelection();
        var queuePanel = Panel(summary); var layout = Columns(main, queuePanel, 240);
        layout.SizeChanged += (_, _) => { var narrow = layout.ActualWidth < 900; Grid.SetColumnSpan(main, narrow ? 3 : 1); queuePanel.Visibility = narrow ? Visibility.Collapsed : Visibility.Visible; compactPanel.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed; };
        Page.Children.Add(compactPanel); Page.Children.Add(layout);
        if (!winget) { Notice(L("WinGet is unavailable. Install or update Microsoft App Installer, then reopen Clearwell.", "WinGet kullanılamıyor. Microsoft App Installer'ı kurun veya güncelleyin, ardından Clearwell'i yeniden açın.")); Page.Children.Add(Action(L("Get App Installer", "App Installer'ı edin"), () => Open("https://apps.microsoft.com/detail/9nblggh4nns1"))); }
        Section(L("Manage an exact package", "Tam paket kimliğiyle yönet"));
        Page.Children.Add(Text(L("Use a WinGet ID, such as Mozilla.Firefox. Uninstall may delete application data.", "Mozilla.Firefox gibi bir WinGet kimliği kullanın. Kaldırma uygulama verilerini silebilir."), 13, true));
        var input = new TextBox { Text = packageId, MaxLength = 121 }; Name(input, L("Exact WinGet package ID", "Tam WinGet paket kimliği")); input.TextChanged += (_, _) => packageId = input.Text.Trim(); Page.Children.Add(input);
        var actions = new WrapPanel(); actions.Children.Add(Action(L("Install by ID", "Kimlik ile kur"), () => _ = PackageAction(packageId, true))); actions.Children.Add(Action(L("Uninstall by ID", "Kimlik ile kaldır"), () => _ = PackageAction(packageId, false))); Page.Children.Add(actions);
    }
    private async Task InstallSelection()
    {
        if (busy) return;
        var packages = WindowsTools.Catalog.Where(p => selectedPackages.Contains(p.Id)).ToArray();
        if (packages.Length == 0) return;
        if (!await Confirm(L("Review installation queue", "Kurulum sırasını inceleyin"), string.Join("\n", packages.Select(p => p.Name + " · " + p.Id)) + "\n\n" + L("WinGet downloads and runs publisher installers, one at a time. Continuing accepts each package and source agreement. There is no automatic undo. The queue stops at the first failure; completed installs remain.", "WinGet yayıncı kurucularını indirir ve sırayla çalıştırır. Devam etmek her paketin ve kaynağın sözleşmesini kabul eder. Otomatik geri alma yoktur. İlk hatada sıra durur; tamamlanan kurulumlar korunur."), L("Install selected apps", "Seçilen uygulamaları kur"))) return;
        var results = new List<string>();
        await Work(L("Installing selected apps…", "Seçilen uygulamalar kuruluyor…"), async token =>
        {
            foreach (var package in packages)
            {
                BusyText.Text = L("Installing ", "Kuruluyor: ") + package.Name;
                var result = await WindowsTools.PackageAction(package.Id, true, token);
                results.Add(package.Name + " · " + PackageStatus(result));
                if (result.Code != 0 || result.Verified != true || token.IsCancellationRequested) { results.Add(result.Output); break; }
                selectedPackages.Remove(package.Id);
            }
        });
        if (results.Count > 0) await Inform(L("Installation results", "Kurulum sonuçları"), string.Join("\n\n", results));
    }
    private string PackageStatus(CommandResult result) => result.Interrupted
        ? L("Stop requested; state unknown. Check the installer and installed apps before retrying.", "Durdurma istendi; durum bilinmiyor. Yeniden denemeden önce kurucuyu ve kurulu uygulamaları kontrol edin.")
        : result.Code == 0 && result.Verified == true ? L("Completed; package inventory verified", "Tamamlandı; paket envanteri doğrulandı")
        : result.Code != 0 ? L("WinGet reported an error; check its output", "WinGet hata bildirdi; çıktısını kontrol edin")
        : L("Command completed; package state could not be verified. The queue was stopped.", "Komut tamamlandı; paket durumu doğrulanamadı. Sıra durduruldu.");
    private void SearchKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { SearchClick(sender, e); e.Handled = true; } }
    private void SearchClick(object sender, RoutedEventArgs e)
    { if (busy || dialogAnswer != null) return; searchQuery = GlobalSearch.Text.Trim(); Navigate("search"); }
    private void SearchResults()
    {
        PageHeader(L("Search workspace", "Çalışma alanında ara"), L("Pages, Windows preferences and curated applications.", "Sayfalar, Windows tercihleri ve seçilmiş uygulamalar."));
        if (searchQuery.Length == 0) { Notice(L("Enter a name in the search field above, then press Enter.", "Yukarıdaki arama alanına bir ad yazıp Enter'a basın.")); return; }
        bool Matches(string text) => text.Contains(searchQuery, StringComparison.OrdinalIgnoreCase);
        var count = 0;
        foreach (var item in Routes.Where(r => Matches(r.En) || Matches(r.Tr))) { Page.Children.Add(Row(L(item.En, item.Tr), L("Workspace page", "Çalışma alanı sayfası"), Action(L("Open", "Aç"), () => Navigate(item.Id)))); count++; }
        foreach (var setting in SystemSettings.All.Where(s => Matches(s.En) || Matches(s.Tr) || Matches(s.DescriptionEn) || Matches(s.DescriptionTr))) { Page.Children.Add(Row(L(setting.En, setting.Tr), L(setting.DescriptionEn, setting.DescriptionTr), Action(L("Review", "İncele"), () => Navigate(setting.Group)))); count++; }
        foreach (var package in WindowsTools.Catalog.Where(p => Matches(p.Name) || Matches(p.Id))) { Page.Children.Add(Row(package.Name, package.Id, Action(L("Show in library", "Kütüphanede göster"), () => { installedView = false; appFilter = "all"; appQuery = package.Name; Navigate("apps"); }))); count++; }
        foreach (var tool in WindowsPanels.Where(p => Matches(p.En) || Matches(p.Tr) || Matches(p.DescriptionEn) || Matches(p.DescriptionTr))) { Page.Children.Add(Row(L(tool.En, tool.Tr), L(tool.DescriptionEn, tool.DescriptionTr), Action(L("Open tool", "Aracı aç"), () => LaunchTool(tool.Target)))); count++; }
        if (count == 0) Notice(L("No matches. Try an application name, Cleanup, Privacy or Power.", "Eşleşme yok. Bir uygulama adı, Temizlik, Gizlilik veya Güç arayın."));
    }
    private static readonly (string En, string Tr, string Target, string DescriptionEn, string DescriptionTr)[] WindowsPanels = [
            ("System information", "Sistem bilgileri", "msinfo32.exe", "System summary and hardware resources", "Sistem özeti ve donanım kaynakları"),
            ("Task Manager", "Görev Yöneticisi", "Taskmgr.exe", "Processes, performance and startup", "İşlemler, performans ve başlangıç"),
            ("Device Manager", "Aygıt Yöneticisi", "devmgmt.msc", "Hardware devices and drivers", "Donanım aygıtları ve sürücüler"),
            ("Disk Management", "Disk Yönetimi", "diskmgmt.msc", "Volumes and partitions", "Birimler ve bölümler"),
            ("Control Panel", "Denetim Masası", "control.exe", "Classic Windows configuration", "Klasik Windows yapılandırması"),
            ("Windows Update", "Windows Update", "ms-settings:windowsupdate", "Update status and Windows controls", "Güncelleme durumu ve Windows denetimleri"),
            ("Windows Security", "Windows Güvenliği", "windowsdefender:", "Protection and security settings", "Koruma ve güvenlik ayarları"),
            ("Recovery", "Kurtarma", "ms-settings:recovery", "Recovery options and reset controls", "Kurtarma seçenekleri ve sıfırlama denetimleri"),
            ("Network settings", "Ağ ayarları", "ms-settings:network-status", "Connections and adapter settings", "Bağlantılar ve bağdaştırıcı ayarları"),
            ("Storage settings", "Depolama ayarları", "ms-settings:storagesense", "Storage usage and Windows cleanup", "Depolama kullanımı ve Windows temizliği")
        ];
    private void LaunchTool(string target) => Open(target.Contains(':') ? target : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), target));
    private void Tools()
    {
        PageHeader(L("Windows tools", "Windows araçları"), L("Direct access to built-in Windows panels. Clearwell opens the tool; you control the changes.", "Yerleşik Windows panellerine doğrudan erişim. Clearwell aracı açar; değişiklikleri siz yönetirsiniz."));
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        for (var i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var i = 0; i < WindowsPanels.Length; i++)
        {
            var tool = WindowsPanels[i];
            var open = Action(L("Open", "Aç"), () => LaunchTool(tool.Target)); open.Padding = new Thickness(12, 8, 12, 8); open.MinHeight = 34;
            var row = Row(L(tool.En, tool.Tr), L(tool.DescriptionEn, tool.DescriptionTr), open); Grid.SetRow(row, i % 5); Grid.SetColumn(row, i < 5 ? 0 : 2); grid.Children.Add(row);
        }
        Page.Children.Add(Panel(grid));
    }
}
