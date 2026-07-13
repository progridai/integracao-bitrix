using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}
