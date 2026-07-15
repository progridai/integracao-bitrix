using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Models;

public class BitrixContactFields
{
    [JsonPropertyName("NAME")]
    public string? Name { get; set; }

    [JsonPropertyName("LAST_NAME")]
    public string? LastName { get; set; }

    [JsonPropertyName("BIRTHDATE")]
    public string? BirthDate { get; set; }

    [JsonPropertyName("COMMENTS")]
    public string? Comments { get; set; }

    [JsonPropertyName("ORIGINATOR_ID")]
    public string? OriginatorId { get; set; }

    [JsonPropertyName("ORIGIN_ID")]
    public string? OriginId { get; set; }

    [JsonPropertyName("ORIGIN_VERSION")]
    public string? OriginVersion { get; set; }

    [JsonPropertyName("PHONE")]
    public List<BitrixPhoneEmail> Phones { get; set; } = new();

    [JsonPropertyName("EMAIL")]
    public List<BitrixPhoneEmail> Emails { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, object?> AdditionalFields { get; set; } = new();
}

public class BitrixCompanyFields
{
    [JsonPropertyName("TITLE")]
    public string? Title { get; set; }

    [JsonPropertyName("COMMENTS")]
    public string? Comments { get; set; }

    [JsonPropertyName("ORIGINATOR_ID")]
    public string? OriginatorId { get; set; }

    [JsonPropertyName("ORIGIN_ID")]
    public string? OriginId { get; set; }

    [JsonPropertyName("ORIGIN_VERSION")]
    public string? OriginVersion { get; set; }

    [JsonPropertyName("PHONE")]
    public List<BitrixPhoneEmail> Phones { get; set; } = new();

    [JsonPropertyName("EMAIL")]
    public List<BitrixPhoneEmail> Emails { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, object?> AdditionalFields { get; set; } = new();
}

public class BitrixPhoneEmail
{
    [JsonPropertyName("VALUE")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("VALUE_TYPE")]
    public string ValueType { get; set; } = "WORK";
}
