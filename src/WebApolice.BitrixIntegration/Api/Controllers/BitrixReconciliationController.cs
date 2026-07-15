using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao.Services;
using WebApolice.BitrixIntegration.Infrastructure.Security;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("admin/reconciliation")]
[RequireAdminApiKey]
public class BitrixReconciliationController : ControllerBase
{
    private readonly CustomerReconciliationService _reconciliationService;

    public BitrixReconciliationController(CustomerReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpGet("customers/{clienteId}")]
    public async Task<IActionResult> ReconcileCustomer(long clienteId, [FromQuery] long pessoaId, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.ReconcileCustomerAsync(clienteId, pessoaId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _reconciliationService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }
}
