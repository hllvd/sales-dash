# Como Usar Solicitações

O módulo de **Solicitações** (`#/requests`) é o canal oficial para você pedir alterações na sua conta, equipe, matrículas ou nível de carreira que dependem de aprovação do seu gestor ou de um SuperAdmin.

---

![Criar Nova Solicitação](/docs/images/solicitacoes-criar-nova.png)

---

## 📑 Índice de Solicitações
Clique no tipo de solicitação desejado para ir direto ao passo a passo:

1. [Alteração de Superior (E-mail)](#1-alteração-de-superior-e-mail)
2. [Solicitar o uso da matrícula do gestor](#2-solicitar-o-uso-da-matrícula-do-gestor)
3. [Eu sou Guimel agora, quero criar minha equipe](#3-eu-sou-guimel-agora-quero-criar-minha-equipe)
4. [Solicitação de Nível de Classificação](#4-solicitação-de-nível-de-classificação)
5. [Solicitação de Matrícula (Proprietário)](#5-solicitação-de-matrícula-proprietário)
6. [Solicitação de Perfil Administrador (Role Admin)](#6-solicitação-de-perfil-administrador-role-admin)
7. [Como Acompanhar o Status da Solicitação](#como-acompanhar-o-status-da-solicitação)

---

## Como Abrir o Modal de Solicitação
1. No menu principal, clique em **Solicitações** (rota: `#/requests`).
2. No canto superior direito, clique no botão **`+ Nova Solicitação`**.
3. No campo **Tipo de Solicitação**, escolha uma das 6 opções explicadas abaixo.

---

### <a id="1-alteração-de-superior-e-mail"></a> 1. Alteração de Superior (E-mail)

- **Quando usar:** Quando você mudou de supervisor, líder direto ou equipe e precisa vincular sua conta ao e-mail do seu novo gestor na árvore hierárquica.
- **Campos a preencher:**
  - **E-mail do Novo Superior:** Informe o e-mail exato do seu novo líder cadastrado no sistema.
- **Como funciona a aprovação:** O novo superior ou o SuperAdmin receberá a notificação para aprovar. Ao ser aprovado, sua árvore hierárquica é atualizada automaticamente.

---

### <a id="2-solicitar-o-uso-da-matrícula-do-gestor"></a> 2. Solicitar o uso da matrícula do gestor

- **Quando usar:** Quando você precisa registrar ou consultar contratos utilizando a matrícula corporativa do seu gestor direto (muito comum em consultores em fase de integração ou atuando sob a matrícula líder).
- **Campos a preencher:**
  - **Número da Matrícula:** Digite apenas os números da matrícula do gestor que você precisa utilizar.
- **Como funciona a aprovação:** O gestor titular da matrícula valida o pedido. Quando aprovado, essa matrícula fica habilitada na sua conta para visualização e atribuição de contratos.

---

### <a id="3-eu-sou-guimel-agora-quero-criar-minha-equipe"></a> 3. Eu sou Guimel agora, quero criar minha equipe

- **Quando usar:** Ao ser promovido ao nível **Guimel** no plano de carreira e estiver pronto para liderar seu próprio time comercial no sistema.
- **Campos a preencher:**
  - **Nome da Equipe:** Escolha o nome oficial da sua nova equipe (exemplo: *Equipe Força Alfa*, *Equipe Sul Comercial*).
- **Como funciona a aprovação:** A solicitação é analisada pelo SuperAdmin. Após a aprovação:
  1. A nova equipe é criada automaticamente no sistema.
  2. Você se torna o **proprietário (Owner)** da equipe.
  3. Seu perfil de usuário é atualizado automaticamente para **Administrador**.

---

### <a id="4-solicitação-de-nível-de-classificação"></a> 4. Solicitação de Nível de Classificação

- **Quando usar:** Quando você atingiu os critérios para subir de nível no plano de carreira (ex: Álef, Bet, Guimel, etc.) e precisa registrar a nova vigência oficial da classificação.
- **Campos a preencher:**
  - **Nível de Classificação Desejado:** Selecione o novo nível na lista suspensa.
  - **Data de Início:** A data em que sua promoção passa a valer oficialmente.
  - **Data de Término do Nível Anterior:** Preenchida automaticamente com a transição do nível anterior.
- **Como funciona a aprovação:** O gestor ou SuperAdmin confirma a vigência da promoção. O histórico de classificações fica registrado para apurações e relatórios.

---

### <a id="5-solicitação-de-matrícula-proprietário"></a> 5. Solicitação de Matrícula (Proprietário)

- **Quando usar:** Utilizado por gestores e proprietários de equipe que precisam cadastrar ou reivindicar uma nova matrícula administrativa ou de filial no sistema.
- **Campos a preencher:**
  - **Número da Matrícula:** Informe o número da matrícula correspondente.
- **Como funciona a aprovação:** Passa pela validação do SuperAdmin para certificar que a matrícula pertence à unidade correta.

---

### <a id="6-solicitação-de-perfil-administrador-role-admin"></a> 6. Solicitação de Perfil Administrador (Role Admin)

- **Quando usar:** Quando um consultor passa a exercer funções de gestão, suporte operacional ou coordenação e necessita de privilégios de Administrador no painel.
- **Campos a preencher:**
  - Não requer campos adicionais; basta selecionar essa opção no menu suspenso.
- **Como funciona a aprovação:** Encaminhada diretamente para o seu superior direto ou SuperAdmin. Ao ser aprovada, suas permissões de acesso são expandidas imediatamente.

---

## <a id="como-acompanhar-o-status-da-solicitação"></a> 📌 Como Acompanhar o Status da Solicitação

Na própria tela de **Solicitações** (`#/requests`), você pode acompanhar o andamento dos seus pedidos:

- Na aba **Minhas Solicitações**, cada registro exibe uma etiqueta (badge) colorida:
  - 🟡 **Pendente:** Aguardando análise do gestor ou SuperAdmin.
  - 🟢 **Aprovado:** O pedido foi aceito e as permissões/mudanças já foram aplicadas.
  - 🔴 **Rejeitado:** O pedido foi recusado (você poderá ver o motivo/justificativa inserido por quem reprovou).

> 💡 **Dica:** Se você é Administrador ou Gestor, a aba **Pendentes de Aprovação** mostrará todos os pedidos da sua equipe aguardando a sua validação, onde você pode clicar em **Aprovar** ou **Rejeitar com Justificativa**.
