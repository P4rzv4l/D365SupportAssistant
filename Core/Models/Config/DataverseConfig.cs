using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Config
{
    public class DataverseConfig
    {
        public string Url { get; set; } = "";
        public string ApiVersion { get; set; } = "9.2";
        public string UserId { get; set; } = "";
        public string ApiBase => $"{Url.TrimEnd('/')}/api/data/v{ApiVersion}";
        public string Scope => $"{Url.TrimEnd('/')}/.default";
    }
}
