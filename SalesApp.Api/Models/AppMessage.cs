namespace SalesApp.Models
{
    /// <summary>
    /// Centralized application messages for errors and success responses
    /// </summary>
    public enum AppMessage
    {
        // Authentication & Authorization
        LoginSuccessful,
        InvalidCredentials,
        Unauthorized,
        
        // User Messages
        UserCreatedSuccessfully,
        UserUpdatedSuccessfully,
        UserDeletedSuccessfully,
        UserNotFound,
        UserRetrievedSuccessfully,
        UsersRetrievedSuccessfully,
        EmailAlreadyExists,
        InvalidRole,
        CurrentUserRetrievedSuccessfully,
        UserCreatedButMatriculaFailed,
        PasswordResetSuccessfully,
        PasswordResetWithEmailSent,
        EmailSendFailed,
        CannotResetOwnPassword,
        
        // Contract Messages
        ContractCreatedSuccessfully,
        ContractUpdatedSuccessfully,
        ContractDeletedSuccessfully,
        ContractNotFound,
        ContractRetrievedSuccessfully,
        ContractsRetrievedSuccessfully,
        ContractAssignedSuccessfully,
        ContractNumberAlreadyExists,
        
        // Group Messages
        GroupCreatedSuccessfully,
        GroupUpdatedSuccessfully,
        GroupDeletedSuccessfully,
        GroupNotFound,
        GroupRetrievedSuccessfully,
        GroupsRetrievedSuccessfully,
        GroupNameAlreadyExists,
        
        // Role Messages
        RoleCreatedSuccessfully,
        RoleUpdatedSuccessfully,
        RoleDeletedSuccessfully,
        RoleNotFound,
        RoleRetrievedSuccessfully,
        RolesRetrievedSuccessfully,
        RoleNameAlreadyExists,
        InvalidRoleName,
        
        // Matricula Messages
        MatriculaCreatedSuccessfully,
        MatriculaUpdatedSuccessfully,
        MatriculaDeletedSuccessfully,
        MatriculaNotFound,
        MatriculaRetrievedSuccessfully,
        MatriculasRetrievedSuccessfully,
        MatriculaAlreadyAssigned,
        MatriculaRequestSubmitted,
        MatriculaApprovedSuccessfully,
        MatriculaRejectedSuccessfully,
        MatriculaNotActive,
        MatriculaExpired,
        MatriculaDoesNotBelongToUser,
        MatriculaOwnershipTransferredSuccessfully,
        
        // Import Messages
        ImportUploadedSuccessfully,
        ImportPreviewRetrievedSuccessfully,
        ImportConfiguredSuccessfully,
        ImportExecutedSuccessfully,
        ImportNotFound,
        ImportDeletedSuccessfully,
        ImportsRetrievedSuccessfully,
        MissingRequiredFields,
        InvalidTotalAmount,
        InvalidGroupId,
        GroupNotFoundForImport,
        UserNotFoundOrInactive,
        InvalidStartDate,
        MatriculaNotFoundForImport,
        MatriculaNotActiveForImport,
        UserAlreadyExists,
        MatriculaDoesNotBelongToUserForImport,
        
        // File Parser Messages
        UnsupportedFileType,
        ExcelFileContainsNoWorksheets,
        
        // Hierarchy Messages
        ParentRetrievedSuccessfully,
        ChildrenRetrievedSuccessfully,
        TreeRetrievedSuccessfully,
        LevelRetrievedSuccessfully,
        RootUserRetrievedSuccessfully,
        CircularReferenceDetected,
        ParentUserDoesNotExistOrInactive,
        
        // PV Messages
        PVCreatedSuccessfully,
        PVUpdatedSuccessfully,
        PVDeletedSuccessfully,
        PVNotFound,
        PVRetrievedSuccessfully,
        PVsRetrievedSuccessfully,
        PVCodeAlreadyExists,
        
        // Historic Production
        HistoricProductionRetrievedSuccessfully,
        
        // Template Messages
        TemplateNotFound,
        TemplatesRetrievedSuccessfully,
        
        // Deprecated
        EndpointDeprecated,
        
        // Generic
        OperationSuccessful,
        OperationFailed,
        InvalidInput,
        
        // Configuration
        JwtKeyNotConfigured,
        
        // Validation
        InvalidContractStatus,
        InvalidMatriculaStatus,
        InvalidContractType,
        InvalidRoleId
    }
}
