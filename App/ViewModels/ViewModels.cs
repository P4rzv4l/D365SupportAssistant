// =============================================================================
//  AlertsViewModel.cs, TrackerViewModel.cs, AIViewModel.cs, SettingsViewModel.cs
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Models.Time;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;

namespace D365Assistant.ViewModels;

// ── Incidents ─────────────────────────────────────────────────────────────────

public partial class IncidentsViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private List<IncidentSnapshot> _all = [];

    [ObservableProperty] private string _searchText = "";
    public ObservableCollection<IncidentSnapshot> Items { get; } = [];

    public IncidentsViewModel(StorageService storage) => _storage = storage;

    public void UpdateData(List<IncidentSnapshot> snapshots)
    {
        _all = snapshots;
        Refresh();
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh()
    {
        var q = SearchText.Trim().ToLower();
        Items.Clear();
        foreach (var s in _all.Where(s =>
            string.IsNullOrEmpty(q) ||
            s.TicketNumber.ToLower().Contains(q) ||
            s.Title.ToLower().Contains(q) ||
            (s.CustomerDisplayName ?? "").ToLower().Contains(q)))
            Items.Add(s);
    }
}

// ── Alerts ────────────────────────────────────────────────────────────────────

public partial class AlertsViewModel : ObservableObject
{
    public ObservableCollection<Alert> Alerts { get; } = [];

    [ObservableProperty] private int _totalCount = 0;

    public void AddAlerts(IEnumerable<Alert> alerts)
    {
        foreach (var a in alerts.OrderByDescending(x => x.PriorityScore))
        {
            Alerts.Insert(0, a);
            // Mantém máximo de 200 alertas
            while (Alerts.Count > 200) Alerts.RemoveAt(Alerts.Count - 1);
        }
        TotalCount = Alerts.Count;
    }

    [RelayCommand]
    public void Clear() { Alerts.Clear(); TotalCount = 0; }
}

// ── Tracker ───────────────────────────────────────────────────────────────────

public partial class TrackerViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private readonly System.Windows.Threading.DispatcherTimer _timer;

    private int _activeEntryId = -1;
    private int _elapsedSeconds = 0;
    private bool _isPaused;

    [ObservableProperty] private string _timerDisplay = "00:00:00";
    [ObservableProperty] private string _activeTicket = "";
    [ObservableProperty] private string _activeTitle = "";
    [ObservableProperty] private string _timerColor = "#484F58";
    [ObservableProperty] private string _statusPill = "PARADO";
    [ObservableProperty] private string _statusPillColor = "#F85149";
    [ObservableProperty] private string _dayTotal = "0h 00m";
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private string _ticketInput = "";

    public ObservableCollection<TimeEntry> TodayEntries { get; } = [];
    public ObservableCollection<string> RecentTickets { get; } = [];

    public TrackerViewModel(StorageService storage)
    {
        _storage = storage;

        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += OnTick;

        // Restaura sessão ativa
        var active = _storage.GetActiveEntry();
        if (active != null)
        {
            _activeEntryId = active.Id;
            _elapsedSeconds = active.Seconds;
            ActiveTicket = active.TicketId;
            ActiveTitle = active.Title;
            StartUiTimer();
        }

        RefreshDayList();
    }

    // ── Comandos ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public void Start()
    {
        var ticket = TicketInput.Trim();
        if (string.IsNullOrEmpty(ticket)) return;

        StopCurrent();
        _elapsedSeconds = 0;
        _activeEntryId = _storage.StartTimer(ticket, "");
        ActiveTicket = ticket;
        ActiveTitle = "";
        StartUiTimer();
    }

    [RelayCommand]
    public void Pause()
    {
        if (!IsRunning && !_isPaused) return;

        if (IsRunning)
        {
            _timer.Stop();
            _isPaused = true;
            IsRunning = false;
            StatusPill = "PAUSADO";
            StatusPillColor = "#D29922";
            TimerColor = "#D29922";
        }
        else
        {
            _timer.Start();
            _isPaused = false;
            IsRunning = true;
            StatusPill = "ATIVO";
            StatusPillColor = "#3FB950";
            TimerColor = "#3FB950";
        }
    }

    [RelayCommand]
    public void Stop()
    {
        StopCurrent();
        _elapsedSeconds = 0;
        ActiveTicket = "";
        ActiveTitle = "";
        TimerDisplay = "00:00:00";
        StatusPill = "PARADO";
        StatusPillColor = "#F85149";
        TimerColor = "#484F58";
        RefreshDayList();
    }

    [RelayCommand]
    public void Switch()
    {
        var ticket = TicketInput.Trim();
        if (string.IsNullOrEmpty(ticket)) { Start(); return; }
        StopCurrent();
        _elapsedSeconds = 0;
        _activeEntryId = _storage.StartTimer(ticket, "");
        ActiveTicket = ticket;
        StartUiTimer();
        RefreshDayList();
    }

    public void UseRecentTicket(string ticket)
    {
        TicketInput = ticket;
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private void StartUiTimer()
    {
        IsRunning = true;
        _isPaused = false;
        StatusPill = "ATIVO";
        StatusPillColor = "#3FB950";
        TimerColor = "#3FB950";
        _timer.Start();
        UpdateDisplay();
    }

    private void StopCurrent()
    {
        _timer.Stop();
        IsRunning = false;
        _isPaused = false;
        if (_activeEntryId > 0)
        {
            _storage.StopTimer(_activeEntryId, _elapsedSeconds);
            _activeEntryId = -1;
        }
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _elapsedSeconds++;
        if (_activeEntryId > 0)
            _storage.UpdateTimer(_activeEntryId, _elapsedSeconds);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var t = TimeSpan.FromSeconds(_elapsedSeconds);
        TimerDisplay = $"{t.Hours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
    }

    public void RefreshDayList()
    {
        TodayEntries.Clear();
        var entries = _storage.GetTodayEntries();
        int totalSec = 0;
        foreach (var e in entries)
        {
            TodayEntries.Add(e);
            totalSec += e.Seconds;

            if (!RecentTickets.Contains(e.TicketId))
                RecentTickets.Insert(0, e.TicketId);
        }
        while (RecentTickets.Count > 8) RecentTickets.RemoveAt(RecentTickets.Count - 1);

        var t = TimeSpan.FromSeconds(totalSec);
        DayTotal = t.Hours > 0 ? $"{t.Hours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";
    }
}

// ── AI ────────────────────────────────────────────────────────────────────────

public partial class AIViewModel : ObservableObject
{
    private readonly GeminiService _gemini;
    private readonly StorageService _storage;

    [ObservableProperty] private string _ticketInput = "";
    [ObservableProperty] private bool _isAnalyzing = false;
    [ObservableProperty] private string _analysisMarkdown = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _statusColor = "#8B949E";
    [ObservableProperty] private bool _geminiEnabled;
    [ObservableProperty] private string? _suggestedResponse;

    public AIViewModel(GeminiService gemini, StorageService storage, AiConfig aiCfg)
    {
        _gemini = gemini;
        _storage = storage;
        GeminiEnabled = aiCfg.Enabled;
        StatusText = aiCfg.Enabled ? "Gemini habilitado" : "IA desabilitada — configure AI.Enabled no appsettings.json";
        StatusColor = aiCfg.Enabled ? "#3FB950" : "#F85149";
    }

    [RelayCommand]
    public async Task AnalyzeAsync()
    {
        var ticket = TicketInput.Trim();
        if (string.IsNullOrEmpty(ticket)) return;

        var snap = _storage.GetSnapshot(ticket);
        if (snap == null)
        {
            StatusText = $"Chamado {ticket} não encontrado no banco local.";
            StatusColor = "#F85149";
            return;
        }

        IsAnalyzing = true;
        StatusText = $"Analisando {ticket} com Gemini...";
        StatusColor = "#D29922";
        AnalysisMarkdown = "";
        SuggestedResponse = null;

        try
        {
            // Cria Incident mínimo a partir do snapshot
            var inc = new Incident
            {
                IncidentId = snap.IncidentId,
                TicketNumber = snap.TicketNumber,
                Title = snap.Title,
                StateCode = snap.StateCode,
                StatusCode = snap.StatusCode,
                PriorityCode = snap.PriorityCode,
                ModifiedOn = snap.ModifiedOn,
                CreatedOn = snap.FirstSeenAt,
                BzpNomeCliente = snap.BzpNomeCliente,
            };

            var result = await _gemini.AnalyzeAsync(inc);

            AnalysisMarkdown = result.ToMarkdown();
            SuggestedResponse = result.SuggestedResponse;
            StatusText = result.Error != null
                ? $"Erro: {result.Error}"
                : $"Análise concluída | Urgência: {result.UrgencyLevel} ({result.UrgencyScore}/100)";
            StatusColor = result.Error != null ? "#F85149" : "#3FB950";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
            StatusColor = "#F85149";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    public void CopyResponse()
    {
        if (!string.IsNullOrEmpty(SuggestedResponse))
            System.Windows.Clipboard.SetText(SuggestedResponse);
    }
}

// ── Settings ──────────────────────────────────────────────────────────────────

public partial class SettingsViewModel : ObservableObject
{
    private readonly AppSettings _settings;

    // AzureAD
    [ObservableProperty] private string _tenantId = "";
    [ObservableProperty] private string _clientId = "";
    [ObservableProperty] private string _authMode = "DeviceFlow";
    // Dataverse
    [ObservableProperty] private string _dataverseUrl = "";
    [ObservableProperty] private string _apiVersion = "9.2";
    [ObservableProperty] private string _userId = "";
    // Monitoring
    [ObservableProperty] private int _pollInterval = 10;
    [ObservableProperty] private int _slaWarning = 2;
    [ObservableProperty] private int _staleHours = 48;
    // Notifications
    [ObservableProperty] private string _teamsWebhook = "";
    [ObservableProperty] private bool _teamsEnabled = true;
    [ObservableProperty] private bool _desktopEnabled = true;
    // AI
    [ObservableProperty] private bool _aiEnabled = false;
    [ObservableProperty] private string _geminiApiKey = "";
    [ObservableProperty] private string _geminiModel = "gemini-2.0-flash";
    // Status
    [ObservableProperty] private string _saveStatus = "";

    public string[] AuthModeOptions { get; } = ["DeviceFlow", "ClientCredentials"];

    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings;
        Load();
    }

    private void Load()
    {
        TenantId = _settings.AzureAd.TenantId;
        ClientId = _settings.AzureAd.ClientId;
        AuthMode = _settings.AzureAd.AuthMode;
        DataverseUrl = _settings.Dataverse.Url;
        ApiVersion = _settings.Dataverse.ApiVersion;
        UserId = _settings.Dataverse.UserId;
        PollInterval = _settings.Monitoring.PollIntervalMinutes;
        SlaWarning = _settings.Monitoring.SlaWarningHours;
        StaleHours = _settings.Monitoring.StaleTicketHours;
        TeamsWebhook = _settings.Notifications.TeamsWebhookUrl;
        TeamsEnabled = _settings.Notifications.TeamsEnabled;
        DesktopEnabled = _settings.Notifications.DesktopEnabled;
        AiEnabled = _settings.AI.Enabled;
        GeminiApiKey = _settings.AI.GeminiApiKey;
        GeminiModel = _settings.AI.GeminiModel;
    }

    [RelayCommand]
    public void Save()
    {
        try
        {
            // Lê o JSON atual e atualiza os valores
            var path = "appsettings.json";
            var json = System.IO.File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            // Reconstrói o objeto com os novos valores
            var newSettings = new
            {
                AzureAd = new { TenantId, ClientId, AuthMode },
                Dataverse = new { Url = DataverseUrl, ApiVersion, UserId },
                Monitoring = new
                {
                    PollIntervalMinutes = PollInterval,
                    SlaWarningHours = SlaWarning,
                    StaleTicketHours = StaleHours
                },
                Notifications = new
                {
                    TeamsWebhookUrl = TeamsWebhook,
                    TeamsEnabled,
                    DesktopEnabled
                },
                AI = new { Enabled = AiEnabled, GeminiApiKey, GeminiModel },
                Database = new { _settings.Database.Path },
                Logging = new { Level = "Information" }
            };

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var newJson = System.Text.Json.JsonSerializer.Serialize(newSettings, options);
            System.IO.File.WriteAllText(path, newJson);

            SaveStatus = "✓ Configurações salvas. Reinicie o app para aplicar.";
        }
        catch (Exception ex)
        {
            SaveStatus = $"✗ Erro ao salvar: {ex.Message}";
        }
    }

    [RelayCommand]
    public void DeleteTokenCache()
    {
        try
        {
            if (System.IO.File.Exists(".token_cache.json"))
            {
                System.IO.File.Delete(".token_cache.json");
                SaveStatus = "✓ Cache de token removido. Próximo ciclo solicitará novo login.";
            }
            else
            {
                SaveStatus = "Nenhum cache de token encontrado.";
            }
        }
        catch (Exception ex)
        {
            SaveStatus = $"✗ Erro: {ex.Message}";
        }
    }
}