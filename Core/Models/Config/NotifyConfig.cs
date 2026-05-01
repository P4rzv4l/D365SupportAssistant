using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Config
{
    public class NotifyConfig
    {
        public string TeamsWebhookUrl { get; set; } = "";
        public bool TeamsEnabled { get; set; } = true;
        public bool DesktopEnabled { get; set; } = true;
    }
}
