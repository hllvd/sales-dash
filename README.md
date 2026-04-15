# SalesApp Boilerplate

Estrutura base para iniciar um projeto fullstack com:
- ASP.NET Core 8.0 (API)
- React + Vite (Frontend)
- Nginx como proxy reverso
- Docker Compose para orquestração
- Certbot para HTTPS automático

---

## 🏷️ Build ID

Every build generates a unique **Build ID** in the format `YYYYMMDD-HHMMSS-<git-short-sha>` (e.g. `20260223-171007-a3f9c12`).  
It is baked into the API image at build time and displayed as a tooltip when hovering over **Painel de Vendas** in the sidebar.

### CI/CD (automático)

The build ID is generated and injected **automatically** by the GitHub Actions workflow on every push to `main`. No action required.

### Build local (manual)

When building locally with `docker compose`, set `BUILD_ID` before running the build:

```bash
export BUILD_ID="$(date -u +%Y%m%d-%H%M%S)-$(git rev-parse --short HEAD)"
docker compose build
```

Or in a single line:

```bash
BUILD_ID="$(date -u +%Y%m%d-%H%M%S)-$(git rev-parse --short HEAD)" docker compose build
```

> **Note:** Without `BUILD_ID`, the API falls back to `local-<runtime-timestamp>` — fine for local development.

---

## CSV Utility Script

Utilitário TypeScript para processamento de arquivos CSV e geração de templates.

### Localização

```
scripts/util/
```

### Instalação

```bash
cd scripts/util
npm install
```

### Uso

```bash
npm start -- -i <arquivo-entrada> <comando>
```

### Comandos Disponíveis

#### `to-csv`
Converte arquivo de entrada para formato CSV (copia para output com timestamp).
**Nota:** Remove automaticamente as colunas 'Total' e 'Numeric' do arquivo gerado.

```bash
npm start -- -i input.csv to-csv
```

**Saída:** `/output/to-csv-YYYYMMDD-HHMMSS.csv`

---

#### `user-temp`
Gera um template CSV de usuários a partir de um CSV de entrada.
**Nota:** Remove duplicatas baseadas no nome e adiciona colunas de matrícula.

```bash
npm start -- -i users.csv user-temp
```

**Saída:** `/data/output/user-template-YYYYMMDD-HHMMSS.csv`

**Colunas do template:**
- Name
- Email
- Surname
- Role
- ParentEmail
- Matricula
- Owner_Matricula

---

#### `preview`
Visualiza as primeiras 10 linhas do arquivo em uma tabela formatada.

```bash
npm start -- -i data.csv preview
```

**Saída:** Exibe dados no console (não cria arquivo)

---

#### `pv-temp`
Gera um template CSV de Ponto de Venda.

```bash
npm start -- -i data.csv pv-temp
```

**Saída:** `/output/pv-temp-YYYYMMDD-HHMMSS.csv`

**Colunas do template:**
- Código PV
- Nome
- Endereço
- Cidade
- Estado

---

#### `pv-mat`
Gera um template CSV de Matrícula.

```bash
npm start -- -i data.csv pv-mat
```

**Saída:** `/output/pv-mat-YYYYMMDD-HHMMSS.csv`

**Colunas do template:**
- Matrícula
- Nome
- Código PV
- Status
- Data Ativação

---

### Validação de Parâmetros

O script valida todos os parâmetros e fornece mensagens de erro claras:

```bash
# Arquivo de entrada ausente
❌ Error: Input file is required. Use -i <file> to specify the input file.

# Arquivo não encontrado
❌ Error: Input file not found: /path/to/file.csv

# Formato de arquivo inválido
❌ Error: Input file must be a CSV file. Got: .txt

# Comando desconhecido
❌ Error: Unknown command 'invalid'
Valid commands: to-csv, user-temp, pv-temp, pv-mat
```

### Pasta de Saída

Todos os arquivos gerados são salvos na pasta `/output` na raiz do projeto com nomenclatura baseada em timestamp:

```
/output/
├── to-csv-20260122-073000.csv
├── user-template-20260122-073100.csv
├── pv-temp-20260122-073200.csv
└── pv-mat-20260122-073300.csv
```

### Desenvolvimento

#### Compilar TypeScript
```bash
cd scripts/util
npm run build
```

#### Executar Testes
```bash
npm test
```

#### Executar Testes com Cobertura
```bash
npm run test:coverage
```

---


---

## 📊 PowerBI Scraper

Microserviço em Node.js para raspagem de dados do PowerBI com suporte a múltiplos usuários.

### Funcionamento
O sistema permite que cada usuário armazene suas próprias credenciais do PowerBI (`PowerBiUsername` e `PowerBiPassword`) de forma segura (texto plano no banco de dados C#). 

1. **Trigger**: O C# API solicita um job ao Scraper, enviando as credenciais do usuário.
2. **Scraper**: O serviço Node.js utiliza Puppeteer para autenticar no Avapro e capturar o token do PowerBI.
3. **Callback**: O Scraper devolve um CSV com os dados raspados.
4. **Auto-Import**: A API processa o CSV e importa os contratos automaticamente.

### Mock Testing
Para testar o fluxo sem usar credenciais reais de produção, utilize o script de mock:

```bash
cd pbi-scraper
node scratch/test_scraper_logic.js
```

Para simular o callback da API:
```bash
curl -X PUT http://localhost:5000/api/scrape/callback \
  -H "Content-Type: application/json" \
  -d '{"jobId":"test","userId":"d290f1ee-6c54-4b01-90e6-d701748f0851","status":"Succeeded","store":"Mock","matricula":"MOCK","fileRelativePath":"sample.csv"}'
```

---

## ☁️ Backups S3 (Produção)

O projeto inclui um serviço de backup automatizado que sincroniza o banco de dados SQLite para um bucket S3 a cada 24 horas.

### Como Funciona
O serviço utiliza uma imagem Docker do `rclone` configurada para rodar em segundo plano. Ele monta o volume de dados do banco de dados (em modo leitura) e sincroniza o conteúdo com o provedor S3 configurado.

### Configuração
Para ativar os backups no VPS, siga estes passos:

1.  **Acesse o VPS**:
    ```bash
    ssh usuario@seu-ip
    cd ~/sales-dash/backup
    ```

2.  **Crie o arquivo de configuração**:
    ```bash
    nano rclone.conf
    ```

3.  **Configure suas credenciais S3**:
    Cole o seguinte conteúdo e preencha com seus dados:
    ```ini
    [mys3]
    type = s3
    provider = AWS
    access_key_id = SU_ACCESS_KEY
    secret_access_key = SUA_SECRET_KEY
    region = sua-regiao
    endpoint = s3.sua-regiao.amazonaws.com
    ```

4.  **Reinicie o serviço** (opcional):
    O serviço irá detectar o arquivo automaticamente, mas você pode garantir rodando:
    ```bash
    docker compose -f docker-compose.prod.yml restart backup
    ```

---

### Ajuda

Para ver todas as opções disponíveis:

```bash
npm start -- --help
```

### Documentação Completa

Para mais detalhes, consulte: [scripts/util/README.md](scripts/util/README.md)
