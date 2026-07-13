using System;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class CustomerSyncRecord
{
    public long Id { get; set; }
    public long ClienteId { get; set; }
    public long PessoaId { get; set; }
    
    // Identificadores de negcio no WebApolice
    public string? DocumentoPrincipal { get; set; }
    public Guid? ClientePublicId { get; set; }
    public Guid? PessoaPublicId { get; set; }

    public string? BitrixEntityType { get; set; }
    public string? BitrixId { get; set; }
    public string Status { get; set; } = string.Empty;

    public int Tentativas { get; set; }
    public string? UltimoErro { get; set; }

    public DateTime? NextAttemptAt { get; set; }
    public Guid? ProcessingToken { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public string? PayloadHash { get; set; }
}
