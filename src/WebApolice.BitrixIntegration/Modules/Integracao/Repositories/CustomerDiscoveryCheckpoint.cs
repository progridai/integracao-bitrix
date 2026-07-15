using System;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class CustomerDiscoveryCheckpoint
{
    public string ProcessName { get; set; } = "WEBAPOLICE_CUSTOMER_DISCOVERY";
    public DateTime LastModifiedAt { get; set; }
    public long LastEntityId { get; set; }
    public DateTime? LastStartedAt { get; set; }
    public DateTime? LastFinishedAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}
