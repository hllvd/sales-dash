# Tutorial: Central de Solicitações

### 📖 O que é?
A Central de Solicitações é uma funcionalidade que permite aos usuários e administradores solicitarem alterações cadastrais e atribuições de novas matrículas de forma organizada e segura. Com ela, mudanças críticas (como a troca de gestor imediato ou criação de novas matrículas) passam por um fluxo de aprovação obrigatório por superiores ou administradores, garantindo a integridade dos dados na plataforma sem a necessidade de intervenção técnica direta.

### ⚙️ Passo a Passo

#### Como criar uma Nova Solicitação:
1. Navegue até a **Central de Solicitações** através do menu lateral ou acesse diretamente pelo endereço hash correspondente (`#/requests`).
2. Clique no botão vermelho **Nova Solicitação** no canto superior direito.
3. No modal que abrir, selecione o **Tipo de Solicitação**:
   - **Alteração de Superior (E-mail)**: Para solicitar a troca do seu gestor direto informando o e-mail dele.
   - **Solicitação de Nova Matrícula**: Para solicitar a atribuição de um número de matrícula.
   - **Criação de Matrícula (Admin / Proprietário)**: Disponível para o perfil de Admin para criar uma nova matrícula que este será proprietário.
4. Preencha os campos obrigatórios (E-mail do Novo Superior ou Número da Matrícula) e clique em **Enviar Solicitação**.

#### Como acompanhar suas solicitações:
1. Acesse a aba **Minhas Solicitações** na Central de Solicitações.
2. Lá você verá uma tabela com o histórico de solicitações que realizou, contendo o tipo, os detalhes, o status atual (**Pendente**, **Aprovado** ou **Rejeitado**), o nome do aprovador e possíveis comentários de rejeição.

#### Como aprovar ou rejeitar solicitações (para Administradores e SuperAdmins):
1. Acesse a aba **Solicitações Pendentes** (esta aba só é visível para perfis autorizados de aprovadores).
2. Na tabela de pendências, você verá os detalhes de cada solicitação. Escolha uma das opções na coluna **Ações**:
   - **Sim (Aprovar)**: Executa a alteração solicitada imediatamente no banco de dados.
   - **Não (Rejeitar)**: Abre uma caixa para justificar a rejeição (opcional) e marca a solicitação como rejeitada.
   - **Depois (Adiar)**: Mantém a solicitação no estado Pendente para ser resolvida posteriormente.

### 💡 Principais Aprendizados
* **Fluxo Automatizado**: Ao clicar em "Sim" (Aprovar), a alteração de dados solicitada é aplicada automaticamente no sistema no mesmo instante, sem necessidade de ações adicionais.
* **Segurança e Visibilidade**: Apenas as partes interessadas (o solicitante, o superior direto envolvido na solicitação ou o SuperAdmin) têm acesso para visualizar e atuar sobre as solicitações.
* **Evite Duplicidade**: Certifique-se de que os e-mails de superiores ou números de matrícula informados estejam corretos antes de enviar, evitando retrabalho e rejeições desnecessárias.
