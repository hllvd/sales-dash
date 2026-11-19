# Como Atribuir e Solicitar Contratos de Vendas

Este guia explica como funciona o fluxo de atribuição de contratos no sistema, cobrindo a atribuição automática durante as importações do Ava Pro e o processo de solicitação e aprovação de transferência de vendas entre consultores e gestores.

---

## 1. Visão Geral: Atribuição Automática na Importação

Sempre que novos contratos são importados do **Ava Pro / Power BI**, o sistema realiza a vinculação imediata com base na titularidade da matrícula:

1. **Vínculo com o Titular da Matrícula**:
   - Todo contrato novo importado que possua uma matrícula cadastrada é atribuído diretamente ao usuário proprietário (*Owner*) dessa matrícula.
   - O gestor ou consultor titular recebe o contrato em sua carteira (**Meus Contratos**) de forma 100% automática, sem necessidade de ações manuais.

2. **Consultores que utilizam a matrícula do Gestor**:
   - Quando um consultor opera vendendo sob a matrícula corporativa de seu gestor, o contrato importado entrará inicialmente vinculado ao gestor (titular da matrícula).
   - O consultor deve então realizar a **Solicitação de Atribuição** para transferir a venda para a sua própria conta, conforme os passos a seguir.

---

## 2. Passo a Passo: Como o Consultor Solicita a Atribuição

Se você realizou uma venda utilizando a matrícula do seu gestor (ou de outro titular), siga os passos abaixo para transferir o contrato para você:

### Passo 1: Acessar a tela Meus Contratos
1. No menu principal, clique em **Meus Contratos** (rota: `#/my-contracts`).
2. No canto superior direito, clique no botão **`+ Novo`** (ou *Atribuir Contrato*).

### Passo 2: Buscar o Contrato
1. No campo **Número do Contrato**, digite o número da cota/contrato vendido.
2. Clique em **Buscar Contrato** (ou pressione `Enter`).
3. O sistema carregará os dados da venda (cliente, grupo, valor total e status).

### Passo 3: Enviar a Solicitação de Atribuição
1. Como o contrato já está vinculado ao titular da matrícula, o sistema exibirá o alerta:  
   *“Este contrato já está atribuído a [Nome do Titular]. Você pode solicitar a atribuição para você mesmo.”*
2. Clique no botão **`Solicitar Atribuição`**.
3. Uma notificação de confirmação será exibida informando que a solicitação foi enviada para aprovação do seu gestor ou do dono da matrícula.

> 💡 **Nota**: Durante a transferência, a matrícula original da venda é preservada intacta no contrato.

---

## 3. Passo a Passo: Como o Gestor / Titular Aprova a Solicitação

Quando um consultor solicita a atribuição de um contrato, a solicitação fica disponível para avaliação dos responsáveis:

### Passo 1: Acessar a Central de Solicitações
1. No menu principal, clique em **Solicitações** (rota: `#/requests`).
2. Caso existam pedidos aguardando você, um contador numérico em vermelho será exibido no menu e na aba **Solicitações Pendentes**.

### Passo 2: Localizar a Solicitação
1. Na aba **Solicitações Pendentes**, localize o item identificado com a etiqueta azul **Solicitação de Atribuição de Contrato**.
2. Veja as informações:
   - **Solicitante**: Nome e e-mail do consultor que está pedindo a venda.
   - **Detalhes**: Número do contrato e o consultor ao qual está atribuído atualmente.
   - **Data**: Data e hora em que a solicitação foi enviada.

### Passo 3: Aprovar ou Rejeitar
1. Para aprovar:
   - Clique no botão verde **Sim**.
   - O contrato é transferido imediatamente para a carteira do solicitante.
2. Para rejeitar:
   - Clique no botão vermelho **Não**.
   - Digite o motivo da recusa (opcional) e confirme. O contrato permanece na carteira do titular atual.
3. Para adiar a decisão:
   - Clique em **Depois** para manter a solicitação na fila pendente.

---

## 4. Regras de Governança e Aprovação

- **Quem pode aprovar?**
  - O **superior imediato** do solicitante (usuário líder na árvore hierárquica).
  - O **proprietário da matrícula** à qual o contrato pertence.
  - Administradores globais (**SuperAdmin**).
- **Regra de Decisão Rápida (OU)**:
  - Qualquer um dos responsáveis elegíveis (superior ou dono da matrícula) que aprovar primeiro efetiva a transferência na hora.
- **Sem Duplicidade de Notificações**:
  - Quando o superior imediato do consultor for também o dono da matrícula da venda, o sistema exibe apenas **uma única solicitação** na Central de Solicitações dele.
- **Bloqueio de Solicitações Repetidas**:
  - Enquanto um pedido de transferência estiver pendente para um contrato, novas solicitações para o mesmo contrato ficam bloqueadas até que a primeira seja resolvida.

---

## 5. Dúvidas Frequentes

- **O que acontece com a matrícula do contrato após a transferência?**  
  A matrícula original do contrato permanece exatamente a mesma. Apenas o consultor responsável (`Vendedor`) é atualizado para o solicitante.

- **Onde o consultor acompanha o andamento do seu pedido?**  
  Em **Solicitações** (`#/requests`), na aba **Minhas Solicitações**. Assim que o gestor aprovar, o status mudará de *Pendente* para *Aprovado* e o contrato já aparecerá na lista de *Meus Contratos*.
