# TODO-fargate.md — SQS + Fargate Spot Scraping Architecture

> **Strategy:** Work from the inside out. Decouple and validate locally first, then layer AWS services on top.  
> Each phase produces a shippable, testable increment before the next begins.

---

## 📌 Resumo Executivo & Status Atual (Atualizado em 18/09/2026)

### 🟢 O que já está PRONTO e validado:
1. **Worker Desacoplado (`pbi-scraper/worker.js`)**:
   - Loop autônomo com SQS long-polling (20s) na fila de Jobs (`SQS_JOBS_QUEUE_URL`).
   - Ciclo de vida com **Timeout de 5 minutos de inatividade** (`IDLE_TIMEOUT_MS=300000`) com renovação a cada mensagem processada e scale-to-zero limpo (`process.exit(0)`).
   - Modo `RESULTS_CONSUMER_MODE` implementado para download S3 e importação no backend sem precisar de Chromium.
   - Decodificação AES-256-GCM para senha protegida (`crypto.js`) e matrícula em plain text.
   - Tratamento de `SIGTERM` / Spot Interruption (drenagem limpa).
   - `Dockerfile` otimizado: usuário não-root `nodeuser`, healthcheck via lockfile e Chromium pré-instalado.
2. **S3 + SQS Output Pipeline**:
   - Upload do CSV resultante para o Amazon S3 (`SCRAPE_S3_BUCKET/scrape-results/`) com expiração de 1 dia configurada via `s3Uploader.js`.
   - Notificação de conclusão de scrape publicada na fila SQS de Resultados (`SQS_RESULTS_QUEUE_URL` - `hdev-sales-dash`) via `sqsPublisher.js`.
   - Script CLI `push-job.js` (`npm run push:job`) para teste local de enfileiramento com criptografia.
   - Serviço `pbi-worker` configurado no `docker-compose.yml` sob profile `worker`.
   - Consumo contínuo de resultados no VPS unificado nativamente na API .NET (`SqsResultBackgroundConsumerService`), eliminando container extra.
3. **Agendamento por Intervalo (Cron) & Disparo Fargate**:
   - Campo `ScrapeIntervalHours` e `LastTriggeredAt` na entidade `ScrapeConfig` (com migration EF Core).
   - Hosted Service `ScrapeSchedulerService` rodando em background no backend a cada 60s.
   - Seletor de intervalo de horas na UI (`ScrapeDashboard.tsx`) com badge visual na tabela.
   - Chamada opcional da Lambda launcher via HTTP em `ScrapeOrchestrator.cs` ao enfileirar no SQS.
4. **Deploy & Infraestrutura Cloud**:
   - Workflow `.github/workflows/deploy-scraper.yml` para build e push ao AWS ECR.
   - Imagem do `pbi-scraper` integrada à matriz de deploy do `.github/workflows/deploy.yml`.
   - Código da Lambda Function (`infra/lambda/scraper-launcher/index.js`) para `RunTask` Fargate Spot sob demanda.
   - Guia passo a passo `infra/aws-setup.md` e template `infra/task-definition.json`.
5. **Admin Tools & Observabilidade**:
   - Painel web `/admin-tools/sqs-queue` implementado para inspecionar, monitorar estatísticas da fila e purgar mensagens.
   - Suporte a `ScrapeType` ('geral' vs 'consultor') e `OutputMode` ('direct' vs 'sqs') no banco e UI.

---

### 🟢 Checklist de Deploy na AWS (Pronto para Execução):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ [x] 1. [App] Campo ScrapeIntervalHours + ScrapeSchedulerService no backend   │
│ [x] 2. [UI] Seletor de Frequência de Extração Automática na tela de Scrapes │
│ [x] 3. [CI/CD] GitHub Actions para Build & Push no AWS ECR e GHCR           │
│ [x] 4. [Código] Lambda Function para ligar Fargate Spot sob demanda         │
│ [x] 5. [App] Consumo nativo de resultados no .NET (SqsResultBackgroundConsumer) │
│ [x] 6. [Doc] Guia de setup AWS detalhado (infra/aws-setup.md)               │
│ [ ] 7. [AWS Console/CLI] Executar comandos únicos do guia infra/aws-setup.md │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 🔐 Topologia de Duas Filas SQS & Segurança de Credenciais

### 1. Topologia de 2 Filas
* **Fila 1: `SQS_JOBS_QUEUE_URL` (Entrada/Inbound - ex: `hdev-sales-scrape-jobs`)**:
  - A API local ou agendador posta ordens de scraping.
  - O Fargate Worker consome essas ordens via long-polling (20s).
* **Fila 2: `SQS_RESULTS_QUEUE_URL` (Saída/Outbound - ex: `hdev-sales-dash`)**:
  - Após concluir o scrape e subir o CSV no S3, o Fargate Worker publica um evento leve avisando que o arquivo está pronto no S3 (`{ jobId, s3Bucket, s3Key, rowCount, status }`).
  - O consumidor/worker local lê essa fila, faz download do CSV do S3 e grava no SQLite.

### 2. Criptografia de Credenciais Ponta a Ponta
* **Regra de Ouro**: As senhas nunca trafegam em texto plano no SQS nem ficam em repositório.
* **Chave Simétrica**: `SCRAPER_ENCRYPTION_KEY` (32 bytes em hex = 64 caracteres).
* **Configuração de Ambiente**:
  - **Local / CI/CD**: Injetada via GitHub Secrets (`SCRAPER_ENCRYPTION_KEY` — já cadastrado no GitHub Actions ✅).
  - **AWS Fargate**: Injetada nas variáveis de ambiente da Task Definition (ou via AWS SSM / Secrets Manager).
* **Algoritmo**: `AES-256-GCM` com IV aleatório de 12 bytes gerado a cada mensagem e AuthTag de 16 bytes.
  - C# (`salesapp-api`) encripta a senha com `AesGcm` antes de enviar à fila SQS de Jobs.
  - Node.js (`pbi-scraper/worker.js`) usa `crypto.js` para validar a AuthTag e decodificar a senha em memória apenas no momento da autenticação.

---

## Phase 1 — Decouple `pbi-scraper` into a Self-Contained Worker
> **Goal:** Refactor `pbi-scraper` from an Express HTTP server into a standalone worker process with all cloud-readiness hooks in place. No AWS required — runs identically with `node worker.js` locally or inside Docker.

### 1.1 — New entry point `worker.js`
- [x] Create `pbi-scraper/worker.js` as the new primary entry point (keep `server.js` alive for legacy local HTTP use if needed during transition, then deprecate it)
- [x] `worker.js` imports a `processMessage(payload)` function (pure, testable, no I/O coupling)
- [x] `worker.js` owns the run-loop: poll → process → delete → repeat
- [x] Move all Puppeteer + scrape logic from `server.js` into a pure `scrape.js` module (function in, data out, no HTTP, no SQS)
- [x] Keep `extractor.js` and `auth.js` as-is (they are already mostly pure)

### 1.2 — Message contract definition
- [x] Define and document the SQS message payload schema (JSON):
  ```json
  {
    "jobId": "string",
    "runId": "string",
    "matricula": "string",
    "store": "string | null",
    "scrapeDate": "string | null",
    "encryptedUsername": "string",
    "encryptedPassword": "string",
    "iv": "string",
    "authTag": "string",
    "userId": "string"
  }
  ```
- [x] This schema is the single source of truth between C# sender and Node.js receiver
- [x] Document separately for username vs password (each gets its own IV + authTag)

### 1.3 — Local credential injection (dev mode)
- [x] Add `dotenv` to `package.json` (`dotenv`, `@aws-sdk/client-sqs`, `@aws-sdk/client-dynamodb`)
- [x] Add `.env` keys for local dev
- [x] **In production (Fargate):** no `.env` file — all vars injected by ECS Task Definition
- [x] Guard: if `NODE_ENV !== production` and `SQS_QUEUE_URL` is empty, use a local mock queue

### 1.4 — AES-256-GCM decryption module (`crypto.js`)
- [x] Create `pbi-scraper/crypto.js`
- [x] Write a standalone test script `pbi-scraper/test-crypto.js`
- [x] Key is read from `process.env.SCRAPER_ENCRYPTION_KEY`

### 1.5 — SIGTERM / Spot Interruption handler
- [x] Graceful shutdown on SIGTERM / SIGINT in `worker.js`
- [x] Drain window and lockfile cleanup

### 1.6 — Scale-to-zero logic
- [x] SQS long-poll with `WaitTimeSeconds: 20`
- [x] Exit process with 0 after `MAX_EMPTY_POLLS` (default 3)
- [x] `SCALE_TO_ZERO` flag to enable/disable for local dev

### 1.7 — Output Pipeline: S3 Upload + SQS Result Notification
- [x] S3 Uploader (`s3Uploader.js`) com expiração de 1 dia
- [x] SQS Publisher (`sqsPublisher.js`) com metadados do job
- [x] Conectar chamada a `uploadToS3` e `publishToQueue` dentro de `worker.js` após conclusão do scrape (ouvindo `SQS_JOBS_QUEUE_URL` e publicando em `SQS_RESULTS_QUEUE_URL`)
- [x] Lógica de lifetime por timeout de inatividade de 5 minutos (`IDLE_TIMEOUT_MS=300000`) com renovação a cada job
- [x] Script CLI `push-job.js` para testes de enfileiramento com senha criptografada em AES-256-GCM

### 1.8 — Local mock queue helper
- [x] Create `pbi-scraper/scripts/push-test-message.js`

### 1.9 — Update `Dockerfile`
- [x] Chromium deps instaladas
- [x] `CMD ["node", "worker.js"]`
- [x] Usuário `nodeuser` (não-root)
- [x] `HEALTHCHECK` configurado via lockfile

### 1.10 — Verification
- [x] Build do container validado
- [x] Criptografia / decriptografia validada
---

## Phase 2 — Encrypt Credentials in `salesapp-api` + Push to SQS
> **Goal:** `salesapp-api` can encrypt credentials with AES-256-GCM and enqueue messages. No Fargate yet — worker still runs locally, consuming from the real SQS queue for verification.

### 2.1 — NuGet packages
- [x] Add `AWSSDK.SQS` to `SalesApp.Api.csproj`
- [x] Add `AWSSDK.S3` to `SalesApp.Api.csproj`
- [ ] Add `AWSSDK.ECS` to `SalesApp.Api.csproj` (used in Phase 3)
- [ ] Keep existing: `AWSSDK.DynamoDBv2`, `AWSSDK.SimpleEmail`, `AWSSDK.CloudWatchLogs`

### 2.2 — Encryption service (`ICredentialEncryptionService`)
- [ ] Create `SalesApp.Api/Services/ICredentialEncryptionService.cs`:
  ```csharp
  public record EncryptedField(string CipherText, string Iv, string AuthTag);
  public interface ICredentialEncryptionService {
      EncryptedField Encrypt(string plaintext);
  }
  ```
- [ ] Create `SalesApp.Api/Services/CredentialEncryptionService.cs`:
  - Uses `System.Security.Cryptography.AesGcm` (built-in, .NET 9)
  - Reads key from `Environment.GetEnvironmentVariable("SCRAPER_ENCRYPTION_KEY")` — not from `appsettings.json`
  - Key is 32 bytes decoded from a 64-char hex string
  - Each call to `Encrypt()` generates a fresh 12-byte random IV
  - Returns `(ciphertext, iv, authTag)` all as base64 strings
  - Throws `InvalidOperationException` at startup if key is missing or wrong length
- [ ] Register as `Singleton` in `Startup.cs`

### 2.3 — `appsettings.json` — AWS section additions
- [ ] Add SQS queue URL placeholder (actual value comes from GitHub Actions env injection):
  ```json
  "AWS": {
    "SqsQueueUrl": "",       // injected at runtime
    "EcsClusterArn": "",     // injected at runtime
    "EcsTaskDefinitionArn": "", // injected at runtime
    "EcsSubnetIds": "",      // comma-separated
    "EcsSecurityGroupIds": "", // comma-separated
    "Region": "us-east-1"
  }
  ```
- [ ] **Never** store `SCRAPER_ENCRYPTION_KEY` in `appsettings.json` — environment only

### 2.4 — SQS service (`ICloudScraperQueueService`)
- [ ] Create `SalesApp.Api/Services/ICloudScraperQueueService.cs`:
  ```csharp
  public interface ICloudScraperQueueService {
      Task<int> EnqueueScrapeJobsAsync(IEnumerable<CloudScrapeJobPayload> payloads);
  }
  ```
- [ ] Create `SalesApp.Api/Services/CloudScraperQueueService.cs`:
  - Uses `IAmazonSQS` (injected)
  - Builds `ScrapeMessagePayload` by calling `ICredentialEncryptionService.Encrypt()` for username and password separately
  - Batches messages using `SendMessageBatchAsync` (max 10 per batch, SQS limit)
  - Returns count of successfully enqueued messages
  - Structured payload matches the Node.js message contract from Phase 1.2
- [ ] Register `IAmazonSQS` in `Startup.cs` using `AWSOptions` from config/environment

### 2.5 — New API endpoint: `POST /api/cloud-scraper/trigger`
- [ ] Create `SalesApp.Api/Controllers/CloudScraperController.cs`
- [ ] Request DTO `CloudScraperTriggerRequest`:
  ```csharp
  public class CloudScraperTriggerRequest {
      public List<int> ScrapeConfigIds { get; set; }  // IDs from ScrapeConfig table
      public int WorkerCount { get; set; } = 1;        // Fargate task count
      public bool UseSpot { get; set; } = true;
      public string? ScrapeDate { get; set; }           // optional override
  }
  ```
- [ ] Endpoint logic (in order):
  1. Validate: all `configIds` must exist and belong to caller (or superadmin)
  2. Load `ScrapeConfig` entities (matricula + password) for each ID
  3. Decrypt stored `PowerBiPassword` using existing `IDataProtector`
  4. Re-encrypt with `ICredentialEncryptionService` (AES-GCM for transport)
  5. Enqueue all messages via `ICloudScraperQueueService`
  6. **Phase 3 gate:** spin up ECS tasks (placeholder for now — log a warning if skipped)
  7. Return `202 Accepted` with `{ runId, enqueuedCount, workerCount, useSpot }`
- [ ] Authorization: `[Authorize(Roles = "admin,superadmin")]`

### 2.6 — New API endpoint: `GET /api/cloud-scraper/available-matriculas`
- [ ] Returns list of all `ScrapeConfig` entries with `CredentialStatus == "ok"` and `IsEnabled == true`
- [ ] Joined with `Matriculas` table to include `contractCount` and `lastUpdatedAt` for the UI
- [ ] Response DTO:
  ```csharp
  public class AvailableMatriculaDto {
      public int ScrapeConfigId { get; set; }
      public string Matricula { get; set; }
      public string? Store { get; set; }
      public int ContractCount { get; set; }
      public DateTime? LastScrapedAt { get; set; }  // from DynamoDB logs or Contracts table
      public string CredentialStatus { get; set; }
  }
  ```

### 2.7 — Local AWS validation
- [ ] Create a real SQS queue in AWS (standard queue, not FIFO) for dev testing
- [ ] Configure `.env` (root) with real `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `SQS_QUEUE_URL`
- [ ] Trigger `POST /api/cloud-scraper/trigger` from Swagger/Postman
- [ ] Verify messages appear in SQS console with correct encrypted payloads
- [ ] Run local `pbi-scraper` worker against that queue: `SQS_QUEUE_URL=<url> node worker.js`
- [ ] Confirm full round-trip: API enqueues → worker decrypts → scrape runs → data saved

---

## Phase 3 — ECS Fargate Task Launch from `salesapp-api`
> **Goal:** `salesapp-api` programmatically launches the exact number of Fargate worker tasks requested by the UI. Worker image must be in ECR.

### 3.1 — ECR repository setup
- [ ] Create ECR repo: `pbi-scraper-worker` (one-time, via AWS Console or Terraform/CDK — document the ARN)
- [ ] Add ECR push to GitHub Actions workflow:
  - Trigger: push to `main` (or manual `workflow_dispatch`)
  - Steps: `docker build`, `docker tag`, `aws ecr get-login-password`, `docker push`
  - Tag strategy: `latest` + `git sha` (e.g., `sha-abc1234`)
- [ ] Store ECR image URI in GitHub Actions secret: `ECR_PBI_SCRAPER_IMAGE_URI`

### 3.2 — ECS Fargate Task Definition
- [ ] Create ECS Task Definition `pbi-scraper-worker` (via AWS Console or IaC):
  - Launch type: `FARGATE`
  - CPU: 1 vCPU, Memory: 2 GB (Puppeteer/Chromium minimum)
  - Container: image from ECR (`pbi-scraper-worker:latest`)
  - Environment variables (injected by Task Definition, sourced from GitHub Secrets):
    ```env
    NODE_ENV=production
    SQS_QUEUE_URL=<from secret>
    SCRAPER_ENCRYPTION_KEY=<from secret>
    AWS_REGION=us-east-1
    SCALE_TO_ZERO=true
    DATABASE_URL=<from secret>
    ```
  - IAM Task Role: grants `sqs:ReceiveMessage`, `sqs:DeleteMessage`, `sqs:GetQueueAttributes`, `logs:PutLogEvents`, `dynamodb:PutItem`
  - Log configuration: `awslogs` driver → CloudWatch log group `/pbi-scraper/worker`
- [ ] **Spot vs On-Demand:** controlled at `RunTask` time, not in Task Definition (see 3.3)

### 3.3 — ECS service (`IFargateScraperLaunchService`)
- [ ] Create `SalesApp.Api/Services/IFargateScraperLaunchService.cs`:
  ```csharp
  public interface IFargateScraperLaunchService {
      Task<List<string>> LaunchWorkersAsync(int count, bool useSpot);
  }
  ```
- [ ] Create `SalesApp.Api/Services/FargateScraperLaunchService.cs`:
  - Uses `IAmazonECS` (injected)
  - Builds `RunTaskRequest`:
    - `TaskDefinition`: from config `AWS:EcsTaskDefinitionArn`
    - `Cluster`: from config `AWS:EcsClusterArn`
    - `Count`: matches UI input (guard: max 20, min 1)
    - `LaunchType`: if `useSpot=true`, set `CapacityProviderStrategy` with `FARGATE_SPOT`; else `FARGATE`
    - `NetworkConfiguration`: `AwsvpcConfiguration` with subnets + security groups from config
  - Returns list of Fargate task ARNs
  - Logs each launched ARN to the structured logger

### 3.4 — Wire ECS launch into `CloudScraperController`
- [ ] Call `IFargateScraperLaunchService.LaunchWorkersAsync(request.WorkerCount, request.UseSpot)` after successful SQS enqueue
- [ ] Include `taskArns` in the `202 Accepted` response
- [ ] Handle `ECS.RunTask` failures gracefully: messages are already in SQS (idempotent) — surface the ECS error without rolling back

### 3.5 — GitHub Actions secrets & injection
- [ ] Add to GitHub Actions Secrets:
  - [x] `SCRAPER_ENCRYPTION_KEY` — 64-char hex (32 bytes) *(Adicionado no GitHub Secrets ✅)*
  - [ ] `AWS_SQS_JOBS_QUEUE_URL` (Fila de ordens para os workers)
  - [ ] `AWS_SQS_RESULTS_QUEUE_URL` (Fila de resultados processados)
  - [ ] `AWS_ECS_CLUSTER_ARN`
  - [ ] `AWS_ECS_TASK_DEFINITION_ARN`
  - [ ] `AWS_ECS_SUBNET_IDS`              # comma-separated
  - [ ] `AWS_ECS_SECURITY_GROUP_IDS`
  - [ ] `ECR_PBI_SCRAPER_IMAGE_URI`
- [ ] In deployment workflow, inject as environment variables into `salesapp-api` container (via ECS task definition override ou docker-compose)
- [ ] `pbi-scraper` worker gets its secrets via ECS Task Definition environment (not GitHub Actions at runtime)

### 3.6 — Verification
- [ ] Trigger from Postman: `POST /api/cloud-scraper/trigger` with `workerCount: 2`, `useSpot: true`
- [ ] Verify in AWS ECS console: 2 tasks are launched
- [ ] Verify in CloudWatch `/pbi-scraper/worker`: logs from running tasks
- [ ] Verify messages consumed and deleted from SQS
- [ ] Kill a task with SIGTERM manually → verify graceful shutdown log + message reappears in queue

---

## Phase 4 — Frontend "Cloud Scraper" UI
> **Goal:** New tab/page under "Extração PowerBi" section with a two-column selector, worker count control, and Spot toggle.

### 4.1 — New page: `CloudScraperPage.tsx`
- [ ] Route: add to `App.tsx` (e.g., `/scrape/cloud`) — superadmin/admin only
- [ ] Add menu item to `Menu.tsx` under "Extração PowerBi" section
- [ ] Page uses Mantine components (consistent with existing `ScrapeDashboard.tsx`)

### 4.2 — Two-column layout
- [ ] **Left column — "Disponíveis":**
  - Fetches from `GET /api/cloud-scraper/available-matriculas`
  - Displays `ScrapeConfig` rows with: `matricula`, `store`, `contractCount`, `lastScrapedAt` (formatted as relative time), `credentialStatus` badge
  - Click or `→` arrow button moves item to right column
  - Search/filter input at top
- [ ] **Right column — "Selecionados":**
  - Appendable list (no duplicates)
  - Each item shows matricula + store + `×` remove button
  - Empty state: "Nenhuma matrícula selecionada"
- [ ] State: `available: AvailableMatriculaDto[]`, `selected: AvailableMatriculaDto[]` — derived from a single source (no duplication)

### 4.3 — Controls section
- [ ] `NumberInput` — "Número de Workers": min 1, max 20, default 1
- [ ] `Switch` — "Usar Fargate Spot": default on (cost optimization)
- [ ] `Button` — "Disparar AWS Scrapers": disabled if `selected` is empty or loading
- [ ] Loading state: spinner + "Enviando para SQS..." feedback during trigger call
- [ ] Success toast: "X matrícula(s) enfileiradas. Y worker(s) iniciados."
- [ ] Error toast: display API error message

### 4.4 — Payload sent to backend
- [ ] On button click:
  ```ts
  POST /api/cloud-scraper/trigger
  {
    scrapeConfigIds: selected.map(m => m.scrapeConfigId),
    workerCount: workerCount,
    useSpot: useSpot,
    scrapeDate: null  // optional future enhancement
  }
  ```

### 4.5 — Service layer
- [ ] Add to `scrapeService.ts` (or new `cloudScraperService.ts`):
  - `getAvailableMatriculas(): Promise<AvailableMatriculaDto[]>`
  - `triggerCloudScraper(payload): Promise<TriggerResponse>`
- [ ] Types in `types/cloudScraper.ts`

### 4.6 — Verification
- [ ] Render both columns; move items back and forth
- [ ] Submit with 2 selected, workerCount=2, useSpot=true
- [ ] Verify request payload in browser network tab
- [ ] Verify toast and API response

---

## Phase 5 — Telemetry, Observability & CI/CD Polish
> **Goal:** Production-grade logging, metrics, and automated deployment pipeline. Everything visible and debuggable.

### 5.1 — CloudWatch logging for `pbi-scraper` worker
- [ ] Worker logs must be structured JSON (use `console.log(JSON.stringify({ level, message, jobId, matricula, timestamp }))`)
- [ ] ECS Task Definition already routes `stdout` to CloudWatch via `awslogs` driver (configured in Phase 3.2)
- [ ] Log events: worker start, message received, decrypt OK/FAIL, scrape start/end, DB save OK/FAIL, message deleted, scale-to-zero exit, SIGTERM received
- [ ] Log retention: set CloudWatch log group retention to 30 days (cost)

### 5.2 — DynamoDB job status logging
- [ ] Reuse existing `IScrapeDynamoLogService` pattern
- [ ] Worker writes job status directly to DynamoDB (not via callback to `salesapp-api`):
  - On receive: `status: "Running"`
  - On success: `status: "Succeeded"`, `rowCount`, `completedAt`
  - On failure: `status: "Failed"`, `errorMessage`
- [ ] Worker needs `DATABASE_URL` or a callback URL to import scraped data — decide pattern:
  - **Option A:** Worker saves raw data to DB directly (needs DB access from Fargate — simpler, tighter coupling)
  - **Option B:** Worker calls `salesapp-api` callback endpoint with results (mirrors current pattern — no DB from worker)
  - **Recommendation:** Option B (keeps pbi-scraper stateless re: DB schema, reuses existing `HandleCallbackAsync`)

### 5.3 — SQS Dead Letter Queue (DLQ)
- [ ] Configure DLQ for the SQS queue: `maxReceiveCount = 3` (message retried 3× before going to DLQ)
- [ ] CloudWatch Alarm: trigger alert when DLQ depth > 0
- [ ] Log DLQ messages to CloudWatch for post-mortem analysis

### 5.4 — `appsettings.json` final structure
- [ ] Document the full final `appsettings.json` with all AWS sections filled:
  ```json
  "AWS": {
    "Region": "us-east-1",
    "DynamoDbTable": "pbi_scrape_logs",
    "CloudWatchLogGroup": "/salesapp/api/errors",
    "SqsQueueUrl": "",
    "EcsClusterArn": "",
    "EcsTaskDefinitionArn": "",
    "EcsSubnetIds": "",
    "EcsSecurityGroupIds": ""
  }
  ```
- [ ] All sensitive values (`SqsQueueUrl`, `EcsClusterArn`, etc.) are injected via environment, not committed

### 5.5 — GitHub Actions CI/CD additions
- [ ] **New workflow:** `deploy-pbi-scraper-worker.yml`
  - Triggers: push to `main` that modifies `pbi-scraper/**`
  - Steps: `docker build` → ECR push → `aws ecs update-service --force-new-deployment` (if running as a long-lived service) or just ECR push (if task-per-run)
- [ ] **Existing workflow:** extend to inject `SCRAPER_ENCRYPTION_KEY` and AWS SQS/ECS vars into `salesapp-api` environment
- [ ] Secret rotation: document procedure for rotating `SCRAPER_ENCRYPTION_KEY` (update GitHub Secret → redeploy both services)

### 5.6 — IAM least-privilege audit
- [ ] `salesapp-api` IAM policy needs: `sqs:SendMessage`, `sqs:SendMessageBatch`, `ecs:RunTask`, `iam:PassRole` (for ECS task role)
- [ ] `pbi-scraper` task IAM role needs: `sqs:ReceiveMessage`, `sqs:DeleteMessage`, `sqs:ChangeMessageVisibility`, `sqs:GetQueueAttributes`, `dynamodb:PutItem`, `logs:PutLogEvents`, `ecr:GetAuthorizationToken`, `ecr:BatchGetImage`
- [ ] No `*` actions; scope all to specific ARNs

### 5.7 — Fargate Spot cost & reliability notes
- [ ] Spot interruption rate is low but non-zero — design SQS visibility timeout (`VisibilityTimeout`) to be longer than max expected scrape time (e.g., 10 minutes)
- [ ] On SIGTERM: worker does not delete message → message reappears after visibility timeout → retried by next available worker
- [ ] Monitor: CloudWatch metric `NumberOfMessagesSent` vs `NumberOfMessagesDeleted` to detect stuck messages
- [ ] Optional: CloudWatch alarm on `ApproximateAgeOfOldestMessage` > 30 minutes

### 5.8 — Final end-to-end smoke test (staging)
- [ ] Deploy to staging environment
- [ ] Open "Cloud Scraper" UI, select 3 matrícula, set workerCount=2, useSpot=true
- [ ] Click trigger → verify 3 SQS messages sent, 2 Fargate tasks launched
- [ ] Monitor CloudWatch logs for each worker
- [ ] Verify all 3 jobs reach "Succeeded" in DynamoDB
- [ ] Verify imported data appears in the contracts view

---

## Cross-Cutting Guidelines (Apply Throughout All Phases)

### Security
- `SCRAPER_ENCRYPTION_KEY` is never logged, never in `appsettings.json`, never in source control
- Each encrypted field has its own IV (12 bytes, random per call) — no IV reuse
- GCM Auth Tag is always validated on decrypt — tampered messages throw, not silently corrupt
- SQS messages do not contain plaintext credentials at any point in transit

### Code Quality
- New C# services follow the existing interface-first pattern (`I<ServiceName>` + implementation)
- New Node.js modules are pure functions where possible (input → output, no side effects inside)
- Side effects (SQS, DB, Puppeteer) are isolated at the worker loop boundary
- No mutation of shared state between concurrent scrapes

### Testability
- `decryptField()` in Node.js: unit-testable with known vectors
- `CredentialEncryptionService` in C#: unit-testable with mock key injection
- `CloudScraperQueueService`: testable with `IAmazonSQS` mock (or `LocalStack`)
- `FargateScraperLaunchService`: testable with `IAmazonECS` mock

### Local Development DX
- `SCALE_TO_ZERO=false` → worker keeps polling indefinitely (no unexpected exits during debugging)
- `NODE_ENV=development` → load `.env` file, use `AWS_PROFILE` credential chain
- `docker compose up` still works for the existing stack — new worker is opt-in (`docker compose --profile worker up`)
- A `push-test-message.js` script makes local queue testing instant

---

## Phase Summary

| Phase | Owner | Testable Without | Delivers |
|-------|-------|-----------------|----------|
| 1 — Worker refactor | `pbi-scraper` | AWS | Self-contained Docker worker |
| 2 — Encrypt + SQS | `salesapp-api` | Fargate | End-to-end local round-trip |
| 3 — ECS launch | `salesapp-api` | Frontend | Full cloud worker dispatch |
| 4 — UI | `client` | nothing new | User-facing trigger page |
| 5 — Telemetry + CI/CD | all | nothing | Production observability |
