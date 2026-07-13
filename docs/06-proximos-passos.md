# Próximos Passos

Esta primeira etapa (Parte 1) serviu apenas para preparar o terreno. Nas próximas etapas avançaremos nas implementações de negócio e de infraestrutura.

## Parte 2: Cliente Bitrix REST API
- Implementação de um `BitrixClient` utilizando o `HttpClientFactory`.
- Criação e chamadas para os endpoints `crm.contact.add`, `crm.contact.update`, `crm.company.add` e `crm.company.update`.
- Tratamento de throttling e retries nas chamadas para a API do Bitrix.

## Parte 3: Leitura do Banco WebApólice
- Queries avançadas usando **Dapper** nos schemas `core` e `cadastro`.
- Identificação dos clientes (Pessoa Física e Pessoa Jurídica) que precisam de sincronização.
- Mapeamento das propriedades do WebApólice para a estrutura requerida pelo Bitrix24.

## Parte 4: Worker de Sincronização
- Implementação da lógica do loop principal do `ClienteSyncWorker`.
- Persistência e controle de fluxo nas tabelas `bitrix_cliente_sync` e `bitrix_log`.
- Tratamento de erros, reprocessamento de falhas e status final de sincronização.

## Parte 5: Finalização e Deploy
- Setup final do Docker para uso em produção (provavelmente no EasyPanel).
- Configuração de um painel web administrativo (opcional) ou endpoints extras para gerenciar a fila de sincronização.
