using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixCompanyDto
{
    [JsonPropertyName("ID")]
    public string? Id { get; set; }

    [JsonPropertyName("TITLE")]
    public string? Title { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object> AdditionalFields { get; set; } = new();
}
