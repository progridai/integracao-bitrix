# Docker

O projeto foi preparado para ser rodado em contêineres Docker no futuro. Por enquanto, o Dockerfile na raiz do projeto (`src/WebApolice.BitrixIntegration/Dockerfile`) serve como documentação de como a aplicação será encapsulada.

## Como fazer build da imagem
Se você quiser testar o Docker localmente:
```bash
cd src/WebApolice.BitrixIntegration
docker build -t webapolice-bitrix-integration:latest .
```

## Como rodar o contêiner
Para executar a aplicação a partir da imagem gerada:
```bash
docker run -p 8080:8080 -e "ASPNETCORE_ENVIRONMENT=Development" webapolice-bitrix-integration:latest
```

> **Aviso:**
> O projeto não depende do Docker para rodar localmente. Você pode simplesmente rodar com o Visual Studio ou `dotnet run` para maior agilidade durante o desenvolvimento.
