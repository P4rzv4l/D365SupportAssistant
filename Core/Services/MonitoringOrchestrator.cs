using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using Serilog;

namespace D365Assistant.Core.Services;

public class MonitoringOrchestrator
{
    private readonly IDataverseService _dataverse;
    private readonly StorageService _storage;
    private readonly RulesEngine _engine;
    private readonly NotifierService _notifier;
    private readonly GeminiService _gemini;
    private readonly AppSettings _cfg;

    // Impede ciclos sobrepostos
    private readonly SemaphoreSlim _cycleLock = new(1, 1);

    // Limite de chamados enriquecidos por ciclo
    private const int MaxAiEnrichPerCycle = 3;

    public event EventHandler<CycleCompletedEventArgs>? CycleCompleted;
    public event EventHandler<string>? CycleError;

    public MonitoringOrchestrator(
        IDataverseService dataverse,
        StorageService storage,
        RulesEngine engine,
        NotifierService notifier,
        GeminiService gemini,
        AppSettings cfg)
    {
        _dataverse = dataverse;
        _storage = storage;
        _engine = engine;
        _notifier = notifier;
        _gemini = gemini;
        _cfg = cfg;
    }

    // ── Ciclo principal ────────────────────────────────────────────────────────

    public async Task RunCycleAsync(CancellationToken ct = default)
    {
        if (!await _cycleLock.WaitAsync(0, ct))
        {
            Log.Warning("Ciclo anterior ainda em execução. Pulando.");
            return;
        }

        var runId = _storage.StartPollRun();
        var start = DateTime.UtcNow;
        int fetched = 0, alerts = 0;
        string? error = null;

        try
        {
            Log.Information("═══ Iniciando ciclo [{RunId}] ═══", runId);

            // 1. Busca chamados
            var incidents = await _dataverse.GetMyIncidentsAsync(false, ct);
            fetched = incidents.Count;

            if (fetched == 0)
            {
                Log.Information("Nenhum chamado ativo.");
                RaiseCycleCompleted([], [], 0, 0);
                return;
            }

            // 2. Avalia regras
            var result = _engine.Run(incidents);

            // 3. Enriquece os alertas mais críticos com IA (em paralelo, máx 3)
            if (_cfg.AI.Enabled && result.HasAlerts)
                await EnrichWithAiAsync(result.Alerts, incidents, ct);

            // 4. Notifica e persiste alertas
            if (result.HasAlerts)
            {
                await _notifier.SendAllAsync(result.AlertsByPriority, ct);
                alerts = result.Alerts.Count;

                foreach (var alert in result.Alerts)
                    _storage.RecordAlert(alert.IncidentId, alert.Type, alert.Message);
            }

            // 5. Dispara evento com snapshots atualizados
            var snapshots = _storage.GetAllSnapshots(activeOnly: true);
            var elapsed = (DateTime.UtcNow - start).TotalSeconds;

            Log.Information(
                "═══ Ciclo [{RunId}] concluído em {Elapsed:F1}s — {Fetched} chamados, {Alerts} alertas ═══",
                runId, elapsed, fetched, alerts);

            RaiseCycleCompleted(snapshots, result.Alerts.ToList(), fetched, alerts);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Ciclo [{RunId}] cancelado.", runId);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Log.Error(ex, "Erro no ciclo [{RunId}]: {Error}", runId, error);
            CycleError?.Invoke(this, error);
        }
        finally
        {
            _storage.FinishPollRun(runId, fetched, 0, alerts, error);
            _cycleLock.Release();
        }
    }

    // ── Enriquecimento com IA ──────────────────────────────────────────────────

    /// <summary>
    /// Analisa os N alertas de maior prioridade em paralelo.
    /// Usa SemaphoreSlim para não estourar a API do Gemini.
    /// </summary>
    private async Task EnrichWithAiAsync(
        IReadOnlyList<Alert> alerts,
        IReadOnlyList<Incident> incidents,
        CancellationToken ct)
    {
        var incidentMap = incidents.ToDictionary(i => i.IncidentId);

        // Pega os top-N distintos por incidentId, ordenados por prioridade
        var targets = alerts
            .OrderByDescending(a => a.PriorityScore)
            .DistinctBy(a => a.IncidentId)
            .Take(MaxAiEnrichPerCycle)
            .Where(a => incidentMap.ContainsKey(a.IncidentId))
            .ToList();

        if (targets.Count == 0) return;

        // Semáforo garante no máx 2 chamadas simultâneas à API
        using var throttle = new SemaphoreSlim(2, 2);

        var tasks = targets.Select(async alert =>
        {
            await throttle.WaitAsync(ct);
            try
            {
                var inc = incidentMap[alert.IncidentId];
                var analysis = await _gemini.AnalyzeAsync(inc, inc.BzHistoricoOcorrencia, ct: ct);

                if (analysis.AiAvailable && string.IsNullOrEmpty(analysis.Error))
                    Log.Information("IA — {Ticket} | urgência={Level} ({Score}/100)",
                        inc.TicketNumber, analysis.UrgencyLevel, analysis.UrgencyScore);
                else
                    Log.Warning("IA indisponível para {Ticket}: {Error}",
                        alert.IncidentId, analysis.Error);
            }
            catch (Exception ex)
            {
                Log.Warning("IA falhou para {IncidentId}: {Error}", alert.IncidentId, ex.Message);
            }
            finally
            {
                throttle.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private void RaiseCycleCompleted(
        List<IncidentSnapshot> snapshots,
        List<Alert> alerts,
        int fetched,
        int alertCount)
        => CycleCompleted?.Invoke(this,
            new CycleCompletedEventArgs(snapshots, alerts, fetched, alertCount));
}

// ── EventArgs ──────────────────────────────────────────────────────────────────

public class CycleCompletedEventArgs(
    List<IncidentSnapshot> snapshots,
    List<Alert> alerts,
    int incidentsFetched,
    int alertsFired) : EventArgs
{
    public List<IncidentSnapshot> Snapshots { get; } = snapshots;
    public List<Alert> Alerts { get; } = alerts;
    public int IncidentsFetched { get; } = incidentsFetched;
    public int AlertsFired { get; } = alertsFired;
    public DateTime CompletedAt { get; } = DateTime.Now;
}