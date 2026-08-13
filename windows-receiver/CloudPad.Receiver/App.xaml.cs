using System.Threading;
using System.Windows;

namespace CloudPad.Receiver;

public partial class App : System.Windows.Application
{
    private Mutex? instanceMutex;
    private bool ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        instanceMutex = new Mutex(true, @"Local\CloudPadReceiver.SingleInstance", out var firstInstance);
        ownsMutex = firstInstance;
        if (!firstInstance)
        {
            System.Windows.MessageBox.Show("CloudPad Receiver zaten çalışıyor. Sistem tepsisini kontrol edin.",
                "CloudPad Receiver", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var settings = AppSettings.Load();
        var window = new MainWindow(settings);
        if (!settings.StartMinimized && !e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
            window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsMutex) instanceMutex?.ReleaseMutex();
        instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
