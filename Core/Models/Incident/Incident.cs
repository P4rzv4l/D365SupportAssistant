using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Incident
{
    public partial class Incident
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
    }
}
