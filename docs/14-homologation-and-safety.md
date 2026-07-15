# 14. Homologação e Segurança (Dry Run & Live)

Este documento descreve os mecanismos de segurança e homologação implementados para garantir que a sincronização com o Bitrix CRM não comprometa dados em produção de forma não intencional.

## Modos de Execução (`SafetySettings.Mode`)

A aplicação suporta três modos de execução:

1. **Disabled**: A sincronização está totalmente desligada. O worker acorda, verifica o modo e retorna imediatamente, sem sequer acessar o repositório ou tentar reservar lotes de registros. Nenhuma operação de rede é realizada.
2. **DryRun**: A sincronização simula todo o fluxo de leitura, transformação (mapeamento), validação de regras de negócio (ambiguidade de origem, public_id duplicado, etc.), mas **intercepta** a chamada final para a API do Bitrix CRM. Ao invés de gravar no Bitrix, a aplicação gera um hash final do payload de criação ou atualização (`last_dry_run_hash`), e salva na tabela `integracao.bitrix_cliente_sync` que aquele registro passou com sucesso pelo teste, incluindo a data da simulação e o timestamp da versão dos dados na origem (`last_dry_run_source_modified_at`).
3. **Live**: O modo de produção real, que efetivamente fará mutações na base do Bitrix CRM, criando Contatos e Empresas.

## Preflight e Validações Antes da Escrita (Live)

O modo Live possui um *preflight check* rigoroso antes de reservar ou enviar lotes de dados:

- Valida as credenciais, Webhook URL e as configurações dos campos personalizados no Bitrix CRM (ex. `ContactDocumentField`, `CompanyDocumentField`, e chaves primárias do WebApolice).
- Conta a quantidade de registros na base do WebApolice que possuem o identificador (`public_id`) nulo, vazio ou duplicado e impede a execução caso alguma violação de integridade no identificador seja detectada.

## VerifyAfterWrite

O `VerifyAfterWrite` adiciona uma camada de consistência após a criação de novos registros no Bitrix. Se habilitado:
1. Um registro é enviado e criado no Bitrix via API.
2. O sistema imediatamente persiste o `bitrix_id` retornado pela API de criação em um comando atômico de banco de dados (`UpdateBitrixIdAsync`), ainda mantendo o status em `SINCRONIZANDO`.
3. Em seguida, a aplicação faz uma releitura do registro na API do Bitrix CRM para confirmar que o registro foi criado corretamente, indexado e que os metadados estão consistentes.
4. Somente após esta validação secundária o registro passa para o estado `SINCRONIZADO` final. Em caso de falha transitória (timeout) nesta etapa de releitura, o registro permanece com erro transitório, e na tentativa seguinte, o worker fará uma atualização (`Update`) usando o `bitrix_id` salvo previamente, prevenindo recriações (loop de duplicação).

## Evitando Loops e Reprocessamento em DryRun

Para evitar que o log do sistema encha infinitamente com operações "Dry Run" dos mesmos dados repetidamente, o repositório utiliza a coluna `last_dry_run_source_modified_at`. Apenas clientes cuja versão de modificação na base original (`source_modified_at`) é mais recente do que o timestamp da última simulação passarão pela reserva do lote de DryRun. Desta forma, após simular todos os pendentes, o worker repousa até que algum cliente sofra alguma alteração de dados.
