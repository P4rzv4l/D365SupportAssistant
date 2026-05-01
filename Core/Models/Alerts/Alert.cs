using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Alerts
{
    public record Alert( string IncidentId, string TicketNumber, string Title, string? CustomerName, AlertType Type, AlertSeverity Severity, string Message, int PriorityScore )
    {
        public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;

        public string TypeLabel => Type switch
        {
            AlertType.NewIncident => "Novo Chamado Adicionado",
            AlertType.SlaRisk => "Risco de SLA",
            AlertType.SlaBreached => "SLA Perdido",
            AlertType.StaleTicket => "Chamado Parado",
            AlertType.AwaitingResponse => "Aguard. Resposta",
            AlertType.HighPriority => "Alta Prioridade",
            AlertType.Escalated => "Escalonado",
            _ => Type.ToString()
        };

        public string SeverityColor => Severity switch
        {
            AlertSeverity.Critical => "#F85149",
            AlertSeverity.Warning => "#D29922",
            _ => "#58A6FF"
        };
    }
}
