// =============================================================================
//  AlertsViewModel.cs, TrackerViewModel.cs, AIViewModel.cs, SettingsViewModel.cs
// =============================================================================

using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Models.Time;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;

namespace D365Assistant.ViewModels;

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
    [ObservableProperty] private string _activeDescription = "";
    [ObservableProperty] private string _timerColor = "#484F58";
    [ObservableProperty] private string _statusPill = "PARADO";
    [ObservableProperty] private string _statusPillColor = "#F85149";
    [ObservableProperty] private string _dayTotal = "0h 00m";
    [ObservableProperty] private bool _isRunning = false;
    [ObservableProperty] private string _ticketInput = "";
    [ObservableProperty] private string _descriptionInput = "";

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
            ActiveDescription = active.Description;
            DescriptionInput = active.Description;
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
        var desc = DescriptionInput.Trim();
        _activeEntryId = _storage.StartTimer(ticket, "", desc);
        ActiveTicket = ticket;
        ActiveTitle = "";
        ActiveDescription = desc;
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
        ActiveDescription = "";
        DescriptionInput = "";
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
        var desc = DescriptionInput.Trim();
        _activeEntryId = _storage.StartTimer(ticket, "", desc);
        ActiveTicket = ticket;
        ActiveDescription = desc;
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

// ── Tracker History ───────────────────────────────────────────────────────────

public partial class TrackerHistoryViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private List<TimeEntry> _rawEntries = [];

    public enum PeriodKind { Day, Week, Month, Year }

    [ObservableProperty] private PeriodKind _selectedPeriod = PeriodKind.Day;
    [ObservableProperty] private DateTime _referenceDate = DateTime.Today;
    [ObservableProperty] private string _periodLabel = "";
    [ObservableProperty] private string _totalFormatted = "0h 00m";
    [ObservableProperty] private string _exportStatus = "";
    [ObservableProperty] private bool _hasData = false;

    // Grouped rows shown in the UI
    public ObservableCollection<TimeEntryGroup> Groups { get; } = [];

    public TrackerHistoryViewModel(StorageService storage)
    {
        _storage = storage;
        Refresh();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    [RelayCommand] public void Previous() { Shift(-1); Refresh(); }
    [RelayCommand] public void Next() { Shift(+1); Refresh(); }
    [RelayCommand] public void Today() { ReferenceDate = DateTime.Today; Refresh(); }

    private void Shift(int delta)
    {
        ReferenceDate = SelectedPeriod switch
        {
            PeriodKind.Day => ReferenceDate.AddDays(delta),
            PeriodKind.Week => ReferenceDate.AddDays(delta * 7),
            PeriodKind.Month => ReferenceDate.AddMonths(delta),
            PeriodKind.Year => ReferenceDate.AddYears(delta),
            _ => ReferenceDate
        };
    }

    partial void OnSelectedPeriodChanged(PeriodKind value) => Refresh();

    // ── Data Loading ──────────────────────────────────────────────────────────

    public void Refresh()
    {
        var (from, to) = GetRange();
        PeriodLabel = FormatLabel(from, to);
        _rawEntries = _storage.GetEntriesByPeriod(from, to);

        Groups.Clear();

        // Group by date then by ticket
        var byDate = _rawEntries
            .GroupBy(e => e.Start.Date)
            .OrderByDescending(g => g.Key);

        foreach (var dayGroup in byDate)
        {
            var dayTotal = dayGroup.Sum(e => e.Seconds);
            var dayLabel = dayGroup.Key == DateTime.Today
                ? "Hoje"
                : dayGroup.Key == DateTime.Today.AddDays(-1)
                ? "Ontem"
                : dayGroup.Key.ToString("ddd, dd/MM/yyyy");

            var byTicket = dayGroup
                .GroupBy(e => e.TicketId)
                .Select(tg => new TicketSummary(
                    tg.Key,
                    tg.FirstOrDefault()?.Title ?? "",
                    tg.Sum(e => e.Seconds),
                    tg.OrderBy(e => e.Start).ToList()))
                .OrderByDescending(t => t.TotalSeconds)
                .ToList();

            Groups.Add(new TimeEntryGroup(dayLabel, dayGroup.Key, dayTotal, byTicket));
        }

        var grandTotal = _rawEntries.Sum(e => e.Seconds);
        var t = TimeSpan.FromSeconds(grandTotal);
        TotalFormatted = t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
            : $"{t.Minutes}m {t.Seconds:D2}s";

        HasData = Groups.Count > 0;
    }

    // ── Export ────────────────────────────────────────────────────────────────

    [RelayCommand]
    public void ExportXlsx()
    {
        try
        {
            var (from, to) = GetRange();
            var safePeriod = PeriodLabel.Replace("/", "-").Replace(":", "-");
            var fileName = $"TimeTracker_{safePeriod}.xlsx";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = ".xlsx",
                Filter = "Excel (*.xlsx)|*.xlsx",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (dlg.ShowDialog() != true) return;

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Time Tracker");

            // ── Header ────────────────────────────────────────────────────────
            ws.Cell(1, 1).Value = "Data";
            ws.Cell(1, 2).Value = "Ticket";
            ws.Cell(1, 3).Value = "Título";
            ws.Cell(1, 4).Value = "Descrição";
            ws.Cell(1, 5).Value = "Início";
            ws.Cell(1, 6).Value = "Fim";
            ws.Cell(1, 7).Value = "Duração (h)";
            ws.Cell(1, 8).Value = "Duração (hh:mm:ss)";

            var hdr = ws.Range(1, 1, 1, 8);
            hdr.Style.Font.Bold = true;
            hdr.Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
            hdr.Style.Font.FontColor = XLColor.White;

            // ── Rows ──────────────────────────────────────────────────────────
            int row = 2;
            foreach (var entry in _rawEntries.OrderBy(e => e.Start))
            {
                ws.Cell(row, 1).Value = entry.Start.ToString("dd/MM/yyyy");
                ws.Cell(row, 2).Value = entry.TicketId;
                ws.Cell(row, 3).Value = entry.Title;
                ws.Cell(row, 4).Value = entry.Description;
                ws.Cell(row, 5).Value = entry.Start.ToString("HH:mm:ss");
                ws.Cell(row, 6).Value = entry.End?.ToString("HH:mm:ss") ?? "(ativo)";
                ws.Cell(row, 7).Value = Math.Round(entry.Seconds / 3600.0, 4);
                var ts = TimeSpan.FromSeconds(entry.Seconds);
                ws.Cell(row, 8).Value = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
                row++;
            }

            // ── Summary sheet ─────────────────────────────────────────────────
            var ws2 = workbook.Worksheets.Add("Resumo por Ticket");
            ws2.Cell(1, 1).Value = "Ticket";
            ws2.Cell(1, 2).Value = "Título";
            ws2.Cell(1, 3).Value = "Total (h)";
            ws2.Cell(1, 4).Value = "Total (hh:mm:ss)";
            ws2.Range(1, 1, 1, 4).Style.Font.Bold = true;
            ws2.Range(1, 1, 1, 4).Style.Fill.BackgroundColor = XLColor.FromHtml("#1D4ED8");
            ws2.Range(1, 1, 1, 4).Style.Font.FontColor = XLColor.White;

            int row2 = 2;
            var byTicket = _rawEntries
                .GroupBy(e => e.TicketId)
                .Select(g => new { Ticket = g.Key, Title = g.FirstOrDefault()?.Title ?? "", Secs = g.Sum(e => e.Seconds) })
                .OrderByDescending(g => g.Secs);

            foreach (var t2 in byTicket)
            {
                var ts2 = TimeSpan.FromSeconds(t2.Secs);
                ws2.Cell(row2, 1).Value = t2.Ticket;
                ws2.Cell(row2, 2).Value = t2.Title;
                ws2.Cell(row2, 3).Value = Math.Round(t2.Secs / 3600.0, 4);
                ws2.Cell(row2, 4).Value = $"{(int)ts2.TotalHours:D2}:{ts2.Minutes:D2}:{ts2.Seconds:D2}";
                row2++;
            }

            ws.Columns().AdjustToContents();
            ws2.Columns().AdjustToContents();
            workbook.SaveAs(dlg.FileName);

            ExportStatus = $"✓ Exportado para {System.IO.Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            ExportStatus = $"✗ Erro: {ex.Message}";
        }
    }

    // ── Internal accessors for TrackerView ───────────────────────────────────
    public List<Core.Models.Time.TimeEntry> _storage_GetPeriod(DateTime from, DateTime to)
        => _storage.GetEntriesByPeriod(from, to);
    public List<Core.Models.Time.TimeEntry> _storage_GetAll()
        => _storage.GetAllEntries();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private (DateTime from, DateTime to) GetRange()
    {
        return SelectedPeriod switch
        {
            PeriodKind.Day => (ReferenceDate, ReferenceDate),
            PeriodKind.Week => StartOfWeek(ReferenceDate),
            PeriodKind.Month => (new DateTime(ReferenceDate.Year, ReferenceDate.Month, 1),
                                  new DateTime(ReferenceDate.Year, ReferenceDate.Month,
                                      DateTime.DaysInMonth(ReferenceDate.Year, ReferenceDate.Month))),
            PeriodKind.Year => (new DateTime(ReferenceDate.Year, 1, 1),
                                  new DateTime(ReferenceDate.Year, 12, 31)),
            _ => (ReferenceDate, ReferenceDate)
        };
    }

    private static (DateTime from, DateTime to) StartOfWeek(DateTime date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = date.AddDays(-diff);
        return (start, start.AddDays(6));
    }

    private string FormatLabel(DateTime from, DateTime to) => SelectedPeriod switch
    {
        PeriodKind.Day => from.ToString("dd/MM/yyyy"),
        PeriodKind.Week => $"{from:dd/MM} – {to:dd/MM/yyyy}",
        PeriodKind.Month => from.ToString("MMMM yyyy", new System.Globalization.CultureInfo("pt-BR")),
        PeriodKind.Year => from.Year.ToString(),
        _ => from.ToString("dd/MM/yyyy")
    };
}

public record TicketSummary(string TicketId, string Title, int TotalSeconds, List<TimeEntry> Entries)
{
    public string Formatted
    {
        get
        {
            var t = TimeSpan.FromSeconds(TotalSeconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";
        }
    }
}

public class TimeEntryGroup(string label, DateTime date, int totalSeconds, List<TicketSummary> tickets)
{
    public string Label { get; } = label;
    public DateTime Date { get; } = date;
    public int TotalSeconds { get; } = totalSeconds;
    public List<TicketSummary> Tickets { get; } = tickets;
    public string TotalFormatted
    {
        get
        {
            var t = TimeSpan.FromSeconds(TotalSeconds);
            return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:D2}m" : $"{t.Minutes}m {t.Seconds:D2}s";
        }
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

// ── Notes ─────────────────────────────────────────────────────────────────────

public partial class NotesViewModel : ObservableObject
{
    private readonly StorageService _storage;
    public ObservableCollection<Core.Models.Notes.Note> Notes { get; } = [];

    // Chamados disponíveis para vincular
    private List<IncidentSnapshot> _incidents = [];
    public List<IncidentSnapshot> Incidents => _incidents;

    public NotesViewModel(StorageService storage)
    {
        _storage = storage;
        Load();
    }

    public void Load()
    {
        Notes.Clear();
        foreach (var n in _storage.GetAllNotes())
            Notes.Add(n);
    }

    public void UpdateIncidents(List<IncidentSnapshot> snapshots)
    {
        _incidents = snapshots;
    }

    public Core.Models.Notes.Note CreateNote(string? incidentId = null, string? incidentTitle = null, string? ticketNumber = null)
    {
        var note = new Core.Models.Notes.Note
        {
            Title = incidentId != null ? $"Nota — {ticketNumber}" : "Nova nota",
            Content = "",
            IncidentId = incidentId,
            IncidentTitle = incidentTitle,
            TicketNumber = ticketNumber,
            Color = "#1E2530",
        };
        _storage.SaveNote(note);
        Notes.Insert(0, note);
        return note;
    }

    public void SaveNote(Core.Models.Notes.Note note)
    {
        _storage.SaveNote(note);
        // bubble UpdatedAt change
        var idx = Notes.IndexOf(note);
        if (idx > 0)
        {
            Notes.RemoveAt(idx);
            Notes.Insert(0, note);
        }
    }

    public void DeleteNote(Core.Models.Notes.Note note)
    {
        _storage.DeleteNote(note.Id);
        Notes.Remove(note);
    }
}