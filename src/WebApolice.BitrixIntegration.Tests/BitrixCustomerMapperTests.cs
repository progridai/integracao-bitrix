using System;
using System.Collections.Generic;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Crm;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class BitrixCustomerMapperTests
{
    private readonly BitrixSettings _settings;

    public BitrixCustomerMapperTests()
    {
        _settings = new BitrixSettings
        {
            ExternalCustomerIdField = "UF_EXT_ID",
            ContactDocumentField = "UF_DOC",
            CompanyDocumentField = "UF_DOC"
        };
    }

    [Fact]
    public void MapToContactFields_ShouldSplitNameAndMapFields()
    {
        var request = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Individual,
            Name = "John Doe Silva",
            ExternalCustomerId = "CUST123",
            Document = "12345678909",
            Phones = new List<CrmPhone> { new CrmPhone { Number = "11999999999" } }
        };

        var contact = BitrixCustomerMapper.MapToContactFields(request, _settings);

        Assert.Equal("John", contact.Name);
        Assert.Equal("Doe Silva", contact.LastName);
        Assert.Single(contact.Phones);
        Assert.Equal("11999999999", contact.Phones[0].Value);
        Assert.True(contact.AdditionalFields.ContainsKey("UF_EXT_ID"));
        Assert.Equal("CUST123", contact.AdditionalFields["UF_EXT_ID"]);
        Assert.True(contact.AdditionalFields.ContainsKey("UF_DOC"));
        Assert.Equal("12345678909", contact.AdditionalFields["UF_DOC"]);
    }

    [Fact]
    public void MapToCompanyFields_ShouldUseFullNameAndMapFields()
    {
        var request = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Company,
            Name = "Acme Corp Ltd",
            ExternalCustomerId = "COMP456",
            Document = "00000000000191"
        };

        var company = BitrixCustomerMapper.MapToCompanyFields(request, _settings);

        Assert.Equal("Acme Corp Ltd", company.Title);
        Assert.True(company.AdditionalFields.ContainsKey("UF_EXT_ID"));
        Assert.Equal("COMP456", company.AdditionalFields["UF_EXT_ID"]);
        Assert.True(company.AdditionalFields.ContainsKey("UF_DOC"));
        Assert.Equal("00000000000191", company.AdditionalFields["UF_DOC"]);
    }
}
