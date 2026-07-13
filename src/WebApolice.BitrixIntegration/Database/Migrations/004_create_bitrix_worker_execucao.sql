CREATE TABLE IF NOT EXISTS integracao.bitrix_worker_execucao (
    id BIGSERIAL PRIMARY KEY,

    correlation_id UUID NOT NULL DEFAULT gen_random_uuid(),

    status VARCHAR(30) NOT NULL,

    inicio_em TIMESTAMP NOT NULL DEFAULT NOW(),
    fim_em TIMESTAMP NULL,

    total_lidos INT NOT NULL DEFAULT 0,
    total_sucesso INT NOT NULL DEFAULT 0,
    total_erro INT NOT NULL DEFAULT 0,
    total_ignorado INT NOT NULL DEFAULT 0,

    erro TEXT NULL,

    created_at TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT ck_bitrix_worker_execucao_status
        CHECK (
            status IN (
                'EM_EXECUCAO',
                'FINALIZADO',
                'FINALIZADO_COM_ERRO'
            )
        )
);

CREATE INDEX IF NOT EXISTS ix_bitrix_worker_execucao_inicio
ON integracao.bitrix_worker_execucao (inicio_em DESC);

CREATE INDEX IF NOT EXISTS ix_bitrix_worker_execucao_status
ON integracao.bitrix_worker_execucao (status);

CREATE INDEX IF NOT EXISTS ix_bitrix_worker_execucao_correlation_id
ON integracao.bitrix_worker_execucao (correlation_id);
