using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Notes
{
    public class Note
    {
        public int Id { get; set; }
        public string Title { get; set; } = "Nova nota";
        public string Content { get; set; } = "";
        public string? IncidentId { get; set; }       // null = nota geral
        public string? IncidentTitle { get; set; }    // título do chamado (cache)
        public string? TicketNumber { get; set; }     // ex: INC-00452
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public string Color { get; set; } = "#1E2530"; // cor do tab
    }
}
