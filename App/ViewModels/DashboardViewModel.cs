// =============================================================================
//  DashboardViewModel.cs — ViewModel do Dashboard
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;

namespace D365Assistant.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly StorageService _storage;
    private List<IncidentSnapshot> _allSnapshots = [];

    // ── KPIs ──────────────────────────────────────────────────────────────────
    [ObservableProperty] private int _totalAtivo = 0;
    [ObservableProperty] private int _urgentes = 0;
    [ObservableProperty] private int _altaPrioridade = 0;
    [ObservableProperty] private int _riscoSla = 0;
    [ObservableProperty] private int _horasEsgotadas = 0;
    [ObservableProperty] private int _novosHoje = 0;
    [ObservableProperty] private string _lastUpdated = "";

    // ── Filtros ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _priFilter = "Todos";
    [ObservableProperty] private string _statusFilter = "Todos";

    // ── Lista de chamados (ObservableCollection atualiza a UI automaticamente) ─
    public ObservableCollection<IncidentSnapshot> Incidents { get; } = [];

    // Opções de filtro para a UI
    public string[] PriorityOptions { get; } = ["Todos", "Urgente", "Alto", "Normal", "Baixa"];
    public string[] StatusOptions { get; } = ["Todos", "Em Atendimento", "Aguardando Fila", "Aguard. cliente", "Impeditivo"];

    // SLA estimado por prioridade (igual ao Python)
    private static readonly Dictionary<int, double> SlaHours = new()
    {
        [419500000] = 2,
        [1] = 4,
        [2] = 8,
        [3] = 24
    };

    public DashboardViewModel(StorageService storage)
    {
        _storage = storage;
    }

    // ── Atualização de dados ──────────────────────────────────────────────────

    public void UpdateData(List<IncidentSnapshot> snapshots, int newToday)
    {
        _allSnapshots = snapshots;

        var now = DateTime.UtcNow;

        // KPIs
        TotalAtivo = snapshots.Count;
        Urgentes = snapshots.Count(s => s.PriorityCode == 419500000);
        AltaPrioridade = snapshots.Count(s => s.PriorityCode == 1);
        HorasEsgotadas = snapshots.Count(s => s.BzHorasEsgotadas);
        NovosHoje = newToday;
        LastUpdated = $"Atualizado {DateTime.Now:HH:mm:ss}";

        // Risco de SLA — chamados onde restam ≤ SlaWarningHours horas
        RiscoSla = snapshots.Count(s =>
        {
            var slaH = SlaHours.GetValueOrDefault(s.PriorityCode ?? 2, 8);
            var first = s.FirstSeenAt.ToUniversalTime();
            var elapsed = (now - first).TotalHours;
            var left = slaH - elapsed;
            return left <= 2 && left >= -24; // em risco ou vencido recente
        });

        ApplyFilter();
    }

    // ── Filtros ───────────────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnPriFilterChanged(string value) => ApplyFilter();
    partial void OnStatusFilterChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var q = SearchText.Trim().ToLower();
        var priMap = new Dictionary<string, int?>
        {
            ["Urgente"] = 419500000,
            ["Alto"] = 1,
            ["Normal"] = 2,
            ["Baixa"] = 3
        };

        var statusMap = new Dictionary<string, int>
        {
            ["Em Atendimento"] = 1,
            ["Aguardando Fila"] = 4,
            ["Aguard. cliente"] = 419500000,
            ["Impeditivo"] = 2,
        };

        var filtered = _allSnapshots.AsEnumerable();

        // Busca textual
        if (!string.IsNullOrEmpty(q))
            filtered = filtered.Where(s =>
                s.TicketNumber.ToLower().Contains(q) ||
                s.Title.ToLower().Contains(q) ||
                (s.CustomerDisplayName ?? "").ToLower().Contains(q));

        // Filtro prioridade
        if (PriFilter != "Todos" && priMap.TryGetValue(PriFilter, out var priCode))
            filtered = filtered.Where(s => s.PriorityCode == priCode);

        // Filtro status
        if (StatusFilter != "Todos" && statusMap.TryGetValue(StatusFilter, out var stCode))
            filtered = filtered.Where(s => s.StatusCode == stCode);

        Incidents.Clear();
        foreach (var snap in filtered.OrderByDescending(s => s.PriorityCode ?? 99))
            Incidents.Add(snap);
    }
}