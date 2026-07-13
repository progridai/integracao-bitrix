# Integração REST Bitrix24

Esta etapa da integração foi desenhada para se comunicar de forma segura e padronizada com o Bitrix24 através de sua API REST.

## Configuração do Webhook
A configuração principal está na chave `Integration:Bitrix:WebhookBaseUrl`. 
Essa URL deve conter o endereço gerado pelo próprio Bitrix24 ao se criar um webhook de entrada (Inbound Webhook).
Exemplo: `https://seu-dominio.bitrix24.com.br/rest/1/codigo_secreto_do_webhook/`

## Cliente HTTP (BitrixClient)
Todas as requisições para o Bitrix passam exclusivamente pela classe `BitrixClient`. Ela é responsável por:
1. Validar a presença da configuração básica.
2. Montar a URL de forma segura anexando o método chamado (ex: `crm.contact.add.json`).
3. Forçar o uso de método POST para todas as chamadas.
4. Serializar o payload de request e desserializar a response genérica.
5. Controlar o Timeout global (`TimeoutSeconds`, padrão 30s).
6. Capturar duração (Stopwatch).
7. Lançar `BitrixException` em caso de erro HTTP ou se o JSON de retorno indicar erro interno (`"error": "..."`).

## Proteção de Segredos e LGPD
Nenhuma credencial ou token real deve ser gravado no código.
O `BitrixClient` grava um histórico de todas as operações em `integracao.bitrix_log`, entretanto:
- O token da URL é ocultado antes da gravação (ex: `abc***xyz`).
- O payload completo das entidades NÃO é gravado no banco de dados, visando preservar a privacidade (LGPD) contra vazamento de dados de clientes, como telefone, e-mail e documento.
- Caso seja necessário depuração de payload, o log de aplicação registra a operação e o Status, mas oculta o conteúdo bruto de pessoa física.

## Validação de Certificado SSL
Em ambientes locais, é possível que requisições passem por proxies de desenvolvimento com certificados autoassinados. A flag `Integration:Bitrix:IgnoreSslValidation = true` pode ser usada para desabilitar a checagem SSL no `HttpClient` do Bitrix.
**Importante:** Nunca habilitar em Produção. Se ativada, a aplicação registrará um aviso (Warning) no startup.
