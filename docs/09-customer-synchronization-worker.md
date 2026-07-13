# Worker de Sincronizao de Clientes

O `CustomerSynchronizationWorker`  um `BackgroundService` responsvel por ler a tabela `integracao.bitrix_cliente_sync` e sincronizar proativamente os clientes do WebApolice para o Bitrix24.

## Arquitetura de Fila

- Usa `FOR UPDATE SKIP LOCKED` no PostgreSQL.
- Registros so lidos e "Reservados" atomicamente via `processing_token`.
- Somente o worker que possui o token pode atualizar o registro como SINCRONIZADO ou ERRO, garantindo concorrncia segura entre rplicas.

## Idempotncia via Hash

O sistema calcula o hash do payload normalizado (CustomerPayloadHasher). Se for igual ao j registrado localmente e possuir vnculo vlido com o Bitrix, ele "pula" a chamada HTTP, voltando o status para `SINCRONIZADO`.

## Classificao de Falhas

- **Transitria**: Cai e re-tenta na prxima rodada (max retries = 5, por padro). Timeout ou indisponibilidade de API.
- **Permanente**: Dados invlidos da parte do sistema origem (CPF falso, campos missing no Bitrix). Vai para `DEAD_LETTER`.
- **Global / Configurao**: Uma falha de token revogado, por exemplo. Aborta todo o lote de processamento, re-liberando os itens que no foram processados de volta pra fila e d um cooldown de 60s antes de tentar de novo.

## Configuraes (`appsettings.json`)

```json
"CustomerSynchronization": {
  "Enabled": false,
  "PollingIntervalSeconds": 30,
  "BatchSize": 50,
  "MaxRetryAttempts": 5,
  "RetryDelaySeconds": 60,
  "ProcessingTimeoutMinutes": 10
}
```
