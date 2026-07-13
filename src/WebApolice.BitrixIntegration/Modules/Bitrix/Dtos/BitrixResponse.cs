using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixResponse<T> : BitrixErrorResponse
{
    [JsonPropertyName("result")]
    public T? Result { get; set; }
}
