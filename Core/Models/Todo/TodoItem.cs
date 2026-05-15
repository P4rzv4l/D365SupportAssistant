using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Todo
{
    public class TodoItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "Geral";
        public int Priority { get; set; } = 2;   // 1=Alta 2=Normal 3=Baixa
        public bool Done { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? DueDate { get; set; }
        public DateTime? DoneAt { get; set; }
        public string? TicketId { get; set; }        // vínculo opcional com chamado

        public string PriorityLabel => Priority switch { 1 => "Alta", 3 => "Baixa", _ => "Normal" };
        public string PriorityColor => Priority switch { 1 => "#EF4444", 3 => "#22C55E", _ => "#3B82F6" };
        public bool IsOverdue => !Done && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;
        public bool IsDueToday => !Done && DueDate.HasValue && DueDate.Value.Date == DateTime.Today;
    }
}
