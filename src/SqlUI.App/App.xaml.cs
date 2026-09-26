using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SqlUI.App.Services;
using SqlUI.App.ViewModels;
using SqlUI.App.Views;
using SqlUI.Core;
using SqlUI.Data.MySql;
using SqlUI.Data.Postgres;
using SqlUI.Data.Sqlite;
using SqlUI.Data.SqlServer;

namespace SqlUI.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SqlUI", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(logDir, "sqlui-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error(ex.Exception, "Yakalanmamis UI istisnasi");
            MessageBox.Show("Beklenmeyen bir hata olustu: " + ex.Exception.Message, "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IDbProvider, SqlServerProvider>();
                services.AddSingleton<IDbProvider, SqliteProvider>();
                services.AddSingleton<IDbProvider, PostgresProvider>();
                services.AddSingleton<IDbProvider, MySqlProvider>();

                services.AddSingleton<ActiveConnection>();
                services.AddSingleton<ConnectionProfileStore>();
                services.AddSingleton<AppSettingsStore>();

                services.AddTransient<ConnectionDialogViewModel>();
                services.AddTransient<ConnectionDialog>();

                services.AddTransient<MainWindowViewModel>();
                services.AddTransient<MainWindow>();
                services.AddTransient<SettingsWindow>();
            })
            .Build();

        _host.Start();

        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => ThemeService.ApplyTitleBar((Window)s)));

        var settingsStore = _host.Services.GetRequiredService<AppSettingsStore>();
        var startupSettings = settingsStore.Load();
        ThemeService.Apply(startupSettings.Theme);
        LocalizationService.Instance.Language = startupSettings.Language;
        EditorSettings.Instance.FontFamily = startupSettings.EditorFontFamily;
        EditorSettings.Instance.FontSize = startupSettings.EditorFontSize;

        // Varsayilan ShutdownMode (OnLastWindowClose) burada tuzak: ConnectionDialog kapandiginda
        // MainWindow henuz acilmamisken acik pencere sayisi anlik olarak sifira duser ve WPF
        // uygulamayi MainWindow.Show() hic calismadan otomatik kapatir - kullanicinin gordugu
        // "login penceresi kapandi, hicbir sey acilmadi" tam olarak buydu. Gecis boyunca kapanma
        // kararini elle veriyoruz.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var connectionDialog = _host.Services.GetRequiredService<ConnectionDialog>();
        bool? connected = connectionDialog.ShowDialog();
        if (connected != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Closed += (_, _) => Shutdown();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
