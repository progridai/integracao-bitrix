# Carga Inicial e Checkpoints

O `CustomerDiscoveryWorker` gerencia seu cursor na tabela `integracao.sync_checkpoint`.

## O que acontece se voc ligar a aplicao pela primeira vez?

Existem duas opes na configurao (`InitialLoadEnabled`):

### 1. `InitialLoadEnabled = false` (Padro)
O sistema iniciar o checkpoint usando o **maior timestamp e ID existentes na origem**. 
Isso significa que nenhum cliente do passado ir pro Bitrix24. Apenas quem for editado *a partir de agora* entrar na fila. O impacto e risco no banco de produo  ZERO.

### 2. `InitialLoadEnabled = true`
O sistema iniciar o checkpoint do Big Bang (ano 1900). A paginao andar do cliente mais antigo at o mais recente, inserindo todos os clientes na Fila.  excelente se voc acabou de contratar o Bitrix24 e quer migrar toda a base de clientes do WebAplice de uma vez.

## Sobreposio de Checkpoint
Se configurado (ex: 5 segundos), o checkpoint retrai levemente a janela antes de cada rodada para cobrir o "buraco negro" de transaes longas que podem ter sido efetivadas no banco da origem com timestamp antigo aps a captura da descoberta. O Upsert idempotente garante que as re-descobertas desnecessrias simplesmente esbarrem no `WHERE target.source_modified_at < EXCLUDED.source_modified_at` e no faam nada.
