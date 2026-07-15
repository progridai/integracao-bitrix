using System;

namespace WebApolice.BitrixIntegration.Modules.Integracao.Repositories;

public class WebApoliceCustomerChange
{
    public long ClienteId { get; set; }
    public long PessoaId { get; set; }
    public DateTime SourceModifiedAt { get; set; }
}
