using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Incident;
using Serilog;

namespace D365Assistant.Core.Services;

public class RulesEngine
{
    private readonly MonitoringConfig _cfg;
    private readonly StorageService _storage;

    public RulesEngine(MonitoringConfig cfg, StorageService storage)
    {
        _cfg = cfg;
        _storage = storage;
    }

    public EngineResult Run(IReadOnlyList<Incident> incidents)
    {
        Log.Information("Avaliando {Count} chamados...", incidents.Count);

        var newIds = _storage.FindNewIncidentIds(incidents);
        _storage.UpsertIncidents(incidents);

        var alerts = new List<Alert>();
        var suppressed = 0;
        var errors = new List<string>();

        foreach (var inc in incidents.Where(i => i.IsActive))
        {
            var snap = _storage.GetSnapshot(inc.IncidentId);

            var rules = new (Func<Incident, IncidentSnapshot?, Alert?> Eval, AlertType Type, int Cooldown)[]
            {
                (RuleNewIncident,              AlertType.NewIncident,      0),
                (RuleStaleTicket,              AlertType.StaleTicket,     120),
                (RuleSlaRisk,                  AlertType.SlaRisk,          30),
                (RuleSlaBreached,              AlertType.SlaBreached,      60),
                (RuleAwaitingResponse,         AlertType.AwaitingResponse,240),
                (RuleEscalated,                AlertType.Escalated,        45),
                (RuleHighPriorityNoResponse,   AlertType.HighPriority,     30),
            };

            foreach (var (eval, type, cooldown) in rules)
            {
                try
                {
                    var alert = eval(inc, snap);
                    if (alert == null) continue;

                    if (cooldown > 0 &&
                        _storage.WasAlertFiredRecently(inc.IncidentId, type, cooldown))
                    {
                        suppressed++;
                        continue;
                    }
                    alerts.Add(alert);
                }
                catch (Exception ex)
                {
                    var msg = $"Erro na regra {type} para {inc.TicketNumber}: {ex.Message}";
                    Log.Error(msg);
                    errors.Add(msg);
                }
            }
        }

        var result = new EngineResult(alerts, incidents.Count, newIds, suppressed, errors);
        Log.Information(result.Summary);
        return result;
    }

    // ── Regra 1: Novo chamado ─────────────────────────────────────────────────

    private Alert? RuleNewIncident(Incident inc, IncidentSnapshot? snap)
    {
        if (snap != null) return null;
        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.NewIncident, AlertSeverity.Info,
            $"Novo chamado atribuído: {inc.TicketNumber}",
            PriorityScore(inc.PriorityCode) + 20);
    }

    // ── Regra 2: Chamado parado ───────────────────────────────────────────────

    private Alert? RuleStaleTicket(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive || inc.HoursSinceModified < _cfg.StaleTicketHours) return null;

        var extra = Math.Max(0, inc.HoursSinceModified - _cfg.StaleTicketHours);
        var score = Math.Min(80, PriorityScore(inc.PriorityCode) + (int)(extra * 3));
        var sev = score >= 70 ? AlertSeverity.Critical : AlertSeverity.Warning;

        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.StaleTicket, sev,
            $"{inc.TicketNumber} sem atualização há {inc.HoursSinceModified:F0}h",
            score);
    }

    // ── Regra 3: Risco de SLA ─────────────────────────────────────────────────

    private Alert? RuleSlaRisk(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive) return null;
        var slaH = SlaHours(inc.PriorityCode);
        var hoursLeft = slaH - inc.HoursSinceCreated;
        if (hoursLeft <= 0 || hoursLeft > _cfg.SlaWarningHours) return null;

        var score = Math.Max(60, (int)(100 - hoursLeft * 10));
        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.SlaRisk,
            hoursLeft <= 1 ? AlertSeverity.Critical : AlertSeverity.Warning,
            $"Risco de SLA em {inc.TicketNumber}: {hoursLeft:F1}h restantes",
            score);
    }

    // ── Regra 4: SLA violado ──────────────────────────────────────────────────

    private Alert? RuleSlaBreached(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive) return null;
        var overdue = inc.HoursSinceCreated - SlaHours(inc.PriorityCode);
        if (overdue <= 0) return null;

        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.SlaBreached, AlertSeverity.Critical,
            $"SLA VIOLADO em {inc.TicketNumber}: {overdue:F1}h de atraso",
            100);
    }

    // ── Regra 5: Aguardando resposta ──────────────────────────────────────────

    private Alert? RuleAwaitingResponse(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive) return null;
        if (inc.StatusCode is not (2 or 3 or 419500000)) return null;
        if (inc.HoursSinceModified < _cfg.StaleTicketHours) return null;

        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.AwaitingResponse, AlertSeverity.Warning,
            $"{inc.TicketNumber} aguardando resposta há {inc.HoursSinceModified:F0}h",
            PriorityScore(inc.PriorityCode) + 10);
    }

    // ── Regra 6: Escalonado ───────────────────────────────────────────────────

    private Alert? RuleEscalated(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive || !inc.IsEscalated) return null;
        if (inc.HoursSinceModified < 1.0) return null;

        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.Escalated, AlertSeverity.Critical,
            $"ESCALONADO sem ação: {inc.TicketNumber} ({inc.HoursSinceModified:F0}h)",
            90);
    }

    // ── Regra 7: Alta prioridade sem primeira resposta ────────────────────────

    private Alert? RuleHighPriorityNoResponse(Incident inc, IncidentSnapshot? _)
    {
        if (!inc.IsActive || inc.PriorityCode != 1) return null;
        if (inc.FirstResponseSent || inc.HoursSinceCreated < 1.0) return null;

        return new Alert(inc.IncidentId, inc.TicketNumber, inc.Title, inc.CustomerDisplayName,
            AlertType.HighPriority, AlertSeverity.Critical,
            $"Alta prioridade sem resposta: {inc.TicketNumber} ({inc.HoursSinceCreated:F1}h)",
            85);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static int PriorityScore(int? p) => p switch { 419500000 => 80, 1 => 60, 2 => 40, 3 => 20, _ => 30 };
    private static double SlaHours(int? p) => p switch { 419500000 => 2, 1 => 4, 2 => 8, _ => 24 };
}

public record EngineResult(
    IReadOnlyList<Alert> Alerts,
    int IncidentsChecked,
    IReadOnlySet<string> NewIncidentIds,
    int SuppressedCount,
    IReadOnlyList<string> Errors)
{
    public bool HasAlerts => Alerts.Count > 0;

    public IEnumerable<Alert> AlertsByPriority =>
        Alerts.OrderByDescending(a => a.PriorityScore);

    public string Summary =>
        $"Avaliados: {IncidentsChecked} | Novos: {NewIncidentIds.Count} | " +
        $"Alertas: {Alerts.Count} | Suprimidos: {SuppressedCount}";
}