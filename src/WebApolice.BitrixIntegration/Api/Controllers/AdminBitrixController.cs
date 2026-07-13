using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Api.Controllers;

[ApiController]
[Route("admin/bitrix")]
public class AdminBitrixController : ControllerBase
{
    private readonly ICustomerCrmProvider _crmProvider;

    public AdminBitrixController(ICustomerCrmProvider crmProvider)
    {
        _crmProvider = crmProvider;
    }

    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var result = await _crmProvider.TestConnectionAsync(cancellationToken);
        if (result.Success)
        {
            return Ok(result);
        }
        
        return BadRequest(new
        {
            success = false,
            message = result.Message
        });
    }

    [HttpGet("fields")]
    public async Task<IActionResult> GetFields(
        [FromQuery] CrmCustomerType customerType = CrmCustomerType.Individual,
        CancellationToken cancellationToken = default)
    {
        var fields = await _crmProvider.GetAvailableFieldsAsync(customerType, cancellationToken);
        return Ok(fields);
    }

    [HttpPost("preview-customer-payload")]
    public IActionResult PreviewCustomerPayload(
        [FromBody] CrmCustomerUpsertRequest request,
        [FromServices] Microsoft.Extensions.Options.IOptions<BitrixSettings> settings)
    {
        // Apenas para testes de mapeamento, no grava nada.
        if (request.CustomerType == CrmCustomerType.Individual)
        {
            var contactFields = BitrixCustomerMapper.MapToContactFields(request, settings.Value);
            return Ok(contactFields);
        }
        else
        {
            var companyFields = BitrixCustomerMapper.MapToCompanyFields(request, settings.Value);
            return Ok(companyFields);
        }
    }
}
