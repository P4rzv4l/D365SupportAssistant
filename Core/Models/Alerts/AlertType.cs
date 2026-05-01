using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Alerts
{
    public enum AlertType
    {
        NewIncident, 
        SlaRisk, 
        SlaBreached,
        StaleTicket, 
        AwaitingResponse, 
        HighPriority, 
        Escalated
    }
}
