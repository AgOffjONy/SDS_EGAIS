using System.Windows;
using StoreScanner.Services;

namespace StoreScanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var networkService = new NetworkService();
        var mainWindow = new Views.MainWindow(networkService);
        mainWindow.Show();
    }
}
