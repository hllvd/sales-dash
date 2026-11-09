# Como Atualizar a Senha da Conta no Robô PowerBI

Este guia ensina o passo a passo para atualizar a senha de acesso das contas configuradas na **Extração PowerBI** (`#/scrapes`) sempre que a credencial corporativa for alterada ou o sistema indicar o alerta **"SENHA EXPIRADA / INVÁLIDA"**.

---

## 1. Contexto e Pré-requisitos
- Ter perfil com permissão de acesso à tela de **Extração Power Bi** (acessível pelo menu **Importação** > **Extração Power Bi** ou pela rota `#/scrapes`).
- Ter a nova senha de acesso válida ao portal corporativo (Avapro / PowerBI) em mãos.

---

## 2. Passo a Passo

### Passo 1: Localizar a Conta com Alerta de Credencial
1. No menu lateral, clique na seção **Importação** e selecione a opção **Extração Power Bi** (ou acesse diretamente a rota `#/scrapes`).
2. Permaneça na aba **"Vínculos de Contas"**.
3. Na tabela **Contas PowerBI Configuradas**, localize a linha da matrícula que apresenta o status em vermelho **"SENHA EXPIRADA / INVÁLIDA"**.

![Lista de Contas PowerBI e Status da Credencial](/docs/images/powerbi-scrape-lista-contas.jpg)

---

### Passo 2: Abrir a Edição da Conta
1. Na linha da conta que precisa de atualização, localize a coluna **Ações** (à direita).
2. Clique no ícone de **Engrenagem** (⚙️ *Editar*).

---

### Passo 3: Inserir a Nova Senha
1. No modal **Editar Conta PowerBI**, localize o campo **Senha do Portal/Avapro**.
2. Digite a sua nova senha de acesso.
> 💡 **Dica:** O campo é protegido por padrão. Você pode clicar no ícone de "olho" para exibir os caracteres e certificar-se de que não houve erro de digitação.

![Modal de Edição da Conta PowerBI](/docs/images/powerbi-scrape-editar-conta.jpg)

---

### Passo 4: Salvar e Validar
1. Certifique-se de que a caixa **"Validar credenciais ao salvar"** ao final do modal esteja selecionada.
2. Clique no botão **"Salvar Configuração"**.
3. O robô testará o login de forma automatizada. Se tudo estiver correto, a notificação de sucesso aparecerá e o status da credencial voltará a ficar regularizado.

---

## 3. Teste Rápido de Autenticação (Sem Editar)
Se quiser verificar o funcionamento do login sem abrir o modal de edição:
1. Na lista de contas, clique no ícone azul de **Usuário com Check** (👤✓ *Testar Autenticação*).
2. O sistema abrirá uma janela diagnóstica informando se o login foi concluído com sucesso ou se ainda há alguma pendência na credencial.
