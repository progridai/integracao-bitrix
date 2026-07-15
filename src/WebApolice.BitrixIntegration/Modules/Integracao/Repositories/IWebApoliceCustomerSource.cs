using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public interface IWebApoliceCustomerSource
{
    Task<CustomerDiscoveryCheckpoint?> GetCheckpointAsync(CancellationToken cancellationToken);
    
    Task<CustomerDiscoveryCheckpoint> GetMaxCursorAsync(CancellationToken cancellationToken);
    
    Task ProcessBatchAsync(CustomerDiscoveryCheckpoint currentCheckpoint, int batchSize, CancellationToken cancellationToken);
    
    Task StartCheckpointAsync(CancellationToken cancellationToken);
    
    Task CreateInitialCheckpointAsync(System.DateTime lastModifiedAt, long lastEntityId, CancellationToken cancellationToken);
}
