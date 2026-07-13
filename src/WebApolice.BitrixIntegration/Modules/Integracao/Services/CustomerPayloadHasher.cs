using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Services;

public static class CustomerPayloadHasher
{
    public static string ComputeHash(CrmCustomerUpsertRequest request)
    {
        // Remove informaes voltadas a runtime ou meta-dados inteis para o Bitrix, para evitar falso positivo.
        // O Request j foi modelado sem esses dados tcnicos, ento serializamos ele de forma estrita.
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        var json = JsonSerializer.Serialize(request, options);

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = sha256.ComputeHash(bytes);

        var builder = new StringBuilder();
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
