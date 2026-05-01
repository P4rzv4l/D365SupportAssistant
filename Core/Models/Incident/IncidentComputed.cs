using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Incident
{
    public partial class Incident
    {
        public bool IsActive => StateCode == 0;
        public double HoursSinceModified => (DateTime.UtcNow - ModifiedOn.ToUniversalTime()).TotalHours;
        public double HoursSinceCreated => (DateTime.UtcNow - CreatedOn.ToUniversalTime()).TotalHours;
        public string CustomerDisplayName => BzpNomeCliente ?? CustomerName ?? "";

        public string DisplayPriority => PriorityCode switch
        {
            419500000 => "Urgente",
            1 => "Alto",
            2 => "Normal",
            3 => "Baixa",
            _ => "—"
        };

        public string DisplayStatus => StatusCode switch
        {
            100000000 => "Novo",
            4 => "Aguardando Fila",
            1 => "Em Atendimento",
            419500000 => "Aguard. cliente",
            3 => "Em Aprovação",
            2 => "Impeditivo",
            5 => "Problema Resolvido",
            1000 => "Informações Fornecidas",
            6 => "Cancelado",
            2000 => "Mesclado",
            419500001 => "Despriorizado",
            121360001 => "Aguard. Microsoft",
            _ => $"Status {StatusCode}"
        };

        public string DisplayCaseType => CaseTypeCode switch
        {
            1 => "Dúvida",
            275500001 => "Garantia",
            3 => "Melhoria",
            2 => "Problema",
            275500000 => "Projeto",
            100000000 => "Solicitação",
            4 => "Sol. Indisponível",
            419500000 => "Sugestão Bot",
            _ => ""
        };

        public string PriorityColor => PriorityCode switch
        {
            419500000 => "#F85149",
            1 => "#F85149",
            2 => "#D29922",
            3 => "#3FB950",
            _ => "#8B949E"
        };

        public string StatusColor => StatusCode switch
        {
            100000000 => "#58A6FF",
            4 => "#8B949E",
            1 => "#3FB950",
            419500000 => "#D29922",
            3 => "#A78BFA",
            2 => "#F85149",
            5 => "#3FB950",
            _ => "#484F58"
        };

        public string IdleText
        {
            get
            {
                var h = HoursSinceModified;
                return h < 1 ? $"{(int)(h * 60)}m parado"
                     : h < 24 ? $"{h:F1}h parado"
                               : $"{h / 24:F1}d parado";
            }
        }

        public string IdleColor => HoursSinceModified switch
        {
            > 48 => "#F85149",
            > 8 => "#D29922",
            _ => "#484F58"
        };
    }
}
