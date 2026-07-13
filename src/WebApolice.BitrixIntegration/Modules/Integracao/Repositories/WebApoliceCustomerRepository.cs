using Dapper;
using System.Threading;
using System.Threading.Tasks;
using WebApolice.BitrixIntegration.Infrastructure.Database;
using WebApolice.BitrixIntegration.Modules.Crm;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class WebApoliceCustomerRepository
{
    private readonly DbConnectionFactory _dbFactory;

    public WebApoliceCustomerRepository(DbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public virtual async Task<CrmCustomerUpsertRequest?> GetCustomerUpsertRequestAsync(long clienteId, long pessoaId, CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        // ATENO: Consulta simplificada assumindo que cadastro.cliente e core.pessoa possuem campos como nome, tipo, documento.
        // Adaptar a query exatamente  estrutura real dessas tabelas na Parte 4.
        var sql = @"
            SELECT 
                c.id as ExternalCustomerId,
                p.id as ExternalPersonId,
                c.public_id as ExternalPublicId,
                p.tipo as CustomerTypeString,
                p.nome as Name,
                p.cpf_cnpj as Document
            FROM cadastro.cliente c
            INNER JOIN core.pessoa p ON c.pessoa_id = p.id
            WHERE c.id = @ClienteId AND p.id = @PessoaId;
        ";

        var dto = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(sql, new { ClienteId = clienteId, PessoaId = pessoaId }, cancellationToken: cancellationToken));
        if (dto == null) return null;

        var request = new CrmCustomerUpsertRequest
        {
            ExternalCustomerId = dto.externalcustomerid?.ToString() ?? clienteId.ToString(),
            ExternalPersonId = dto.externalpersonid?.ToString(),
            ExternalPublicId = dto.externalpublicid?.ToString(),
            Name = dto.name ?? "Cliente Sem Nome",
            Document = dto.document,
            CustomerType = (dto.customertypestring?.ToString()?.ToUpper() == "J") ? CrmCustomerType.Company : CrmCustomerType.Individual
        };

        // TODO: Popular Phones, Emails, Address (ficar para uma query de joins na estrutura completa).

        return request;
    }
}
