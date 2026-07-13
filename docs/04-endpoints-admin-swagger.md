# Endpoints Admin e Swagger

A aplicação utiliza o Swagger para documentar e facilitar os testes da API. O Swagger pode ser acessado localmente navegando para `/swagger`.

## Autenticação com X-Admin-Key
Todas as rotas iniciadas com `/admin` são protegidas por um middleware customizado que valida a presença e validade de uma chave de API.

Para enviar requisições administrativas, inclua no Header HTTP:
```text
X-Admin-Key: <Sua-Chave-Configurada-Em-Integration:Admin:ApiKey>
```

Se a chave estiver incorreta ou ausente, o sistema retorna `401 Unauthorized`.

## Endpoints Criados
### `GET /health`
Verifica se a aplicação está no ar. Não exige `X-Admin-Key`.

**Response:**
```json
{
  "status": "ok",
  "application": "WebApolice.BitrixIntegration",
  "environment": "Development",
  "timestamp": "2024-05-15T12:00:00Z"
}
```

### `GET /admin/config`
Retorna as configurações ativas da aplicação com dados sensíveis mascarados. Exige `X-Admin-Key`.

**Response:**
```json
{
  "worker": { "enabled": false, "intervalSeconds": 30, "batchSize": 50 },
  "database": { "runMigrationsOnStartup": true },
  "bitrix": { "baseUrl": "https://bitrix24.local", "restUserId": "3", "webhookTokenConfigured": true, "webhookTokenMasked": "abc***xyz" },
  "webApoliceDatabase": { "connectionStringConfigured": true, "connectionStringMasked": "Host=***;Database=***;Username=***;Password=***" }
}
```

### `POST /admin/test/webapolice-db`
Testa a conectividade com o banco do PostgreSQL e verifica se o schema `integracao` existe. Exige `X-Admin-Key`.

**Response:**
```json
{
  "success": true,
  "message": "Conexão PostgreSQL realizada com sucesso.",
  "schemaIntegracaoExists": true
}
```

### `GET /admin/status`
Retorna o status técnico básico do sistema (incluindo status do worker e migrations). Exige `X-Admin-Key`.
