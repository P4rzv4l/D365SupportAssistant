using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Config
{
    public class AppSettings
    {
        public AzureAdConfig AzureAd { get; set; } = new AzureAdConfig();
        public DataverseConfig Dataverse { get; set; } = new DataverseConfig();
        public MonitoringConfig Monitoring { get; set; } = new MonitoringConfig();
        public NotifyConfig Notifications { get; set; } = new NotifyConfig();
        public AiConfig AI { get; set; } = new AiConfig();
        public DatabaseConfig Database { get; set; } = new DatabaseConfig();
    }
}
