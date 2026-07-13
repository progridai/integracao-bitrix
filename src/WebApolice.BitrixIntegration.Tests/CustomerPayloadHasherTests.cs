using System;
using System.Collections.Generic;
using WebApolice.BitrixIntegration.Modules.Crm;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using Xunit;

namespace WebApolice.BitrixIntegration.Tests;

public class CustomerPayloadHasherTests
{
    [Fact]
    public void ComputeHash_ShouldBeDeterministic()
    {
        var request1 = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Individual,
            ExternalCustomerId = "123",
            Name = "John",
            Document = "12345678909",
            Phones = new List<CrmPhone> { new CrmPhone { Number = "11999999999" } }
        };

        var request2 = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Individual,
            ExternalCustomerId = "123",
            Name = "John",
            Document = "12345678909",
            Phones = new List<CrmPhone> { new CrmPhone { Number = "11999999999" } }
        };

        var hash1 = CustomerPayloadHasher.ComputeHash(request1);
        var hash2 = CustomerPayloadHasher.ComputeHash(request2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_WhenDataChanges_ShouldBeDifferent()
    {
        var request1 = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Individual,
            ExternalCustomerId = "123",
            Name = "John",
        };

        var request2 = new CrmCustomerUpsertRequest
        {
            CustomerType = CrmCustomerType.Individual,
            ExternalCustomerId = "123",
            Name = "John Doe", // Change
        };

        var hash1 = CustomerPayloadHasher.ComputeHash(request1);
        var hash2 = CustomerPayloadHasher.ComputeHash(request2);

        Assert.NotEqual(hash1, hash2);
    }
}
