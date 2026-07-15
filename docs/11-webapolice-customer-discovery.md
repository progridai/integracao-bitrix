# Descoberta de Clientes do WebAplice

A **Parte 4** introduz o `WebApoliceCustomerDiscoveryWorker`, um processo de background proativo, porm seguro, desenhado para injetar os clientes novos ou modificados na Fila de Sincronizao.

## Estratgia Incremental
Como o volume de dados grande, o worker no examina tudo toda hora.
A consulta usa o padro `GREATEST(c.updated_at, p.updated_at) AS source_modified_at`.

A proteo de saltos e duplicidades  cuidada por:
```sql
WHERE (source_modified_at, cliente_id) > (@LastModifiedAt, @LastEntityId)
```

Isso garante que se 500 usurios forem atualizados exatamente no mesmo milissegundo, a paginao usa a chave secundria (ID) para caminhar corretamente de lote em lote.

## Upsert Seguro
Se um usurio altera o nome de um cliente no WebAplice exatamente no momento em que o Sync Worker est tentando mandar esse cliente pro Bitrix24, o que acontece?

A Descoberta faz um UPSERT na tabela `bitrix_cliente_sync`:
- Ela mantm o registro `SINCRONIZANDO`.
- MAS, ela atualiza a coluna **`source_modified_at`**.
- Quando o Sync Worker (da Parte 3) conclui a chamada no Bitrix, ele percebe que `source_modified_at` agora  MAIOR que o `processing_source_modified_at` original.
- Com isso, ele transita o registro para `PENDENTE` em vez de `SINCRONIZADO`!
A prxima rodada do Sync Worker capturar e mandar o novo nome!

## Transao nica
Por ter cincia de que o banco de Origem e o banco da Integrao so *o mesmo*, a Descoberta abraa tudo (Ler Origem -> Upsert -> Update Checkpoint) numa nica transao PostgreSQL, alcanando ACID.
