# Tutorial: Atualizar Senha do Robô de Extração do PowerBI

### 📖 O que é?
O robô de extração do PowerBI (Scraper) realiza o login automatizado no portal da Avapro/PowerBI para consultar e sincronizar a carteira de contratos e vendas dos consultores e lojas. Quando a sua senha do portal corporativo é alterada ou expira, o sistema exibe o status de alerta **"SENHA EXPIRADA / INVÁLIDA"** e pausa as coletas agendadas até que a credencial seja atualizada.

---

### ⚙️ Passo a Passo

#### 1. Acessar a Tela de Extração PowerBI
1. No menu lateral do sistema, clique na seção **Importação** para expandir as opções.
2. Dentro de **Importação**, selecione **Extração Power Bi** (ou navegue diretamente para a rota `#/scrapes`).
3. Certifique-se de estar na aba **"Vínculos de Contas"**.
4. Localize na tabela a conta/matrícula que apresenta o status em vermelho **"SENHA EXPIRADA / INVÁLIDA"** (ou qualquer conta cuja senha você tenha acabado de alterar no portal da empresa).

![Lista de Contas PowerBI e Status da Credencial](/docs/images/powerbi-scrape-lista-contas.jpg)

---

#### 2. Abrir o Formulário de Edição da Conta
1. Na linha da matrícula desejada, vá até a coluna **Ações** (à direita).
2. Clique no ícone de **Engrenagem** (⚙️ *Editar Conta*) para abrir a janela de configuração.

---

#### 3. Informar a Nova Senha
1. Na janela **"Editar Conta PowerBI"**, localize o campo **Senha do Portal/Avapro**.
2. Digite a sua nova senha de acesso no campo de texto (você pode clicar no ícone de "olho" para conferir se digitou corretamente).
> *Nota: Se você deixar o campo em branco, o sistema manterá a senha anterior sem alterações.*

![Modal de Edição da Conta PowerBI](/docs/images/powerbi-scrape-editar-conta.jpg)

---

#### 4. Validar e Salvar a Nova Credencial
1. Ao final do modal, mantenha marcada a caixa **"Validar credenciais ao salvar"**. Com essa opção ativa, o robô testará o login em tempo real no PowerBI antes de salvar, garantindo que a nova senha está 100% funcional.
2. Clique no botão azul **"Salvar Configuração"**.
3. Aguarde alguns segundos enquanto o robô realiza o teste de login:
   - Se os dados estiverem corretos, uma notificação de sucesso será exibida e a janela fechará automaticamente.
   - O status da credencial na lista mudará para **"VÁLIDA / AUTENTICADA"** (ou verde).

---

#### 5. (Opcional) Testar Manualmente a Credencial
Caso queira testar a qualquer momento se a senha continua válida sem abrir a edição:
- Na coluna **Ações**, clique no ícone azul de **Usuário com Check** (👤✓ *Testar Autenticação*). O robô iniciará um teste rápido de acesso e exibirá o relatório diagnóstico confirmando a validação.

---

### 💡 Principais Aprendizados
* **Segurança e Continuidade:** Sempre que você atualizar sua senha da rede ou portal da empresa, atualize imediatamente a senha correspondente na tela de Extração PowerBI para evitar que suas rotinas automáticas de importação de contratos falhem.
* **Validação Prévia:** Deixar a opção *"Validar credenciais ao salvar"* marcada evita salvar senhas com erros de digitação, prevenindo bloqueios de conta por tentativas inválidas.
* **Preservação de Dados:** Ao editar uma conta, as configurações de Unidade, Período de Extração, Agendamentos (Cron) e Opções Avançadas são preservadas intactas, bastando preencher a nova senha.
