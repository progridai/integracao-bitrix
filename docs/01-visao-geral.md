# Visão Geral

## Objetivo do Projeto
O `WebApolice.BitrixIntegration` é o projeto responsável por integrar o sistema principal WebApólice com o CRM Bitrix24.

## Integração WebApólice → Bitrix24
O fluxo de integração consiste na leitura de dados do banco de dados do WebApólice e sincronização (criação/atualização) de clientes, leads e empresas (Companies/Contacts) no Bitrix24 utilizando suas APIs REST e Webhooks.

## Monólito Modular
O projeto adota uma arquitetura modular, onde cada contexto de integração e infraestrutura está isolado em pastas e namespaces lógicos:
- `Modules.WebApolice`: Acesso aos dados de origem.
- `Modules.Bitrix`: Integração com API do Bitrix.
- `Modules.Integracao`: Controle de sincronização e worker.

## Primeira Etapa
Esta etapa cobre a criação da infraestrutura base oficial:
- Estrutura de pastas modular.
- Configuração de appsettings e secrets.
- Injeção de dependência e Options Pattern.
- Configuração do DbUp para migrations SQL no PostgreSQL.
- Endpoints administrativos iniciais protegidos com `X-Admin-Key`.
- Preparação de um Worker herdando de `BackgroundService`.
- Dockerfile inicial para futuros deploys em containers.

## O que ainda será implementado
- Sincronização real de clientes com o Bitrix24 (criação e atualização).
- Comunicação via REST API do Bitrix24 usando `HttpClient`.
- Leitura estruturada de dados da base do WebApólice (schemas `core` e `cadastro`).
- Reprocessamento de erros e controle avançado de filas.
- Tela web administrativa para acompanhamento visual da integração.
