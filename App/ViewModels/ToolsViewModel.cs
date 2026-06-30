// =============================================================================
//  ToolsViewModel.cs — Ferramentas > Web Resources (fix: HttpClient reutilizável)
// =============================================================================
// Fix: HttpClient.BaseAddress e DefaultRequestHeaders não podem ser modificados
// após a primeira requisição. Solução: usar HttpRequestMessage com URL absoluta
// e headers por request, sem tocar no cliente compartilhado.
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.OData;
using D365Assistant.Core.Models.WebResource;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace D365Assistant.ViewModels;

public record WebResourceTypeOption(int? Code, string Label)
{
    public override string ToString() => Label;
}

public partial class WebResourcesViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly IExternalAuthService _auth;  // ← agora usa IExternalAuthService
    private readonly AppSettings _cfg;

    private List<WebResource> _all = [];

    // ── Observable properties ─────────────────────────────────────────────────
    [ObservableProperty] private string _environmentUrl = "";
    [ObservableProperty] private WebResourceTypeOption? _selectedType;
    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private string _statusText = "Configure o ambiente e clique em Buscar.";
    [ObservableProperty] private string _statusColor = "#8B949E";
    [ObservableProperty] private bool _isBusy = false;
    [ObservableProperty] private int _totalCount = 0;
    [ObservableProperty] private bool _hasResults = false;
    [ObservableProperty] private int _countJs = 0;
    [ObservableProperty] private int _countHtml = 0;
    [ObservableProperty] private int _countCss = 0;
    [ObservableProperty] private int _countOther = 0;

    public ObservableCollection<WebResource> Items { get; } = [];

    public static IReadOnlyList<WebResourceTypeOption> TypeOptions { get; } =
    [
        new(null, "Todos os tipos"),
        new(1,    "HTML"),
        new(2,    "CSS"),
        new(3,    "JavaScript"),
        new(4,    "XML"),
        new(5,    "PNG"),
        new(6,    "JPG"),
        new(7,    "GIF"),
        new(8,    "XAP (Silverlight)"),
        new(9,    "XSL"),
        new(10,   "ICO"),
        new(11,   "SVG"),
        new(12,   "RESX"),
    ];

    public WebResourcesViewModel(HttpClient http, IExternalAuthService auth, AppSettings cfg)
    {
        _http = http;
        _auth = auth;
        _cfg = cfg;

        _environmentUrl = cfg.Dataverse?.Url?.TrimEnd('/') ?? "";
        _selectedType = TypeOptions[0];
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task SearchAsync()
    {
        var filtro = FilterText.Trim();
        var baseUrl = ResolveUrl();

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            StatusText = "Informe a URL do ambiente Dynamics 365.";
            StatusColor = "#D29922";
            return;
        }

        if (string.IsNullOrWhiteSpace(filtro))
        {
            StatusText = "Digite um termo para filtrar pelo nome lógico.";
            StatusColor = "#D29922";
            return;
        }

        IsBusy = true;
        HasResults = false;
        StatusText = $"Buscando recursos com nome contendo \"{filtro}\"...";
        StatusColor = "#58A6FF";
        Items.Clear();
        _all.Clear();
        TotalCount = 0;
        ResetStats();

        try
        {
            // Obtém headers frescos a cada busca — sem mutar o HttpClient
            var authHeaders = await _auth.GetHeadersAsync(baseUrl);

            var filters = new List<string> { $"contains(name,'{filtro}')" };
            if (SelectedType?.Code is int typeCode)
                filters.Add($"webresourcetype eq {typeCode}");

            var select = "webresourceid,name,displayname,webresourcetype,ismanaged,modifiedon";
            var filter = string.Join(" and ", filters);
            var relPath = $"webresourceset?$select={select}&$filter={filter}&$orderby=name asc";
            var apiBase = baseUrl.TrimEnd('/') + "/api/data/v9.2/";

            _all = await FetchAllPagesAsync(relPath, apiBase, authHeaders);

            foreach (var item in _all)
                Items.Add(item);

            TotalCount = _all.Count;
            HasResults = TotalCount > 0;
            UpdateStats();

            var host = new Uri(baseUrl).Host;
            StatusText = TotalCount == 0
                ? $"Nenhum recurso encontrado para \"{filtro}\"."
                : $"{TotalCount} recurso(s) encontrado(s) em {host}.";
            StatusColor = TotalCount == 0 ? "#D29922" : "#3FB950";
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
        FilterText = "";
        Items.Clear();
        _all.Clear();
        TotalCount = 0;
        HasResults = false;
        StatusText = "Configure o ambiente e clique em Buscar.";
        StatusColor = "#8B949E";
        ResetStats();
    }

    public void ApplyLocalFilter(string query)
    {
        if (_all.Count == 0) return;
        var q = query.Trim().ToLower();
        Items.Clear();
        var filtered = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(i =>
                i.Name.ToLower().Contains(q) ||
                i.DisplayName.ToLower().Contains(q)).ToList();
        foreach (var item in filtered) Items.Add(item);
        TotalCount = Items.Count;
    }

    [RelayCommand]
    public void CopyName(WebResource? item)
    {
        if (item is not null) System.Windows.Clipboard.SetText(item.Name);
    }

    [RelayCommand]
    public void CopyId(WebResource? item)
    {
        if (item is not null) System.Windows.Clipboard.SetText(item.WebResourceId);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    private string ResolveUrl()
    {
        var typed = EnvironmentUrl.Trim().TrimEnd('/');
        return !string.IsNullOrWhiteSpace(typed)
            ? typed
            : (_cfg.Dataverse?.Url?.TrimEnd('/') ?? "");
    }

    /// <summary>
    /// Busca todas as páginas OData usando HttpRequestMessage com URL absoluta.
    /// Não modifica BaseAddress nem DefaultRequestHeaders do HttpClient compartilhado.
    /// </summary>
    private async Task<List<WebResource>> FetchAllPagesAsync(
        string initialRelPath,
        string apiBase,
        Dictionary<string, string> authHeaders)
    {
        var all = new List<WebResource>();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        string? next = apiBase + initialRelPath;

        while (next is not null)
        {
            var requestUrl = next.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? next
                : apiBase + next;

            var response = await SendRequestAsync(requestUrl, authHeaders);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _auth.InvalidateCache(apiBase);
                authHeaders = await _auth.GetHeadersAsync(apiBase);
                response = await SendRequestAsync(requestUrl, authHeaders);
            }

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ODataResponse<RawWebResource>>(json, opts);

            if (envelope?.Value is not null)
                all.AddRange(envelope.Value.Select(Map));

            next = envelope?.NextLink;
        }

        return all;
    }

    private async Task<HttpResponseMessage> SendRequestAsync(
        string url,
        Dictionary<string, string> authHeaders)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("Prefer", "odata.maxpagesize=100");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        foreach (var (key, value) in authHeaders)
            req.Headers.TryAddWithoutValidation(key, value);

        return await _http.SendAsync(req);
    }

    private static WebResource Map(RawWebResource r) => new()
    {
        WebResourceId = r.WebResourceId ?? "",
        Name = r.Name ?? "",
        DisplayName = r.DisplayName ?? "",
        TypeCode = r.WebResourceType,
        IsManaged = r.IsManaged,
        ModifiedOn = DateTime.TryParse(r.ModifiedOn, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                        ? dt : default,
    };

    private void UpdateStats()
    {
        CountJs = _all.Count(w => w.TypeCode == 3);
        CountHtml = _all.Count(w => w.TypeCode == 1);
        CountCss = _all.Count(w => w.TypeCode == 2);
        CountOther = _all.Count(w => w.TypeCode is not (1 or 2 or 3));
    }

    private void ResetStats() =>
        CountJs = CountHtml = CountCss = CountOther = 0;
}