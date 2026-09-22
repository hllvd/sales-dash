# Guia de Configuração AWS — Fargate Scraper & Lambda Launcher

Este guia contém os comandos exatos para configurar a infraestrutura na AWS uma única vez.
Não é necessário criar VPC customizada — usamos a **VPC e Subnet padrão** com IP público direto (`assignPublicIp: ENABLED`).

---

## 1. Criar o Repositório ECR

```bash
aws ecr create-repository \
  --repository-name pbi-scraper-worker \
  --region us-east-1
```
*Anote a URI retornada, ex:* `123456789012.dkr.ecr.us-east-1.amazonaws.com/pbi-scraper-worker`

---

## 2. Criar a Fila SQS de Entrada (Jobs)

```bash
aws sqs create-queue \
  --queue-name hdev-sales-scrape-jobs \
  --attributes VisibilityTimeout=900 \
  --region us-east-1
```
*Anote a URL da fila retornada.*

---

## 3. Criar o Cluster ECS

```bash
aws ecs create-cluster \
  --cluster-name pbi-scraper-cluster \
  --capacity-providers FARGATE_SPOT FARGATE \
  --default-capacity-provider-strategy capacityProvider=FARGATE_SPOT,weight=1 \
  --region us-east-1
```

---

## 4. Criar Roles IAM para o ECS

### A. Execution Role (para o ECS puxar imagem do ECR e logs):
```bash
aws iam attach-role-policy \
  --role-name ecsTaskExecutionRole \
  --policy-arn arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy
```

### B. Task Role (permissões do worker Node.js para SQS, S3 e logs):
Crie o arquivo `task-policy.json`:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "sqs:ReceiveMessage",
        "sqs:DeleteMessage",
        "sqs:GetQueueAttributes",
        "sqs:SendMessage"
      ],
      "Resource": "*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "s3:PutObject",
        "s3:GetObject"
      ],
      "Resource": "arn:aws:s3:::hdev-sales-dash/*"
    },
    {
      "Effect": "Allow",
      "Action": [
        "logs:CreateLogStream",
        "logs:PutLogEvents"
      ],
      "Resource": "*"
    }
  ]
}
```

E registre a role:
```bash
aws iam create-role \
  --role-name pbi-scraper-task-role \
  --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"ecs-tasks.amazonaws.com"},"Action":"sts:AssumeRole"}]}'

aws iam put-role-policy \
  --role-name pbi-scraper-task-role \
  --policy-name pbi-scraper-task-policy \
  --policy-document file://task-policy.json
```

---

## 5. Registrar a Task Definition no ECS

Edite o arquivo `infra/task-definition.json` substituindo `ACCOUNT_ID` pelo seu ID da AWS e sua chave `SCRAPER_ENCRYPTION_KEY`. Em seguida:

```bash
aws ecs register-task-definition \
  --cli-input-json file://infra/task-definition.json \
  --region us-east-1
```

---

## 6. Criar e Publicar a Lambda Function (Launcher)

### A. Criar a Role da Lambda:
```bash
aws iam create-role \
  --role-name pbi-scraper-lambda-role \
  --assume-role-policy-document '{"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"sts:AssumeRole"}]}'

aws iam put-role-policy \
  --role-name pbi-scraper-lambda-role \
  --policy-name pbi-scraper-lambda-policy \
  --policy-document '{
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": [
          "ecs:RunTask",
          "iam:PassRole",
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ],
        "Resource": "*"
      }
    ]
  }'
```

### B. Descobrir a Subnet padrão e Security Group padrão:
```bash
# Obter uma subnet pública padrão:
aws ec2 describe-subnets \
  --filters "Name=default-for-az,Values=true" \
  --query "Subnets[0].SubnetId" \
  --output text

# Obter o Security Group padrão da VPC:
aws ec2 describe-security-groups \
  --filters "Name=group-name,Values=default" \
  --query "SecurityGroups[0].GroupId" \
  --output text
```

### C. Empacotar e Criar a Lambda:
```bash
cd infra/lambda/scraper-launcher
npm install --production
zip -r function.zip index.js node_modules

aws lambda create-function \
  --function-name pbi-scraper-launcher \
  --runtime nodejs20.x \
  --role arn:aws:iam::ACCOUNT_ID:role/pbi-scraper-lambda-role \
  --handler index.handler \
  --zip-file fileb://function.zip \
  --timeout 30 \
  --environment "Variables={ECS_CLUSTER=pbi-scraper-cluster,ECS_TASK_DEF=pbi-scraper-worker,ECS_SUBNET=SUBNET_ID_AQUI,ECS_SG=SG_ID_AQUI}" \
  --region us-east-1
```

### D. Criar a Function URL pública para a Lambda:
```bash
aws lambda create-function-url-config \
  --function-name pbi-scraper-launcher \
  --auth-type NONE \
  --region us-east-1

aws lambda add-permission \
  --function-name pbi-scraper-launcher \
  --statement-id FunctionURLAllowPublicAccess \
  --action lambda:InvokeFunctionUrl \
  --principal "*" \
  --function-url-auth-type NONE \
  --region us-east-1
```
*Anote a Function URL gerada (ex: `https://xxxxxx.lambda-url.us-east-1.on.aws/`).*

---

## 7. Variáveis de Ambiente no Servidor de Produção (VPS)

No arquivo `.env` do diretório `~/sales-dash` na VPS, adicione:

```env
SCRAPER_LAUNCHER_LAMBDA_URL=https://xxxxxx.lambda-url.us-east-1.on.aws/
SQS_JOBS_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/hdev-sales-scrape-jobs
SQS_RESULTS_QUEUE_URL=https://sqs.us-east-1.amazonaws.com/ACCOUNT_ID/hdev-sales-dash
SCRAPER_ENCRYPTION_KEY=64_CARACTERES_HEX_CHAVE_AES_GCM
```

E no GitHub Secrets do repositório:
- `AWS_ECR_REPOSITORY`: `pbi-scraper-worker`

> **Nota de Arquitetura:** O consumo da fila de resultados `SQS_RESULTS_QUEUE_URL` e a importação dos CSVs do S3 são executados nativamente pela própria API .NET (`salesapp-api`) através do serviço `SqsResultBackgroundConsumerService`, dispensando containers adicionais na VPS e economizando memória RAM.
