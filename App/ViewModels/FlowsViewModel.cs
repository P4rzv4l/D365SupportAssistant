// =============================================================================
//  FlowsViewModel.cs — ViewModel de Analisador de Fluxos do Dynamics
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Flows;
using D365Assistant.Core.Models.OData;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace D365Assistant.ViewModels;

public record FlowTypeOption(int Code, string Label)
{
    public override string ToString() => Label;
}

public partial class FlowsViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly IExternalAuthService _auth;

    // ── All items (unfiltered) ────────────────────────────────────────────────
    private List<WorkflowItem> _all = [];

    // ── Observable properties ─────────────────────────────────────────────────
    [ObservableProperty] private string _environmentUrl = "";
    [ObservableProperty] private FlowTypeOption? _selectedType;
    [ObservableProperty] private string _searchTerm1 = "";
    [ObservableProperty] private string _searchTerm2 = "";
    [ObservableProperty] private bool _searchAnd = true;  // AND vs OR
    [ObservableProperty] private string _statusFilter = "Todos"; // Todos|Ativo|Inativo
    [ObservableProperty] private bool _onlyHttps = false;
    [ObservableProperty] private string _sortOrder = "A-Z";
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _statusColor = "#8B949E";
    [ObservableProperty] private int _totalCount = 0;
    [ObservableProperty] private bool _hasResults = false;
    [ObservableProperty] private bool _hasCloudFlows = false;
    [ObservableProperty] private string _connectedUrl = "";

    // ── Stats ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private int _countActive = 0;
    [ObservableProperty] private int _countInactive = 0;
    [ObservableProperty] private int _countCloud = 0;
    [ObservableProperty] private int _countClassic = 0;
    [ObservableProperty] private int _countRules = 0;

    public ObservableCollection<WorkflowItem> Items { get; } = [];

    public static IReadOnlyList<FlowTypeOption> TypeOptions { get; } =
    [
        new(5, "Fluxos Modernos (Cloud Flows)"),
        new(0, "Workflows Clássicos"),
        new(2, "Regras de Negócio"),
    ];

    public FlowsViewModel(HttpClient http, IExternalAuthService auth)
    {
        _http = http;
        _auth = auth;
        _selectedType = TypeOptions[0];
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task FetchAsync()
    {
        var url = EnvironmentUrl.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(url))
        {
            StatusText = "Informe a URL do ambiente Dynamics 365.";
            StatusColor = "#D29922";
            return;
        }

        IsBusy = true;
        HasResults = false;
        StatusText = $"Buscando {SelectedType?.Label ?? "fluxos"}...";
        StatusColor = "#58A6FF";
        Items.Clear();
        _all.Clear();
        TotalCount = 0;
        ResetStats();

        try
        {
            var cat = SelectedType?.Code ?? 5;
            var apiBase = url + "/api/data/v9.2/";
            var relPath = $"workflows?$filter=category eq {cat}" +
                          "&$select=name,workflowid,statecode,category," +
                          "_ownerid_value,clientdata,xaml" +
                          "&$orderby=name asc";

            var headers = await _auth.GetHeadersAsync(url);
            _all = await FetchAllPagesAsync(relPath, apiBase, headers, url);

            ApplyFilter();

            ConnectedUrl = url;
            HasResults = _all.Count > 0;
            HasCloudFlows = _all.Any(w => w.Category == 5);
            UpdateStats();

            StatusText = _all.Count == 0
                ? "Nenhum fluxo encontrado."
                : $"{_all.Count} fluxo(s) carregado(s) de {new Uri(url).Host}";
            StatusColor = _all.Count == 0 ? "#D29922" : "#3FB950";
        }
        catch (Exception ex)
        {
            StatusText = $"Erro: {ex.Message}";
            StatusColor = "#F85149";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void ClearResults()
    {
        _all.Clear();
        Items.Clear();
        TotalCount = 0;
        HasResults = false;
        HasCloudFlows = false;
        ConnectedUrl = "";
        StatusText = "";
        ResetStats();
    }

    [RelayCommand]
    public void ApplyFilter()
    {
        if (_all.Count == 0) return;

        var q = _all.AsEnumerable();

        // Status filter
        q = StatusFilter switch
        {
            "Ativo" => q.Where(w => w.IsActive),
            "Inativo" => q.Where(w => !w.IsActive),
            _ => q,
        };

        // HTTPS trigger filter (Cloud Flows only)
        if (OnlyHttps)
            q = q.Where(w => w.HasHttpsTrigger);

        // Text search
        var t1 = SearchTerm1.Trim().ToLower();
        var t2 = SearchTerm2.Trim().ToLower();

        if (!string.IsNullOrEmpty(t1) || !string.IsNullOrEmpty(t2))
        {
            q = q.Where(w =>
            {
                var content = $"{w.Name} {w.WorkflowId} {w.OwnerName} {w.ClientData} {w.Xaml}"
                              .ToLower();
                var m1 = string.IsNullOrEmpty(t1) || content.Contains(t1);
                var m2 = string.IsNullOrEmpty(t2) || content.Contains(t2);

                if (!string.IsNullOrEmpty(t1) && !string.IsNullOrEmpty(t2))
                    return SearchAnd ? (m1 && m2) : (m1 || m2);
                return m1 && m2;
            });
        }

        // Sort
        q = SortOrder == "Z-A"
            ? q.OrderByDescending(w => w.Name)
            : q.OrderBy(w => w.Name);

        Items.Clear();
        foreach (var item in q) Items.Add(item);
        TotalCount = Items.Count;
    }

    [RelayCommand]
    public void ExportJson()
    {
        if (Items.Count == 0) return;

        var opts = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(
            new
            {
                value = Items.Select(w => new
                {
                    workflowid = w.WorkflowId,
                    name = w.Name,
                    statecode = w.StateCode,
                    category = w.Category,
                    _ownerid_value = w.OwnerId,
                    ownerName = w.OwnerName,
                })
            },
            opts);

        var path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"workflows_{DateTime.Now:yyyyMMdd_HHmmss}.json");

        System.IO.File.WriteAllText(path, json);
        StatusText = $"✓ Exportado para {path}";
        StatusColor = "#3FB950";
    }

    // ── Fetch helpers ─────────────────────────────────────────────────────────

    private async Task<List<WorkflowItem>> FetchAllPagesAsync(
        string relPath, string apiBase,
        Dictionary<string, string> headers,
        string envUrl)
    {
        var all = new List<WorkflowItem>();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string? next = apiBase + relPath;

        while (next is not null)
        {
            var url = next.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? next : apiBase + next;

            var resp = await SendAsync(url, headers);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _auth.InvalidateCache(envUrl);
                headers = await _auth.GetHeadersAsync(envUrl);
                resp = await SendAsync(url, headers);
            }

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ODataResponse<RawWorkflow>>(json, opts);

            if (envelope?.Value is not null)
                all.AddRange(envelope.Value.Select(Map));

            next = envelope?.NextLink;
        }

        return all;
    }

    private async Task<HttpResponseMessage> SendAsync(
        string url, Dictionary<string, string> headers)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Prefer", "odata.maxpagesize=250,odata.include-annotations=*");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        foreach (var (k, v) in headers)
            req.Headers.TryAddWithoutValidation(k, v);
        return await _http.SendAsync(req);
    }

    private static WorkflowItem Map(RawWorkflow r) => new()
    {
        WorkflowId = r.WorkflowId ?? "",
        Name = r.Name ?? "Sem nome",
        StateCode = r.StateCode,
        Category = r.Category,
        OwnerId = r.OwnerId ?? "",
        OwnerName = r.OwnerName ?? r.OwnerId ?? "—",
        ClientData = r.ClientData,
        Xaml = r.Xaml,
    };

    private void UpdateStats()
    {
        CountActive = _all.Count(w => w.IsActive);
        CountInactive = _all.Count(w => !w.IsActive);
        CountCloud = _all.Count(w => w.Category == 5);
        CountClassic = _all.Count(w => w.Category == 0);
        CountRules = _all.Count(w => w.Category == 2);
    }

    private void ResetStats() =>
        CountActive = CountInactive = CountCloud = CountClassic = CountRules = 0;

    // ── Property change hooks ─────────────────────────────────────────────────

    partial void OnSearchTerm1Changed(string _) => ApplyFilter();
    partial void OnSearchTerm2Changed(string _) => ApplyFilter();
    partial void OnSearchAndChanged(bool _) => ApplyFilter();
    partial void OnStatusFilterChanged(string _) => ApplyFilter();
    partial void OnOnlyHttpsChanged(bool _) => ApplyFilter();
    partial void OnSortOrderChanged(string _) => ApplyFilter();
}