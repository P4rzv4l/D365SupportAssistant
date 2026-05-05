// =============================================================================
//  ToolsViewModel.cs — Ferramentas > Web Resources
//  URL, tipo de recurso e filtro de nome configuráveis diretamente na tela.
// =============================================================================

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.OData;
using D365Assistant.Core.Models.WebResource;
using D365Assistant.Core.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text.Json;

namespace D365Assistant.ViewModels;

public record WebResourceTypeOption(int? Code, string Label)
{
    public override string ToString() => Label;
}

public partial class WebResourcesViewModel : ObservableObject
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;
    private readonly AppSettings _cfg;

    private List<WebResource> _all = [];

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
        new(null,  "Todos os tipos"),
        new(1,     "HTML"),
        new(2,     "CSS"),
        new(3,     "JavaScript"),
        new(4,     "XML"),
        new(5,     "PNG"),
        new(6,     "JPG"),
        new(7,     "GIF"),
        new(8,     "XAP (Silverlight)"),
        new(9,     "XSL"),
        new(10,    "ICO"),
        new(11,    "SVG"),
        new(12,    "RESX"),
    ];

    public WebResourcesViewModel(HttpClient http, IAuthService auth, AppSettings cfg)
    {
        _http = http;
        _auth = auth;
        _cfg = cfg;

        _environmentUrl = cfg.Dataverse?.Url?.TrimEnd('/') ?? "";
        _selectedType = TypeOptions[0];
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var filtro = FilterText.Trim();
        var url = ResolveUrl();

        if (string.IsNullOrWhiteSpace(url))
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
            await RefreshHeadersAsync(url);

            var filters = new List<string> { $"contains(name,'{filtro}')" };
            if (SelectedType?.Code is int typeCode)
                filters.Add($"webresourcetype eq {typeCode}");

            var select = "webresourceid,name,displayname,webresourcetype,ismanaged,modifiedon,createdon,description";
            var filter = string.Join(" and ", filters);
            var apiUrl = $"webresourceset?$select={select}&$filter={filter}&$orderby=name asc";

            _all = await FetchAllPagesAsync(apiUrl, url);

            foreach (var item in _all)
                Items.Add(item);

            TotalCount = _all.Count;
            HasResults = TotalCount > 0;
            UpdateStats();

            var host = new Uri(url).Host;
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
            : _all.Where(i => i.Name.ToLower().Contains(q) || i.DisplayName.ToLower().Contains(q)).ToList();
        foreach (var item in filtered)
            Items.Add(item);
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
        return !string.IsNullOrWhiteSpace(typed) ? typed : (_cfg.Dataverse?.Url?.TrimEnd('/') ?? "");
    }

    private async Task<List<WebResource>> FetchAllPagesAsync(string initialUrl, string baseUrl)
    {
        var all = new List<WebResource>();
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var apiBase = baseUrl.TrimEnd('/') + "/api/data/v9.2/";
        string? next = initialUrl;

        while (next is not null)
        {
            var requestUrl = next.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? next : apiBase + next;

            var resp = await _http.GetAsync(requestUrl);

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _auth.InvalidateCache();
                await RefreshHeadersAsync(baseUrl);
                resp = await _http.GetAsync(requestUrl);
            }

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var envelope = JsonSerializer.Deserialize<ODataResponse<RawWebResource>>(json, opts);

            if (envelope?.Value is not null)
                all.AddRange(envelope.Value.Select(Map));

            next = envelope?.NextLink;
        }

        return all;
    }

    private static WebResource Map(RawWebResource r) => new()
    {
        WebResourceId = r.WebResourceId ?? "",
        Name = r.Name ?? "",
        DisplayName = r.DisplayName ?? "",
        TypeCode = r.WebResourceType,
        IsManaged = r.IsManaged,
        ModifiedOn = DateTime.TryParse(r.ModifiedOn, null,
                            System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : default,
    };

    private async Task RefreshHeadersAsync(string baseUrl)
    {
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/data/v9.2/");
        _http.Timeout = TimeSpan.FromSeconds(60);

        var headers = await _auth.GetHeadersAsync(CancellationToken.None);
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.TryAddWithoutValidation("Prefer", "odata.maxpagesize=100");
        foreach (var (k, v) in headers)
            _http.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
    }

    private void UpdateStats()
    {
        CountJs = _all.Count(w => w.TypeCode == 3);
        CountHtml = _all.Count(w => w.TypeCode == 1);
        CountCss = _all.Count(w => w.TypeCode == 2);
        CountOther = _all.Count(w => w.TypeCode is not (1 or 2 or 3));
    }

    private void ResetStats()
    {
        CountJs = CountHtml = CountCss = CountOther = 0;
    }
}