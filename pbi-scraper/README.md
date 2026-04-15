# 📊 PowerBI Scraper Microservice

Microserviço em Node.js projetado para realizar a raspagem de dados (scraping) do PowerBI através do portal Avapro.

## 🚀 Funcionalidades

- **Autenticação Automática**: Utiliza Puppeteer para realizar login no portal Avapro e capturar tokens MWC (PowerBI).
- **Extração de Dados**: Realiza consultas diretas às APIs do PowerBI (Query Execution Service) utilizando os tokens capturados.
- **Suporte a Múltiplos Usuários**: Aceita credenciais dinâmicas (`avaproUsername` e `avaproPassword`) no corpo da requisição, permitindo que cada raspagem use as credenciais do usuário solicitante.
- **Conversão para CSV**: Processa as respostas complexas do PowerBI (formato DSR) e as converte em arquivos CSV simplificados.
- **Callback HTTP**: Notifica a API principal (`SalesApp.Api`) assim que a raspagem é concluída, fornecendo o caminho do arquivo e a contagem de linhas.

## 🛠️ Arquitetura

- **`server.js`**: Servidor Express que gerencia a fila de jobs (`p-queue`) e as rotas da API.
- **`auth.js`**: Lógica de automação do browser para obtenção do token.
- **`extractor.js`**: Lógica de construção de payloads e parsing dos resultados do PowerBI.

## 📋 Pré-requisitos

Os serviços dependem de variáveis de ambiente básicas para fallback caso não sejam fornecidas credenciais dinâmicas:

- `AVAPRO_MATRICULA`: Matrícula padrão.
- `AVAPRO_PASSWORD`: Senha padrão.
- `PBI_TOKEN`: (Opcional) Token manual para testes rápidos.

## 🧪 Testes e Debugging

Existem scripts utilitários na pasta `scratch/` para verificação rápida sem a necessidade de rodar o fluxo completo:

### Testar Autenticação e Extração Real (Produção)
Para testar se o PowerBI mudou e se os seletores de login ainda funcionam:
1. Edite `scratch/tester.js` com credenciais válidas.
2. Execute:
   ```bash
   node scratch/tester.js
   ```

### Testar Lógica de Integração (Mock)
Para testar se a API do scraper está processando os campos corretamente:
```bash
node scratch/test_scraper_logic.js
```

## ⚠️ Manutenção (Solução de Problemas)

### Seletores de Login
Se o login falhar com `No element found for selector`, verifique se o portal Avapro mudou suas classes CSS. O seletor atual prioriza botões com texto "Entrar" e fallback para `button.inline-flex`.

### PowerBI Payload
O PowerBI costuma atualizar frequentemente seus filtros internos (como `DatasetId` ou filtros de data). Caso o scraper retorne **0 linhas** mas a autenticação tenha tido sucesso, capture um novo payload no navegador (aba Network → query) e atualize as funções `buildPayload1` e `buildPayload2` no arquivo `extractor.js`.

---
*Desenvolvido pela equipe de Advanced Agentic Coding.*
