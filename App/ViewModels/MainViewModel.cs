// =============================================================================
//  MainViewModel.cs — ViewModel da janela principal
//  Gerencia o loop de monitoramento, navegação e estado global
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Services;
using Serilog;
using System.Windows.Threading;

namespace D365Assistant.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly MonitoringOrchestrator _orchestrator;
    private readonly AppSettings _cfg;
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _clockTimer;
    private CancellationTokenSource _cts = new();

    // ── Propriedades observáveis (CommunityToolkit gera INotifyPropertyChanged) ──

    [ObservableProperty] private string _statusText = "Iniciando...";
    [ObservableProperty] private string _statusDotColor = "#484F58";
    [ObservableProperty] private string _clockText = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] private string _nextCycleText = "";
    [ObservableProperty] private int _alertBadgeCount = 0;
    [ObservableProperty] private bool _alertBadgeVisible = false;
    [ObservableProperty] private string _statusBarText = "Pronto.";
    [ObservableProperty] private int _activeIncidents = 0;

    // Evento para a UI: novos dados de monitoramento disponíveis
    public event EventHandler<CycleCompletedEventArgs>? DataRefreshed;
    public event EventHandler<string>? MonitorError;

    private DateTime _nextCycleAt;

    public MainViewModel(MonitoringOrchestrator orchestrator, AppSettings cfg)
    {
        _orchestrator = orchestrator;
        _cfg = cfg;

        // Conecta eventos do orquestrador
        _orchestrator.CycleCompleted += OnCycleCompleted;
        _orchestrator.CycleError += OnCycleError;

        // Timer de polling
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_cfg.Monitoring.PollIntervalMinutes)
        };
        _pollTimer.Tick += async (_, _) => await RunCycleAsync();

        // Timer do relógio e countdown
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += OnClockTick;
        _clockTimer.Start();
    }

    // ── Comandos ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusText = "Buscando chamados...";
        StatusDotColor = "#D29922";
        await RunCycleAsync();
    }

    // ── Ciclo de monitoramento ────────────────────────────────────────────────

    public async Task StartMonitoringAsync()
    {
        Log.Information("Monitoramento iniciado | intervalo={Min}min",
            _cfg.Monitoring.PollIntervalMinutes);

        // Primeiro ciclo imediato
        await RunCycleAsync();

        _nextCycleAt = DateTime.Now.AddMinutes(_cfg.Monitoring.PollIntervalMinutes);
        _pollTimer.Start();
    }

    private async Task RunCycleAsync()
    {
        _cts.Cancel();
        _cts = new CancellationTokenSource();

        StatusText = "Buscando chamados...";
        StatusDotColor = "#D29922";

        try
        {
            await _orchestrator.RunCycleAsync(_cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    private void OnCycleCompleted(object? sender, CycleCompletedEventArgs e)
    {
        // Roda na UI thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ActiveIncidents = e.Snapshots.Count;
            AlertBadgeCount = e.AlertsFired;
            AlertBadgeVisible = e.AlertsFired > 0;

            StatusText = $"{e.IncidentsFetched} chamados do CRM";
            StatusDotColor = "#3FB950";
            StatusBarText = $"Último ciclo: {e.CompletedAt:HH:mm:ss} — " +
                             $"{e.IncidentsFetched} chamados, {e.AlertsFired} alertas";

            _nextCycleAt = DateTime.Now.AddMinutes(_cfg.Monitoring.PollIntervalMinutes);
            _pollTimer.Interval = TimeSpan.FromMinutes(_cfg.Monitoring.PollIntervalMinutes);

            DataRefreshed?.Invoke(this, e);
        });
    }

    private void OnCycleError(object? sender, string error)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = $"Erro: {error[..Math.Min(error.Length, 60)]}";
            StatusDotColor = "#F85149";
            StatusBarText = $"Erro no ciclo: {error[..Math.Min(error.Length, 100)]}";
            MonitorError?.Invoke(this, error);
        });
    }

    private void OnClockTick(object? sender, EventArgs e)
    {
        ClockText = DateTime.Now.ToString("HH:mm:ss");

        if (_nextCycleAt == default) return;
        var remaining = _nextCycleAt - DateTime.Now;
        NextCycleText = remaining.TotalSeconds > 0
            ? $"Próximo em {(int)remaining.TotalMinutes}:{remaining.Seconds:D2}"
            : "Atualizando...";
    }

    public void StopMonitoring()
    {
        _cts.Cancel();
        _pollTimer.Stop();
        _clockTimer.Stop();
    }
}