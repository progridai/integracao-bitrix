# Configuração Local

Para rodar o projeto localmente, não insira dados sensíveis no `appsettings.Development.json`. Em vez disso, utilize o `dotnet user-secrets`.

## Configurando appsettings.Development.json
No repositório, o arquivo `appsettings.Development.json` serve apenas como modelo.
Ele já possui as chaves de integração e flags com valores fictícios ou vazios.

## Como configurar user-secrets
Inicialize o user-secrets para o projeto:
```bash
cd src/WebApolice.BitrixIntegration
dotnet user-secrets init
```

## Exemplo de comandos user-secrets
Defina suas strings de conexão, tokens e chaves através da CLI do dotnet:

```bash
dotnet user-secrets set "Integration:WebApoliceDatabase:ConnectionString" "Host=localhost;Database=webapolice;Username=meuusuario;Password=minhasenha"
dotnet user-secrets set "Integration:Bitrix:WebhookToken" "SEU_TOKEN_SECRETO_AQUI"
dotnet user-secrets set "Integration:Admin:ApiKey" "CHAVE_SUPER_SECRETA"
```

## Rodando pelo Visual Studio
1. Abra a solution `WebApolice.BitrixIntegration.sln`.
2. Defina `WebApolice.BitrixIntegration` como o projeto de inicialização (Startup Project).
3. Pressione F5 ou clique em "Run" (perfil `http` ou `https`).

## Rodando via CLI (dotnet run)
```bash
cd src/WebApolice.BitrixIntegration
dotnet run --launch-profile "http"
```
