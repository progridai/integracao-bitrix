# Reprocessamento e Operao da Fila

Esta seo detalha como operar a integrao na camada administrativa.

## Health Checks

- `/health/live`: Ping bsico atestando que a aplicao iniciou.
- `/health/ready`: Faz um ping no banco, verifica se a configurao est vlida e se o `CustomerSynchronizationWorker` no travou. Se `Enabled=false`, o Ready Check continuar sendo 'Healthy'.

## Endpoints de Gesto da Integrao

Voc precisa passar o header `X-Admin-Key` configurado no `appsettings.json` para todos eles.

### 1. Consultar Status Geral
`GET /admin/synchronization/status`
Retorna uma agregao limpa do banco de dados indicando o volume processado, em pendncia e as falhas nas ltimas 24 horas. Alm disso, exibe o horrio de ltima execuo com sucesso do worker.

### 2. Reprocessar a Dead-Letter Queue (DLQ)
`POST /admin/synchronization/dead-letter/retry`
Payload (opcional):
```json
{
  "limit": 100
}
```
Volta em at N registros da DLQ de volta pro fluxo `PENDENTE`. Muito til aps consertar um bug de validao.

### 3. Reprocessar Individual
`POST /admin/synchronization/customers/{clienteId}/retry`
Fora um reprocessamento na prxima rodada do worker limpando o hash e timers de tentativa.
