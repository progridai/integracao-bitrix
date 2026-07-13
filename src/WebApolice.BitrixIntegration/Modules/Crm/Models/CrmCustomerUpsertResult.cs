namespace WebApolice.BitrixIntegration.Modules.Crm;

public class CrmCustomerUpsertResult
{
    public string CrmId { get; set; } = string.Empty;
    public bool WasCreated { get; set; }
    public bool WasUpdated { get; set; }
}
