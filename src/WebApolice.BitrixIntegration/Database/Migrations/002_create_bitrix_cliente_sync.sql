CREATE TABLE IF NOT EXISTS integracao.bitrix_cliente_sync (
    id BIGSERIAL PRIMARY KEY,

    cliente_id BIGINT NOT NULL,
    pessoa_id BIGINT NOT NULL,

    cliente_public_id UUID NULL,
    pessoa_public_id UUID NULL,

    documento_principal VARCHAR(50) NULL,

    bitrix_entity_type VARCHAR(30) NULL,
    bitrix_id VARCHAR(50) NULL,

    status VARCHAR(30) NOT NULL DEFAULT 'PENDENTE',

    ultima_origem_atualizacao TIMESTAMP NULL,
    ultima_sincronizacao TIMESTAMP NULL,

    tentativas INT NOT NULL DEFAULT 0,
    ultimo_erro TEXT NULL,

    payload_enviado JSONB NULL,
    retorno_bitrix JSONB NULL,

    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT uq_bitrix_cliente_sync_cliente UNIQUE (cliente_id),

    CONSTRAINT fk_bitrix_cliente_sync_cliente
        FOREIGN KEY (cliente_id)
        REFERENCES cadastro.cliente (id),

    CONSTRAINT fk_bitrix_cliente_sync_pessoa
        FOREIGN KEY (pessoa_id)
        REFERENCES core.pessoa (id),

    CONSTRAINT ck_bitrix_cliente_sync_entity_type
        CHECK (
            bitrix_entity_type IS NULL
            OR bitrix_entity_type IN ('CONTACT', 'COMPANY', 'LEAD')
        ),

    CONSTRAINT ck_bitrix_cliente_sync_status
        CHECK (
            status IN (
                'PENDENTE',
                'SINCRONIZANDO',
                'SINCRONIZADO',
                'ERRO',
                'IGNORADO'
            )
        )
);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_status
ON integracao.bitrix_cliente_sync (status);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_cliente_id
ON integracao.bitrix_cliente_sync (cliente_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_pessoa_id
ON integracao.bitrix_cliente_sync (pessoa_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_bitrix
ON integracao.bitrix_cliente_sync (bitrix_entity_type, bitrix_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_ultima_origem
ON integracao.bitrix_cliente_sync (ultima_origem_atualizacao);

CREATE INDEX IF NOT EXISTS ix_bitrix_cliente_sync_documento
ON integracao.bitrix_cliente_sync (documento_principal);

DROP TRIGGER IF EXISTS trg_bitrix_cliente_sync_updated_at
ON integracao.bitrix_cliente_sync;

CREATE TRIGGER trg_bitrix_cliente_sync_updated_at
BEFORE UPDATE ON integracao.bitrix_cliente_sync
FOR EACH ROW
EXECUTE FUNCTION integracao.set_updated_at();
