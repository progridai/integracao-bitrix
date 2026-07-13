using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix.Services;
using WebApolice.BitrixIntegration.Modules.Crm;
using WebApolice.BitrixIntegration.Modules.Crm.Exceptions;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public class BitrixCustomerCrmProvider : ICustomerCrmProvider
{
    private readonly BitrixProfileService _profileService;
    private readonly BitrixContactService _contactService;
    private readonly BitrixCompanyService _companyService;
    private readonly BitrixSettings _settings;
    private readonly ILogger<BitrixCustomerCrmProvider> _logger;

    public BitrixCustomerCrmProvider(
        BitrixProfileService profileService,
        BitrixContactService contactService,
        BitrixCompanyService companyService,
        IOptions<BitrixSettings> settings,
        ILogger<BitrixCustomerCrmProvider> logger)
    {
        _profileService = profileService;
        _contactService = contactService;
        _companyService = companyService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<CrmConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var profile = await _profileService.GetProfileAsync(cancellationToken);
            return new CrmConnectionTestResult
            {
                Success = true,
                Message = "Conexão com o Bitrix realizada com sucesso.",
                Profile = profile
            };
        }
        catch (Exception ex)
        {
            return new CrmConnectionTestResult
            {
                Success = false,
                Message = $"A configuração do Bitrix está incompleta ou inválida. Erro: {ex.Message}"
            };
        }
    }

    public async Task<CrmCustomerUpsertResult> UpsertCustomerAsync(
        CrmCustomerUpsertRequest request, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ExternalCustomerIdField))
            throw new CrmProviderException("O campo customizado ExternalCustomerIdField não está configurado.");

        _logger.LogInformation("Iniciando UpsertCustomerAsync para {ExternalId} - Tipo: {Type}", request.ExternalCustomerId, request.CustomerType);

        string? existingId = await FindCustomerExistingIdAsync(request, cancellationToken);

        if (existingId != null)
        {
            _logger.LogInformation("Cliente {ExternalId} encontrado no Bitrix com ID {CrmId}. Atualizando...", request.ExternalCustomerId, existingId);
            
            if (request.CustomerType == CrmCustomerType.Individual)
            {
                var fields = BitrixCustomerMapper.MapToContactFields(request, _settings);
                await _contactService.UpdateAsync(existingId, fields, cancellationToken);
            }
            else
            {
                var fields = BitrixCustomerMapper.MapToCompanyFields(request, _settings);
                await _companyService.UpdateAsync(existingId, fields, cancellationToken);
            }

            return new CrmCustomerUpsertResult
            {
                CrmId = existingId,
                WasCreated = false,
                WasUpdated = true
            };
        }
        else
        {
            _logger.LogInformation("Cliente {ExternalId} NÃO encontrado no Bitrix. Criando...", request.ExternalCustomerId);
            
            string newId;
            if (request.CustomerType == CrmCustomerType.Individual)
            {
                var fields = BitrixCustomerMapper.MapToContactFields(request, _settings);
                newId = await _contactService.AddAsync(fields, cancellationToken);
            }
            else
            {
                var fields = BitrixCustomerMapper.MapToCompanyFields(request, _settings);
                newId = await _companyService.AddAsync(fields, cancellationToken);
            }

            return new CrmCustomerUpsertResult
            {
                CrmId = newId,
                WasCreated = true,
                WasUpdated = false
            };
        }
    }

    private async Task<string?> FindCustomerExistingIdAsync(CrmCustomerUpsertRequest request, CancellationToken cancellationToken)
    {
        // 1. Busca pelo ID externo (ExternalCustomerIdField)
        var filterByExtId = new Dictionary<string, string>
        {
            { _settings.ExternalCustomerIdField, request.ExternalCustomerId }
        };

        if (request.CustomerType == CrmCustomerType.Individual)
        {
            var byExtId = await _contactService.ListByFilterAsync(filterByExtId, cancellationToken);
            if (byExtId.Any()) return byExtId.First().Id;

            // 2. Opcional: Buscar por CPF/CNPJ
            if (!string.IsNullOrWhiteSpace(_settings.DocumentField) && !string.IsNullOrWhiteSpace(request.Document))
            {
                var filterByDoc = new Dictionary<string, string>
                {
                    { _settings.DocumentField, request.Document }
                };
                var byDoc = await _contactService.ListByFilterAsync(filterByDoc, cancellationToken);
                if (byDoc.Any()) return byDoc.First().Id;
            }
        }
        else
        {
            var byExtId = await _companyService.ListByFilterAsync(filterByExtId, cancellationToken);
            if (byExtId.Any()) return byExtId.First().Id;

            if (!string.IsNullOrWhiteSpace(_settings.DocumentField) && !string.IsNullOrWhiteSpace(request.Document))
            {
                var filterByDoc = new Dictionary<string, string>
                {
                    { _settings.DocumentField, request.Document }
                };
                var byDoc = await _companyService.ListByFilterAsync(filterByDoc, cancellationToken);
                if (byDoc.Any()) return byDoc.First().Id;
            }
        }

        return null;
    }

    public async Task<IReadOnlyDictionary<string, object?>> GetAvailableFieldsAsync(
        CrmCustomerType customerType, 
        CancellationToken cancellationToken)
    {
        JsonDocument doc;
        if (customerType == CrmCustomerType.Individual)
        {
            doc = await _contactService.GetFieldsAsync(cancellationToken);
        }
        else
        {
            doc = await _companyService.GetFieldsAsync(cancellationToken);
        }

        var dict = new Dictionary<string, object?>();
        if (doc.RootElement.TryGetProperty("result", out var resultElement))
        {
            foreach (var property in resultElement.EnumerateObject())
            {
                dict.Add(property.Name, property.Value.ToString());
            }
        }

        return dict;
    }
}
