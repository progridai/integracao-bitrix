namespace WebApolice.BitrixIntegration.Modules.Crm;

public class CrmConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public object? Profile { get; set; }
}
