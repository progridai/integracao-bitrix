using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;
using WebApolice.BitrixIntegration.Modules.Bitrix.Models;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Services;

public class BitrixContactService
{
    private readonly BitrixClient _client;

    public BitrixContactService(BitrixClient client)
    {
        _client = client;
    }

    public async Task<JsonDocument> GetFieldsAsync(CancellationToken cancellationToken)
    {
        return await _client.ExecuteAsync("crm.contact.fields.json", null, cancellationToken);
    }

    public async Task<BitrixContactDto?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var payload = new { id };
        var response = await _client.ExecuteAsync<BitrixResponse<BitrixContactDto>>(
            "crm.contact.get.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<IReadOnlyList<BitrixContactDto>> ListAsync(
        int start, 
        int limit, 
        CancellationToken cancellationToken)
    {
        // O Bitrix usa start
        var payload = new 
        { 
            start = start,
            select = new[] { "ID", "NAME", "LAST_NAME", "*" }
        };

        var response = await _client.ExecuteAsync<BitrixListResponse<BitrixContactDto>>(
            "crm.contact.list.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<IReadOnlyList<BitrixContactDto>> ListByFilterAsync(
        object filter,
        CancellationToken cancellationToken)
    {
        var payload = new 
        { 
            filter = filter,
            select = new[] { "ID", "NAME", "LAST_NAME", "*" }
        };

        var response = await _client.ExecuteAsync<BitrixListResponse<BitrixContactDto>>(
            "crm.contact.list.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }

    public async Task<string> AddAsync(BitrixContactFields fields, CancellationToken cancellationToken)
    {
        var payload = new { fields = fields };
        var response = await _client.ExecuteAsync<BitrixResponse<int>>(
            "crm.contact.add.json", 
            payload, 
            cancellationToken);

        return response.Result.ToString();
    }

    public async Task<bool> UpdateAsync(string id, BitrixContactFields fields, CancellationToken cancellationToken)
    {
        var payload = new 
        { 
            id = id,
            fields = fields 
        };
        var response = await _client.ExecuteAsync<BitrixResponse<bool>>(
            "crm.contact.update.json", 
            payload, 
            cancellationToken);

        return response.Result;
    }
}
