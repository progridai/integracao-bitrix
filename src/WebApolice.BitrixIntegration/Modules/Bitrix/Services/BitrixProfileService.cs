using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Modules.Bitrix.Dtos;

namespace WebApolice.BitrixIntegration.Modules.Bitrix.Services;

public class BitrixProfileService
{
    private readonly BitrixClient _client;

    public BitrixProfileService(BitrixClient client)
    {
        _client = client;
    }

    public async Task<BitrixProfileDto> GetProfileAsync(CancellationToken cancellationToken)
    {
        var response = await _client.ExecuteAsync<BitrixResponse<BitrixProfileDto>>(
            "profile.json", 
            null, 
            cancellationToken);

        return response.Result ?? new BitrixProfileDto();
    }
}
