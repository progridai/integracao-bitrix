namespace WebApolice.BitrixIntegration.Modules.Integracao;

public class CustomerDiscoverySettings
{
    public bool Enabled { get; set; } = false;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 500;
    public bool InitialLoadEnabled { get; set; } = false;
    public int InitialLoadBatchSize { get; set; } = 500;
    public int CheckpointOverlapSeconds { get; set; } = 5;
}
