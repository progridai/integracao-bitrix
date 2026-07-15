namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public class BitrixSettings
{
    public string WebhookBaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool IgnoreSslValidation { get; set; }
    public string ExternalCustomerIdField { get; set; } = string.Empty;
    public string ContactDocumentField { get; set; } = string.Empty;
    public string CompanyDocumentField { get; set; } = string.Empty;
}
