using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Integracao.Repositories;
using WebApolice.BitrixIntegration.Infrastructure.Security;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("admin")]
[RequireAdminApiKey]
public class BitrixAdminController : ControllerBase
{
    private readonly WebApoliceCustomerRepository _customerRepository;

    public BitrixAdminController(WebApoliceCustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    [HttpGet("preview-customer-payload/{clienteId}")]
    public async Task<IActionResult> PreviewCustomerPayload(long clienteId, [FromQuery] long pessoaId, CancellationToken cancellationToken)
    {
        try
        {
            var request = await _customerRepository.GetCustomerUpsertRequestAsync(clienteId, pessoaId, cancellationToken);
            if (request == null)
            {
                return NotFound($"Cliente {clienteId} (Pessoa: {pessoaId}) não encontrado.");
            }

            return Ok(request);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
