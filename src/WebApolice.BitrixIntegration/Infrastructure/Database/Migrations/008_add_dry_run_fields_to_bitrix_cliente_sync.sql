-- Adicionando campos de controle de Dry-Run na tabela de sincronização
ALTER TABLE integracao.bitrix_cliente_sync 
ADD COLUMN IF NOT EXISTS last_dry_run_hash VARCHAR(64) NULL,
ADD COLUMN IF NOT EXISTS last_dry_run_at TIMESTAMP WITH TIME ZONE NULL,
ADD COLUMN IF NOT EXISTS last_dry_run_result TEXT NULL,
ADD COLUMN IF NOT EXISTS last_dry_run_source_modified_at TIMESTAMP WITH TIME ZONE NULL;
