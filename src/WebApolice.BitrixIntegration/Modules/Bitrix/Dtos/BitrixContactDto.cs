using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixContactDto
{
    [JsonPropertyName("ID")]
    public string? Id { get; set; }

    [JsonPropertyName("NAME")]
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> AdditionalFields { get; set; } = new();
}
