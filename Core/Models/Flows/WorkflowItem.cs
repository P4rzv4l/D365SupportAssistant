// =============================================================================
//  WorkflowItem.cs — Modelo de um Workflow/Flow do Dynamics
// =============================================================================

namespace D365Assistant.Core.Models.Flows;

public class WorkflowItem
{
    public string WorkflowId { get; set; } = "";
    public string Name { get; set; } = "";
    public int StateCode { get; set; }   // 0=inativo, 1=ativo
    public int Category { get; set; }   // 0=Clássico, 2=Regra, 5=Cloud
    public string OwnerId { get; set; } = "";
    public string OwnerName { get; set; } = "";
    public string? ClientData { get; set; }
    public string? Xaml { get; set; }

    // ── Computed ──────────────────────────────────────────────────────────────
    public bool IsActive => StateCode == 1;
    public string StatusLabel => IsActive ? "Ativo" : "Inativo";

    public string CategoryLabel => Category switch
    {
        5 => "Cloud Flow",
        0 => "Workflow Clássico",
        2 => "Regra de Negócio",
        _ => $"Categoria {Category}",
    };

    public bool HasHttpsTrigger =>
        Category == 5 &&
        (ClientData?.Contains("\"type\":\"request\"") == true ||
         ClientData?.Contains("\"kind\":\"http\"") == true);
}

/// <summary>DTO para deserialização da resposta OData.</summary>
public class RawWorkflow
{
    [System.Text.Json.Serialization.JsonPropertyName("workflowid")]
    public string? WorkflowId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string? Name { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("statecode")]
    public int StateCode { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("category")]
    public int Category { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("_ownerid_value")]
    public string? OwnerId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("_ownerid_value@OData.Community.Display.V1.FormattedValue")]
    public string? OwnerName { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("clientdata")]
    public string? ClientData { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("xaml")]
    public string? Xaml { get; set; }
}