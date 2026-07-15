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
                p.cpf_cnpj as Document,
                p.data_nascimento as BirthDate,
                (SELECT c.updated_at) as SourceModifiedAt,
                e.valor as EmailValue,
                t.valor as TelefoneValue,
                cel.valor as CelularValue
            FROM cadastro.cliente c
            INNER JOIN core.pessoa p ON c.pessoa_id = p.id
            LEFT JOIN LATERAL (
                SELECT pc.valor 
                FROM core.pessoa_contato pc
                WHERE pc.pessoa_id = p.id 
                  AND pc.tipo_contato = 'EMAIL'
                  AND pc.ativo = true
                ORDER BY pc.principal DESC, pc.id ASC
                LIMIT 1
            ) e ON true
            LEFT JOIN LATERAL (
                SELECT pc.valor 
                FROM core.pessoa_contato pc
                WHERE pc.pessoa_id = p.id 
                  AND pc.tipo_contato = 'TELEFONE'
                  AND pc.ativo = true
                ORDER BY pc.principal DESC, pc.id ASC
                LIMIT 1
            ) t ON true
            LEFT JOIN LATERAL (
                SELECT pc.valor 
                FROM core.pessoa_contato pc
                WHERE pc.pessoa_id = p.id 
                  AND pc.tipo_contato = 'CELULAR'
                  AND pc.ativo = true
                ORDER BY pc.principal DESC, pc.id ASC
                LIMIT 1
            ) cel ON true
            WHERE c.id = @ClienteId AND p.id = @PessoaId;
        ";

        var dto = await connection.QueryFirstOrDefaultAsync(new CommandDefinition(sql, new { ClienteId = clienteId, PessoaId = pessoaId }, cancellationToken: cancellationToken));
        if (dto == null) return null;

        var request = new CrmCustomerUpsertRequest
        {
            ExternalCustomerId = dto.externalcustomerid?.ToString() ?? clienteId.ToString(),
            ExternalPersonId = dto.externalpersonid?.ToString(),
            ExternalPublicId = dto.externalpublicid?.ToString(),
            SourceModifiedAt = dto.sourcemodifiedat,
            Name = dto.name ?? "Cliente Sem Nome",
            Document = dto.document,
            BirthDate = dto.birthdate,
            CustomerType = (dto.customertypestring?.ToString()?.ToUpper() == "J") ? CrmCustomerType.Company : CrmCustomerType.Individual
        };

        if (!string.IsNullOrWhiteSpace((string?)dto.emailvalue))
            request.Emails.Add(new CrmEmail { Address = (string)dto.emailvalue, Type = "EMAIL" });

        if (!string.IsNullOrWhiteSpace((string?)dto.telefonevalue))
            request.Phones.Add(new CrmPhone { Number = (string)dto.telefonevalue, Type = "TELEFONE" });
            
        if (!string.IsNullOrWhiteSpace((string?)dto.celularvalue))
            request.Phones.Add(new CrmPhone { Number = (string)dto.celularvalue, Type = "CELULAR" });

        return request;
    }

    public async Task<int> GetInvalidPublicIdsCountAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbFactory.CreateConnection();
        var sql = @"
            SELECT COUNT(*) 
            FROM integracao.bitrix_cliente_sync s
            JOIN cadastro.cliente c ON s.cliente_id = c.id
            WHERE c.public_id IS NULL OR c.public_id = '00000000-0000-0000-0000-000000000000'
               OR c.public_id IN (
                   SELECT public_id 
                   FROM cadastro.cliente 
                   WHERE public_id IS NOT NULL 
                   GROUP BY public_id 
                   HAVING COUNT(*) > 1
               )
        ";
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }
}
