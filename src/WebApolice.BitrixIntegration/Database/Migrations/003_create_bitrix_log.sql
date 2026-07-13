CREATE TABLE IF NOT EXISTS integracao.bitrix_log (
    id BIGSERIAL PRIMARY KEY,

    correlation_id UUID NOT NULL DEFAULT gen_random_uuid(),

    cliente_id BIGINT NULL,
    pessoa_id BIGINT NULL,

    bitrix_entity_type VARCHAR(30) NULL,
    bitrix_id VARCHAR(50) NULL,

    direcao VARCHAR(50) NOT NULL DEFAULT 'WEBAPOLICE_TO_BITRIX',
    operacao VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL,

    request_url TEXT NULL,
    request_json JSONB NULL,

    response_status_code INT NULL,
    response_json JSONB NULL,

    erro TEXT NULL,
    duracao_ms INT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_bitrix_log_cliente
        FOREIGN KEY (cliente_id)
        REFERENCES cadastro.cliente (id),

    CONSTRAINT fk_bitrix_log_pessoa
        FOREIGN KEY (pessoa_id)
        REFERENCES core.pessoa (id),

    CONSTRAINT ck_bitrix_log_status
        CHECK (
            status IN (
                'SUCESSO',
                'ERRO',
                'AVISO'
            )
        ),

    CONSTRAINT ck_bitrix_log_direcao
        CHECK (
            direcao IN (
                'WEBAPOLICE_TO_BITRIX',
                'BITRIX_TO_WEBAPOLICE',
                'ADMIN_TEST'
            )
        )
);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_created_at
ON integracao.bitrix_log (created_at DESC);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_cliente_id
ON integracao.bitrix_log (cliente_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_pessoa_id
ON integracao.bitrix_log (pessoa_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_correlation_id
ON integracao.bitrix_log (correlation_id);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_status
ON integracao.bitrix_log (status);

CREATE INDEX IF NOT EXISTS ix_bitrix_log_operacao
ON integracao.bitrix_log (operacao);
