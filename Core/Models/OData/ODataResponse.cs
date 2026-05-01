using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.OData
{
    public class ODataResponse<T>
    {
        [System.Text.Json.Serialization.JsonPropertyName("value")]
        public List<T> Value { get; set; } = [];

        [System.Text.Json.Serialization.JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; set; }
    }
}
