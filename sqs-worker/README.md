# SalesApp SQS Worker (Local)

Worker standalone Node.js responsável por escutar notificações de scraping finalizado na fila AWS SQS (`salesapp-scrape-results`), baixar o arquivo CSV do Amazon S3 correspondente e disparar a importação direta no SalesApp via API local.

## Como Funciona

```
1. Scraping no pbi-scraper (com OutputMode: sqs)
   ↳ Salva CSV localmente
   ↳ Faz upload para o bucket S3 (ex: s3://hdev-sales-dash/scrape-results/...)
   ↳ Envia mensagem JSON para a fila SQS

2. Este Worker (sqs-worker rodando localmente)
   ↳ Long-polling (20s) na fila SQS
   ↳ Recebe notificação com s3Bucket e s3Key
   ↳ Chama POST /api/scrape/import-from-s3 na API .NET
   ↳ API baixa o CSV do S3 e executa a importação
   ↳ Se importação for bem-sucedida, exclui mensagem da fila SQS
```

## Requisitos

- Node.js 18+
- Fila SQS Standard criada na AWS (ex: `salesapp-scrape-results`) com retenção de 3 dias (`MessageRetentionPeriod = 259200`)
- API .NET em execução (ex: `http://localhost:5001`)

## Instalação e Execução

```bash
cd sqs-worker

# 1. Instalar dependências
npm install

# 2. Configurar variáveis de ambiente
cp .env.example .env
# Preencha SQS_QUEUE_URL e credenciais AWS se necessário no arquivo .env

# 3. Iniciar o worker
npm start
```
