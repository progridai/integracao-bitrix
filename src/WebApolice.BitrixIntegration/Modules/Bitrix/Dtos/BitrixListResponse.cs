using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixListResponse<T> : BitrixErrorResponse
{
    [JsonPropertyName("result")]
    public List<T> Result { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
