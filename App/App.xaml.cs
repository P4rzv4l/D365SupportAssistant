using D365Assistant.Core.Models;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Services;
using D365Assistant.ViewModels;
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

    // Caminho base de dados — MSIX-safe (LocalAppData)
    public static string DataDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "D365SupportAssistant");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(Path.Combine(DataDir, "logs"));

        // ── 1. Serilog ────────────────────────────────────────────────────────
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(DataDir, "logs", "d365_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} | {Level:u3} | {SourceContext} — {Message:lj}{NewLine}{Exception}")
            .WriteTo.Debug()
            .CreateLogger();

        Log.Information("D365 Support Assistant v2.0 iniciando...");

        // ── 1.1 Handlers globais de exceção — sem isso, crash na inicialização
        //       mata o processo em silêncio (nenhuma janela, nenhum aviso).
        var logsDir = Path.Combine(DataDir, "logs");
        this.DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "Exceção não tratada na UI thread");
            MessageBox.Show(
                $"Erro inesperado:\n\n{args.Exception.Message}\n\nDetalhes em:\n{logsDir}",
                "D365 Support Assistant — Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "Exceção fatal não tratada (AppDomain)");
            Log.CloseAndFlush();
            MessageBox.Show(
                $"Erro fatal ao iniciar:\n\n{ex?.Message}\n\nDetalhes em:\n{logsDir}",
                "D365 Support Assistant — Erro Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Log.Error(args.Exception, "Exceção não observada em Task");
            args.SetObserved();
        };

        // ── 2. Configuração ───────────────────────────────────────────────────
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration(cfg =>
                cfg.SetBasePath(AppContext.BaseDirectory)
                   .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true))
            .ConfigureServices((ctx, services) =>
            {
                // Settings
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
                services.AddSingleton<IExternalAuthService, ExternalAuthService>();
                services.AddHttpClient<WebResourcesViewModel>((sp, client) =>
                {
                    var cfg = sp.GetRequiredService<AppSettings>();
                    client.BaseAddress = new Uri(cfg.Dataverse.ApiBase.TrimEnd('/') + "/");
                    client.Timeout = TimeSpan.FromSeconds(60);
                });

                // Core services
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IDataverseService, DataverseService>();
                services.AddSingleton<StorageService>();
                services.AddSingleton<RulesEngine>();
                services.AddSingleton<NotifierService>();
                services.AddSingleton<GeminiService>();
                services.AddSingleton<MonitoringOrchestrator>();

                // ✅ Vault — banco próprio, isolado do banco principal
                services.AddSingleton<VaultService>(_ =>
                    new VaultService(Path.Combine(DataDir, "vault.db")));

                // ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddTransient<DashboardViewModel>();
                services.AddTransient<AlertsViewModel>();
                services.AddTransient<TrackerViewModel>();
                services.AddTransient<TrackerHistoryViewModel>();
                services.AddTransient<AIViewModel>();
                services.AddTransient<SettingsViewModel>();
                services.AddTransient<VaultViewModel>();
                services.AddTransient<WebResourcesViewModel>();
                services.AddSingleton<TodoViewModel>();
                services.AddSingleton<NotesViewModel>();
                services.AddSingleton<FlowsViewModel>();

                // Janela principal
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        var settings = Services.GetRequiredService<AppSettings>();
        if (string.IsNullOrWhiteSpace(settings.AzureAd?.TenantId))
            MessageBox.Show(
                "appsettings.json não encontrado ou vazio!\n\nVá em Configurações para preencher.",
                "Configuração", MessageBoxButton.OK, MessageBoxImage.Warning);

        // ── 3. Inicializa banco principal ─────────────────────────────────────
        var storage = Services.GetRequiredService<StorageService>();
        storage.Initialize();

        // ── 4. Abre janela ────────────────────────────────────────────────────
        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        await _host.StartAsync();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        // Bloqueia o vault ao fechar
        try { Services.GetRequiredService<VaultService>().Lock(); } catch { }

        if (_host != null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
            _host.Dispose();
        }
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}