using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.WebResource;

/// <summary>
/// Representa um Web Resource do Dataverse após mapeamento do OData.
/// </summary>
public class WebResource
{
    public string WebResourceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int TypeCode { get; set; }
    public bool IsManaged { get; set; }
    public DateTime ModifiedOn { get; set; }

    // ── Propriedades computadas ──────────────────────────────────────────

    public string TypeLabel => TypeCode switch
    {
        1 => "HTML",
        2 => "CSS",
        3 => "JavaScript",
        4 => "XML",
        5 => "PNG",
        6 => "JPG",
        7 => "GIF",
        8 => "XAP",
        9 => "XSL",
        10 => "ICO",
        11 => "SVG",
        12 => "RESX",
        _ => $"Tipo {TypeCode}",
    };

    public string ManagedLabel => IsManaged ? "Gerenciado" : "Não gerenciado";

    public string ModifiedOnFormatted =>
        ModifiedOn == default ? "—" : ModifiedOn.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
