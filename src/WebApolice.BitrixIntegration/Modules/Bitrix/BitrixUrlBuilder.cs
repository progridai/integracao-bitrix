using System;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public static class BitrixUrlBuilder
{
    public static string BuildUrl(string webhookBaseUrl, string method)
    {
        if (string.IsNullOrWhiteSpace(webhookBaseUrl))
            throw new InvalidOperationException("WebhookBaseUrl is not configured.");

        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("Method cannot be empty", nameof(method));

        var baseUri = webhookBaseUrl.EndsWith("/") ? webhookBaseUrl : $"{webhookBaseUrl}/";
        return $"{baseUri}{method}";
    }

    public static string MaskWebhookUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        // Tenta mascarar a parte do token que vem antes do mtodo na URL REST do Bitrix
        // Formato comum: https://dominio/rest/1/TOKEN123XYZ/method.json
        var parts = url.Split('/');
        if (parts.Length > 2)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                // Tokens normalmente tm mais de 10 caracteres
                if (part.Length > 10 && !part.Contains(".")) 
                {
                    parts[i] = $"{part.Substring(0, 3)}***{part.Substring(part.Length - 3)}";
                }
            }
        }
        return string.Join("/", parts);
    }
}
