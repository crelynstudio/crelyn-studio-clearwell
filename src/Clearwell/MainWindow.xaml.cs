using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Principal;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Clearwell.Core;
using Microsoft.Win32;
using DrawingPath = System.Windows.Shapes.Path;
using Path = System.IO.Path;

namespace Clearwell;

public partial class MainWindow : Window
{
    private readonly LocalStore store;
    private string route = "home";
    private bool busy;
    private CancellationTokenSource? operation;
    private TaskCompletionSource<bool>? dialogAnswer;
    private IInputElement? previousFocus;
    private List<CleanupScan>? scans;
    private readonly HashSet<string> selectedRoots = ["user-temp", "system-temp"];
    private readonly HashSet<string> selectedSettings = [];
    private List<InstalledApp>? installed;
    private List<StartupEntry>? startup;
    private List<PowerPlan>? powerPlans;
    private string packageId = "";
    private bool Tr => store.Preferences.Language == "tr";
    private string L(string en, string tr) => Tr ? tr : en;
    private static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public MainWindow(LocalStore localStore)
    {
        store = localStore;
        ApplyTheme();
        InitializeComponent();
        AutomationProperties.SetLiveSetting(Status, AutomationLiveSetting.Polite);
        SizeChanged += (_, _) =>
        {
            PreviewLabel.Visibility = ActualWidth < 1100 ? Visibility.Collapsed : Visibility.Visible;
            DialogCard.MaxWidth = Math.Max(200, ActualWidth - 40);
            DialogCard.MaxHeight = Math.Min(660, Math.Max(200, ActualHeight - 40));
            if (DialogMessage.Parent is System.Windows.Controls.ScrollViewer messages) messages.MaxHeight = Math.Min(440, Math.Max(60, ActualHeight - 280));
        };
        StateChanged += (_, _) => UpdateCaptionIcon();
        SystemParameters.StaticPropertyChanged += SystemAppearanceChanged;
        Closed += (_, _) => SystemParameters.StaticPropertyChanged -= SystemAppearanceChanged;
        RefreshChrome();
        Navigate("home");
        Closing += OnClosing;
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && dialogAnswer != null) { CompleteDialog(false); e.Handled = true; } };
        Loaded += async (_, _) =>
        {
            FitToMonitor(true);
            try { system = await Task.Run(SystemSnapshot.Read); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException) { systemReadFailed = true; }
            if (route == "home") Navigate("home");
            if (App.CaptureFolder != null) await VerifyUi(App.CaptureFolder);
        };
    }

    private void RefreshChrome()
    {
        Language = System.Windows.Markup.XmlLanguage.GetLanguage(Tr ? "tr-TR" : "en-US");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(Tr ? "tr-TR" : "en-US");
        PreviewLabel.Text = L("PREVIEW · ", "ÖNİZLEME · ") + VersionInfo.Current;
        BrandSubtitle.Text = L("Windows workspace", "Windows çalışma alanı");
        SearchButton.Content = L("Search", "Ara");
        SearchHint.Text = L("Search apps, settings and tools", "Uygulama, ayar ve araç ara");
        Name(GlobalSearch, L("Search pages, settings and applications", "Sayfa, ayar ve uygulama ara"));
        GlobalSearch.ToolTip = L("Search pages, settings and apps · Press Enter", "Sayfa, ayar ve uygulama ara · Enter'a basın");
        GlobalSearch.IsEnabled = !busy && dialogAnswer == null; SearchButton.IsEnabled = !busy && dialogAnswer == null;
        LocalLabel.Text = L("Local-first · No telemetry", "Yerel çalışma · Telemetri yok");
        Status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        Status.Text = IsAdmin ? L("Administrator access", "Yönetici erişimi") : L("Standard access · Changes are always your choice", "Standart erişim · Değişikliklere siz karar verirsiniz");
        CancelButton.Content = L("Cancel", "İptal");
        ThemeButton.Content = NavContent("theme", store.Preferences.Dark ? L("Light appearance", "Açık görünüm") : L("Dark appearance", "Koyu görünüm"));
        LanguageButton.Content = NavContent("language", Tr ? "English" : "Türkçe");
        AboutButton.Content = NavContent("about", "Crelyn Studio");
        MinimizeButton.Content = Icon("minimize", 12); UpdateCaptionIcon(); CloseButton.Content = Icon("close", 12);
        Name(MinimizeButton, L("Minimize", "Küçült")); Name(MaximizeButton, L("Maximize or restore", "Büyüt veya geri yükle")); Name(CloseButton, L("Close", "Kapat"));
        Name(ThemeButton, L("Switch appearance", "Görünümü değiştir")); Name(LanguageButton, L("Switch to Turkish", "İngilizceye geç"));
        Navigation.Children.Clear();
        foreach (var item in Routes)
        {
            var target = item.Id;
            if (target == "home" || target == "tools") { var label = Text(target == "home" ? L("Workspace", "Çalışma alanı") : L("System", "Sistem"), 11, true); label.Margin = new Thickness(12, target == "home" ? 6 : 16, 0, 6); Navigation.Children.Add(label); }
            var button = new Button { Style = (Style)FindResource("Nav"), Content = NavContent(target, L(item.En, item.Tr)), HorizontalAlignment = HorizontalAlignment.Stretch };
            if (route == target) { button.SetResourceReference(BackgroundProperty, "AccentSoft"); button.SetResourceReference(ForegroundProperty, "LinkInk"); }
            Name(button, L(item.En, item.Tr));
            button.Click += (_, _) => { if (!busy) Navigate(target); };
            Navigation.Children.Add(button);
        }
    }
    private new static void Name(DependencyObject element, string text) => AutomationProperties.SetName(element, text);
    private new static DrawingPath Icon(string id, double size = 18)
    {
        var geometry = id switch
        {
            "home" => "M3,10 L12,3 21,10 M5,9 L5,21 10,21 10,14 14,14 14,21 19,21 19,9",
            "cleanup" => "M5,6 L19,6 M9,6 L9,3 15,3 15,6 M7,6 L8,21 16,21 17,6 M10,10 L10,17 M14,10 L14,17",
            "performance" => "M3,16 A9,9 0 1 1 21,16 M12,15 L17,8 M6,18 L18,18",
            "apps" => "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M14,14 L21,14 21,21 14,21 Z",
            "privacy" => "M12,2 L21,6 20,14 Q18,20 12,23 Q6,20 4,14 L3,6 Z M8,12 L11,15 16,9",
            "power" => "M13,2 L5,14 11,14 10,23 20,10 14,10 Z",
            "history" => "M3,10 A9,9 0 1 1 5,18 M3,4 L3,10 9,10 M12,7 L12,13 16,15",
            "cpu" => "M6,6 L18,6 18,18 6,18 Z M9,9 L15,9 15,15 9,15 Z M9,2 L9,6 M15,2 L15,6 M9,18 L9,22 M15,18 L15,22 M2,9 L6,9 M2,15 L6,15 M18,9 L22,9 M18,15 L22,15",
            "display" => "M3,4 L21,4 21,17 3,17 Z M12,17 L12,21 M7,21 L17,21",
            "device" => "M6,2 L18,2 18,22 6,22 Z M9,6 L15,6 M9,10 L15,10 M11,18 L13,18",
            "tools" => "M14,3 A6,6 0 0 0 7,10 L2,17 6,21 13,14 A6,6 0 0 0 21,7 L17,11 13,7 Z",
            "theme" => "M20,15 A9,9 0 1 1 9,3 A8,8 0 0 0 20,15",
            "language" => "M3,5 L16,5 M9,2 L9,5 M5,5 Q6,13 14,16 M14,5 Q12,12 4,17 M14,22 L18,12 22,22 M16,18 L20,18",
            "about" => "M12,3 A9,9 0 1 1 11.99,3 M12,10 L12,17 M12,7 L12,7.1",
            "mail" => "M3,5 L21,5 21,19 3,19 Z M3,5 L12,12 21,5",
            "design" => "M4,16 L16,4 20,8 8,20 3,21 Z M13,7 L17,11",
            "minimize" => "M3,12 L21,12", "maximize" => "M4,4 L20,4 20,20 4,20 Z", "restore" => "M8,4 L20,4 20,16 M4,8 L16,8 16,20 4,20 Z", "close" => "M5,5 L19,19 M19,5 L5,19",
            _ => "M5,12 L19,12 M13,6 L19,12 13,18"
        };
        var icon = new DrawingPath { Data = Geometry.Parse(geometry), Width = size, Height = size, Stretch = Stretch.Uniform, StrokeThickness = 1.6, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
        icon.SetResourceReference(Shape.StrokeProperty, "Ink");
        return icon;
    }
    private static Grid NavContent(string icon, string label)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var picture = Icon(icon, 17); picture.HorizontalAlignment = HorizontalAlignment.Left; picture.VerticalAlignment = VerticalAlignment.Center;
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, FontSize = 13, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(text, 1); row.Children.Add(picture); row.Children.Add(text);
        return row;
    }
    private void ApplyTheme()
    {
        var dark = store.Preferences.Dark;
        foreach (var pair in new[] {
            ("Canvas", "#20232A", "#F3F5F8"), ("Chrome", "#191D24", "#E9EDF2"), ("Surface", "#2B3039", "#FFFFFF"), ("Raised", "#363D49", "#E3E9F0"),
            ("Ink", "#F5F5F5", "#20232A"), ("Muted", "#AAB4C2", "#526175"), ("Line", "#3D4552", "#D3DAE4"), ("ControlLine", "#697788", "#758397"),
            ("Accent", "#60CDFF", "#0078D4"), ("AccentInk", "#191D24", "#FFFFFF"), ("AccentSoft", "#213D4D", "#DFEFFB"), ("LinkInk", "#60CDFF", "#0069BA"),
            ("AccentHover", "#8ADBFF", "#006CBD"), ("AccentPressed", "#44B9EC", "#005DA6"),
            ("Good", "#6CCB8F", "#247341"), ("GoodSoft", "#233D30", "#E2F3E7"),
            ("Warning", "#F7C46C", "#7A480D"), ("WarningSoft", "#403421", "#FFF1D9"), ("Danger", "#FF6469", "#B82032"), ("DangerInk", "#191D24", "#FFFFFF") })
            Application.Current.Resources[pair.Item1] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark ? pair.Item2 : pair.Item3));
        if (SystemParameters.HighContrast)
        {
            foreach (var key in new[] { "Canvas", "Chrome", "Surface", "Raised", "AccentSoft", "GoodSoft", "WarningSoft" }) Application.Current.Resources[key] = SystemColors.WindowBrush;
            foreach (var key in new[] { "Ink", "Muted", "Line", "ControlLine", "LinkInk", "Good", "Warning" }) Application.Current.Resources[key] = SystemColors.WindowTextBrush;
            foreach (var key in new[] { "Accent", "AccentHover", "AccentPressed", "Danger" }) Application.Current.Resources[key] = SystemColors.HighlightBrush;
            foreach (var key in new[] { "AccentInk", "DangerInk" }) Application.Current.Resources[key] = SystemColors.HighlightTextBrush;
        }
    }
    private void SystemAppearanceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.Invoke(() => { ApplyTheme(); if (!busy && dialogAnswer == null) Navigate(route); });
    }
    private void Navigate(string target)
    {
        route = target; Page.Children.Clear(); PageScroll.ScrollToTop(); RefreshChrome();
        if (store.Damaged) Notice(L("Recovery data could not be read. Settings and power changes are blocked. Preserve your local files and contact support.", "Kurtarma verileri okunamadı. Ayar ve güç değişiklikleri engellendi. Yerel dosyalarınızı koruyun ve destekle iletişime geçin."));
        if (store.PreferencesDamaged) Notice(L("Preferences were unreadable; defaults are in use. Recovery history is independent. Saving a preference preserves the damaged file first.", "Tercihler okunamadı; varsayılanlar kullanılıyor. Kurtarma geçmişi bağımsızdır. Tercih kaydetmek önce bozuk dosyayı korur."), warning: true);
        switch (target)
        {
            case "home": Home(); break;
            case "cleanup": Cleanup(); break;
            case "cleanup-review": ReviewCleanup(); break;
            case "performance": SettingsPage("performance"); break;
            case "privacy": SettingsPage("privacy"); break;
            case "apps": Apps(); break;
            case "power": Power(); break;
            case "history": History(); break;
            case "about": About(); break;
            case "tools": Tools(); break;
            case "search": SearchResults(); break;
        }
    }
    private TextBlock Text(string text, double size = 14, bool muted = false)
    {
        var block = new TextBlock { Text = text, FontSize = size, Margin = new Thickness(0, 0, 0, 8) };
        if (muted) block.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        return block;
    }
    private void Heading(string title, string description)
    {
        PageHeader(title, description);
    }
    private void Section(string text)
    { var block = Text(text, 18); block.FontWeight = FontWeights.SemiBold; block.Margin = new Thickness(0, 26, 0, 14); Page.Children.Add(block); }
    private void Notice(string message, bool warning = false, bool success = false)
    {
        var notice = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(16), Margin = new Thickness(0, 8, 0, 16), Child = Text(message, 13) };
        notice.SetResourceReference(Border.BackgroundProperty, warning ? "WarningSoft" : success ? "GoodSoft" : "AccentSoft");
        if (notice.Child is TextBlock label) label.SetResourceReference(TextBlock.ForegroundProperty, warning ? "Warning" : success ? "Good" : "Ink");
        Page.Children.Add(notice);
    }
    private Button Action(string label, Action callback, bool primary = false)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 10, 6), IsEnabled = !busy };
        if (primary) button.Style = (Style)FindResource("Primary");
        Name(button, label); button.Click += (_, _) => callback(); return button;
    }
    private Grid Row(string title, string detail, UIElement? control = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var labels = new StackPanel { Margin = new Thickness(0, 16, 22, 16) };
        var name = Text(title, 15); name.FontWeight = FontWeights.SemiBold; labels.Children.Add(name); labels.Children.Add(Text(detail, 13, true)); grid.Children.Add(labels);
        if (control != null)
        {
            if (control is Button button) Name(button, AutomationProperties.GetName(button) + " — " + title);
            Grid.SetColumn(control, 1); grid.Children.Add(control);
        }
        var line = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom }; line.SetResourceReference(Border.BackgroundProperty, "Line"); Grid.SetColumnSpan(line, 2); grid.Children.Add(line);
        return grid;
    }
    private void Home() => Dashboard();
    private static string Bytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var number = (double)value; var unit = 0;
        while (number >= 1024 && unit < units.Length - 1) { number /= 1024; unit++; }
        return $"{number:0.#} {units[unit]}";
    }
    private void SettingsPage(string group)
    {
        Heading(group == "performance" ? L("Performance preferences", "Performans tercihleri") : L("Privacy preferences", "Gizlilik tercihleri"), L("Compare the current state, select changes and review them before applying.", "Mevcut durumu karşılaştırın, değişiklikleri seçin ve uygulamadan önce inceleyin."));
        var toolbar = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
        toolbar.Children.Add(Action(L("Select available", "Uygun olanları seç"), () => { foreach (var s in SystemSettings.All.Where(s => s.Group == group)) { try { if (SystemSettings.Read(s) != s.Desired.ToString()) selectedSettings.Add(s.Id); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { } } Navigate(route); }));
        toolbar.Children.Add(Action(L("Clear selection", "Seçimi temizle"), () => { foreach (var s in SystemSettings.All.Where(s => s.Group == group)) selectedSettings.Remove(s.Id); Navigate(route); })); Page.Children.Add(toolbar);
        var preferences = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var review = Action("", () => _ = ApplySettings(group), true);
        void UpdateSettingsSelection() => review.Content = L($"Review changes ({SystemSettings.All.Count(s => s.Group == group && selectedSettings.Contains(s.Id))})", $"Değişiklikleri incele ({SystemSettings.All.Count(s => s.Group == group && selectedSettings.Contains(s.Id))})");
        foreach (var setting in SystemSettings.All.Where(s => s.Group == group))
        {
            string? value = null; var readable = true;
            try { value = SystemSettings.Read(setting); } catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { readable = false; }
            var applied = value == setting.Desired.ToString();
            var check = new CheckBox { Content = applied ? L("Set", "Ayarlı") : L("Select", "Seç"), IsChecked = selectedSettings.Contains(setting.Id), IsEnabled = !applied && readable && !busy && !store.Damaged, VerticalAlignment = VerticalAlignment.Center };
            Name(check, L(setting.En, setting.Tr)); check.Checked += (_, _) => { selectedSettings.Add(setting.Id); UpdateSettingsSelection(); }; check.Unchecked += (_, _) => { selectedSettings.Remove(setting.Id); UpdateSettingsSelection(); };
            var state = !readable ? L("Cannot read current preference.", "Mevcut tercih okunamıyor.") : DescribeSetting(setting, value);
            var body = new StackPanel(); var title = Text(L(setting.En, setting.Tr), 17); title.FontWeight = FontWeights.SemiBold; body.Children.Add(title);
            body.Children.Add(Text(L(setting.DescriptionEn, setting.DescriptionTr), 13, true));
            var status = new Border { Padding = new Thickness(10), CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 10, 0, 10), Child = Text(state, 12) }; status.SetResourceReference(Border.BackgroundProperty, "Raised"); body.Children.Add(status); body.Children.Add(check);
            var panel = Panel(body); panel.Margin = new Thickness(0, 0, 16, 16); preferences.Children.Add(panel);
        }
        Page.Children.Add(preferences); UpdateSettingsSelection(); Name(review, L("Review selected Windows changes", "Seçilen Windows değişikliklerini incele")); Page.Children.Add(review);
        Page.Children.Add(Text(L("These are saved Windows preferences. System policies may override them; runtime behavior is not independently verified.", "Bunlar kayıtlı Windows tercihleridir. Sistem ilkeleri bunları geçersiz kılabilir; çalışma anındaki davranış ayrıca doğrulanmaz."), 12, true));
        if (group == "performance")
        {
            Section(L("Startup applications · Partial view", "Başlangıç uygulamaları · Kısmi görünüm"));
            Page.Children.Add(Text(L("Review Run keys and startup-folder entries. Use Windows Settings to enable or disable apps; scheduled tasks and services are not listed here.", "Run kayıtlarını ve başlangıç klasörü öğelerini inceleyin. Açıp kapatmak için Windows Ayarları'nı kullanın; zamanlanmış görevler ve hizmetler burada listelenmez."), 13, true));
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(Action(L("Read startup entries", "Başlangıç kayıtlarını oku"), () => _ = Work(L("Reading startup entries…", "Başlangıç kayıtları okunuyor…"), async token => { startup = await Task.Run(WindowsTools.StartupEntries, token); })));
            actions.Children.Add(Action(L("Manage startup in Windows", "Windows'ta başlangıcı yönet"), () => Open("ms-settings:startupapps"))); Page.Children.Add(actions);
            if (startup != null)
            {
                if (startup.Count == 0) Page.Children.Add(Text(L("No entries found in these locations.", "Bu konumlarda kayıt bulunamadı."), 13, true));
                foreach (var entry in startup) Page.Children.Add(Row(entry.Name, entry.Command));
            }
            Page.Children.Add(Action(L("Windows visual effects", "Windows görsel efektleri"), () => Open(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "SystemPropertiesPerformance.exe"))));
        }
        else Page.Children.Add(Action(L("More privacy settings in Windows", "Windows'ta diğer gizlilik ayarları"), () => Open("ms-settings:privacy-general")));
    }
    private string DescribeSetting(SettingDefinition setting, string? value)
    {
        if (value == null) return L("Windows default; effective state not determined.", "Windows varsayılanı; etkin durum belirlenmedi.");
        var state = setting.Id == "visual-effects" ? value switch
        {
            "0" => L("Windows chooses automatically", "Windows otomatik seçer"),
            "1" => L("Best appearance", "En iyi görünüm"),
            "2" => L("Best performance", "En iyi performans"),
            "3" => L("Custom effects", "Özel efektler"),
            _ => L($"Unknown advanced value ({value})", $"Bilinmeyen gelişmiş değer ({value})")
        } : value switch
        {
            "0" => L("Off", "Kapalı"), "1" => L("On", "Açık"),
            _ => L($"Unknown advanced value ({value})", $"Bilinmeyen gelişmiş değer ({value})")
        };
        return L("Saved preference: ", "Kayıtlı tercih: ") + state;
    }
    private async Task ApplySettings(string group)
    {
        if (busy || store.Damaged) return;
        var selected = SystemSettings.All.Where(s => s.Group == group && selectedSettings.Contains(s.Id)).ToList();
        if (selected.Count == 0) { await Inform(L("Choose a change", "Bir değişiklik seçin"), L("Nothing is selected. Review the descriptions and select the changes you want.", "Hiçbir şey seçilmedi. Açıklamaları inceleyip istediğiniz değişiklikleri seçin.")); return; }
        if (!await Confirm(L("Review Windows changes", "Windows değişikliklerini inceleyin"), string.Join("\n\n", selected.Select(s => L(s.En, s.Tr) + "\n" + L(s.DescriptionEn, s.DescriptionTr))) + "\n\n" + L("Original values are saved locally. Undo is available in Change history. Sign-out or restart may be needed.", "Özgün değerler yerelde saklanır. Değişiklik geçmişinden geri alabilirsiniz. Oturumu kapatma veya yeniden başlatma gerekebilir."), L("Apply changes", "Değişiklikleri uygula"))) return;
        await Work(L("Applying selected settings…", "Seçilen ayarlar uygulanıyor…"), token =>
        {
            foreach (var setting in selected) { token.ThrowIfCancellationRequested(); SystemSettings.Apply(setting, store); selectedSettings.Remove(setting.Id); }
            return Task.CompletedTask;
        });
    }
    private void Apps() => CatalogWorkspace();
    private async Task PackageAction(string id, bool install)
    {
        if (busy) return;
        if (!WindowsTools.ValidPackageId(id)) { await Inform(L("Check the package ID", "Paket kimliğini kontrol edin"), L("Enter an exact ID using letters, numbers, dots, dashes, plus or underscore. Spaces and command options are not accepted.", "Harf, rakam, nokta, tire, artı veya alt çizgi içeren tam kimliği girin. Boşluk ve komut seçenekleri kabul edilmez.")); return; }
        if (!await Confirm(install ? L("Install application?", "Uygulama kurulsun mu?") : L("Uninstall application?", "Uygulama kaldırılsın mı?"), id + "\n\n" + (install ? L("WinGet will download and run the publisher's installer from the winget source. You accept its package and source agreements. This action cannot be undone in Change history.", "WinGet, winget kaynağından yayıncının kurucusunu indirip çalıştırır. Paket ve kaynak sözleşmelerini kabul edersiniz. Bu işlem Değişiklik geçmişinden geri alınamaz.") : L("WinGet will uninstall this exact package ID. Application data may be removed. There is no automatic undo.", "WinGet bu tam paket kimliğini kaldıracak. Uygulama verileri silinebilir. Otomatik geri alma yoktur.")), install ? L("Install", "Kur") : L("Uninstall", "Kaldır"), destructive: !install)) return;
        CommandResult? result = null;
        if (!install)
        {
            CommandResult? target = null;
            await Work(L("Reading exact installed target…", "Tam kurulu hedef okunuyor…"), async token => target = await WindowsTools.QueryPackage(id, token));
            if (target?.Code != 0) return;
            if (!await Confirm(L("Confirm installed target", "Kurulu hedefi onaylayın"), target.Output, L("Uninstall this target", "Bu hedefi kaldır"), true)) return;
        }
        await Work(L("WinGet is working… Cancel requests a stop; detached installers may continue.", "WinGet çalışıyor… İptal durdurmayı ister; ayrılan kurucular devam edebilir."), async token => { result = await WindowsTools.PackageAction(id, install, token); });
        if (result != null) await Inform(L("Package result", "Paket sonucu"), PackageStatus(result) + L($"\nExit code: {result.Code}\n\nWindows / WinGet output:\n", $"\nÇıkış kodu: {result.Code}\n\nWindows / WinGet çıktısı:\n") + result.Output);
    }
    private void Power()
    {
        Heading(L("Power plans", "Güç planları"), L("Compare and switch the plans available on this device.", "Bu cihazdaki kullanılabilir planları karşılaştırın ve değiştirin."));
        Notice(L("Higher performance can increase heat, fan noise and battery use. Modern Standby devices may expose only Balanced. Clearwell does not invent unsupported plans or promise a speed boost.", "Yüksek performans ısıyı, fan sesini ve pil tüketimini artırabilir. Modern Standby cihazlarında yalnızca Dengeli görünebilir. Clearwell desteklenmeyen planlar üretmez veya hız artışı vaat etmez."));
        Page.Children.Add(Action(L("Read available plans", "Kullanılabilir planları oku"), () => _ = Work(L("Reading power plans…", "Güç planları okunuyor…"), async token => powerPlans = await WindowsTools.PowerPlans(token)), true));
        if (powerPlans != null)
        {
            if (powerPlans.Count == 0) Notice(L("Windows returned no power plans. Use Windows power settings.", "Windows güç planı döndürmedi. Windows güç ayarlarını kullanın."));
            foreach (var plan in powerPlans)
            {
                var action = Action(plan.Active ? L("Active", "Etkin") : L("Use this plan", "Bu planı kullan"), () => _ = SetPower(plan)); action.IsEnabled = !plan.Active && !busy && !store.Damaged;
                Page.Children.Add(Row(plan.Name, plan.Id, action));
            }
        }
        Page.Children.Add(Action(L("Windows power settings", "Windows güç ayarları"), () => Open("ms-settings:powersleep")));
    }
    private async Task SetPower(PowerPlan plan)
    {
        if (!await Confirm(L("Switch power plan?", "Güç planı değiştirilsin mi?"), plan.Name + "\n\n" + L("The previous plan is saved for undo. Battery use and heat can change.", "Önceki plan geri almak için saklanır. Pil tüketimi ve ısı değişebilir."), L("Switch plan", "Planı değiştir"))) return;
        await Work(L("Changing power plan…", "Güç planı değiştiriliyor…"), async token => { await WindowsTools.SetPower(plan, store, token); powerPlans = await WindowsTools.PowerPlans(token); });
    }
    private void About()
    {
        var introduction = new StackPanel { Margin = new Thickness(0, 0, 28, 0) };
        var image = new Image { Source = new BitmapImage(new Uri("pack://application:,,,/Assets/" + (store.Preferences.Dark ? "CRELYN-WHITE.png" : "CRELYN-DARK.png"))), Width = 250, Height = 70, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 28) };
        Name(image, "Crelyn Studio"); introduction.Children.Add(image);
        var title = Text(L("Design and technology,\nworking together.", "Tasarım ve teknoloji,\nbir arada."), 36);
        title.FontWeight = FontWeights.SemiBold; title.LineHeight = 44; introduction.Children.Add(title);
        var description = Text(L("Crelyn Studio brings brand identity, web design, e-commerce and custom software together. A design and software studio connecting visual identity with digital experiences.", "Crelyn Studio; marka kimliği, web tasarımı, e-ticaret ve özel yazılımı bir araya getirir. Görsel kimlikle dijital deneyimleri buluşturan bir tasarım ve yazılım stüdyosudur."), 15, true);
        description.LineHeight = 25; description.MaxWidth = 580; description.HorizontalAlignment = HorizontalAlignment.Left; description.Margin = new Thickness(0, 12, 0, 20); introduction.Children.Add(description);
        var credit = Text(L("Clearwell is developed by Crelyn Studio.", "Clearwell, Crelyn Studio tarafından geliştirildi."), 13);
        credit.SetResourceReference(TextBlock.ForegroundProperty, "LinkInk"); introduction.Children.Add(credit);

        Button StudioLink(string label, string address, string icon)
        {
            var content = new Grid();
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
            content.ColumnDefinitions.Add(new ColumnDefinition());
            var symbol = Icon(icon, 18); symbol.HorizontalAlignment = HorizontalAlignment.Left; content.Children.Add(symbol);
            var name = Text(label, 13); name.Margin = new Thickness(0); name.VerticalAlignment = VerticalAlignment.Center; name.FontWeight = FontWeights.SemiBold; Grid.SetColumn(name, 1); content.Children.Add(name);
            var button = Action(label, () => Open(address)); button.Style = (Style)FindResource("StudioLink"); button.Content = content; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.Margin = new Thickness(0, 2, 0, 2);
            Name(button, "Crelyn Studio — " + label); return button;
        }
        var contact = new StackPanel();
        contact.Children.Add(PanelTitle(L("Find Crelyn Studio", "Crelyn Studio bağlantıları")));
        var website = Action("crelynstudio.com", () => Open("https://crelynstudio.com"), true);
        website.HorizontalAlignment = HorizontalAlignment.Stretch; website.Margin = new Thickness(0, 0, 0, 10); contact.Children.Add(website);
        contact.Children.Add(StudioLink(L("About the studio", "Stüdyo hakkında"), "https://crelynstudio.com/hakkimizda", "about"));
        contact.Children.Add(StudioLink(L("Portfolio", "Portfolyo"), "https://crelynstudio.com/portfolyo", "design"));
        contact.Children.Add(StudioLink("info@crelynstudio.com", "mailto:info@crelynstudio.com", "mail"));
        var divider = new Border { Height = 1, Margin = new Thickness(10, 16, 10, 18) }; divider.SetResourceReference(Border.BackgroundProperty, "Line"); contact.Children.Add(divider);
        var socialHeading = Text(L("Social platforms", "Sosyal medya"), 13);
        socialHeading.FontWeight = FontWeights.SemiBold; socialHeading.Margin = new Thickness(10, 0, 0, 10); contact.Children.Add(socialHeading);
        var social = new System.Windows.Controls.Primitives.UniformGrid { Columns = 5 };
        foreach (var link in new[] { ("GitHub", "https://github.com/crelynstudio", "github"), ("Behance", "https://www.behance.net/crelynstudio", "behance"), ("Instagram", "https://www.instagram.com/crelynstudio/", "instagram"), ("X", "https://x.com/crelynstudio", "x-twitter"), ("Threads", "https://www.threads.com/@crelynstudio", "threads") })
        {
            var logo = new DrawingPath { Data = (Geometry)FindResource("Social-" + link.Item3), Width = 23, Height = 23, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
            logo.SetResourceReference(Shape.FillProperty, "Ink");
            var button = Action(link.Item1, () => Open(link.Item2)); button.Style = (Style)FindResource("StudioLink"); button.Content = logo; button.HorizontalAlignment = HorizontalAlignment.Stretch; button.MinHeight = 48; button.Margin = new Thickness(0, 2, 0, 2);
            button.ToolTip = "Crelyn Studio — " + link.Item1; ToolTipService.SetShowsToolTipOnKeyboardFocus(button, true); Name(button, "Crelyn Studio — " + link.Item1); social.Children.Add(button);
        }
        contact.Children.Add(social);
        var contactPanel = Panel(contact);
        var top = Columns(introduction, contactPanel, 350); top.Margin = new Thickness(0, 8, 0, 24);
        top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        top.SizeChanged += (_, _) =>
        {
            var narrow = top.ActualWidth < 850;
            top.ColumnDefinitions[2].Width = narrow ? new GridLength(0) : new GridLength(350);
            Grid.SetColumn(contactPanel, narrow ? 0 : 2); Grid.SetRow(contactPanel, narrow ? 1 : 0);
            introduction.Margin = new Thickness(0, 0, narrow ? 0 : 28, narrow ? 24 : 0);
        };
        Page.Children.Add(top);

        var disciplines = new StackPanel();
        disciplines.Children.Add(PanelTitle(L("What Crelyn Studio creates", "Crelyn Studio neler üretir?")));
        disciplines.Children.Add(Row(L("Brand & visual identity", "Marka ve görsel kimlik"), L("Logo, color, typography and visual communication.", "Logo, renk, tipografi ve görsel iletişim.")));
        disciplines.Children.Add(Row(L("Web & e-commerce", "Web ve e-ticaret"), L("Websites and online shopping experiences.", "Web siteleri ve çevrim içi alışveriş deneyimleri.")));
        disciplines.Children.Add(Row(L("Software & digital tools", "Yazılım ve dijital araçlar"), L("Custom applications and tools for everyday workflows.", "İhtiyaca özel uygulamalar ve günlük işleri kolaylaştıran araçlar.")));
        Page.Children.Add(Panel(disciplines));

        var application = new StackPanel();
        var appHeading = new Grid(); appHeading.ColumnDefinitions.Add(new ColumnDefinition()); appHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        appHeading.Children.Add(PanelTitle(L("About Clearwell", "Clearwell hakkında")));
        var version = Text(VersionInfo.Current, 12, true); Grid.SetColumn(version, 1); appHeading.Children.Add(version); application.Children.Add(appHeading);
        application.Children.Add(Text(L("Open-source Windows maintenance. Preferences and recovery records stay on your PC; app installation uses WinGet and third-party sources.", "Açık kaynaklı Windows bakım aracı. Tercihler ve kurtarma kayıtları bilgisayarınızda kalır; uygulama kurulumu WinGet ve üçüncü taraf kaynakları kullanır."), 13, true));
        var motion = new CheckBox { Content = L("Subtle interface animations", "Hafif arayüz animasyonları"), IsChecked = store.Preferences.Motion, Margin = new Thickness(0, 8, 0, 6) };
        motion.Checked += (_, _) => { store.Preferences.Motion = true; SavePreferences(); }; motion.Unchecked += (_, _) => { store.Preferences.Motion = false; SavePreferences(); }; application.Children.Add(motion);
        application.Children.Add(Text(L("Windows' reduced-motion preference is respected. No animations run continuously.", "Windows'un azaltılmış hareket tercihi dikkate alınır. Sürekli çalışan animasyon yoktur."), 12, true));
        Page.Children.Add(Panel(application));
        Page.Children.Add(Text(L("MIT-licensed source code. Crelyn Studio names and logos are reserved brand assets; the software license does not grant trademark rights. Not affiliated with Microsoft or Chris Titus Tech.", "Kaynak kod MIT lisanslıdır. Crelyn Studio adı ve logoları marka varlıklarıdır; yazılım lisansı marka hakkı vermez. Microsoft veya Chris Titus Tech ile bağlantısı yoktur."), 12, true));
    }

    private async Task Work(string label, Func<CancellationToken, Task> action, Func<string>? result = null, bool cancellable = true)
    {
        if (busy) return;
        busy = true; operation = new CancellationTokenSource(); BusyText.Text = label; BusyBanner.Visibility = Visibility.Visible; CancelButton.Visibility = cancellable ? Visibility.Visible : Visibility.Collapsed;
        var activityArea = route;
        Navigate(route); string? failure = null; var cancelled = false; int? errorCode = null;
        try { await action(operation.Token); Status.Text = result?.Invoke() ?? (activityArea == "apps" ? L("Operation ended. Inspect the package or inventory result.", "İşlem sona erdi. Paket veya envanter sonucunu inceleyin.") : L("Completed. Review the results.", "Tamamlandı. Sonuçları inceleyin.")); }
        catch (OperationCanceledException) { cancelled = true; Status.Text = L("Stopped. Completed actions remain applied; scan again or check Change history.", "Durduruldu. Tamamlanan işlemler korunur; yeniden tarayın veya Değişiklik geçmişini kontrol edin."); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or Win32Exception or System.Security.SecurityException or ArgumentException or TimeoutException)
        { errorCode = e.HResult; failure = L("The operation could not finish. Check permissions, availability and Change history before retrying. Completed changes are not automatically rolled back.\n\nWindows details:\n", "İşlem tamamlanamadı. Yeniden denemeden önce izinleri, kullanılabilirliği ve Değişiklik geçmişini kontrol edin. Tamamlanan değişiklikler otomatik geri alınmaz.\n\nWindows ayrıntıları:\n") + e.Message; }
        finally
        {
            busy = false; operation.Dispose(); operation = null; BusyBanner.Visibility = Visibility.Collapsed;
            var status = Status.Text; Navigate(route); Status.Text = status;
            Status.SetResourceReference(TextBlock.ForegroundProperty, failure != null ? "Danger" : cancelled ? "Warning" : "Good");
            try { ActivityLog.Add(store.Folder, activityArea, failure != null ? "failed" : cancelled ? "stopped" : activityArea == "apps" ? "inspect" : "completed", errorCode); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException) { Status.Text += L(" Activity could not be saved.", " Etkinlik kaydedilemedi."); }
        }
        if (failure != null) await Inform(L("Action needs attention", "İşlemi inceleyin"), failure);
        else if (cancelled || result != null) await Inform(L("Operation result", "İşlem sonucu"), Status.Text);
    }
    private Task<bool> Confirm(string title, string message, string accept, bool destructive = false) => ShowDialogPanel(title, message, accept, true, destructive);
    private async Task Inform(string title, string message) => _ = await ShowDialogPanel(title, message, L("Close", "Kapat"), false);
    private Task<bool> ShowDialogPanel(string title, string message, string accept, bool cancel, bool destructive = false)
    {
        if (dialogAnswer != null) return Task.FromResult(false);
        previousFocus = Keyboard.FocusedElement;
        dialogAnswer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        DialogTitle.Text = title; DialogMessage.Text = message; DialogAccept.Content = accept; DialogCancel.Content = L("Cancel", "İptal"); DialogCancel.Visibility = cancel ? Visibility.Visible : Visibility.Collapsed;
        DialogAccept.SetResourceReference(BackgroundProperty, destructive ? "Danger" : "Accent");
        DialogAccept.SetResourceReference(ForegroundProperty, destructive ? "DangerInk" : "AccentInk");
        DialogLayer.Visibility = Visibility.Visible;
        KeyboardNavigation.SetTabNavigation(DialogCard, KeyboardNavigationMode.Cycle);
        PageScroll.IsEnabled = false; Navigation.IsEnabled = false; ThemeButton.IsEnabled = false; LanguageButton.IsEnabled = false; AboutButton.IsEnabled = false; GlobalSearch.IsEnabled = false; SearchButton.IsEnabled = false;
        DialogCancel.IsDefault = false; DialogAccept.IsDefault = !cancel;
        if (cancel) DialogCancel.Focus(); else DialogAccept.Focus();
        if (store.Preferences.Motion && SystemParameters.ClientAreaAnimation)
            DialogCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0.8, 1, TimeSpan.FromMilliseconds(140)) { EasingFunction = new ExponentialEase { EasingMode = EasingMode.EaseOut } });
        return dialogAnswer.Task;
    }
    private void CompleteDialog(bool result)
    {
        var answer = dialogAnswer; dialogAnswer = null; DialogLayer.Visibility = Visibility.Collapsed;
        PageScroll.IsEnabled = true; Navigation.IsEnabled = true; ThemeButton.IsEnabled = true; LanguageButton.IsEnabled = true; AboutButton.IsEnabled = true; GlobalSearch.IsEnabled = !busy; SearchButton.IsEnabled = !busy;
        previousFocus?.Focus(); answer?.TrySetResult(result);
    }
    private void DialogCancelClick(object sender, RoutedEventArgs e) => CompleteDialog(false);
    private void DialogAcceptClick(object sender, RoutedEventArgs e) => CompleteDialog(true);
    private void CancelClick(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void ThemeClick(object sender, RoutedEventArgs e) { if (busy) return; store.Preferences.Dark = !store.Preferences.Dark; ApplyTheme(); SavePreferences(); Navigate(route); }
    private void LanguageClick(object sender, RoutedEventArgs e) { if (busy) return; store.Preferences.Language = Tr ? "en" : "tr"; SavePreferences(); Navigate(route); }
    private void AboutClick(object sender, RoutedEventArgs e) { if (!busy) Navigate("about"); }
    private void SavePreferences()
    {
        try { store.SavePreferences(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status.Text = L("Preferences could not be saved. Check folder permissions.", "Tercihler kaydedilemedi. Klasör izinlerini kontrol edin."); }
    }
    private async Task RestartAdmin()
    {
        if (!await Confirm(L("Restart with administrator access?", "Yönetici erişimiyle yeniden başlatılsın mı?"), L("Windows will ask for permission. No cleanup starts automatically. If you use another administrator account, settings and history belong to that account.", "Windows izin isteyecek. Temizlik otomatik başlamaz. Başka yönetici hesabı kullanırsanız ayarlar ve geçmiş o hesaba ait olur."), L("Restart", "Yeniden başlat"))) return;
        try { Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--wait-for-parent " + Environment.ProcessId) { UseShellExecute = true, Verb = "runas" }); Close(); }
        catch (Win32Exception) { Status.Text = L("Administrator restart was cancelled or unavailable.", "Yönetici olarak başlatma iptal edildi veya kullanılamıyor."); }
    }
    private void Open(string target)
    {
        try { Process.Start(new ProcessStartInfo(target) { UseShellExecute = true }); }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException) { _ = Inform(L("Could not open", "Açılamadı"), L("Windows could not open this link or settings page. Try through Windows Settings.", "Windows bu bağlantıyı veya ayar sayfasını açamadı. Windows Ayarları üzerinden deneyin.")); }
    }
    private void UpdateCaptionIcon() => MaximizeButton.Content = Icon(WindowState == WindowState.Maximized ? "restore" : "maximize", 12);
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; operation?.Cancel(); Status.Text = L("Stop requested. Detached installers may continue; review their windows before retrying. Close again when this operation stops.", "Durdurma istendi. Ayrılan kurucular devam edebilir; yeniden denemeden önce pencerelerini kontrol edin. İşlem durunca yeniden kapatın."); }
    }

    private async Task VerifyUi(string folder)
    {
        Directory.CreateDirectory(folder);
        try
        {
            VerifyWindowPlacement(Path.Combine(folder, "startup-bounds.json"));
            VerifyCaption();
            WindowState = WindowState.Maximized;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            VerifyWindowPlacement(Path.Combine(folder, "maximized-bounds.json")); Capture(Path.Combine(folder, "window-maximized.png"));
            VerifyCaption();
            WindowState = WindowState.Normal;
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            FitToMonitor(false);
            await Task.Delay(5000);
            using (var process = Process.GetCurrentProcess())
            {
                var cpuBefore = process.TotalProcessorTime;
                var timer = Stopwatch.StartNew();
                await Task.Delay(2000);
                process.Refresh();
                File.WriteAllText(Path.Combine(folder, "idle-metrics.json"), System.Text.Json.JsonSerializer.Serialize(new
                {
                    WorkingSetMiB = Math.Round(process.WorkingSet64 / 1048576.0, 1),
                    PrivateMemoryMiB = Math.Round(process.PrivateMemorySize64 / 1048576.0, 1),
                    IdleCpuMilliseconds = Math.Round((process.TotalProcessorTime - cpuBefore).TotalMilliseconds, 1),
                    SampleMilliseconds = timer.ElapsedMilliseconds
                }));
            }
            foreach (var dark in new[] { true, false })
            foreach (var language in new[] { "en", "tr" })
            {
                store.Preferences.Dark = dark; store.Preferences.Language = language; store.Preferences.Motion = false; ApplyTheme();
                foreach (var page in new[] { "home", "cleanup", "performance", "apps", "privacy", "power", "tools", "search", "history", "about" })
                {
                    Navigate(page); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    Capture(Path.Combine(folder, $"{page}-{language}-{(dark ? "dark" : "light")}.png"));
                }
            }
            Navigate("about"); PageScroll.ScrollToBottom(); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Capture(Path.Combine(folder, "about-details-tr-light.png"));
            store.Preferences.Dark = true; store.Preferences.Language = "en"; ApplyTheme();
            Width = 1000; Height = 700;
            foreach (var compact in new[] { "home", "cleanup", "apps", "performance", "tools", "about" }) { Navigate(compact); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture(Path.Combine(folder, compact + "-compact.png")); }
            Width = 900; Height = 560;
            foreach (var compact in new[] { "home", "cleanup", "apps", "performance", "tools", "about" }) { Navigate(compact); await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture(Path.Combine(folder, compact + "-small.png")); }
            var confirm = Confirm("Review permanent deletion", "UI verification only. No files will be deleted.", "Delete selected files", destructive: true);
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture(Path.Combine(folder, "confirmation.png"));
            CompleteDialog(false); if (await confirm) throw new InvalidOperationException("Cancel failed.");
            Width = 1380; Height = 900; Navigate("cleanup"); await ScanAsync();
            if (scans == null || scans.Count < 4) throw new InvalidOperationException("Scan UI failed.");
            Capture(Path.Combine(folder, "cleanup-scanned-en-dark.png"));
            var liveScans = scans;
            var fixtureFolder = Path.Combine(folder, "review-fixtures"); Directory.CreateDirectory(fixtureFolder);
            foreach (var file in new[] { "keep-important.tmp", "review-example.tmp" })
            { var path = Path.Combine(fixtureFolder, file); File.WriteAllText(path, "Example fixture only"); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-40)); }
            var fixtureRoot = new CleanupRoot("user-temp", fixtureFolder, "Example temporary files", "Örnek geçici dosyalar", "Example only", "Yalnızca örnek", false);
            scans = [CleanupService.Scan(fixtureRoot, CancellationToken.None) with { Truncated = true }];
            foreach (var language in new[] { "en", "tr" })
            foreach (var dark in new[] { true, false })
            {
                store.Preferences.Language = language; store.Preferences.Dark = dark; ApplyTheme(); Navigate("cleanup-review");
                var table = FindVisual<DataGrid>(Page).Single();
                if (table.Items.Count != 2 || !table.EnableRowVirtualization) throw new InvalidOperationException("File review data or virtualization failed.");
                ((ReviewFile)table.Items[0]).Selected = false;
                if (excludedFiles.Count != 1) throw new InvalidOperationException("File exclusion failed.");
                await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                Capture(Path.Combine(folder, $"cleanup-review-{language}-{(dark ? "dark" : "light")}.png"));
                excludedFiles.Clear();
            }
            store.Preferences.Language = "en"; store.Preferences.Dark = true; ApplyTheme();
            var partialReview = CleanAsync();
            if (!DialogTitle.Text.Contains("Partial")) throw new InvalidOperationException("Partial scan confirmation failed.");
            CompleteDialog(false); await partialReview;
            scans = liveScans; excludedFiles.Clear();
            foreach (var category in WindowsTools.Catalog.Select(p => p.Category).Distinct())
            {
                appFilter = category; appQuery = ""; Navigate("apps");
                var expected = WindowsTools.Catalog.Where(p => p.Category == category).Select(p => p.Name).ToHashSet();
                if (!expected.SetEquals(FindVisual<CheckBox>(Page).Select(c => c.Content as string ?? ""))) throw new InvalidOperationException("Catalog category failed: " + category);
                if (category == "runtimes") Capture(Path.Combine(folder, "apps-runtimes-en-dark.png"));
            }
            Navigate("apps"); appFilter = "dev"; appQuery = "Git.Git"; Navigate("apps");
            if (FindVisual<CheckBox>(Page).Count() != 1) throw new InvalidOperationException("Catalog filter failed.");
            var selection = FindVisual<CheckBox>(Page).Single(); selection.IsChecked = true;
            if (!selectedPackages.Contains("Git.Git")) throw new InvalidOperationException("Catalog selection failed.");
            appFilter = "communication"; appQuery = "Discord"; Navigate("apps"); FindVisual<CheckBox>(Page).Single().IsChecked = true;
            if (!selectedPackages.Contains("Git.Git") || !selectedPackages.Contains("Discord.Discord")) throw new InvalidOperationException("Cross-category selection failed.");
            selectedPackages.Remove("Discord.Discord");
            appFilter = "runtimes"; appQuery = "Microsoft.VCRedist.2015+.x64"; Navigate("apps"); FindVisual<CheckBox>(Page).Single().IsChecked = true;
            if (!selectedPackages.Contains("Git.Git") || !selectedPackages.Contains("Microsoft.VCRedist.2015+.x64")) throw new InvalidOperationException("Runtime package search and cross-category selection failed.");
            selectedPackages.Remove("Microsoft.VCRedist.2015+.x64");
            appFilter = "communication"; appQuery = "WhatsApp"; Navigate("apps");
            FindVisual<CheckBox>(Page).Single().IsChecked = true;
            if (!selectedPackages.Contains("9NKSQGP7F2NH")) throw new InvalidOperationException("WhatsApp selection failed.");
            Capture(Path.Combine(folder, "apps-whatsapp-en-dark.png"));
            selectedPackages.Remove("9NKSQGP7F2NH");
            installed = await Task.Run(WindowsTools.InstalledApps); installedView = true; Navigate("apps"); UpdateLayout();
            if (FindVisual<CheckBox>(Page).Any() || !FindVisual<TextBlock>(Page).Any(t => t.Text.Contains(Tr ? "masaüstü uygulaması" : "desktop applications"))) throw new InvalidOperationException("Installed-app tab failed.");
            var storeInventory = await WindowsTools.StoreApps(CancellationToken.None);
            if (storeInventory.Any(a => a.Source != "MSIX / Appx" || string.IsNullOrWhiteSpace(a.Name))) throw new InvalidOperationException("Store inventory failed.");
            installed = installed!.Concat(storeInventory).ToList(); Navigate("apps"); UpdateLayout();
            installedView = false;
            foreach (var percentage in new[] { -1.0, 0, 50, 100, 101 })
            {
                var chart = (Grid)MemoryChart(percentage);
                var arcs = chart.Children.OfType<System.Windows.Shapes.Path>().ToArray();
                if (arcs.Length != (percentage <= 0 ? 0 : 1) || arcs.Any(a => a.Data.IsEmpty() || !double.IsFinite(a.Data.Bounds.Width) || !double.IsFinite(a.Data.Bounds.Height))) throw new InvalidOperationException("Memory chart boundary failed.");
                if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(chart))) throw new InvalidOperationException("Memory chart accessibility label failed.");
            }
            appFilter = "all"; appQuery = ""; selectedPackages.Add("Microsoft.VisualStudioCode"); Navigate("apps");
            Capture(Path.Combine(folder, "apps-selected-en-dark.png")); selectedPackages.Clear();
            GlobalSearch.Text = "Firefox"; SearchClick(this, new RoutedEventArgs());
            if (route != "search" || !FindVisual<TextBlock>(Page).Any(t => t.Text == "Firefox")) throw new InvalidOperationException("Workspace search failed.");
            Capture(Path.Combine(folder, "search-en-dark.png")); GlobalSearch.Text = ""; searchQuery = "";
            foreach (var term in new[] { "Task Manager", "Aygıt Yöneticisi" })
            {
                GlobalSearch.Text = term; SearchClick(this, new RoutedEventArgs());
                if (!FindVisual<Button>(Page).Any(b => b.Content is string text && text == "Open tool")) throw new InvalidOperationException("Tool search failed: " + term);
                if (term == "Task Manager") Capture(Path.Combine(folder, "search-tool-en-dark.png"));
            }
            GlobalSearch.Text = ""; searchQuery = "";
            Navigate("power"); powerPlans = await WindowsTools.PowerPlans(CancellationToken.None); Navigate("power");
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle); Capture(Path.Combine(folder, "power-loaded-en-dark.png"));
            ThemeClick(this, new RoutedEventArgs()); if (store.Preferences.Dark) throw new InvalidOperationException("Theme switch failed.");
            LanguageClick(this, new RoutedEventArgs()); if (!Tr) throw new InvalidOperationException("Language switch failed.");
            var reload = new LocalStore(store.Folder); if (reload.Preferences.Dark || reload.Preferences.Language != "tr") throw new InvalidOperationException("Preferences persistence failed.");
            File.WriteAllText(Path.Combine(folder, "result.txt"), "PASS: native startup/maximized bounds, 40 page/theme/language renders, 12 compact renders, 4 bilingual themed file-review renders with exclusions and partial-confirmation cancellation, about details, read-only live scan, power/hardware reads, chart boundaries, desktop and current-user Store inventory, app category/search/queue selection, workspace search, theme/language persistence. No system mutations executed.");
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(folder, "result.txt"), "FAIL: " + e); }
        finally { Application.Current.Shutdown(); }
    }
    private void Capture(string path)
    {
        UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path); encoder.Save(file);
    }
    private static IEnumerable<T> FindVisual<T>(DependencyObject root) where T : DependencyObject
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var nested in FindVisual<T>(child)) yield return nested; } }
}
