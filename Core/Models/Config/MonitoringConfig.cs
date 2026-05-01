using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Config
{
    public class MonitoringConfig
    {
        public int PollIntervalMinutes { get; set; } = 10;
        public int SlaWarningHours { get; set; } = 2;
        public int StaleTicketHours { get; set; } = 48;
    }
}
