using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Clearwell.Core;

public record CleanupRoot(string Id, string Path, string En, string Tr, string DetailEn, string DetailTr, bool Advanced);
public record FileCandidate(string Path, long Length, long LastWriteTicks, uint Volume = 0, ulong FileId = 0);
public record CleanupScan(CleanupRoot Root, List<FileCandidate> Files, int Skipped, bool Truncated)
{
    public long Bytes => Files.Sum(f => f.Length);
    public Dictionary<string, int> Reasons { get; init; } = [];
}
public record CleanupResult(long Bytes, int Deleted, int Skipped);

public static class CleanupService
{
    public const int MaximumFiles = 100_000;
    public static string ReviewCsv(IEnumerable<CleanupScan> scans, ISet<string> excluded)
    {
        static string Cell(string value) => "\"" + (value.Length > 0 && "=+-@\t\r\n".Contains(value[0]) ? "'" : "") + value.Replace("\"", "\"\"") + "\"";
        return "Selected,Path,Bytes,ModifiedUtc,Category\r\n" + string.Join("\r\n", scans.SelectMany(s => s.Files.Select(f =>
            $"{!excluded.Contains(f.Path)},{Cell(f.Path)},{f.Length},{new DateTime(f.LastWriteTicks, DateTimeKind.Utc):O},{Cell(s.Root.Id)}")));
    }
    public static IReadOnlyList<CleanupRoot> Roots()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var roots = new List<CleanupRoot> {
            new("user-temp", Path.Combine(local, "Temp"), "User temporary files", "Kullanıcı geçici dosyaları", "Local Temp files beyond the selected retention period. Files in use are skipped.", "Seçilen koruma süresinden eski yerel Temp dosyaları. Kullanılan dosyalar atlanır.", false),
            new("system-temp", Path.Combine(windows, "Temp"), "Windows temporary files", "Windows geçici dosyaları", "Windows Temp files beyond the selected retention period. Some files require administrator access.", "Seçilen koruma süresinden eski Windows Temp dosyaları. Bazı dosyalar yönetici erişimi gerektirir.", false),
            new("prefetch", Path.Combine(windows, "Prefetch"), "Prefetch cache", "Prefetch önbelleği", "Advanced · Windows rebuilds this cache. Application launches may be slower temporarily; not a speed boost.", "Gelişmiş · Windows bu önbelleği yeniden oluşturur. Uygulamalar geçici olarak yavaş açılabilir; hızlandırma işlemi değildir.", true),
            new("crash-dumps", Path.Combine(local, "CrashDumps"), "Application crash dumps", "Uygulama çökme dökümleri", "Advanced · Removes old diagnostic dumps. Keep these if you are troubleshooting crashes.", "Gelişmiş · Eski tanılama dökümlerini siler. Çökmeleri araştırıyorsanız bunları koruyun.", true)
        };
        var custom = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        if (!custom.Equals(roots[0].Path, StringComparison.OrdinalIgnoreCase) && SafeCustomTemp(custom))
            roots.Add(new("custom-temp", custom, "Custom TEMP location", "Özel TEMP konumu", "Advanced · Review this redirected TEMP path explicitly before selecting it.", "Gelişmiş · Bu yönlendirilmiş TEMP yolunu seçmeden önce açıkça inceleyin.", true));
        return roots;
    }
    public static bool SafeCustomTemp(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\")) return false;
        var canonical = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
        var name = Path.GetFileName(canonical);
        if (!name.Equals("temp", StringComparison.OrdinalIgnoreCase) && !name.Equals("tmp", StringComparison.OrdinalIgnoreCase)) return false;
        return !HasReparseAncestor(canonical) && new[] { Environment.SpecialFolder.UserProfile, Environment.SpecialFolder.Windows, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.DesktopDirectory, Environment.SpecialFolder.MyDocuments }
            .Select(Environment.GetFolderPath).Where(p => p.Length > 0).All(p => !p.Equals(canonical, StringComparison.OrdinalIgnoreCase) && !IsInside(canonical, p));
    }

    public static bool IsInside(string root, string path)
    {
        var canonical = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(canonical, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasReparseAncestor(string path)
    {
        for (var current = new DirectoryInfo(path); current != null; current = current.Parent)
            if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    public static CleanupScan Scan(CleanupRoot root, CancellationToken token, DateTime? utcNow = null, int retentionDays = 1, int maximumEntries = MaximumFiles)
    {
        if (retentionDays < 1 || maximumEntries < 1 || maximumEntries > MaximumFiles) throw new ArgumentOutOfRangeException();
        var files = new List<FileCandidate>();
        var reasons = new Dictionary<string, int>();
        var skipped = 0;
        void Skip(string reason) { skipped++; reasons[reason] = reasons.GetValueOrDefault(reason) + 1; }
        CleanupScan Result(bool truncated) => new(root, files, skipped, truncated) { Reasons = reasons };
        var cutoff = (utcNow ?? DateTime.UtcNow).AddDays(-retentionDays);
        if (!Directory.Exists(root.Path)) return new(root, files, skipped, false);
        if (HasReparseAncestor(root.Path)) { Skip("link"); return Result(false); }
        var pending = new Stack<string>();
        pending.Push(root.Path);
        var visited = 0;
        var inspected = 0;
        while (pending.TryPop(out var directory))
        {
            token.ThrowIfCancellationRequested();
            if (++visited > maximumEntries) return Result(true);
            try
            {
                foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
                {
                    token.ThrowIfCancellationRequested();
                    if (++inspected > maximumEntries) return Result(true);
                    if (files.Count >= maximumEntries) return Result(true);
                    try
                    {
                        if (!IsInside(root.Path, entry.FullName) || (entry.Attributes & FileAttributes.ReparsePoint) != 0) { Skip("link"); continue; }
                        if (entry is DirectoryInfo) pending.Push(entry.FullName);
                        else if (entry is FileInfo file && file.LastWriteTimeUtc <= cutoff)
                        {
                            var candidate = Snapshot(file.FullName);
                            if (new DateTime(candidate.LastWriteTicks, DateTimeKind.Utc) <= cutoff) files.Add(candidate);
                        }
                    }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception) { Skip(e is UnauthorizedAccessException || e is Win32Exception { NativeErrorCode: 5 } ? "access" : e is Win32Exception { NativeErrorCode: 32 or 33 } ? "locked" : "io"); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Skip(e is UnauthorizedAccessException ? "access" : "io"); }
        }
        return Result(false);
    }

    public static CleanupResult Delete(CleanupScan scan, CancellationToken token)
    {
        long bytes = 0;
        var deleted = 0;
        var skipped = scan.Skipped;
        foreach (var candidate in scan.Files)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (DeleteChecked(scan.Root.Path, candidate)) { bytes += candidate.Length; deleted++; }
                else skipped++;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception) { skipped++; }
        }
        return new(bytes, deleted, skipped);
    }

    public static bool DeleteChecked(string root, FileCandidate candidate)
    {
        if (!IsInside(root, candidate.Path) || HasReparseAncestor(Path.GetDirectoryName(candidate.Path)!)) return false;
        // Delete by verified handle so a renamed or replaced path cannot redirect deletion.
        using var handle = CreateFile(candidate.Path, 0x10080, 1, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        var length = ((long)info.SizeHigh << 32) | info.SizeLow;
        var writeTime = ((long)info.WriteHigh << 32) | info.WriteLow;
        if ((info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0 ||
            length != candidate.Length || DateTime.FromFileTimeUtc(writeTime).Ticks != candidate.LastWriteTicks ||
            candidate.FileId == 0 || info.VolumeSerial != candidate.Volume ||
            (((ulong)info.IndexHigh << 32) | info.IndexLow) != candidate.FileId) return false;
        var final = new StringBuilder(32768);
        var count = GetFinalPathNameByHandle(handle, final, (uint)final.Capacity, 0);
        if (count == 0 || count >= final.Capacity) return false;
        var resolved = final.ToString();
        if (resolved.StartsWith(@"\\?\")) resolved = resolved[4..];
        if (!IsInside(root, resolved)) return false;
        var disposition = new Disposition { Delete = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, Marshal.SizeOf<Disposition>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return true;
    }

    private static FileCandidate Snapshot(string path)
    {
        using var handle = CreateFile(path, 0x81, 7, IntPtr.Zero, 3, 0x00200000, IntPtr.Zero);
        if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if ((info.Attributes & (uint)(FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0) throw new IOException("Linked file excluded.");
        var writeTime = ((long)info.WriteHigh << 32) | info.WriteLow;
        return new(path, ((long)info.SizeHigh << 32) | info.SizeLow, DateTime.FromFileTimeUtc(writeTime).Ticks,
            info.VolumeSerial, ((ulong)info.IndexHigh << 32) | info.IndexLow);
    }

    [StructLayout(LayoutKind.Sequential)] private struct FileInfoNative
    {
        public uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
        public uint VolumeSerial, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Disposition { [MarshalAs(UnmanagedType.Bool)] public bool Delete; }
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out FileInfoNative info);
    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int type, ref Disposition info, int size);
}
