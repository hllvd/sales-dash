# TODO: Estratégias de Extração e Filtragem por Data no Scrape Consultor

Este documento rastreia todas as abordagens, testes realizados e próximas possibilidades para o scrape da visualização **Consultor** no `pbi-scraper`.

---

## 📋 Status Geral das Etapas

- [x] **Item 1: Navegação Automatizada** — Login no AvaPro e redirecionamento para o dashboard de Consultor (`/dashboard/consultor`).
- [x] **Item 2: Virtualização no Scroll** — Posicionamento do cursor no centro da tela e disparo de `PageDown` + `mouse.wheel` para carregar a carteira inteira.
- [x] **Item 3: Critério de Parada** — Encerramento automático após 30 segundos consecutivos sem novas queries interceptadas.
- [x] **Item 4: Decodificação de 64 bits (BigInt)** — Suporte a bitmask `R` e `Ø` para tabelas de 55 colunas e alinhamento de datas, grupos, cotas e nomes.
- [x] **Item 5: Métricas de Execução** — Registro de data/hora de início, fim e duração total no terminal e no JSON.
- [ ] **Item 6: Filtragem por Datas / Meses (`SCRAPE_DATES`)** — *Em análise / testes*.

---

## 🔍 Possibilidades para o Item 6 (Filtragem por Datas)

### 🔴 Abordagens Já Testadas

- [x] **Abordagem 1: Filtragem em memória pós-scroll (Puppeteer)** — **REVERTIDA**
  - **Mecanismo**: O Puppeteer rodava o fluxo de scroll completo, extraía todas as cotas da carteira do consultor (~1.625 registros) e, antes de salvar o CSV, filtrava pela coluna `2 Rel Carteira.Data.Venda`.
  - **Por que não atendeu**:
    1. O processo continuava lento (demorava os 2 a 3 minutos inteiros de scroll para carregar tudo e só filtrar no final).
    2. A passagem de argumentos via CLI/variáveis de ambiente no shell não refletiu a filtragem esperada pelo usuário.
  - **Status**: Alterações revertidas do código base. Mantidas intactas as métricas de tempo de execução (`Início`, `Fim`, `Duração`).

---

### 🟢 Próximas Possibilidades a Testar

- [x] **Possibilidade A: Extração Direta HTTP POST com Filtro de Data e Paginação Automática** 🚀 *(CONCLUÍDO / EM VALIDAÇÃO)*
  - **Mecanismo**:
    1. Template real de 55 colunas interceptado e salvo em `templates/consultorQueryTemplate.json`.
    2. Token de autorização específico do Consultor (`MWCToken`) capturado e armazenado com cache de 40 min.
    3. Cláusula `Where` com filtro de data por intervalo: `Data.Venda >= datetime'...' E Data.Venda < datetime'...'` (`ComparisonKind: 2` e `3`).
    4. **Paginação Automática (`RestartTokens` + `IC`)**: Executa o equivalente ao "scrolldown", mas via chamadas HTTP sequenciais de ~1s cada, solicitando os próximos blocos de 500 registros até `IC === true`, recuperando 100% dos dados filtrados sem limitação.
  - **Vantagens**:
    - **Execução em poucos segundos**: Cada bloco de 500 registros retorna em ~1s.
    - **100% dos registros**: Não fica limitado ao teto inicial de 499 registros.
    - **Leve**: Consumo mínimo de CPU e RAM no ECS Fargate.

- [ ] **Possibilidade B: Interceptação do Payload Real da Query 3 + Replay com Injeção de Filtro**
  - **Mecanismo**:
    1. O script intercepta o corpo da requisição (`req.postData()`) da query com as 55 colunas e salva um template `scratch/consultor_template_query.json`.
    2. Modifica a cláusula `Where` inserindo os meses informados.
    3. Dispara via HTTP para validar a sintaxe exata aceita pelo modelo do Consultor.

- [ ] **Possibilidade C: Diagnóstico e Ajuste do CLI para Filtro Local**
  - **Mecanismo**:
    1. Adicionar logs imediatos na primeira linha de execução exibindo: `[DEBUG] Argumentos recebidos do terminal: ...`.
    2. Tratar a passagem de argumentos no npm scripts usando o delimitador `--`.
    3. Validar se a coluna de filtro deve ser `2 Rel Carteira.Data.Venda` ou outra data da carteira.
