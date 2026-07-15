using System.Collections.Generic;

namespace WebApolice.BitrixIntegration.Modules.Integracao;

public class SynchronizationSafetySettings
{
    public string Mode { get; set; } = "Disabled"; // Disabled, DryRun, Live
    public int? MaximumLiveBatchSize { get; set; } = 10;
    public bool AllowAllCustomers { get; set; } = false;
    public List<long> AllowedCustomerIds { get; set; } = new();
    public bool VerifyAfterWrite { get; set; } = true;
    public string AdminKey { get; set; } = string.Empty;
}
