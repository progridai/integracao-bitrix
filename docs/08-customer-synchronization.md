# Sincronização de Clientes

A sincronização de clientes é o núcleo do serviço de integração da Parte 2. Ela foca estritamente na sincronização dos dados do WebApólice para entidades de CRM do Bitrix24.

## Regra de Negócio (Pessoa Física vs Pessoa Jurídica)
O Provider genérico avalia a propriedade `CustomerType`:
- **Individual (Pessoa Física)**: É convertido e cadastrado como um `Contact` (Contato) no Bitrix24.
- **Company (Pessoa Jurídica)**: É convertido e cadastrado como uma `Company` (Empresa) no Bitrix24.

## Chaves Externas e Campos Customizados
A sincronização **não** usa IDs numéricos internos nativos do Bitrix como referência inicial. Toda vez que um cliente precisa ser sincronizado, a aplicação buscará o cliente por chaves externas no Bitrix.
Os nomes dos campos no Bitrix não estão hardcoded. Eles vêm de configurações:
- `Integration:Bitrix:ExternalCustomerIdField`: Representa o campo no Bitrix que guarda o `cliente_id` do WebApólice.
- `Integration:Bitrix:DocumentField`: Representa o campo no Bitrix que guarda o CPF ou CNPJ.

## Idempotência e Estratégia de Localização
Quando a aplicação precisa fazer o *Upsert* (inserir ou atualizar) de um cliente, ocorre o seguinte fluxo (Idempotência):
1. O CRM Provider tenta listar registros filtrando pelo `ExternalCustomerIdField` (Prioridade 1).
2. Se não encontrar, e se um documento for informado, tenta listar filtrando pelo `DocumentField` (Prioridade 2).
3. Se o registro for localizado no passo 1 ou 2, ele recebe a operação de `Update` (`crm.contact.update` ou `crm.company.update`) enviando apenas o ID descoberto e os novos campos mapeados.
4. Se o registro não for localizado, ele é Criado (`crm.contact.add` ou `crm.company.add`).
5. O ID final do CRM é retornado pelo Provider em um `CrmCustomerUpsertResult`.

## Operações Disponíveis
Nesta etapa, o `ICustomerCrmProvider` suporta:
- Testar conexão (`TestConnectionAsync`).
- Upsert de Cliente (`UpsertCustomerAsync`).
- Consultar de Campos Disponíveis (`GetAvailableFieldsAsync`).

**Obs:** Operações de exclusão, assim como entidades Deals (Negócios) e Leads, não foram implementadas de forma proposital para manter o escopo seguro e aderente aos requisitos do cliente final.
