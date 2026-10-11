# Perguntas Frequentes (FAQ) & Regras do Sistema

Esta seção reúne respostas para as dúvidas técnicas, operacionais e regras de negócio mais importantes do **Sales Dashboard**.

---

## 📑 Índice de Perguntas

1. [Importação de Contratos: O que acontece quando a matrícula na planilha é diferente?](#1-importação-de-contratos-o-que-acontece-quando-a-matrícula-na-planilha-é-diferente)
2. [O status do contrato sempre é atualizado na importação?](#2-o-status-do-contrato-sempre-é-atualizado-na-importação)
3. [Como funciona a opção "Atualizar matrícula em contratos existentes"?](#3-como-funciona-a-opção-atualizar-matrícula-em-contratos-existentes)
4. [Por que preciso cadastrar meu contrato assim que fecho a venda?](#4-por-que-preciso-cadastrar-meu-contrato-assim-que-fecho-a-venda)
5. [O que significa o status "Aguardando importação" em Contratos Solicitados?](#5-o-que-significa-o-status-aguardando-importação-em-contratos-solicitados)
6. [Por que o consultor deve sempre ter uma equipe ativa no Calendário?](#6-por-que-o-consultor-deve-sempre-ter-uma-equipe-ativa-no-calendário)
7. [O que é o módulo de Perguntas e Respostas (QA) no menu?](#7-o-que-é-o-módulo-de-perguntas-e-respostas-qa-no-menu)

---

### <a id="1-importação-de-contratos-o-que-acontece-quando-a-matrícula-na-planilha-é-diferente"></a> 1. Importação de Contratos: O que acontece quando a matrícula na planilha é diferente?

Quando você importa uma planilha de contratos (ex: extraída do Power BI ou gerada pelo sistema) e um contrato existente já está cadastrado com uma determinada matrícula, o comportamento depende da configuração de importação selecionada na tela:

- **O status e os valores do contrato SEMPRE são atualizados** pela nova linha da planilha.
- **O vínculo da matrícula** só será alterado se a opção **"Atualizar matrícula em contratos existentes"** estiver ativada.
- **O consultor atribuído é preservado**: mesmo que a matrícula do contrato seja atualizada, o consultor não perde o contrato. O sistema automaticamente vincula o consultor à nova matrícula para que ele mantenha o acesso.

---

### <a id="2-o-status-do-contrato-sempre-é-atualizado-na-importação"></a> 2. O status do contrato sempre é atualizado na importação?

**Sim, sempre.** 
Independentemente de haver alteração de matrícula ou não, os contratos correspondentes (localizados pelo `Número do Contrato`) têm seus campos de status (`ContractStatusId`, `RawStatus`), data de atualização (`UpdatedAt`) e status ativo (`IsActive = true`) atualizados diretamente a partir da linha importada.

---

### <a id="3-como-funciona-a-opção-atualizar-matrícula-em-contratos-existentes"></a> 3. Como funciona a opção "Atualizar matrícula em contratos existentes"?

No modal de confirmação de importação em lote (`BulkImportModal`), existe a opção **"Atualizar matrícula em contratos existentes"** (`updateMatriculaOnExisting`):

#### Caso A: Opção MARCADA (`true`)
1. **Matrícula Atualizada**: A matrícula vinculada ao contrato passa a ser a nova matrícula presente na planilha.
2. **Retenção do Vendedor**: O usuário atribuído ao contrato é mantido intacto.
3. **Vínculo Automático**: Se o usuário ainda não possuir ligação com a nova matrícula na tabela de permissões, o sistema cria o vínculo automaticamente.
4. **Auditoria**: O sistema registra uma entrada no histórico de alterações de matrícula (`MatriculaChanges`) indicando o número do contrato, a matrícula anterior e a nova matrícula.

#### Caso B: Opção DESMARCADA (`false` — Padrão)
1. **Matrícula Inalterada**: O contrato permanece vinculado à matrícula que já possuía anteriormente no sistema.
2. **Outros Campos Atualizados**: Valores, status do contrato e datas continuam sendo atualizados normalmente.

#### 📊 Tabela Resumo:
| Cenário | Status Atualizado? | Matrícula Atualizada? | Atribuição do Vendedor |
| :--- | :---: | :---: | :--- |
| **Opção Marcada (`true`)** | Sim | Sim (Atualiza para a nova) | Mantido e vinculado à nova matrícula |
| **Opção Desmarcada (`false`)** | Sim | Não (Mantém a matrícula atual) | Inalterado |

---

### <a id="4-por-que-preciso-cadastrar-meu-contrato-assim-que-fecho-a-venda"></a> 4. Por que preciso cadastrar meu contrato assim que fecho a venda?

No menu **Meus Contratos** (`#/my-contracts`), no botão **`+ Novo`**, é fundamental registrar a venda imediatamente após o fechamento. 
Se o consultor esquecer de registrar o contrato no sistema, ele corre o risco de **ficar sem receber a comissão** correspondente até que o contrato seja devidamente incluído e regularizado na apuração oficial.

---

### <a id="5-o-que-significa-o-status-aguardando-importação-em-contratos-solicitados"></a> 5. O que significa o status "Aguardando importação" em Contratos Solicitados?

É um comportamento perfeitamente normal. Quando você solicita um contrato recém-fechado que ainda não constava na última base de dados extraída, ele fica na lista de **Contratos Solicitados** aguardando a próxima sincronização.
Assim que o gestor ou administrador sobe a atualização da planilha no sistema, o contrato é reconhecido e vinculado à sua conta de forma 100% automática.

---

### <a id="6-por-que-o-consultor-deve-sempre-ter-uma-equipe-ativa-no-calendário"></a> 6. Por que o consultor deve sempre ter uma equipe ativa no Calendário?

Na tela de **Calendário de Equipes** (`#/teams/calendar`), a equipe mais recente do consultor deve **sempre estar sem data final** (ou seja, com status "Atual" / Ativa).
Se um consultor ficar sem nenhuma equipe ativa cadastrada, as novas vendas e importações não conseguirão associá-lo corretamente aos relatórios de produção e dashboards da liderança.

---

### <a id="7-o-que-é-o-módulo-de-perguntas-e-respostas-qa-no-menu"></a> 7. O que é o módulo de Perguntas e Respostas (QA) no menu?

No menu lateral, o item **QA** (`#/qa`) é o canal interativo onde os consultores e membros da equipe respondem a questionários, pesquisas de alinhamento e enquetes operacionais enviadas pela diretoria ou administradores (`#/surveys`).
Sempre que houver uma nova pergunta com resposta pendente, um selo numérico vermelho aparecerá no item QA do menu para alertar você.
