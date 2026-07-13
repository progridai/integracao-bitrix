using System;
using System.Collections.Generic;

namespace WebApolice.BitrixIntegration.Modules.Crm;

public class CrmPhone
{
    public string Number { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class CrmEmail
{
    public string Address { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class CrmAddress
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class CrmCustomerUpsertRequest
{
    public CrmCustomerType CustomerType { get; set; }
    
    public string ExternalCustomerId { get; set; } = string.Empty;
    public string? ExternalPersonId { get; set; }
    public string? ExternalPublicId { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string? Document { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? Notes { get; set; }

    public List<CrmPhone> Phones { get; set; } = new();
    public List<CrmEmail> Emails { get; set; } = new();
    public CrmAddress? Address { get; set; }

    public Dictionary<string, object?> AdditionalFields { get; set; } = new();
}
