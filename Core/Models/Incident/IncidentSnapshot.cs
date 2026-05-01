using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Incident
{
    public class IncidentSnapshot
    {
        public string IncidentId { get; set; } = "";
        public string TicketNumber { get; set; } = "";
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public int StateCode { get; set; }
        public int StatusCode { get; set; }
        public int? PriorityCode { get; set; }
        public int? CaseTypeCode { get; set; }
        public int? OriginCode { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ModifiedOn { get; set; }
        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }
        public bool IsEscalated { get; set; }
        public bool FirstResponseSent { get; set; }
        public string? BzpNomeCliente { get; set; }
        public string? BzpUrl { get; set; }
        public bool BzHorasEsgotadas { get; set; }
        public int? BzSai { get; set; }
        public string? BzMotivoStatus { get; set; }
        public double? BzTotalHoras { get; set; }
        public double? BzHorasFaturaveis { get; set; }
        public string? BzHistoricoOcorrencia { get; set; }
        public int? BzStatusKpiFirst { get; set; }
        public int? BzStatusKpiResolveby { get; set; }
        public string? EntitlementId { get; set; }
        public string? CustomerName { get; set; }
        public string? OwnerName { get; set; }
        public int AlertCount { get; set; }

        public string CustomerDisplayName => BzpNomeCliente ?? CustomerName ?? string.Empty;

        public double HoursSinceModified => (DateTime.UtcNow - ModifiedOn.ToUniversalTime()).TotalHours;

        public double HoursSinceLastSeen => (DateTime.UtcNow - LastSeenAt.ToUniversalTime()).TotalHours;

        public static IncidentSnapshot FromIncident(Incident incident, DateTime? firstSeenAt = null, int alertCount = 0)
        {
            var now = DateTime.UtcNow;

            return new IncidentSnapshot
            {
                IncidentId = incident.IncidentId,
                TicketNumber = incident.TicketNumber,
                Title = incident.Title,
                Description = incident.Description,
                StateCode = incident.StateCode,
                StatusCode = incident.StatusCode,
                PriorityCode = incident.PriorityCode,
                CaseTypeCode = incident.CaseTypeCode,
                OriginCode = incident.OriginCode,
                CreatedOn = incident.CreatedOn,
                ModifiedOn = incident.ModifiedOn,
                FirstSeenAt = firstSeenAt ?? now,
                LastSeenAt = now,
                IsEscalated = incident.IsEscalated,
                FirstResponseSent = incident.FirstResponseSent,
                BzpNomeCliente = incident.BzpNomeCliente,
                BzpUrl = incident.BzpUrl,
                BzHorasEsgotadas = incident.BzHorasEsgotadas,
                BzSai = incident.BzSai,
                BzMotivoStatus = incident.BzMotivoStatus,
                BzTotalHoras = incident.BzTotalHoras,
                BzHorasFaturaveis = incident.BzHorasFaturaveis,
                BzHistoricoOcorrencia = incident.BzHistoricoOcorrencia,
                BzStatusKpiFirst = incident.BzStatusKpiFirst,
                BzStatusKpiResolveby = incident.BzStatusKpiResolveby,
                EntitlementId = incident.EntitlementId,
                CustomerName = incident.CustomerName,
                OwnerName = incident.OwnerName,
                AlertCount = alertCount
            };
        }

        public void updateFromIncident( Incident incident )
        {
            LastSeenAt = DateTime.UtcNow;

            if(incident.ModifiedOn != ModifiedOn)
            {
                ModifiedOn = incident.ModifiedOn;
            }

            IncidentId = incident.IncidentId;
            TicketNumber = incident.TicketNumber;
            Title = incident.Title;
            Description = incident.Description;
            StateCode = incident.StateCode;
            StatusCode = incident.StatusCode;
            PriorityCode = incident.PriorityCode;
            CaseTypeCode = incident.CaseTypeCode;
            OriginCode = incident.OriginCode;
            CreatedOn = incident.CreatedOn;
            ModifiedOn = incident.ModifiedOn;
            IsEscalated = incident.IsEscalated;
            FirstResponseSent = incident.FirstResponseSent;
            BzpNomeCliente = incident.BzpNomeCliente;
            BzpUrl = incident.BzpUrl;
            BzHorasEsgotadas = incident.BzHorasEsgotadas;
            BzSai = incident.BzSai;
            BzMotivoStatus = incident.BzMotivoStatus;
            BzTotalHoras = incident.BzTotalHoras;
            BzHorasFaturaveis = incident.BzHorasFaturaveis;
            BzHistoricoOcorrencia = incident.BzHistoricoOcorrencia;
            BzStatusKpiFirst = incident.BzStatusKpiFirst;
            BzStatusKpiResolveby = incident.BzStatusKpiResolveby;
            EntitlementId = incident.EntitlementId;
            CustomerName = incident.CustomerName;
            OwnerName = incident.OwnerName;
        }
    }
}
