using System;

namespace WebApolice.BitrixIntegration.Modules.Crm.Exceptions;

public class CrmProviderException : Exception
{
    public CrmProviderException(string message) : base(message)
    {
    }

    public CrmProviderException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
