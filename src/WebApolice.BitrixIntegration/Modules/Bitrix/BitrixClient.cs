using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

namespace WebApolice.BitrixIntegration.Modules.Bitrix;

public class BitrixClient
{
    private readonly HttpClient _httpClient;
    private readonly BitrixSettings _settings;
    private readonly ILogger<BitrixClient> _logger;
    private readonly DbConnectionFactory _dbConnectionFactory;

    public BitrixClient(
        HttpClient httpClient,
        IOptions<BitrixSettings> settings,
        ILogger<BitrixClient> logger,
        DbConnectionFactory dbConnectionFactory)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        _dbConnectionFactory = dbConnectionFactory;

        if (_settings.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }
    }

    public async Task<JsonDocument> ExecuteAsync(
        string method,
        object? payload,
        CancellationToken cancellationToken)
    {
        return await ExecuteAsync<JsonDocument>(method, payload, cancellationToken);
    }

    public async Task<T> ExecuteAsync<T>(
        string method,
        object? payload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.WebhookBaseUrl))
        {
            throw new InvalidOperationException("Bitrix WebhookBaseUrl is not configured.");
        }

        var url = BitrixUrlBuilder.BuildUrl(_settings.WebhookBaseUrl, method);
        var correlationId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();
        
        string requestJson = payload != null ? JsonSerializer.Serialize(payload) : "{}";
        string maskedUrl = BitrixUrlBuilder.MaskWebhookUrl(url);

        _logger.LogInformation("Bitrix Request [{Method}]. CorrelationId: {CorrelationId}", method, correlationId);

        HttpResponseMessage? response = null;
        string? responseString = null;
        string logStatus = "SUCESSO";
        string? errorLog = null;

        try
        {
            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            response = await _httpClient.PostAsync(url, content, cancellationToken);
            
            responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logStatus = "ERRO";
                errorLog = $"HTTP Error {(int)response.StatusCode}";
                throw new BitrixException(
                    "Error executing Bitrix request", 
                    errorLog, 
                    responseString, 
                    response.StatusCode, 
                    method, 
                    correlationId.ToString());
            }

            var errorCheck = JsonSerializer.Deserialize<BitrixErrorResponse>(responseString);
            if (errorCheck != null && !string.IsNullOrWhiteSpace(errorCheck.Error))
            {
                logStatus = "ERRO";
                errorLog = $"Bitrix Error: {errorCheck.Error}";
                throw new BitrixException(
                    "Bitrix returned an error", 
                    errorCheck.Error, 
                    errorCheck.ErrorDescription ?? string.Empty, 
                    response.StatusCode, 
                    method, 
                    correlationId.ToString());
            }

            var result = JsonSerializer.Deserialize<T>(responseString);
            if (result == null)
            {
                throw new InvalidOperationException("Failed to deserialize Bitrix response.");
            }

            return result;
        }
        catch (Exception ex) when (!(ex is BitrixException))
        {
            logStatus = "ERRO";
            errorLog = ex.Message;
            throw new BitrixException(
                "Unexpected error during Bitrix request", 
                "UNEXPECTED_ERROR", 
                ex.Message, 
                response?.StatusCode, 
                method, 
                correlationId.ToString());
        }
        finally
        {
            stopwatch.Stop();
            await LogToDatabaseAsync(
                correlationId, 
                method, 
                maskedUrl, 
                requestJson, 
                response != null ? (int)response.StatusCode : null, 
                responseString, 
                logStatus, 
                errorLog, 
                (int)stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task LogToDatabaseAsync(
        Guid correlationId,
        string method,
        string requestUrl,
        string requestJson,
        int? responseStatusCode,
        string? responseJson,
        string status,
        string? erro,
        int duracaoMs)
    {
        try
        {
            using var connection = _dbConnectionFactory.CreateConnection();
            var sql = @"
                INSERT INTO integracao.bitrix_log (
                    correlation_id, operacao, status, request_url, 
                    response_status_code, erro, duracao_ms, created_at
                ) VALUES (
                    @CorrelationId, @Operacao, @Status, @RequestUrl, 
                    @ResponseStatusCode, @Erro, @DuracaoMs, NOW()
                )";
                
            // LGPD: No salvamos payload completo no BD para evitar expor dados sensveis indiscriminadamente.
            // A operao gravada  o mtodo, assim podemos auditar chamadas.
            
            await connection.ExecuteAsync(sql, new
            {
                CorrelationId = correlationId,
                Operacao = method,
                Status = status,
                RequestUrl = requestUrl,
                ResponseStatusCode = responseStatusCode,
                Erro = erro,
                DuracaoMs = duracaoMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write Bitrix log to database. CorrelationId: {CorrelationId}", correlationId);
        }
    }
}
