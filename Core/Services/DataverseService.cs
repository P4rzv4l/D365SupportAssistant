using D365Assistant.Core.Models.Auth;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using D365Assistant.Core.Models.OData;
using Serilog;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace D365Assistant.Core.Services;

public interface IDataverseService
{
    Task<List<Incident>> GetMyIncidentsAsync(bool includeResolved = false, CancellationToken ct = default);
    Task<WhoAmIResult> WhoAmIAsync(CancellationToken ct = default);
    Task<string?> GetUserFullNameAsync(string userId, CancellationToken ct = default);
}

public class DataverseService : IDataverseService
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;
    private readonly AppSettings _cfg;

    private const int MaxRetries = 2;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly string[] SelectFields =
    [
        "incidentid", "ticketnumber", "title", "description",
        "statecode", "statuscode", "prioritycode", "casetypecode",
        "caseorigincode", "createdon", "modifiedon",
        "_customerid_value", "_ownerid_value", "_slaid_value",
        "_slainvokedid_value", "isescalated", "firstresponsesent",
        "firstresponseslastatus", "resolvebyslastatus",
        "bzp_nome_cliente", "bzp_url",
        "bz_horas_esgotadas", "bz_sai", "bz_motivo_status",
        "bz_total_horas", "bz_horas_faturaveis",
        "bz_historico_ocorrencia", "bz_status_kpi_first",
        "bz_status_kpi_resolveby", "_entitlementid_value",
        "customersatisfactioncode",
    ];

    private static readonly string[] ExpandFields =
    [
        "customerid_account($select=name)",
    ];

    public DataverseService(HttpClient http, IAuthService auth, AppSettings cfg)
    {
        _http = http;
        _auth = auth;
        _cfg = cfg;

        _http.BaseAddress = new Uri(cfg.Dataverse.ApiBase.TrimEnd('/') + "/");
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    // ── API pública ────────────────────────────────────────────────────────────

    public async Task<List<Incident>> GetMyIncidentsAsync(
        bool includeResolved = false, CancellationToken ct = default)
    {
        await RefreshHeadersAsync(ct);

        var select = string.Join(",", SelectFields);
        var expand = string.Join(",", ExpandFields);
        var filter = $"_ownerid_value eq {_cfg.Dataverse.UserId}";
        if (!includeResolved) filter += " and statecode eq 0";

        var query = $"incidents?$select={select}&$expand={expand}" +
                    $"&$filter={filter}&$orderby=createdon desc&$top=250";

        Log.Information("Buscando chamados | userId={UserId}", _cfg.Dataverse.UserId);

        var raw = await FetchAllPagesAsync<RawIncident>(query, ct);
        var incidents = raw.Select(MapIncident).ToList();

        Log.Information("Chamados retornados: {Count}", incidents.Count);
        return incidents;
    }

    public async Task<WhoAmIResult> WhoAmIAsync(CancellationToken ct = default)
    {
        await RefreshHeadersAsync(ct);
        var result = await _http.GetFromJsonAsync<WhoAmIResult>("WhoAmI", JsonOpts, ct)
            ?? throw new InvalidOperationException("WhoAmI retornou null");
        Log.Information("WhoAmI | UserId={UserId}", result.UserId);
        return result;
    }

    // ── Paginação com retry automático ────────────────────────────────────────

    private async Task<List<T>> FetchAllPagesAsync<T>(string initialQuery, CancellationToken ct)
    {
        var all = new List<T>();
        var nextUrl = (string?)initialQuery;

        while (nextUrl is not null)
        {
            var response = await ExecuteWithRetryAsync(nextUrl, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            var envelope = JsonSerializer.Deserialize<ODataResponse<T>>(content, JsonOpts);

            if (envelope?.Value is not null)
                all.AddRange(envelope.Value);

            nextUrl = envelope?.NextLink;
        }

        return all;
    }

    /// <summary>
    /// Faz GET com retry: em 401 renova o token; em falha de rede aguarda e tenta de novo.
    /// </summary>
    private async Task<HttpResponseMessage> ExecuteWithRetryAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= MaxRetries + 1; attempt++)
        {
            var response = await _http.GetAsync(url, ct);

            // Token expirado — renova e tenta mais uma vez
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                if (attempt > MaxRetries)
                    throw new HttpRequestException("Unauthorized após renovação de token.");

                Log.Warning("401 recebido — renovando token (tentativa {Attempt})...", attempt);
                _auth.InvalidateCache();
                await RefreshHeadersAsync(ct);
                continue;
            }

            if (response.IsSuccessStatusCode) return response;

            // Outro erro HTTP — lança com corpo para facilitar debug
            if (attempt > MaxRetries)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new HttpRequestException(
                    $"Dataverse {(int)response.StatusCode}: {body[..Math.Min(300, body.Length)]}");
            }

            // Backoff simples antes de tentar novamente
            var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            Log.Warning("Dataverse {Status} — aguardando {Delay}s antes de tentar novamente...",
                response.StatusCode, delay.TotalSeconds);
            await Task.Delay(delay, ct);
        }

        // Nunca alcançado, mas necessário para o compilador
        throw new InvalidOperationException("Falha inesperada no retry.");
    }

    public async Task<string?> GetUserFullNameAsync(string userId, CancellationToken ct = default)
    {
        await RefreshHeadersAsync(ct);

        var url = $"systemusers({userId})?$select=firstname";

        var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Erro ao buscar usuário: {body}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        if (json.TryGetProperty("firstname", out var name))
            return name.GetString();

        return null;
    }

    // ── Mapeamento JSON → Incident ─────────────────────────────────────────────

    private static Incident MapIncident(RawIncident r)
    {
        r.Extra.TryGetValue(
            "_ownerid_value@OData.Community.Display.V1.FormattedValue", out var ownerEl);
        r.Extra.TryGetValue(
            "_customerid_value@OData.Community.Display.V1.FormattedValue", out var customerFallbackEl);

        return new Incident
        {
            IncidentId = r.IncidentId ?? "",
            TicketNumber = r.TicketNumber ?? "",
            Title = r.Title ?? "",
            Description = r.Description,
            StateCode = r.StateCode,
            StatusCode = r.StatusCode,
            PriorityCode = r.PriorityCode,
            CaseTypeCode = r.CaseTypeCode,
            OriginCode = r.OriginCode,
            CreatedOn = ParseDate(r.CreatedOn),
            ModifiedOn = ParseDate(r.ModifiedOn),
            IsEscalated = r.IsEscalated,
            FirstResponseSent = r.FirstResponseSent,
            BzpNomeCliente = r.BzpNomeCliente,
            BzpUrl = r.BzpUrl,
            BzHorasEsgotadas = r.BzHorasEsgotadas,
            BzSai = r.BzSai,
            BzMotivoStatus = r.BzMotivoStatus,
            BzTotalHoras = r.BzTotalHoras,
            BzHorasFaturaveis = r.BzHorasFaturaveis,
            BzHistoricoOcorrencia = r.BzHistoricoOcorrencia,
            BzStatusKpiFirst = r.BzStatusKpiFirst,
            BzStatusKpiResolveby = r.BzStatusKpiResolveby,
            CustomerSatisfactionCode = r.CustomerSatisfactionCode,
            CustomerName = r.CustomerAccount?.Name
                                    ?? GetString(customerFallbackEl),
            OwnerName = GetString(ownerEl),
        };
    }

    private static string? GetString(JsonElement el) =>
        el.ValueKind != JsonValueKind.Undefined ? el.GetString() : null;

    private static DateTime ParseDate(string? s) =>
        s is null ? DateTime.UtcNow
                  : DateTime.Parse(s, null, System.Globalization.DateTimeStyles.RoundtripKind);

    private async Task RefreshHeadersAsync(CancellationToken ct)
    {
        var headers = await _auth.GetHeadersAsync(ct);
        _http.DefaultRequestHeaders.Clear();
        foreach (var (k, v) in headers)
            _http.DefaultRequestHeaders.TryAddWithoutValidation(k, v);
    }
}

// ── DTOs OData ─────────────────────────────────────────────────────────────────

internal class RawIncident
{
    [JsonPropertyName("incidentid")] public string? IncidentId { get; set; }
    [JsonPropertyName("ticketnumber")] public string? TicketNumber { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("statecode")] public int StateCode { get; set; }
    [JsonPropertyName("statuscode")] public int StatusCode { get; set; }
    [JsonPropertyName("prioritycode")] public int? PriorityCode { get; set; }
    [JsonPropertyName("casetypecode")] public int? CaseTypeCode { get; set; }
    [JsonPropertyName("caseorigincode")] public int? OriginCode { get; set; }
    [JsonPropertyName("createdon")] public string? CreatedOn { get; set; }
    [JsonPropertyName("modifiedon")] public string? ModifiedOn { get; set; }
    [JsonPropertyName("isescalated")] public bool IsEscalated { get; set; }
    [JsonPropertyName("firstresponsesent")] public bool FirstResponseSent { get; set; }
    [JsonPropertyName("bzp_nome_cliente")] public string? BzpNomeCliente { get; set; }
    [JsonPropertyName("bzp_url")] public string? BzpUrl { get; set; }
    [JsonPropertyName("bz_horas_esgotadas")] public bool BzHorasEsgotadas { get; set; }
    [JsonPropertyName("bz_sai")] public int? BzSai { get; set; }
    [JsonPropertyName("bz_motivo_status")] public string? BzMotivoStatus { get; set; }
    [JsonPropertyName("bz_total_horas")] public double? BzTotalHoras { get; set; }
    [JsonPropertyName("bz_horas_faturaveis")] public double? BzHorasFaturaveis { get; set; }
    [JsonPropertyName("bz_historico_ocorrencia")] public string? BzHistoricoOcorrencia { get; set; }
    [JsonPropertyName("bz_status_kpi_first")] public int? BzStatusKpiFirst { get; set; }
    [JsonPropertyName("bz_status_kpi_resolveby")] public int? BzStatusKpiResolveby { get; set; }
    [JsonPropertyName("customersatisfactioncode")] public int? CustomerSatisfactionCode { get; set; }
    [JsonPropertyName("customerid_account")] public AccountRef? CustomerAccount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; } = [];
}

internal class AccountRef
{
    [JsonPropertyName("name")] public string? Name { get; set; }
}