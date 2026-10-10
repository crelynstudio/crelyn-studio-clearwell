using Clearwell.Core;
using System.IO;

if (args.Length != 1) throw new ArgumentException("Pass an empty test workspace directory.");
var testRoot = Path.GetFullPath(args[0]);
if (Directory.Exists(testRoot) && Directory.EnumerateFileSystemEntries(testRoot).Any()) throw new InvalidOperationException("Test directory must be empty.");
Directory.CreateDirectory(testRoot);
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + name);
    checks++;
    Console.WriteLine("PASS: " + name);
}
void Throws<T>(Action action, string name) where T : Exception
{
    try { action(); } catch (T) { Check(true, name); return; }
    throw new InvalidOperationException("FAIL: " + name);
}
var sandbox = Path.Combine(testRoot, "cleanup");
foreach (var scale in new[] { 1.0, 1.25, 1.5, 2.0 })
foreach (var size in new[] { (1366, 728), (1920, 1040), (2560, 1400) })
{
    var work = new System.Windows.Rect(0, 0, size.Item1, size.Item2);
    var fitted = WindowBounds.Fit(work, new System.Windows.Rect(-40, -100, 1380 * scale, 900 * scale), true);
    Check(work.Contains(fitted) && fitted.Width <= 1380 * scale && fitted.Height <= 900 * scale, $"Startup fits {size.Item1}x{size.Item2} work area at {scale:P0} DPI");
}
var secondaryWork = new System.Windows.Rect(-1920, 40, 1920, 1040);
var secondaryFit = WindowBounds.Fit(secondaryWork, new System.Windows.Rect(-300, -120, 1380, 900), false);
Check(secondaryWork.Contains(secondaryFit) && secondaryFit.Left == -1380 && secondaryFit.Top == 40, "Secondary monitor with negative coordinates and top taskbar clamps correctly");
var smallWork = new System.Windows.Rect(0, 0, 800, 520);
Check(WindowBounds.Fit(smallWork, new System.Windows.Rect(0, 0, 1380, 900), true) == smallWork, "Oversized window shrinks to small work area");
Directory.CreateDirectory(sandbox);
var old = Path.Combine(sandbox, "old.tmp");
var recent = Path.Combine(sandbox, "recent.tmp");
var changed = Path.Combine(sandbox, "changed.tmp");
File.WriteAllText(old, "old-file"); File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-3));
File.WriteAllText(recent, "recent-file");
File.WriteAllText(changed, "unchanged"); File.SetLastWriteTimeUtc(changed, DateTime.UtcNow.AddDays(-3));
var root = new CleanupRoot("test", sandbox, "Test", "Test", "", "", false);
var scan = CleanupService.Scan(root, CancellationToken.None);
Check(scan.Files.Count == 2, "Only old files are eligible");
Check(scan.Bytes == 17, "Byte total equals actual file sizes");
Check(!CleanupService.IsInside(sandbox, sandbox + "-sibling\\victim.txt"), "Sibling prefix is not inside root");
Check(!CleanupService.IsInside(sandbox, Path.Combine(sandbox, "..", "victim.txt")), "Traversal is not inside root");
Check(!CleanupService.IsInside(sandbox, sandbox), "Root itself is not an eligible file");
File.AppendAllText(changed, "-modified");
var result = CleanupService.Delete(scan, CancellationToken.None);
Check(result.Deleted == 1 && result.Skipped == 1 && result.Bytes == 8, "Changed file skipped and deleted bytes counted accurately");
Check(!File.Exists(old) && File.Exists(recent) && File.Exists(changed), "Only unchanged old fixture deleted");
var replaced = Path.Combine(sandbox, "replaced.tmp");
File.WriteAllText(replaced, "before"); File.SetLastWriteTimeUtc(replaced, DateTime.UtcNow.AddDays(-3));
var beforeReplacement = CleanupService.Scan(root, CancellationToken.None).Files.Single(f => f.Path == replaced);
File.Move(replaced, Path.Combine(sandbox, "original-moved.tmp"));
File.WriteAllText(replaced, "after!"); File.SetLastWriteTimeUtc(replaced, new DateTime(beforeReplacement.LastWriteTicks, DateTimeKind.Utc));
Check(!CleanupService.DeleteChecked(sandbox, beforeReplacement) && File.Exists(replaced), "Replacement with identical size/time is protected by file identity");
var locked = Path.Combine(sandbox, "locked.tmp");
File.WriteAllText(locked, "locked"); File.SetLastWriteTimeUtc(locked, DateTime.UtcNow.AddDays(-2));
var lockedScan = CleanupService.Scan(root, CancellationToken.None);
using (var handle = new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
{
    var lockedResult = CleanupService.Delete(lockedScan, CancellationToken.None);
    Check(File.Exists(locked) && lockedResult.Skipped > 0, "Locked file survives deletion");
}
var outside = Path.Combine(testRoot, "outside"); Directory.CreateDirectory(outside);
var victim = Path.Combine(outside, "keep.txt"); File.WriteAllText(victim, "keep"); File.SetLastWriteTimeUtc(victim, DateTime.UtcNow.AddDays(-3));
var link = Path.Combine(sandbox, "junction");
var linkResult = await WindowsTools.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), ["/d", "/c", "mklink", "/J", link, outside], CancellationToken.None);
Check(linkResult.Code == 0, "Junction fixture created");
var linkedScan = CleanupService.Scan(root, CancellationToken.None);
Check(linkedScan.Files.All(f => f.Path != victim) && linkedScan.Skipped > 0, "Junction traversal is excluded");
Check(!CleanupService.DeleteChecked(sandbox, new FileCandidate(victim, 4, File.GetLastWriteTimeUtc(victim).Ticks)), "Deletion outside root refused");
Check(File.Exists(victim), "Outside-root victim survives");
using (var cts = new CancellationTokenSource())
{
    cts.Cancel(); Throws<OperationCanceledException>(() => CleanupService.Scan(root, cts.Token), "Cancelled scan stops");
}
Check(WindowsTools.ValidPackageId("Mozilla.Firefox") && WindowsTools.ValidPackageId("Notepad++.Notepad++"), "Valid exact package IDs accepted");
foreach (var bad in new[] { "", "--silent", "foo & calc", "foo\nbar", "a b", "foo;bar", "$(calc)", "a" })
    Check(!WindowsTools.ValidPackageId(bad), "Command-like package ID refused: " + bad.Replace("\n", "\\n"));
Check(WindowsTools.Catalog.All(p => WindowsTools.ValidPackageId(p.Id)), "Catalog IDs pass validation");
Check(WindowsTools.Catalog.Select(p => p.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == WindowsTools.Catalog.Count, "Catalog package IDs are unique");
Check(WindowsTools.Catalog.All(p => p.Source is "winget" or "msstore"), "Catalog sources are supported");
Check(WindowsTools.Catalog.Single(p => p.Name == "WhatsApp") is { Id: "9NKSQGP7F2NH", Category: "communication", Source: "msstore" }, "WhatsApp uses the official Store package");
Check(WindowsTools.PackageArguments("9nksqgp7f2nh", true).SequenceEqual(new[] { "install", "--id", "9nksqgp7f2nh", "--exact", "--disable-interactivity", "--source", "msstore", "--accept-source-agreements", "--accept-package-agreements" }), "WhatsApp installation selects Microsoft Store");
Check(WindowsTools.PackageArguments("Git.Git", true).Contains("winget") && WindowsTools.PackageArguments("Example.Package", true).Contains("winget"), "Desktop and custom packages retain winget source");
Check(!WindowsTools.PackageArguments("9NKSQGP7F2NH", false).Contains("--source"), "Uninstall retains local package discovery");
Check(WindowsTools.Catalog.All(p => p.Category is "web" or "communication" or "dev" or "media" or "productivity" or "utilities" or "games" or "runtimes"), "Catalog categories have UI translations");
Check(WindowsTools.Catalog.All(p => !string.IsNullOrWhiteSpace(p.Name) && !string.IsNullOrWhiteSpace(p.DescriptionEn) && !string.IsNullOrWhiteSpace(p.DescriptionTr)), "Every catalog entry has a name and bilingual descriptions");
Check(SystemSettings.All.Select(s => s.Id).Distinct().Count() == SystemSettings.All.Count, "Setting IDs are unique");
Check(SystemSettings.All.All(s => s.En.Length > 0 && s.Tr.Length > 0 && s.DescriptionEn.Length > 0 && s.DescriptionTr.Length > 0), "All setting descriptions have both languages");
var stateDir = Path.Combine(testRoot, "state");
var store = new LocalStore(stateDir);
store.Preferences.Language = "tr"; store.Preferences.Dark = false; store.SavePreferences();
store.Add(new ChangeRecord { Target = "advertising", Before = null, After = "0" });
var reloaded = new LocalStore(stateDir);
Check(reloaded.Preferences.Language == "tr" && !reloaded.Preferences.Dark && reloaded.Changes.Count == 1 && reloaded.Changes[0].Before == null, "Preferences and null original values round-trip");
File.WriteAllText(Path.Combine(stateDir, "changes.json"), "{broken");
var corrupt = new LocalStore(stateDir);
Check(corrupt.Damaged, "Corrupt recovery data detected");
Throws<InvalidDataException>(() => SystemSettings.Undo(new ChangeRecord(), corrupt), "Undo refuses damaged history before any registry mutation");
Throws<InvalidOperationException>(() => SystemSettings.Undo(new ChangeRecord { State = "applied", Target = "advertising", After = "untrusted" }, store), "Undo refuses unexpected target value before registry mutation");
Throws<InvalidDataException>(corrupt.SaveChanges, "Corrupt history cannot be silently overwritten");
Check(File.ReadAllText(Path.Combine(stateDir, "changes.json")) == "{broken", "Corrupt file preserved for recovery");
File.WriteAllText(Path.Combine(stateDir, "changes.json"), "[null]");
Check(new LocalStore(stateDir).Damaged, "Semantically invalid history fails closed");
File.WriteAllText(Path.Combine(stateDir, "changes.json"), "[{}]");
Check(new LocalStore(stateDir).Damaged, "Unknown history targets fail closed");
var atomicDir = Path.Combine(testRoot, "blocked"); Directory.CreateDirectory(atomicDir); Directory.CreateDirectory(Path.Combine(atomicDir, "changes.json.tmp"));
var blocked = new LocalStore(atomicDir);
Throws<UnauthorizedAccessException>(() => blocked.Add(new ChangeRecord()), "Write failure stops recovery record creation");
Check(blocked.Changes.Count == 0, "Failed journal write does not leave a fake record");
var snapshot = SystemSnapshot.Read();
Check(snapshot.MemoryTotal > 0 && snapshot.MemoryAvailable <= snapshot.MemoryTotal, "Native memory snapshot reports valid physical memory");
Check(snapshot.MemoryUsedPercent is >= 0 and <= 100 && snapshot.LogicalProcessors > 0 && snapshot.Uptime > TimeSpan.Zero, "Hardware snapshot has bounded usage and real uptime");
Check(snapshot.Drives.Count > 0 && snapshot.Drives.All(d => d.Total > 0 && d.UsedPercent is >= 0 and <= 100), "Fixed-volume discovery returns bounded storage use");
Check(new StorageSnapshot("test", "NTFS", 100, 25).UsedPercent == 75 && new StorageSnapshot("empty", "", 0, 0).UsedPercent == 0, "Storage percentages handle empty and known capacities");
Check((snapshot with { MemoryTotal = 0, MemoryAvailable = 0 }).MemoryUsedPercent == 0 && (snapshot with { MemoryTotal = 100, MemoryAvailable = 200 }).MemoryUsedPercent == 0, "Memory percentage handles unavailable and inconsistent snapshots");
var plans = await WindowsTools.PowerPlans(CancellationToken.None);
Check(plans.Count > 0 && plans.Count(p => p.Active) == 1, "Read-only power plan discovery works on host");
var cmd = await WindowsTools.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"), ["/d", "/c", "echo", "clearwell-check"], CancellationToken.None);
Check(cmd.Code == 0 && cmd.Output.Contains("clearwell-check"), "Process runner captures real output");
Console.WriteLine($"{checks} checks passed. Only fixture files were deleted; no real system settings changed.");
