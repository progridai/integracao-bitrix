namespace WebApolice.BitrixIntegration.Modules.Integracao;

public class CustomerSynchronizationSettings
{
    public bool Enabled { get; set; } = false;
    public int PollingIntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    public int MaxRetryAttempts { get; set; } = 5;
    public int RetryDelaySeconds { get; set; } = 60;
    public int ProcessingTimeoutMinutes { get; set; } = 10;
}
