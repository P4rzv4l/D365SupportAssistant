using Serilog;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Toolkit.Uwp.Notifications;
using D365Assistant.Core.Models.Config;
using D365Assistant.Core.Models.Alerts;
using D365Assistant.Core.Models.Incident;

namespace D365Assistant.Core.Services;

public class NotifierService
{
    private readonly AppSettings _cfg;
    private readonly HttpClient _http;

    public NotifierService(AppSettings cfg, HttpClient http)
    {
        _cfg = cfg;
        _http = http;
    }

    // ── Envio para todos os canais ─────────────────────────────────────────────

    public async Task SendAllAsync(IEnumerable<Alert> alerts, CancellationToken ct = default)
    {
        var list = alerts.ToList();
        if (list.Count == 0) return;

        var tasks = new List<Task>();

        if (_cfg.Notifications.DesktopEnabled)
            tasks.Add(Task.Run(() => SendToast(list), ct));

        if (_cfg.Notifications.TeamsEnabled &&
            !string.IsNullOrWhiteSpace(_cfg.Notifications.TeamsWebhookUrl))
            tasks.Add(SendTeamsAsync(list, ct));

        await Task.WhenAll(tasks);
    }

    // ── Toast (Windows nativo) ────────────────────────────────────────────────

    private static void SendToast(IList<Alert> alerts)
    {
        try
        {
            // Agrupa por chamado
            var topAlert = alerts.OrderByDescending(a => a.PriorityScore).First();

            var title = alerts.Count == 1
                ? $"{topAlert.TypeLabel} — {topAlert.TicketNumber}"
                : $"{alerts.Count} novos alertas D365";

            var body = alerts.Count == 1
                ? topAlert.Message
                : string.Join("\n", alerts.Take(3).Select(a => $"• {a.Message}"));

            // Microsoft.Toolkit.Uwp.Notifications — Toast nativo Windows 10/11
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch (Exception ex)
        {
            Log.Warning("Toast notification falhou: {Error}", ex.Message);
        }
    }

    // ── Teams Webhook ─────────────────────────────────────────────────────────

    private async Task SendTeamsAsync(IList<Alert> alerts, CancellationToken ct)
    {
        try
        {
            var top = alerts.OrderByDescending(a => a.PriorityScore).First();
            var color = top.PriorityScore >= 90 ? "D13438"
                      : top.PriorityScore >= 70 ? "F7A72B"
                                                 : "0078D4";

            var facts = alerts.Take(5).Select(a => new
            {
                name = a.TypeLabel,
                value = a.Message
            }).ToList();

            var card = new
            {
                type = "MessageCard",
                context = "https://schema.org/extensions",
                themeColor = color,
                summary = $"D365 — {alerts.Count} alerta(s)",
                title = $"🔔 D365 Support — {alerts.Count} alerta(s)",
                sections = new[] { new { facts, markdown = true } }
            };

            var json = JsonSerializer.Serialize(card);
            using var content = new StringContent(json,
                System.Text.Encoding.UTF8, "application/json");

            var response = await _http.PostAsync(
                _cfg.Notifications.TeamsWebhookUrl, content, ct);

            if (response.IsSuccessStatusCode)
                Log.Information("Teams notificado | {Count} alertas", alerts.Count);
            else
                Log.Warning("Teams falhou: {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            Log.Error("Erro ao notificar Teams: {Error}", ex.Message);
        }
    }

    // ── Resumo diário ─────────────────────────────────────────────────────────

    public async Task SendDailySummaryAsync(List<IncidentSnapshot> snapshots,
                                             CancellationToken ct = default)
    {
        if (!_cfg.Notifications.TeamsEnabled ||
            string.IsNullOrWhiteSpace(_cfg.Notifications.TeamsWebhookUrl))
            return;

        try
        {
            var now = DateTime.Now;
            var total = snapshots.Count;
            var esgotados = snapshots.Count(s => s.BzHorasEsgotadas);
            var stale = snapshots.Count(s => s.HoursSinceModified > _cfg.Monitoring.StaleTicketHours);

            var facts = new[]
            {
                new { name = "📂 Total abertos",    value = total.ToString()     },
                new { name = "⏰ Hrs esgotadas",    value = esgotados.ToString() },
                new { name = "🕐 Sem atualização",  value = stale.ToString()     },
            };

            var card = new
            {
                type = "MessageCard",
                context = "https://schema.org/extensions",
                themeColor = esgotados > 0 ? "F7A72B" : "0078D4",
                title = $"📅 Resumo D365 — {now:dd/MM/yyyy}",
                sections = new[] { new { facts, markdown = true } }
            };

            var json = JsonSerializer.Serialize(card);
            using var content = new StringContent(json,
                System.Text.Encoding.UTF8, "application/json");

            await _http.PostAsync(_cfg.Notifications.TeamsWebhookUrl, content, ct);
            Log.Information("Resumo diário enviado ao Teams.");
        }
        catch (Exception ex)
        {
            Log.Error("Erro no resumo diário: {Error}", ex.Message);
        }
    }
}