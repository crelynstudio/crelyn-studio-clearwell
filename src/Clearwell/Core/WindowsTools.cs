using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Clearwell.Core;

public record CommandResult(int Code, string Output);
public record PowerPlan(string Id, string Name, bool Active);
public record InstalledApp(string Name, string Version, string Publisher);
public record StartupEntry(string Name, string Command, string Location);
public record AppPackage(string Id, string Name, string Category, string DescriptionEn, string DescriptionTr, string Source = "winget");

public static partial class WindowsTools
{
    public static IReadOnlyList<AppPackage> Catalog { get; } = [
        new("Brave.Brave", "Brave", "web", "Privacy-focused web browser", "Gizlilik odaklı web tarayıcısı"),
        new("Mozilla.Firefox", "Firefox", "web", "Web browser", "Web tarayıcısı"),
        new("Google.Chrome", "Google Chrome", "web", "Google web browser", "Google web tarayıcısı"),
        new("LibreWolf.LibreWolf", "LibreWolf", "web", "Firefox-based web browser", "Firefox tabanlı web tarayıcısı"),
        new("Microsoft.Edge", "Microsoft Edge", "web", "Microsoft web browser", "Microsoft web tarayıcısı"),
        new("Opera.Opera", "Opera", "web", "Web browser with workspace tools", "Çalışma alanı araçları sunan tarayıcı"),
        new("Opera.OperaGX", "Opera GX", "web", "Gaming-oriented web browser", "Oyun odaklı web tarayıcısı"),
        new("TorProject.TorBrowser", "Tor Browser", "web", "Browser for the Tor network", "Tor ağı için tarayıcı"),
        new("Vivaldi.Vivaldi", "Vivaldi", "web", "Customizable web browser", "Özelleştirilebilir web tarayıcısı"),
        new("Waterfox.Waterfox", "Waterfox", "web", "Firefox-based desktop browser", "Firefox tabanlı masaüstü tarayıcı"),
        new("Discord.Discord", "Discord", "communication", "Voice, video and community chat", "Sesli, görüntülü ve topluluk sohbeti"),
        new("Element.Element", "Element", "communication", "Matrix messaging client", "Matrix mesajlaşma istemcisi"),
        new("Microsoft.Teams", "Microsoft Teams", "communication", "Team chat and online meetings", "Ekip sohbeti ve çevrim içi toplantılar"),
        new("OpenWhisperSystems.Signal", "Signal", "communication", "Private messaging and calls", "Özel mesajlaşma ve aramalar"),
        new("SlackTechnologies.Slack", "Slack", "communication", "Team messaging and collaboration", "Ekip mesajlaşması ve iş birliği"),
        new("TeamSpeakSystems.TeamSpeakClient", "TeamSpeak", "communication", "Voice chat client", "Sesli sohbet istemcisi"),
        new("Telegram.TelegramDesktop", "Telegram Desktop", "communication", "Desktop messaging client", "Masaüstü mesajlaşma istemcisi"),
        new("Rakuten.Viber", "Viber", "communication", "Messaging and voice calls", "Mesajlaşma ve sesli aramalar"),
        new("9NKSQGP7F2NH", "WhatsApp", "communication", "Messaging and calls · Microsoft Store · Requires a WhatsApp account", "Mesajlaşma ve aramalar · Microsoft Store · WhatsApp hesabı gerekir", "msstore"),
        new("Zoom.Zoom", "Zoom", "communication", "Video meetings and calls", "Görüntülü toplantılar ve aramalar"),
        new("DBBrowserForSQLite.DBBrowserForSQLite", "DB Browser for SQLite", "dev", "Browse and edit SQLite databases", "SQLite veritabanlarını inceleme ve düzenleme"),
        new("DBeaver.DBeaver.Community", "DBeaver Community", "dev", "Database management and SQL tools", "Veritabanı yönetimi ve SQL araçları"),
        new("Docker.DockerDesktop", "Docker Desktop", "dev", "Container development environment", "Konteyner geliştirme ortamı"),
        new("Git.Git", "Git", "dev", "Version control", "Sürüm kontrolü"),
        new("GitHub.GitHubDesktop", "GitHub Desktop", "dev", "Desktop Git repository client", "Masaüstü Git depo istemcisi"),
        new("HeidiSQL.HeidiSQL", "HeidiSQL", "dev", "Database administration client", "Veritabanı yönetim istemcisi"),
        new("JetBrains.Toolbox", "JetBrains Toolbox", "dev", "JetBrains IDE installation manager", "JetBrains geliştirme ortamı yöneticisi"),
        new("OpenJS.NodeJS.LTS", "Node.js LTS", "dev", "JavaScript runtime, LTS channel", "JavaScript çalışma zamanı, LTS kanalı"),
        new("Notepad++.Notepad++", "Notepad++", "dev", "Text and source editor", "Metin ve kaynak kod düzenleyici"),
        new("Postman.Postman", "Postman", "dev", "API development and testing", "API geliştirme ve test araçları"),
        new("Microsoft.PowerShell", "PowerShell", "dev", "Cross-platform command shell", "Platformlar arası komut kabuğu"),
        new("Python.Python.3.13", "Python 3.13", "dev", "Python 3.13 language runtime", "Python 3.13 dil çalışma zamanı"),
        new("SublimeHQ.SublimeText.4", "Sublime Text", "dev", "Text and code editor; paid license available", "Metin ve kod düzenleyici; ücretli lisans sunulur"),
        new("Microsoft.VisualStudioCode", "Visual Studio Code", "dev", "Code editor", "Kod düzenleyici"),
        new("Microsoft.VisualStudio.2022.Community", "Visual Studio Community 2022", "dev", "Development environment; license terms apply", "Geliştirme ortamı; lisans koşulları geçerlidir"),
        new("Microsoft.WindowsTerminal", "Windows Terminal", "dev", "Terminal for command-line tools", "Komut satırı araçları için terminal"),
        new("WinSCP.WinSCP", "WinSCP", "dev", "SFTP and SCP file transfer", "SFTP ve SCP dosya aktarımı"),
        new("Audacity.Audacity", "Audacity", "media", "Audio recording and editing", "Ses kaydı ve düzenleme"),
        new("BlenderFoundation.Blender", "Blender", "media", "3D modeling and animation", "3B modelleme ve animasyon"),
        new("Figma.Figma", "Figma", "media", "Collaborative interface design; account required", "Ortak arayüz tasarımı; hesap gerekir"),
        new("PeterPawlowski.foobar2000", "foobar2000", "media", "Desktop audio player", "Masaüstü ses oynatıcı"),
        new("GIMP.GIMP", "GIMP", "media", "Image editing", "Görsel düzenleme"),
        new("HandBrake.HandBrake", "HandBrake", "media", "Video conversion and encoding", "Video dönüştürme ve kodlama"),
        new("Inkscape.Inkscape", "Inkscape", "media", "Vector graphics editor", "Vektör grafik düzenleyici"),
        new("IrfanSkiljan.IrfanView", "IrfanView", "media", "Image viewer and converter", "Görsel görüntüleyici ve dönüştürücü"),
        new("KDE.Kdenlive", "Kdenlive", "media", "Nonlinear video editing", "Doğrusal olmayan video düzenleme"),
        new("KDE.Krita", "Krita", "media", "Digital painting and illustration", "Dijital boyama ve illüstrasyon"),
        new("clsid2.mpc-hc", "MPC-HC", "media", "Media Player Classic Home Cinema", "Klasik masaüstü medya oynatıcı"),
        new("OBSProject.OBSStudio", "OBS Studio", "media", "Video recording and live streaming", "Video kaydı ve canlı yayın"),
        new("dotPDN.PaintDotNet", "paint.net", "media", "Image and photo editing", "Görsel ve fotoğraf düzenleme"),
        new("ShareX.ShareX", "ShareX", "media", "Screen capture and recording", "Ekran yakalama ve kayıt"),
        new("Meltytech.Shotcut", "Shotcut", "media", "Video editor", "Video düzenleyici"),
        new("Spotify.Spotify", "Spotify", "media", "Music and podcast player", "Müzik ve podcast oynatıcı"),
        new("VideoLAN.VLC", "VLC", "media", "Audio and video player", "Ses ve video oynatıcı"),
        new("XnSoft.XnViewMP", "XnView MP", "media", "Image browsing and conversion; license terms apply", "Görsel inceleme ve dönüştürme; lisans koşulları geçerlidir"),
        new("Adobe.Acrobat.Reader.64-bit", "Adobe Acrobat Reader", "productivity", "PDF viewing, printing and annotations", "PDF görüntüleme, yazdırma ve açıklamalar"),
        new("Anki.Anki", "Anki", "productivity", "Spaced-repetition flashcards", "Aralıklı tekrar bilgi kartları"),
        new("calibre.calibre", "calibre", "productivity", "E-book library and conversion", "E-kitap kütüphanesi ve dönüştürme"),
        new("Dropbox.Dropbox", "Dropbox", "productivity", "Cloud file synchronization; account required", "Bulut dosya eşitleme; hesap gerekir"),
        new("Google.GoogleDrive", "Google Drive", "productivity", "Cloud file synchronization; Google account required", "Bulut dosya eşitleme; Google hesabı gerekir"),
        new("Joplin.Joplin", "Joplin", "productivity", "Notes and to-do lists", "Notlar ve yapılacaklar listeleri"),
        new("TheDocumentFoundation.LibreOffice", "LibreOffice", "productivity", "Documents, spreadsheets and presentations", "Belgeler, tablolar ve sunumlar"),
        new("Microsoft.Office", "Microsoft 365 / Office", "productivity", "Office installer; account and paid license may be required", "Office kurucusu; hesap ve ücretli lisans gerekebilir"),
        new("Notion.Notion", "Notion", "productivity", "Notes and shared workspaces", "Notlar ve ortak çalışma alanları"),
        new("Obsidian.Obsidian", "Obsidian", "productivity", "Local Markdown notes", "Yerel Markdown notları"),
        new("Microsoft.OneDrive", "OneDrive", "productivity", "Cloud file synchronization; Microsoft account required", "Bulut dosya eşitleme; Microsoft hesabı gerekir"),
        new("ONLYOFFICE.DesktopEditors", "ONLYOFFICE Desktop Editors", "productivity", "Office document editors", "Ofis belgesi düzenleyicileri"),
        new("SumatraPDF.SumatraPDF", "SumatraPDF", "productivity", "PDF and document reader", "PDF ve belge okuyucu"),
        new("Mozilla.Thunderbird", "Thunderbird", "productivity", "Email and calendar client", "E-posta ve takvim istemcisi"),
        new("7zip.7zip", "7-Zip", "utilities", "File compression and archives", "Dosya sıkıştırma ve arşivler"),
        new("AnyDesk.AnyDesk", "AnyDesk", "utilities", "Remote desktop; license terms apply", "Uzak masaüstü; lisans koşulları geçerlidir"),
        new("Microsoft.Sysinternals.Autoruns", "Autoruns", "utilities", "Inspect Windows startup entries", "Windows başlangıç kayıtlarını inceleme"),
        new("Bitwarden.Bitwarden", "Bitwarden", "utilities", "Password manager; account required", "Parola yöneticisi; hesap gerekir"),
        new("CPUID.CPU-Z", "CPU-Z", "utilities", "Processor and hardware information", "İşlemci ve donanım bilgileri"),
        new("CrystalDewWorld.CrystalDiskInfo", "CrystalDiskInfo", "utilities", "Drive information and health readings", "Disk bilgileri ve sağlık göstergeleri"),
        new("CrystalDewWorld.CrystalDiskMark", "CrystalDiskMark", "utilities", "Storage performance benchmark", "Depolama performansı kıyaslaması"),
        new("voidtools.Everything", "Everything", "utilities", "File and folder name search", "Dosya ve klasör adı arama"),
        new("SoftDeluxe.FreeDownloadManager", "Free Download Manager", "utilities", "Download manager", "İndirme yöneticisi"),
        new("REALiX.HWiNFO", "HWiNFO", "utilities", "Hardware information and sensors; license terms apply", "Donanım bilgileri ve sensörler; lisans koşulları geçerlidir"),
        new("Intel.IntelDriverAndSupportAssistant", "Intel Driver & Support Assistant", "utilities", "Intel driver discovery and updates", "Intel sürücü keşfi ve güncellemeleri"),
        new("KeePassXCTeam.KeePassXC", "KeePassXC", "utilities", "Local password manager", "Yerel parola yöneticisi"),
        new("Logitech.GHUB", "Logitech G HUB", "utilities", "Logitech gaming device configuration", "Logitech oyun aygıtlarını yapılandırma"),
        new("Guru3D.Afterburner", "MSI Afterburner", "utilities", "GPU monitoring and tuning; changes affect hardware", "GPU izleme ve ayarlama; değişiklikler donanımı etkiler"),
        new("Parsec.Parsec", "Parsec", "utilities", "Remote desktop streaming; account required", "Uzak masaüstü yayını; hesap gerekir"),
        new("Microsoft.PowerToys", "PowerToys", "utilities", "Windows productivity tools", "Windows verimlilik araçları"),
        new("Microsoft.Sysinternals.ProcessExplorer", "Process Explorer", "utilities", "Detailed process inspection", "Ayrıntılı işlem inceleme"),
        new("Microsoft.Sysinternals.ProcessMonitor", "Process Monitor", "utilities", "Monitor file, registry and process activity", "Dosya, kayıt defteri ve işlem etkinliğini izleme"),
        new("PuTTY.PuTTY", "PuTTY", "utilities", "SSH and terminal client", "SSH ve terminal istemcisi"),
        new("qBittorrent.qBittorrent", "qBittorrent", "utilities", "BitTorrent download client", "BitTorrent indirme istemcisi"),
        new("Rufus.Rufus", "Rufus", "utilities", "Bootable USB media creation", "Önyüklenebilir USB oluşturma"),
        new("SteelSeries.GG", "SteelSeries GG", "utilities", "SteelSeries device and capture tools", "SteelSeries aygıt ve kayıt araçları"),
        new("TeamViewer.TeamViewer", "TeamViewer", "utilities", "Remote access; account and license terms apply", "Uzaktan erişim; hesap ve lisans koşulları geçerlidir"),
        new("Ventoy.Ventoy", "Ventoy", "utilities", "Create a multiboot USB drive", "Çoklu önyükleme USB sürücüsü oluşturma"),
        new("WinDirStat.WinDirStat", "WinDirStat", "utilities", "Disk usage analysis", "Disk kullanım analizi"),
        new("RARLab.WinRAR", "WinRAR", "utilities", "Archive manager; trial and paid license", "Arşiv yöneticisi; deneme ve ücretli lisans"),
        new("AntibodySoftware.WizTree", "WizTree", "utilities", "Disk space analyzer; license terms apply", "Disk alanı çözümleyici; lisans koşulları geçerlidir"),
        new("Blizzard.BattleNet", "Battle.net", "games", "Blizzard game library and launcher", "Blizzard oyun kütüphanesi ve başlatıcısı"),
        new("ElectronicArts.EADesktop", "EA app", "games", "EA game library and launcher", "EA oyun kütüphanesi ve başlatıcısı"),
        new("EpicGames.EpicGamesLauncher", "Epic Games Launcher", "games", "Epic Games library and launcher", "Epic Games kütüphanesi ve başlatıcısı"),
        new("GOG.Galaxy", "GOG Galaxy", "games", "GOG game library and launcher", "GOG oyun kütüphanesi ve başlatıcısı"),
        new("Playnite.Playnite", "Playnite", "games", "Unified game library manager", "Birleşik oyun kütüphanesi yöneticisi"),
        new("PrismLauncher.PrismLauncher", "Prism Launcher", "games", "Minecraft launcher; game entitlement required", "Minecraft başlatıcısı; oyun kullanım hakkı gerekir"),
        new("Valve.Steam", "Steam", "games", "PC game library and launcher", "Bilgisayar oyunu kütüphanesi"),
        new("Ubisoft.Connect", "Ubisoft Connect", "games", "Ubisoft game library and launcher", "Ubisoft oyun kütüphanesi ve başlatıcısı"),
        new("Microsoft.DotNet.DesktopRuntime.10", ".NET Desktop Runtime 10", "runtimes", "Runtime for .NET 10 Windows desktop apps", ".NET 10 Windows masaüstü uygulamaları için çalışma zamanı"),
        new("Microsoft.DotNet.DesktopRuntime.8", ".NET Desktop Runtime 8", "runtimes", "Runtime for .NET 8 Windows desktop apps", ".NET 8 Windows masaüstü uygulamaları için çalışma zamanı"),
        new("Microsoft.DotNet.SDK.10", ".NET SDK 10", "runtimes", ".NET 10 development SDK", ".NET 10 geliştirme SDK'sı"),
        new("Microsoft.DotNet.SDK.8", ".NET SDK 8", "runtimes", ".NET 8 development SDK", ".NET 8 geliştirme SDK'sı"),
        new("Microsoft.DirectX", "DirectX End-User Runtime", "runtimes", "Legacy DirectX libraries needed by some games", "Bazı oyunların gerektirdiği eski DirectX kitaplıkları"),
        new("Microsoft.EdgeWebView2Runtime", "Microsoft Edge WebView2 Runtime", "runtimes", "Embedded web-view runtime used by Windows apps", "Windows uygulamalarının kullandığı gömülü web görünümü çalışma zamanı"),
        new("Microsoft.VCRedist.2015+.x64", "Visual C++ Redistributable x64", "runtimes", "Shared Visual C++ runtime for 64-bit apps", "64 bit uygulamalar için ortak Visual C++ çalışma zamanı"),
        new("Microsoft.VCRedist.2015+.x86", "Visual C++ Redistributable x86", "runtimes", "Shared Visual C++ runtime for 32-bit apps", "32 bit uygulamalar için ortak Visual C++ çalışma zamanı")
    ];

    public static async Task<CommandResult> Run(string executable, IEnumerable<string> arguments, CancellationToken token, int timeoutSeconds = 60)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new IOException("Process could not start.");
        var stdout = ReadTail(process.StandardOutput, token);
        var stderr = ReadTail(process.StandardError, token);
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try { await process.WaitForExitAsync(limit.Token); }
        catch (OperationCanceledException)
        {
            // Installer descendants may have their own UAC boundary; do not claim cancellation rolled them back.
            try { process.Kill(false); } catch (InvalidOperationException) { }
            throw;
        }
        return new(process.ExitCode, (await stdout + "\n" + await stderr).Trim());
    }
    private static async Task<string> ReadTail(StreamReader reader, CancellationToken token)
    {
        var tail = new System.Text.StringBuilder();
        var buffer = new char[4096];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) > 0)
        {
            tail.Append(buffer, 0, count);
            if (tail.Length > 65536) tail.Remove(0, tail.Length - 65536);
        }
        return tail.ToString();
    }
    public static string PowerCfg => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "powercfg.exe");
    public static async Task<List<PowerPlan>> PowerPlans(CancellationToken token)
    {
        var result = await Run(PowerCfg, ["/list"], token);
        if (result.Code != 0) throw new IOException(result.Output);
        return result.Output.Split('\n').Select(line => (line, match: PlanPattern().Match(line)))
            .Where(x => x.match.Success).Select(x => new PowerPlan(x.match.Groups[1].Value, x.match.Groups[2].Value, x.line.Contains('*'))).ToList();
    }
    public static async Task SetPower(PowerPlan plan, LocalStore store, CancellationToken token)
    {
        var plans = await PowerPlans(token);
        if (!plans.Any(p => p.Id == plan.Id)) throw new InvalidOperationException();
        var previous = plans.Single(p => p.Active);
        if (previous.Id == plan.Id) return;
        var change = new ChangeRecord { Kind = "power", Target = plan.Id, Before = previous.Id, After = plan.Id };
        store.Add(change);
        var result = await Run(PowerCfg, ["/setactive", plan.Id], token);
        if (result.Code != 0) throw new IOException(result.Output);
        if (!(await PowerPlans(token)).Any(p => p.Id == plan.Id && p.Active)) throw new IOException("Verification failed.");
        change.State = "applied";
        store.SaveChanges();
    }
    public static async Task UndoPower(ChangeRecord change, LocalStore store, CancellationToken token)
    {
        if (store.Damaged) throw new InvalidDataException("Recovery data is damaged.");
        if (change.Kind != "power" || change.State is not ("pending" or "applied") || !Guid.TryParse(change.Before, out _)) throw new InvalidDataException();
        var plans = await PowerPlans(token);
        if (!plans.Any(p => p.Id == change.After && p.Active) || !plans.Any(p => p.Id == change.Before)) throw new InvalidOperationException();
        var result = await Run(PowerCfg, ["/setactive", change.Before!], token);
        if (result.Code != 0) throw new IOException(result.Output);
        if (!(await PowerPlans(token)).Any(p => p.Id == change.Before && p.Active)) throw new IOException("Restored plan verification failed.");
        change.State = "undone";
        store.SaveChanges();
    }
    public static bool ValidPackageId(string id) => PackagePattern().IsMatch(id);
    public static string? FindWinget()
    {
        var alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(alias) ? alias : null;
    }
    public static async Task<CommandResult> PackageAction(string id, bool install, CancellationToken token)
    {
        var args = PackageArguments(id, install);
        var executable = FindWinget() ?? throw new FileNotFoundException("WinGet is missing.");
        return await Run(executable, args, token, 1800);
    }
    public static List<string> PackageArguments(string id, bool install)
    {
        if (!ValidPackageId(id)) throw new ArgumentException("Invalid package ID.");
        var args = new List<string> { install ? "install" : "uninstall", "--id", id, "--exact", "--disable-interactivity" };
        if (install) args.AddRange(["--source", Catalog.FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Source ?? "winget", "--accept-source-agreements", "--accept-package-agreements"]);
        return args;
    }
    public static List<InstalledApp> InstalledApps()
    {
        var apps = new List<InstalledApp>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var registry = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            if (uninstall == null) continue;
            foreach (var name in uninstall.GetSubKeyNames())
            {
                try
                {
                    using var app = uninstall.OpenSubKey(name);
                    if (app?.GetValue("DisplayName") is string title && app.GetValue("SystemComponent") is not 1)
                        apps.Add(new(title, app.GetValue("DisplayVersion")?.ToString() ?? "", app.GetValue("Publisher")?.ToString() ?? ""));
                }
                catch (System.Security.SecurityException) { }
            }
        }
        return apps.DistinctBy(a => a.Name + a.Version).OrderBy(a => a.Name).ToList();
    }
    public static List<StartupEntry> StartupEntries()
    {
        var result = new List<StartupEntry>();
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            using var registry = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var run = registry.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (run == null) continue;
            result.AddRange(run.GetValueNames().Select(n => new StartupEntry(n, run.GetValue(n)?.ToString() ?? "", hive.ToString())));
        }
        foreach (var folder in new[] { Environment.SpecialFolder.Startup, Environment.SpecialFolder.CommonStartup })
        {
            var path = Environment.GetFolderPath(folder);
            if (!Directory.Exists(path)) continue;
            result.AddRange(Directory.EnumerateFiles(path).Select(p => new StartupEntry(Path.GetFileNameWithoutExtension(p), p, folder.ToString())));
        }
        return result.OrderBy(x => x.Name).ToList();
    }
    [GeneratedRegex(@"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})\s+\((.*)\)")]
    private static partial Regex PlanPattern();
    [GeneratedRegex(@"\A[A-Za-z0-9][A-Za-z0-9._+-]{1,120}\z")]
    private static partial Regex PackagePattern();
}
