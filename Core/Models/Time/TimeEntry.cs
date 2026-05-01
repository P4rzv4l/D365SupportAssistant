using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Time
{
    public class TimeEntry
    {
        public int Id { get; set; }
        public string TicketId { get; set; } = "";
        public string Title { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public int Seconds { get; set; }
        public bool IsActive { get; set; }
        public string Formatted => TimeSpan.FromSeconds(Seconds).ToString(@"hh\:mm\:ss");
    }
}
