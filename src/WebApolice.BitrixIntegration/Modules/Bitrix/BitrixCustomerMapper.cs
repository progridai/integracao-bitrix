using System;
using System.Collections.Generic;
using WebApolice.BitrixIntegration.Modules.Bitrix.Models;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public static class BitrixCustomerMapper
{
    public static BitrixContactFields MapToContactFields(
        CrmCustomerUpsertRequest request, 
        BitrixSettings settings)
    {
        var fields = new BitrixContactFields
        {
            Name = request.Name,
            LastName = string.Empty,
            BirthDate = request.BirthDate?.ToString("yyyy-MM-dd"),
            Comments = request.Notes
        };

        MapCommonFields(fields.Phones, fields.Emails, fields.AdditionalFields, request, settings);
        
        return fields;
    }

    public static BitrixCompanyFields MapToCompanyFields(
        CrmCustomerUpsertRequest request, 
        BitrixSettings settings)
    {
        var fields = new BitrixCompanyFields
        {
            Title = request.Name,
            Comments = request.Notes
        };

        MapCommonFields(fields.Phones, fields.Emails, fields.AdditionalFields, request, settings);

        return fields;
    }

    private static void MapCommonFields(
        List<BitrixPhoneEmail> phones, 
        List<BitrixPhoneEmail> emails, 
        Dictionary<string, object?> additionalFields,
        CrmCustomerUpsertRequest request, 
        BitrixSettings settings)
    {
        foreach (var phone in request.Phones)
        {
            phones.Add(new BitrixPhoneEmail { Value = phone.Number, ValueType = string.IsNullOrWhiteSpace(phone.Type) ? "WORK" : phone.Type });
        }

        foreach (var email in request.Emails)
        {
            emails.Add(new BitrixPhoneEmail { Value = email.Address, ValueType = string.IsNullOrWhiteSpace(email.Type) ? "WORK" : email.Type });
        }

        if (!string.IsNullOrWhiteSpace(settings.ExternalCustomerIdField) && !string.IsNullOrWhiteSpace(request.ExternalCustomerId))
        {
            additionalFields[settings.ExternalCustomerIdField] = request.ExternalCustomerId;
        }

        additionalFields["ORIGINATOR_ID"] = "WEBAPOLICE";
        additionalFields["ORIGIN_ID"] = request.ExternalPublicId;
        additionalFields["ORIGIN_VERSION"] = request.SourceModifiedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ");

        // Set document fields based on the configured mapping
        if (request.CustomerType == CrmCustomerType.Individual && !string.IsNullOrWhiteSpace(settings.ContactDocumentField) && !string.IsNullOrWhiteSpace(request.Document))
        {
            additionalFields[settings.ContactDocumentField] = request.Document;
        }
        else if (request.CustomerType == CrmCustomerType.Company && !string.IsNullOrWhiteSpace(settings.CompanyDocumentField) && !string.IsNullOrWhiteSpace(request.Document))
        {
            additionalFields[settings.CompanyDocumentField] = request.Document;
        }

        additionalFields[BitrixCustomFields.WebApolicePessoaId] = request.ExternalPersonId;
        additionalFields[BitrixCustomFields.WebApolicePublicId] = request.ExternalPublicId;
        additionalFields[BitrixCustomFields.Origem] = "WebApolice";

        foreach (var kvp in request.AdditionalFields)
        {
            additionalFields[kvp.Key] = kvp.Value;
        }
    }

    public static string SplitFirstName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : fullName;
    }

    public static string SplitLastName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return string.Empty;
        var parts = fullName.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1] : string.Empty;
    }
}
