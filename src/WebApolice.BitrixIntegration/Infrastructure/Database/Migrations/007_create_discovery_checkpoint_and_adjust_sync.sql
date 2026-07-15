CREATE TABLE IF NOT EXISTS integracao.sync_checkpoint (
    process_name VARCHAR(50) PRIMARY KEY,
    last_modified_at TIMESTAMPTZ NOT NULL,
    last_entity_id BIGINT NOT NULL,
    last_started_at TIMESTAMPTZ NULL,
    last_finished_at TIMESTAMPTZ NULL,
    last_success_at TIMESTAMPTZ NULL,
    last_error TEXT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE integracao.bitrix_cliente_sync
    ADD COLUMN IF NOT EXISTS source_modified_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS processing_source_modified_at TIMESTAMPTZ NULL,
    ADD COLUMN IF NOT EXISTS last_synced_source_modified_at TIMESTAMPTZ NULL;
