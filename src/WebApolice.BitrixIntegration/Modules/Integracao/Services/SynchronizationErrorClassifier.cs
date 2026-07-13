using System;
using System.Net;
using WebApolice.BitrixIntegration.Modules.Bitrix;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Services;

public enum SynchronizationErrorType
{
    Transient,
    Permanent,
    Configuration,
    Cancelled
}

public static class SynchronizationErrorClassifier
{
    public static SynchronizationErrorType Classify(Exception ex)
    {
        if (ex is OperationCanceledException || ex is System.Threading.Tasks.TaskCanceledException)
        {
            return SynchronizationErrorType.Cancelled;
        }

        if (ex is BitrixException bitrixEx)
        {
            // Erros Globais de Configurao ou Autenticao
            if (bitrixEx.HttpStatusCode == HttpStatusCode.Unauthorized ||
                bitrixEx.HttpStatusCode == HttpStatusCode.Forbidden ||
                bitrixEx.ErrorCode == "ERROR_OAUTH" ||
                bitrixEx.ErrorCode == "insufficient_scope" ||
                bitrixEx.ErrorCode == "ERROR_METHOD_NOT_FOUND") // Falha na URL do Webhook
            {
                return SynchronizationErrorType.Configuration;
            }

            // Erros de Rate Limit e Timeout (Transitrios)
            if (bitrixEx.HttpStatusCode == HttpStatusCode.TooManyRequests ||
                bitrixEx.HttpStatusCode == HttpStatusCode.RequestTimeout ||
                bitrixEx.HttpStatusCode == HttpStatusCode.BadGateway ||
                bitrixEx.HttpStatusCode == HttpStatusCode.ServiceUnavailable ||
                bitrixEx.HttpStatusCode == HttpStatusCode.GatewayTimeout ||
                bitrixEx.HttpStatusCode == HttpStatusCode.InternalServerError)
            {
                return SynchronizationErrorType.Transient;
            }

            // Erros de validao ou negcio do Bitrix
            if (bitrixEx.HttpStatusCode == HttpStatusCode.BadRequest || 
                !string.IsNullOrWhiteSpace(bitrixEx.ErrorCode))
            {
                return SynchronizationErrorType.Permanent;
            }
        }

        if (ex is System.Net.Sockets.SocketException || 
            ex is System.Net.Http.HttpRequestException ||
            ex is TimeoutException)
        {
            return SynchronizationErrorType.Transient;
        }

        if (ex is ArgumentException || ex is InvalidOperationException)
        {
            // Se faltar um campo obrigatrio no mapeamento
            if (ex.Message.Contains("configurado"))
                return SynchronizationErrorType.Configuration;

            return SynchronizationErrorType.Permanent;
        }

        return SynchronizationErrorType.Transient;
    }
}
