using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;
using WebApolice.BitrixIntegration.Modules.Bitrix.Models;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Services;

public class BitrixCompanyService
{
    private readonly BitrixClient _client;

    public BitrixCompanyService(BitrixClient client)
    {
        _client = client;
    }

    public async Task<JsonDocument> GetFieldsAsync(CancellationToken cancellationToken)
    {
        return await _client.ExecuteAsync("crm.company.fields.json", null, cancellationToken);
    }

    public async Task<BitrixCompanyDto?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var payload = new { id };
        var response = await _client.ExecuteAsync<BitrixResponse<BitrixCompanyDto>>(
            "crm.company.get.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<IReadOnlyList<BitrixCompanyDto>> ListAsync(
        int start, 
        int limit, 
        CancellationToken cancellationToken)
    {
        var payload = new 
        { 
            start = start,
            select = new[] { "ID", "TITLE", "*" }
        };

        var response = await _client.ExecuteAsync<BitrixListResponse<BitrixCompanyDto>>(
            "crm.company.list.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<IReadOnlyList<BitrixCompanyDto>> ListByFilterAsync(
        object filter,
        CancellationToken cancellationToken)
    {
        var payload = new 
        { 
            filter = filter,
            select = new[] { "ID", "TITLE", "*" }
        };

        var response = await _client.ExecuteAsync<BitrixListResponse<BitrixCompanyDto>>(
            "crm.company.list.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<string> AddAsync(BitrixCompanyFields fields, CancellationToken cancellationToken)
    {
        var payload = new { fields = fields };
        var response = await _client.ExecuteAsync<BitrixResponse<int>>(
            "crm.company.add.json", 
            payload, 
            cancellationToken);

        return response.Result.ToString();
    }

    public async Task<bool> UpdateAsync(string id, BitrixCompanyFields fields, CancellationToken cancellationToken)
    {
        var payload = new 
        { 
            id = id,
            fields = fields 
        };
        var response = await _client.ExecuteAsync<BitrixResponse<bool>>(
            "crm.company.update.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }
}
