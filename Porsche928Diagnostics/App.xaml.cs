using System.Windows;
using Porsche928Diagnostics.Theme;
using Porsche928Diagnostics.ViewModels;

namespace Porsche928Diagnostics;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeService.Initialize();
        var mainVm = new MainViewModel();
        var mainWindow = new MainWindow { DataContext = mainVm };
        mainWindow.Show();
    }
}
