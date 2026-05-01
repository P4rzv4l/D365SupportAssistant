using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using Serilog;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace D365Assistant.Core.Services;

public record IncidentAnalysis
{
    public string IncidentId { get; set; } = "";
    public string TicketNumber { get; set; } = "";
    public bool AiAvailable { get; set; } = true;
    public string? ProblemSummary { get; set; }
    public List<string> PossibleCauses { get; set; } = [];
    public string? UrgencyLevel { get; set; }
    public string? UrgencyReason { get; set; }
    public int? UrgencyScore { get; set; }
    public string? SuggestedResponse { get; set; }
    public List<string> NextSteps { get; set; } = [];
    public string? EstimatedEffort { get; set; }
    public string? Error { get; set; }
    public bool Cached { get; set; }

    public bool IsCritical =>
        UrgencyLevel is "Crítica" or "Alta" || UrgencyScore >= 75;

    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## Análise — {TicketNumber}");

        if (!string.IsNullOrEmpty(UrgencyLevel))
            sb.AppendLine($"**Urgência:** {UrgencyLevel}" +
                (UrgencyScore.HasValue ? $" (score: {UrgencyScore}/100)" : ""));

        sb.AppendLine();

        AppendSection(sb, "### Resumo do problema", ProblemSummary);
        AppendList(sb, "### Possíveis causas", PossibleCauses);
        AppendSection(sb, "### Sugestão de resposta ao cliente", SuggestedResponse);
        AppendNumberedList(sb, "### Próximos passos", NextSteps);

        if (!string.IsNullOrEmpty(EstimatedEffort))
            sb.AppendLine($"**Esforço estimado:** {EstimatedEffort}");

        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string header, string? content)
    {
        if (string.IsNullOrEmpty(content)) return;
        sb.AppendLine(header);
        sb.AppendLine(content);
        sb.AppendLine();
    }

    private static void AppendList(StringBuilder sb, string header, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine(header);
        foreach (var item in items) sb.AppendLine($"- {item}");
        sb.AppendLine();
    }

    private static void AppendNumberedList(StringBuilder sb, string header, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine(header);
        for (int i = 0; i < items.Count; i++) sb.AppendLine($"{i + 1}. {items[i]}");
        sb.AppendLine();
    }
}

public class GeminiService
{
    private readonly AiConfig _cfg;
    private readonly HttpClient _http;

    // Cache em memória: chave derivada do incidentId+modifiedOn → análise
    private readonly Dictionary<string, IncidentAnalysis> _cache = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly TimeSpan ApiTimeout = TimeSpan.FromSeconds(30);

    private const string SystemPrompt = """
        Você é um especialista em suporte técnico N2/N3 especializado em Microsoft Dynamics 365.
        Analise o chamado e responda APENAS com JSON válido, sem blocos markdown, sem texto adicional.

        O JSON deve ter exatamente estas chaves:
        {
          "problem_summary":    "string — resumo técnico em 2-3 frases",
          "possible_causes":    ["causa 1", "causa 2"],
          "urgency_level":      "Crítica | Alta | Média | Baixa",
          "urgency_reason":     "justificativa em 1-2 frases",
          "urgency_score":      0,
          "suggested_response": "resposta profissional em português do Brasil",
          "next_steps":         ["ação 1", "ação 2"],
          "estimated_effort":   "< 1h | 1-4h | > 4h"
        }
        """;

    public GeminiService(AiConfig cfg, HttpClient http)
    {
        _cfg = cfg;
        _http = http;

        if (cfg.Enabled)
            Log.Information("GeminiService inicializado | model={Model}", cfg.GeminiModel);
    }

    // ── API pública ────────────────────────────────────────────────────────────

    public async Task<IncidentAnalysis> AnalyzeAsync(
        Incident incident,
        string? notes = null,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled)
            return Unavailable(incident, "IA desabilitada. Configure AI.Enabled=true no appsettings.json.");

        var cacheKey = MakeCacheKey(incident);

        if (!forceRefresh)
        {
            var cached = await TryGetCacheAsync(cacheKey, ct);
            if (cached is not null) return cached;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(ApiTimeout);

            var analysis = await CallGeminiAsync(incident, notes, cts.Token);
            await SetCacheAsync(cacheKey, analysis, ct);
            return analysis;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            Log.Warning("Gemini timeout ({Timeout}s) para {Ticket}", ApiTimeout.TotalSeconds, incident.TicketNumber);
            return Error(incident, $"Timeout após {ApiTimeout.TotalSeconds}s");
        }
        catch (Exception ex)
        {
            Log.Error("Gemini falhou para {Ticket}: {Error}", incident.TicketNumber, ex.Message);
            return Error(incident, ex.Message);
        }
    }

    public async Task ClearCacheAsync()
    {
        await _lock.WaitAsync();
        try { _cache.Clear(); }
        finally { _lock.Release(); }
        Log.Debug("Cache Gemini limpo.");
    }

    // ── Cache ──────────────────────────────────────────────────────────────────

    private async Task<IncidentAnalysis?> TryGetCacheAsync(string key, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            return _cache.TryGetValue(key, out var hit) ? hit with { Cached = true } : null;
        }
        finally { _lock.Release(); }
    }

    private async Task SetCacheAsync(string key, IncidentAnalysis value, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try { _cache[key] = value; }
        finally { _lock.Release(); }
    }

    // ── Chamada à API Gemini ───────────────────────────────────────────────────

    private async Task<IncidentAnalysis> CallGeminiAsync(
        Incident inc, string? notes, CancellationToken ct)
    {
        var prompt = BuildPrompt(inc, notes);
        var url = $"{ApiBase}/models/{_cfg.GeminiModel}:generateContent?key={_cfg.GeminiApiKey}";

        var body = new
        {
            contents = new[]
            {
                new
                {
                    role  = "user",
                    parts = new[] { new { text = SystemPrompt + "\n\n---\n\n" + prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 1500,
                responseMimeType = "application/json",
            }
        };

        var json = JsonSerializer.Serialize(body);
        using var payload = new StringContent(json, Encoding.UTF8, "application/json");

        var resp = await _http.PostAsync(url, payload, ct);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync(ct);
        var text = ExtractTextFromResponse(respJson);

        return ParseResponse(inc, text);
    }

    private static string ExtractTextFromResponse(string respJson)
    {
        using var doc = JsonDocument.Parse(respJson);
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";
    }

    private static IncidentAnalysis ParseResponse(Incident inc, string raw)
    {
        // Remove blocos ```json ... ``` se existirem
        var clean = raw.Trim();
        if (clean.StartsWith("```"))
        {
            clean = clean[(clean.IndexOf('\n') + 1)..];
            if (clean.EndsWith("```")) clean = clean[..^3].TrimEnd();
        }

        try
        {
            using var doc = JsonDocument.Parse(clean);
            var root = doc.RootElement;

            return new IncidentAnalysis
            {
                IncidentId = inc.IncidentId,
                TicketNumber = inc.TicketNumber,
                ProblemSummary = Str(root, "problem_summary"),
                PossibleCauses = StrList(root, "possible_causes"),
                UrgencyLevel = Str(root, "urgency_level"),
                UrgencyReason = Str(root, "urgency_reason"),
                UrgencyScore = Int(root, "urgency_score"),
                SuggestedResponse = Str(root, "suggested_response"),
                NextSteps = StrList(root, "next_steps"),
                EstimatedEffort = Str(root, "estimated_effort"),
            };
        }
        catch (Exception ex)
        {
            return Error(inc, $"JSON inválido: {ex.Message}");
        }
    }

    // ── Prompt ─────────────────────────────────────────────────────────────────

    private static string BuildPrompt(Incident inc, string? notes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"**Ticket:** {inc.TicketNumber}");
        sb.AppendLine($"**Título:** {inc.Title}");
        sb.AppendLine($"**Prioridade:** {inc.DisplayPriority}");
        sb.AppendLine($"**Status:** {inc.DisplayStatus}");
        sb.AppendLine($"**Cliente:** {inc.CustomerDisplayName}");
        sb.AppendLine($"**Criado há:** {inc.HoursSinceCreated:F1}h");
        sb.AppendLine($"**Sem atualização há:** {inc.HoursSinceModified:F1}h");
        sb.AppendLine();
        sb.AppendLine("## Descrição");
        sb.AppendLine(inc.Description ?? "_Sem descrição._");

        if (!string.IsNullOrEmpty(notes))
        {
            sb.AppendLine();
            sb.AppendLine("## Histórico / Notas");
            sb.AppendLine(notes[..Math.Min(notes.Length, 2000)]);
        }

        return sb.ToString();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static IncidentAnalysis Unavailable(Incident inc, string reason) => new()
    {
        IncidentId = inc.IncidentId,
        TicketNumber = inc.TicketNumber,
        AiAvailable = false,
        ProblemSummary = reason,
    };

    private static IncidentAnalysis Error(Incident inc, string message) => new()
    {
        IncidentId = inc.IncidentId,
        TicketNumber = inc.TicketNumber,
        Error = message,
    };

    private static string MakeCacheKey(Incident inc)
    {
        var raw = $"{inc.IncidentId}:{inc.ModifiedOn:o}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
    }

    // ── Parsing helpers ────────────────────────────────────────────────────────

    private static string? Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static List<string> StrList(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var v)) return [];
        return v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToList()
            : [v.GetString() ?? ""];
    }

    private static int? Int(JsonElement e, string key)
    {
        if (!e.TryGetProperty(key, out var v)) return null;
        if (v.TryGetInt32(out var i)) return i;
        if (int.TryParse(v.GetString(), out var p)) return p;
        return null;
    }
}