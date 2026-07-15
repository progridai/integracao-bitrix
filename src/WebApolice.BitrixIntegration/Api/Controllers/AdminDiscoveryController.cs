using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao;
using WebApolice.BitrixIntegration.Modules.Integracao.Workers;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("admin/discovery")]
public class AdminDiscoveryController : ControllerBase
{
    private readonly CustomerDiscoveryWorkerState _workerState;
    private readonly CustomerDiscoverySettings _settings;
    private readonly WebApoliceCustomerDiscoveryWorker? _discoveryWorker;

    public AdminDiscoveryController(
        CustomerDiscoveryWorkerState workerState,
        Microsoft.Extensions.Options.IOptions<CustomerDiscoverySettings> settings,
        System.Collections.Generic.IEnumerable<Microsoft.Extensions.Hosting.IHostedService> hostedServices)
    {
        _workerState = workerState;
        _settings = settings.Value;
        
        foreach (var service in hostedServices)
        {
            if (service is WebApoliceCustomerDiscoveryWorker dw)
            {
                _discoveryWorker = dw;
                break;
            }
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            enabled = _settings.Enabled,
            running = _workerState.IsRunning,
            initialLoadEnabled = _settings.InitialLoadEnabled,
            lastCycleStartedAt = _workerState.LastCycleStartedAt,
            lastCycleFinishedAt = _workerState.LastCycleFinishedAt,
            lastSuccessfulCycleAt = _workerState.LastSuccessfulCycleAt,
            lastCheckpointAt = _workerState.LastCheckpointAt,
            lastCheckpointCustomerId = _workerState.LastCheckpointCustomerId,
            lastError = _workerState.LastError
        });
    }

    [HttpPost("run-once")]
    public IActionResult RunOnce()
    {
        if (_discoveryWorker == null)
            return BadRequest(new { success = false, message = "O worker de descoberta no est ativo no container de injeo." });

        if (!_settings.Enabled)
            return BadRequest(new { success = false, message = "A descoberta de clientes est desabilitada na configurao." });

        _discoveryWorker.RequestRunOnce();

        return Ok(new { success = true, message = "Sinal enviado para executar a descoberta em segundo plano." });
    }
}
