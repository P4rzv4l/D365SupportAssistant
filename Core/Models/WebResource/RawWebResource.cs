using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.WebResource;

/// <summary>
/// DTO interno usado para deserializar a resposta OData da API webresourceset.
/// Não exposto fora de Core.
/// </summary>
internal class RawWebResource
{
    [JsonPropertyName("webresourceid")]
    public string? WebResourceId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayname")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("webresourcetype")]
    public int WebResourceType { get; set; }

    [JsonPropertyName("ismanaged")]
    public bool IsManaged { get; set; }

    [JsonPropertyName("modifiedon")]
    public string? ModifiedOn { get; set; }
}
