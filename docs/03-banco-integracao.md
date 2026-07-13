# Banco de Dados da Integração

Este projeto utiliza o PostgreSQL.

## Schema Integracao
Para não misturar as tabelas da integração com as do sistema principal, todas as tabelas exclusivas deste projeto residem em um schema separado chamado `integracao`.

## Schemas Core e Cadastro
**IMPORTANTE:** As tabelas dos schemas `core` e `cadastro` são consideradas "read-only" para o contexto da integração. Este projeto **não deve alterar dados nestas tabelas**. Toda a informação necessária será lida dessas tabelas, mas persistida/mapeada nas tabelas do schema `integracao`.

## DbUp e Migrations
A gestão das alterações do banco é feita utilizando o **DbUp**. 
As migrations são controladas no código (pasta `Database/Migrations`). O DbUp executa todos os scripts que nunca foram rodados com base numa tabela de histórico que ele cria automaticamente.

Para rodar as migrations na inicialização, a propriedade `Integration:Database:RunMigrationsOnStartup` deve estar configurada como `true` no appsettings.

## Tabelas
- `bitrix_cliente_sync`: Tabela principal que mantém a rastreabilidade do envio de cada cliente/pessoa para o Bitrix (mapeia IDs do WebApólice para IDs do Bitrix). Armazena status, tentativas e JSONs enviados.
- `bitrix_log`: Tabela para log de todas as operações e requisições feitas ao Bitrix, incluindo status HTTP, duração e payloads de erro ou sucesso.
- `bitrix_worker_execucao`: Mantém o registro histórico de cada ciclo de execução do worker (horário de início, fim, e total de registros processados/ignorados/erros).
