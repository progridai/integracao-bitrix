using System;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Workers;

public class CustomerDiscoveryWorkerState
{
    public bool IsRunning { get; set; }
    public DateTime? LastCycleStartedAt { get; set; }
    public DateTime? LastCycleFinishedAt { get; set; }
    public DateTime? LastSuccessfulCycleAt { get; set; }
    public DateTime? LastCheckpointAt { get; set; }
    public long? LastCheckpointCustomerId { get; set; }
    public string? LastError { get; set; }
    public bool InitialLoadRunning { get; set; }
}
