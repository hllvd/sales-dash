using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Service for retrieving translated application messages (PT-BR)
    /// </summary>
    public class MessageService : IMessageService
    {
        private static readonly Dictionary<AppMessage, string> Messages = new()
        {
            // Authentication & Authorization
            [AppMessage.LoginSuccessful] = "Login realizado com sucesso",
            [AppMessage.InvalidCredentials] = "Credenciais inválidas",
            [AppMessage.Unauthorized] = "Não autorizado",
            
            // User Messages
            [AppMessage.UserCreatedSuccessfully] = "Usuário criado com sucesso",
            [AppMessage.UserUpdatedSuccessfully] = "Usuário atualizado com sucesso",
            [AppMessage.UserDeletedSuccessfully] = "Usuário excluído com sucesso",
            [AppMessage.UserNotFound] = "Usuário não encontrado",
            [AppMessage.UserRetrievedSuccessfully] = "Usuário recuperado com sucesso",
            [AppMessage.UsersRetrievedSuccessfully] = "Usuários recuperados com sucesso",
            [AppMessage.EmailAlreadyExists] = "Email já existe",
            [AppMessage.InvalidRole] = "Função inválida. Deve ser: user, admin ou superadmin",
            [AppMessage.CurrentUserRetrievedSuccessfully] = "Usuário atual recuperado com sucesso",
            [AppMessage.UserCreatedButMatriculaFailed] = "Usuário criado com sucesso, mas a atribuição de matrícula falhou: {0}",
            [AppMessage.PasswordResetSuccessfully] = "Senha redefinida com sucesso",
            [AppMessage.PasswordResetWithEmailSent] = "Senha redefinida com sucesso. Um email foi enviado para o usuário com a nova senha.",
            [AppMessage.EmailSendFailed] = "Senha redefinida, mas falha ao enviar email: {0}",
            [AppMessage.CannotResetOwnPassword] = "Você não pode redefinir sua própria senha usando este endpoint",
            
            // Contract Messages
            [AppMessage.ContractCreatedSuccessfully] = "Contrato criado com sucesso",
            [AppMessage.ContractUpdatedSuccessfully] = "Contrato atualizado com sucesso",
            [AppMessage.ContractDeletedSuccessfully] = "Contrato excluído com sucesso",
            [AppMessage.ContractNotFound] = "Contrato não encontrado",
            [AppMessage.ContractRetrievedSuccessfully] = "Contrato recuperado com sucesso",
            [AppMessage.ContractsRetrievedSuccessfully] = "Contratos recuperados com sucesso",
            [AppMessage.ContractAssignedSuccessfully] = "Contrato atribuído com sucesso",
            [AppMessage.ContractNumberAlreadyExists] = "Número de contrato já existe",
            
            // Group Messages
            [AppMessage.GroupCreatedSuccessfully] = "Grupo criado com sucesso",
            [AppMessage.GroupUpdatedSuccessfully] = "Grupo atualizado com sucesso",
            [AppMessage.GroupDeletedSuccessfully] = "Grupo excluído com sucesso",
            [AppMessage.GroupNotFound] = "Grupo não encontrado",
            [AppMessage.GroupRetrievedSuccessfully] = "Grupo recuperado com sucesso",
            [AppMessage.GroupsRetrievedSuccessfully] = "Grupos recuperados com sucesso",
            [AppMessage.GroupNameAlreadyExists] = "Nome do grupo já existe",
            
            // Role Messages
            [AppMessage.RoleCreatedSuccessfully] = "Função criada com sucesso",
            [AppMessage.RoleUpdatedSuccessfully] = "Função atualizada com sucesso",
            [AppMessage.RoleDeletedSuccessfully] = "Função excluída com sucesso",
            [AppMessage.RoleNotFound] = "Função não encontrada",
            [AppMessage.RoleRetrievedSuccessfully] = "Função recuperada com sucesso",
            [AppMessage.RolesRetrievedSuccessfully] = "Funções recuperadas com sucesso",
            [AppMessage.RoleNameAlreadyExists] = "Nome da função já existe",
            [AppMessage.InvalidRoleName] = "Nome de função inválido: {0}",
            
            // Matricula Messages
            [AppMessage.MatriculaCreatedSuccessfully] = "Matrícula criada com sucesso",
            [AppMessage.MatriculaUpdatedSuccessfully] = "Matrícula atualizada com sucesso",
            [AppMessage.MatriculaDeletedSuccessfully] = "Matrícula excluída com sucesso",
            [AppMessage.MatriculaNotFound] = "Matrícula não encontrada",
            [AppMessage.MatriculaRetrievedSuccessfully] = "Matrícula recuperada com sucesso",
            [AppMessage.MatriculasRetrievedSuccessfully] = "Matrículas recuperadas com sucesso",
            [AppMessage.MatriculaAlreadyAssigned] = "Você já tem esta matrícula atribuída ou solicitada",
            [AppMessage.MatriculaRequestSubmitted] = "Solicitação de matrícula enviada com sucesso. Aguardando aprovação.",
            [AppMessage.MatriculaApprovedSuccessfully] = "Matrícula aprovada com sucesso",
            [AppMessage.MatriculaRejectedSuccessfully] = "Matrícula rejeitada com sucesso",
            [AppMessage.MatriculaNotActive] = "Matrícula não está ativa",
            [AppMessage.MatriculaExpired] = "Matrícula expirou",
            [AppMessage.MatriculaDoesNotBelongToUser] = "Matrícula não encontrada ou não pertence a você",
            [AppMessage.MatriculaOwnershipTransferredSuccessfully] = "Propriedade da matrícula transferida com sucesso",
            
            // Import Messages
            [AppMessage.ImportUploadedSuccessfully] = "Arquivo enviado com sucesso",
            [AppMessage.ImportPreviewRetrievedSuccessfully] = "Pré-visualização da importação recuperada com sucesso",
            [AppMessage.ImportConfiguredSuccessfully] = "Mapeamento de importação configurado com sucesso",
            [AppMessage.ImportExecutedSuccessfully] = "Importação executada com sucesso",
            [AppMessage.ImportNotFound] = "Importação não encontrada",
            [AppMessage.ImportDeletedSuccessfully] = "Importação excluída com sucesso",
            [AppMessage.ImportsRetrievedSuccessfully] = "Importações recuperadas com sucesso",
            [AppMessage.MissingRequiredFields] = "Campos obrigatórios ausentes",
            [AppMessage.InvalidTotalAmount] = "Valor total inválido: {0}",
            [AppMessage.InvalidGroupId] = "ID de grupo inválido: {0}",
            [AppMessage.GroupNotFoundForImport] = "Grupo não encontrado: {0}",
            [AppMessage.UserNotFoundOrInactive] = "Usuário não encontrado ou inativo: {0}",
            [AppMessage.InvalidStartDate] = "Data de início inválida: {0}",
            [AppMessage.MatriculaNotFoundForImport] = "Matrícula '{0}' não encontrada",
            [AppMessage.MatriculaNotActiveForImport] = "Matrícula '{0}' não está ativa",
            [AppMessage.UserAlreadyExists] = "Usuário com email {0} já existe",
            [AppMessage.MatriculaDoesNotBelongToUserForImport] = "Matrícula '{0}' não pertence ao usuário {1}",
            
            // File Parser Messages
            [AppMessage.UnsupportedFileType] = "Tipo de arquivo não suportado: {0}",
            [AppMessage.ExcelFileContainsNoWorksheets] = "Arquivo Excel não contém planilhas",
            
            // Hierarchy Messages
            [AppMessage.ParentRetrievedSuccessfully] = "Pai recuperado com sucesso",
            [AppMessage.ChildrenRetrievedSuccessfully] = "Filhos recuperados com sucesso",
            [AppMessage.TreeRetrievedSuccessfully] = "Árvore recuperada com sucesso",
            [AppMessage.LevelRetrievedSuccessfully] = "Nível recuperado com sucesso",
            [AppMessage.RootUserRetrievedSuccessfully] = "Usuário raiz recuperado com sucesso",
            [AppMessage.CircularReferenceDetected] = "Referência circular detectada na hierarquia de usuários",
            [AppMessage.ParentUserDoesNotExistOrInactive] = "Usuário pai não existe ou está inativo",
            
            // PV Messages
            [AppMessage.PVCreatedSuccessfully] = "PV criado com sucesso",
            [AppMessage.PVUpdatedSuccessfully] = "PV atualizado com sucesso",
            [AppMessage.PVDeletedSuccessfully] = "PV excluído com sucesso",
            [AppMessage.PVNotFound] = "PV não encontrado",
            [AppMessage.PVRetrievedSuccessfully] = "PV recuperado com sucesso",
            [AppMessage.PVsRetrievedSuccessfully] = "PVs recuperados com sucesso",
            [AppMessage.PVCodeAlreadyExists] = "Código PV já existe",
            
            // Historic Production
            [AppMessage.HistoricProductionRetrievedSuccessfully] = "Produção histórica recuperada com sucesso",
            
            // Template Messages
            [AppMessage.TemplateNotFound] = "Modelo não encontrado",
            [AppMessage.TemplatesRetrievedSuccessfully] = "Modelos recuperados com sucesso",
            
            // Deprecated
            [AppMessage.EndpointDeprecated] = "Este endpoint está obsoleto. Por favor, use /api/usermatriculas para gerenciar matrículas.",
            
            // Generic
            [AppMessage.OperationSuccessful] = "Operação realizada com sucesso",
            [AppMessage.OperationFailed] = "Operação falhou",
            [AppMessage.InvalidInput] = "Entrada inválida",
            
            // Configuration
            [AppMessage.JwtKeyNotConfigured] = "Chave JWT não configurada",
            
            // Validation
            [AppMessage.InvalidContractStatus] = "Status de contrato inválido: {0}. Valores válidos são: Active, Late1, Late2, Late3, Defaulted",
            [AppMessage.InvalidMatriculaStatus] = "Status de matrícula inválido: {0}. Valores válidos são: active, pending, inactive, approved, rejected",
            [AppMessage.InvalidContractType] = "Tipo de contrato inválido: {0}. Deve ser 'lar' ou 'motores'",
            [AppMessage.InvalidRoleId] = "ID de função inválido: {0}"
        };
        
        /// <summary>
        /// Gets the translated message for the specified message key
        /// </summary>
        public string Get(AppMessage message)
        {
            return Messages.TryGetValue(message, out var text) ? text : message.ToString();
        }
        
        /// <summary>
        /// Gets the translated message with formatted parameters
        /// </summary>
        public string Get(AppMessage message, params object[] args)
        {
            var template = Get(message);
            try
            {
                return string.Format(template, args);
            }
            catch (FormatException)
            {
                // If formatting fails, return template with args appended
                return $"{template} [{string.Join(", ", args)}]";
            }
        }
    }
}
