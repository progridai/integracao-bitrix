# 15. Reconciliação e Produção (Production Rollout)

Este documento descreve as ferramentas e o plano de lançamento para produção da integração Bitrix-WebApolice, bem como o processo de reconciliação de dados ao longo do tempo.

## API de Administração e Reconciliação

Para viabilizar monitoramento e auditoria em produção, a aplicação disponibiliza endpoints sob a rota `/admin`, todos protegidos pelo cabeçalho `X-Admin-Key` configurado pelo `appsettings.json`.

1. **`GET /admin/preview-customer-payload/{clienteId}`**: Fornece uma simulação ao vivo do payload de um determinado cliente antes de qualquer sincronização com o Bitrix, permitindo que a equipe de suporte analise como os dados locais foram mapeados.
2. **`GET /admin/reconciliation/customers/{clienteId}`**: Consulta a API do Bitrix CRM (Contatos ou Empresas, com base no Tipo de Pessoa) e a base local do WebApolice, comparando a estrutura de dados (Telefones, E-mails, Documento, etc.). Esta rota expõe divergências explícitas, listando propriedades que estão dessincronizadas, ignorando a ordem de arrays (no caso de múltiplos telefones).
3. **`GET /admin/reconciliation/summary`**: Rota sumária para exibir contadores de integridade de todo o sistema de mensageria da integração (Total Sincronizados, Total com Erros Transitórios, Total com Erros Permanentes, Pendentes).

*Nota: Em ambiente On-Premise do Bitrix, as consultas de reconciliação são feitas buscando a Entidade primária filtrando pela chave `ORIGIN_ID = public_id`.*

## Estratégia de Rollout e AllowList

Para habilitar a integração no modo Live minimizando riscos de inundar a base de produção, uma estratégia de Rollout foi projetada baseando-se no `AllowedCustomerIds`. 

- A configuração pode ser ativada inicialmente como `DryRun`, para validar volumetria e conformidade sem impactar o sistema remoto.
- Ao mudar para modo `Live`, recomenda-se manter a flag `AllowAllCustomers = false`, preenchendo o array `AllowedCustomerIds` progressivamente com IDs de teste (rollout controlado).
- Os clientes fora da `AllowList` nem sequer são reservados nas *transactions* do PostgreSQL, e não entram no Worker.
- Depois de estabilizados e monitorados sem loops, o `AllowAllCustomers` pode ser ativado para processamento total, sujeito ainda ao `MaximumLiveBatchSize`.

## Tolerância a Falhas e Recuperação

O processo utiliza `FOR UPDATE SKIP LOCKED` e `UPDATE` usando concorrência otimista via token (`processing_token`). Além de proteger contra condições de corrida, o mecanismo de retentativas conta com `MaxRetryAttempts` para lidar com instabilidades do Bitrix (Erros Temporários). Caso um erro seja classificado como "Permanente" pelo `SynchronizationErrorClassifier` (ex.: credenciais expiradas, campos faltando no Bitrix), o processo suspende a evolução e entra em repouso programado, até intervenção administrativa.
