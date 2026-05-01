using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace D365Assistant.Core.Models.Auth
{
    public record WhoAmIResult(
        [property: System.Text.Json.Serialization.JsonPropertyName("UserId")] string UserId,
        [property: System.Text.Json.Serialization.JsonPropertyName("OrganizationId")] string OrganizationId,
        [property: System.Text.Json.Serialization.JsonPropertyName("BusinessUnitId")] string BusinessUnitId
    );
}
