using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApolice.BitrixIntegration.Modules.Crm;

public interface ICustomerCrmProvider
{
    Task<CrmConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken);
    
    Task<CrmCustomerUpsertResult> UpsertCustomerAsync(
        CrmCustomerUpsertRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object?>> GetAvailableFieldsAsync(
        CrmCustomerType customerType,
        CancellationToken cancellationToken);
}
