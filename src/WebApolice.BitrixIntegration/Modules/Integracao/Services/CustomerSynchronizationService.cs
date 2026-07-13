using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Services;

public class CustomerSynchronizationService
{
    private readonly ICustomerCrmProvider _crmProvider;
    private readonly ILogger<CustomerSynchronizationService> _logger;

    public CustomerSynchronizationService(
        ICustomerCrmProvider crmProvider,
        ILogger<CustomerSynchronizationService> logger)
    {
        _crmProvider = crmProvider;
        _logger = logger;
    }

    public async Task<CrmCustomerUpsertResult> SynchronizeCustomerAsync(
        CrmCustomerUpsertRequest request, 
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Iniciando fluxo de sincronizacao do cliente WebApolice: {ExternalId}", request.ExternalCustomerId);

        if (string.IsNullOrWhiteSpace(request.ExternalCustomerId))
        {
            throw new ArgumentException("O ID externo do WebApolice  obrigatrio para a sincronizacao.", nameof(request.ExternalCustomerId));
        }

        try
        {
            // O ICustomerCrmProvider encapsula a logica de idempotencia (buscar, criar ou atualizar).
            var result = await _crmProvider.UpsertCustomerAsync(request, cancellationToken);

            _logger.LogInformation("Cliente {ExternalId} sincronizado com sucesso. CRM ID: {CrmId}. (Created: {Created}, Updated: {Updated})", 
                request.ExternalCustomerId, result.CrmId, result.WasCreated, result.WasUpdated);

            // TODO: Atualizar tabela de controle de integracao (bitrix_cliente_sync) com o result.CrmId e status SINCRONIZADO.
            // Essa tabela ja foi criada na Parte 1, a conexo real sera na Parte 4.

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na sincronizacao do cliente WebApolice {ExternalId}", request.ExternalCustomerId);
            
            // TODO: Registrar falha na tabela de controle (bitrix_cliente_sync) com status ERRO.
            throw;
        }
    }
}
