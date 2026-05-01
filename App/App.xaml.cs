using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Services;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;

namespace D365Assistant;

public partial class App : Application
{
    private IHost? _host;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── 1. Serilog ────────────────────────────────────────────────────────
        Directory.CreateDirectory("logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File("logs/d365_.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} | {Level:u3} | {SourceContext} — {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information("D365 Support Assistant v2.0 iniciando...");

        // ── 2. Configuração ───────────────────────────────────────────────────
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration(cfg =>
                cfg.SetBasePath(AppContext.BaseDirectory)
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
            .ConfigureServices((ctx, services) =>
            {
                // Settings singleton
                var settings = ctx.Configuration.Get<AppSettings>() ?? new AppSettings();
                services.AddSingleton(settings);
                services.AddSingleton(settings.AzureAd);
                services.AddSingleton(settings.Dataverse);
                services.AddSingleton(settings.Monitoring);
                services.AddSingleton(settings.Notifications);
                services.AddSingleton(settings.AI);
                services.AddSingleton(settings.Database);

                // HTTP clients
                services.AddHttpClient<DataverseService>();
                services.AddHttpClient<NotifierService>();
                services.AddHttpClient<GeminiService>();

                // Core services
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IDataverseService, DataverseService>();
                services.AddSingleton<StorageService>();
                services.AddSingleton<RulesEngine>();
                services.AddSingleton<NotifierService>();
                services.AddSingleton<GeminiService>();
                services.AddSingleton<MonitoringOrchestrator>();

                // ViewModels
                services.AddSingleton<ViewModels.MainViewModel>();
                services.AddTransient<ViewModels.DashboardViewModel>();
                services.AddTransient<ViewModels.IncidentsViewModel>();
                services.AddTransient<ViewModels.AlertsViewModel>();
                services.AddTransient<ViewModels.TrackerViewModel>();
                services.AddTransient<ViewModels.AIViewModel>();
                services.AddTransient<ViewModels.SettingsViewModel>();

                // Janela principal
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        var settings = Services.GetRequiredService<AppSettings>();
        if (string.IsNullOrWhiteSpace(settings.AzureAd?.TenantId))
            MessageBox.Show("appsettings.json não encontrado ou vazio!", "Configuração", MessageBoxButton.OK, MessageBoxImage.Warning);

        // ── 3. Inicializa banco ───────────────────────────────────────────────
        var storage = Services.GetRequiredService<StorageService>();
        storage.Initialize();

        // ── 4. Abre janela ────────────────────────────────────────────────────
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        await _host.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}