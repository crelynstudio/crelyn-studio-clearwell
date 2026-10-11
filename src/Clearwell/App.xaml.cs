using System.Windows;
using Clearwell.Core;

namespace Clearwell;

public partial class App : Application
{
    private Mutex? instance;
    public static string? CaptureFolder { get; private set; }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--wait-for-parent" && int.TryParse(e.Args[1], out var parentId))
        {
            try { using var parent = System.Diagnostics.Process.GetProcessById(parentId); parent.WaitForExit(15000); }
            catch (ArgumentException) { }
        }
        if (e.Args.Length == 2 && e.Args[0] == "--verify-ui") CaptureFolder = Path.GetFullPath(e.Args[1]);
        if (CaptureFolder != null && Directory.Exists(CaptureFolder) && Directory.EnumerateFileSystemEntries(CaptureFolder).Any())
        { Shutdown(2); return; }
        if (CaptureFolder == null)
        {
            var user = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            instance = new Mutex(true, "Local\\CrelynStudio.Clearwell." + user, out var created);
            if (!created)
            {
                MessageBox.Show("Clearwell is already open.\nClearwell zaten açık.", "Clearwell");
                instance.Dispose(); instance = null; Shutdown(); return;
            }
        }
        var folder = CaptureFolder == null
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrelynStudio", "Clearwell")
            : Path.Combine(CaptureFolder, "profile");
        DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show("Clearwell could not complete the action. No further actions were started.\nClearwell işlemi tamamlayamadı. Başka işlem başlatılmadı.", "Clearwell", MessageBoxButton.OK, MessageBoxImage.Error);
            error.Handled = true;
        };
        MainWindow = new MainWindow(new LocalStore(folder));
        MainWindow.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.ReleaseMutex(); instance?.Dispose(); base.OnExit(e);
    }
}
