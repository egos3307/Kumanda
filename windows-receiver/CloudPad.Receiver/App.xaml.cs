using System.Windows;
namespace CloudPad.Receiver;
public partial class App:System.Windows.Application
{
 protected override void OnStartup(StartupEventArgs e){base.OnStartup(e); var settings=AppSettings.Load(); var w=new MainWindow(settings); if(!settings.StartMinimized)w.Show();}
}
