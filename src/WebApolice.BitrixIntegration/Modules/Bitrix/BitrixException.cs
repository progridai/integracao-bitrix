using System;
using System.Net;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public class BitrixException : Exception
{
    public string ErrorCode { get; }
    public string ErrorDescription { get; }
    public HttpStatusCode? HttpStatusCode { get; }
    public string Method { get; }
    public string CorrelationId { get; }

    public BitrixException(
        string message,
        string errorCode,
        string errorDescription,
        HttpStatusCode? httpStatusCode,
        string method,
        string correlationId) : base(message)
    {
        ErrorCode = errorCode;
        ErrorDescription = errorDescription;
        HttpStatusCode = httpStatusCode;
        Method = method;
        CorrelationId = correlationId;
    }
}
