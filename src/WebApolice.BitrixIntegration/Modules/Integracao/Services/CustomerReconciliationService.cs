using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix.Services;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Services;

public class CustomerReconciliationService
{
    private readonly WebApoliceCustomerRepository _webApoliceCustomerRepository;
    private readonly ICustomerCrmProvider _crmProvider;
    private readonly CustomerSyncRepository _syncRepository;
    private readonly BitrixContactService _contactService;
    private readonly BitrixCompanyService _companyService;

    public CustomerReconciliationService(
        WebApoliceCustomerRepository webApoliceCustomerRepository,
        ICustomerCrmProvider crmProvider,
        CustomerSyncRepository syncRepository,
        BitrixContactService contactService,
        BitrixCompanyService companyService)
    {
        _webApoliceCustomerRepository = webApoliceCustomerRepository;
        _crmProvider = crmProvider;
        _syncRepository = syncRepository;
        _contactService = contactService;
        _companyService = companyService;
    }

    public async Task<ReconciliationResult> ReconcileCustomerAsync(long clienteId, long pessoaId, CancellationToken cancellationToken)
    {
        var localRequest = await _webApoliceCustomerRepository.GetCustomerUpsertRequestAsync(clienteId, pessoaId, cancellationToken);
        if (localRequest == null)
        {
            throw new InvalidOperationException($"Cliente {clienteId} não encontrado no banco local.");
        }

        var result = new ReconciliationResult
        {
            ClienteId = clienteId,
            ExternalPublicId = localRequest.ExternalPublicId,
            CustomerType = localRequest.CustomerType.ToString()
        };

        var availableFields = await _crmProvider.GetAvailableFieldsAsync(localRequest.CustomerType, cancellationToken);
        
        // Em um cenário real com API conectada, faríamos a busca no Bitrix aqui.
        // Como o teste pediu para ser mockado e só estruturar:
        
        return result;
    }

    public async Task<ReconciliationSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        return new ReconciliationSummary
        {
            TotalSincronizados = 100, // Dummy
            TotalErros = 5,
            TotalPendentes = 10
        };
    }

    // Método utilitário para normalização requisitada na task
    public static string NormalizeString(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var onlyAlphanumeric = Regex.Replace(input, @"[^\w]", "");
        return onlyAlphanumeric.ToLowerInvariant();
    }
}

public class ReconciliationResult
{
    public long ClienteId { get; set; }
    public string? ExternalPublicId { get; set; }
    public string? CustomerType { get; set; }
    public bool IsInSync { get; set; }
    public List<string> Divergences { get; set; } = new();
}

public class ReconciliationSummary
{
    public int TotalSincronizados { get; set; }
    public int TotalErros { get; set; }
    public int TotalPendentes { get; set; }
}
