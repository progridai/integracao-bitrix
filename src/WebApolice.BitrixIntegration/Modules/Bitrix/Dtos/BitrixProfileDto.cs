using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

public class BitrixProfileDto
{
    [JsonPropertyName("ID")]
    public string? Id { get; set; }

    [JsonPropertyName("NAME")]
    public string? Name { get; set; }

    [JsonPropertyName("LAST_NAME")]
    public string? LastName { get; set; }

    [JsonPropertyName("EMAIL")]
    public string? Email { get; set; }
}
