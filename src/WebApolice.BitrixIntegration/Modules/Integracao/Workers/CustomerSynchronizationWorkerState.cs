using System;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Workers;

public class CustomerSynchronizationWorkerState
{
    public bool IsRunning { get; set; }
    public DateTime? LastCycleStartedAt { get; set; }
    public DateTime? LastCycleFinishedAt { get; set; }
    public DateTime? LastSuccessfulCycleAt { get; set; }
    public string? LastError { get; set; }
}
