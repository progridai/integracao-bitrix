ALTER TABLE integracao.bitrix_cliente_sync
    DROP CONSTRAINT IF EXISTS ck_bitrix_cliente_sync_status;

ALTER TABLE integracao.bitrix_cliente_sync
    ADD CONSTRAINT ck_bitrix_cliente_sync_status
    CHECK (
        status IN (
            'PENDENTE',
            'SINCRONIZANDO',
            'SINCRONIZADO',
            'ERRO',
            'IGNORADO',
            'DEAD_LETTER'
        )
    );

ALTER TABLE integracao.bitrix_cliente_sync
    ADD COLUMN IF NOT EXISTS next_attempt_at TIMESTAMP NULL,
    ADD COLUMN IF NOT EXISTS processing_token UUID NULL,
    ADD COLUMN IF NOT EXISTS processing_started_at TIMESTAMP NULL,
    ADD COLUMN IF NOT EXISTS payload_hash VARCHAR(64) NULL,
    ADD COLUMN IF NOT EXISTS last_http_status INT NULL;
