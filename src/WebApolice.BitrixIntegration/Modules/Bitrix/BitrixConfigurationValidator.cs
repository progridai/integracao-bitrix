using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WebApolice.BitrixIntegration.Modules.Bitrix.Services;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public class BitrixConfigurationValidator : IBitrixConfigurationValidator
{
    private readonly BitrixContactService _contactService;
    private readonly BitrixCompanyService _companyService;
    private readonly BitrixSettings _settings;

    public BitrixConfigurationValidator(
        BitrixContactService contactService,
        BitrixCompanyService companyService,
        IOptions<BitrixSettings> settings)
    {
        _contactService = contactService;
        _companyService = companyService;
        _settings = settings.Value;
    }

    public async Task<List<string>> ValidateAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        var contactFieldsDoc = await _contactService.GetFieldsAsync(cancellationToken);
        var companyFieldsDoc = await _companyService.GetFieldsAsync(cancellationToken);

        var contactFields = GetFieldNames(contactFieldsDoc);
        var companyFields = GetFieldNames(companyFieldsDoc);

        var requiredBaseFields = new[] { "ORIGINATOR_ID", "ORIGIN_ID", "ORIGIN_VERSION", "PHONE", "EMAIL" };

        foreach (var req in requiredBaseFields)
        {
            if (!contactFields.Contains(req)) errors.Add($"Campo {req} não encontrado no metadata de Contact.");
            if (!companyFields.Contains(req)) errors.Add($"Campo {req} não encontrado no metadata de Company.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ContactDocumentField))
        {
            errors.Add("ContactDocumentField não configurado no appsettings.");
        }
        else if (!contactFields.Contains(_settings.ContactDocumentField))
        {
            errors.Add($"Campo {_settings.ContactDocumentField} não encontrado no metadata de Contact.");
        }

        if (string.IsNullOrWhiteSpace(_settings.CompanyDocumentField))
        {
            errors.Add("CompanyDocumentField não configurado no appsettings.");
        }
        else if (!companyFields.Contains(_settings.CompanyDocumentField))
        {
            errors.Add($"Campo {_settings.CompanyDocumentField} não encontrado no metadata de Company.");
        }

        return errors;
    }

    private HashSet<string> GetFieldNames(System.Text.Json.JsonDocument doc)
    {
        var names = new HashSet<string>();
        if (doc.RootElement.TryGetProperty("result", out var resultElement))
        {
            foreach (var property in resultElement.EnumerateObject())
            {
                names.Add(property.Name);
            }
        }
        return names;
    }
}
