using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Clearwell.Core;

public record StorageSnapshot(string Name, string Format, long Total, long Free)
{
    public double UsedPercent => Total <= 0 ? 0 : Math.Clamp(100.0 * (Total - Free) / Total, 0, 100);
}
public record SystemSnapshot(string Processor, int LogicalProcessors, string Graphics, string Model, string Windows, ulong MemoryTotal, ulong MemoryAvailable, TimeSpan Uptime, IReadOnlyList<StorageSnapshot> Drives, DateTimeOffset CapturedAt)
{
    public double MemoryUsedPercent => MemoryTotal == 0 ? 0 : Math.Clamp(100.0 * (MemoryTotal - Math.Min(MemoryAvailable, MemoryTotal)) / MemoryTotal, 0, 100);
    public static SystemSnapshot Read()
    {
        static string ReadValue(string key, string name)
        {
            try { using var registry = Registry.LocalMachine.OpenSubKey(key); return registry?.GetValue(name)?.ToString()?.Trim() ?? ""; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return ""; }
        }
        var cpu = ReadValue(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
        var model = ReadValue(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemManufacturer") + " " + ReadValue(@"HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName");
        const string windowsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        var version = Environment.OSVersion.Version;
        var windows = $"Windows {(version.Build >= 22000 ? "11" : "10")} {ReadValue(windowsKey, "EditionID")} · {ReadValue(windowsKey, "DisplayVersion")} · {version.Build}.{ReadValue(windowsKey, "UBR")}";
        var memory = new MemoryStatus { Size = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref memory)) memory = default;
        var adapters = new List<string>();
        for (uint index = 0; index < 32; index++)
        {
            var display = new DisplayDevice { Size = (uint)Marshal.SizeOf<DisplayDevice>(), Name = "", Description = "", Id = "", Key = "" };
            if (!EnumDisplayDevices(null, index, ref display, 0)) break;
            if ((display.Flags & 1) != 0 && (display.Flags & 8) == 0 && !string.IsNullOrWhiteSpace(display.Description)) adapters.Add(display.Description);
        }
        var drives = new List<StorageSnapshot>();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            try { if (drive.IsReady) drives.Add(new(drive.Name, drive.DriveFormat, drive.TotalSize, drive.AvailableFreeSpace)); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return new(cpu, Environment.ProcessorCount, string.Join(" / ", adapters.Distinct()), model.Trim(), windows, memory.TotalPhysical, memory.AvailablePhysical, TimeSpan.FromMilliseconds(Environment.TickCount64), drives, DateTimeOffset.Now);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Size, Load;
        public ulong TotalPhysical, AvailablePhysical, TotalPageFile, AvailablePageFile, TotalVirtual, AvailableVirtual, AvailableExtendedVirtual;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public uint Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
    [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayDevice display, uint flags);
}
