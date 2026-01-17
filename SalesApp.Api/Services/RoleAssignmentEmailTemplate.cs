using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email template for role/qualification assignment notifications
    /// </summary>
    public class RoleAssignmentEmailTemplate : IEmailTemplate
    {
        public EmailMessage Build(Dictionary<string, string> parameters)
        {
            var userName = parameters.GetValueOrDefault("userName", "User");
            var userEmail = parameters.GetValueOrDefault("userEmail", "");
            var roleName = parameters.GetValueOrDefault("roleName", "");
            var qualificationName = parameters.GetValueOrDefault("qualificationName", "");
            var assignedBy = parameters.GetValueOrDefault("assignedBy", "Administrador");

            var isRole = !string.IsNullOrEmpty(roleName);
            var assignmentType = isRole ? "Função" : "Qualificação";
            var assignmentValue = isRole ? roleName : qualificationName;

            var subject = $"Nova {assignmentType} Atribuída - SalesApp";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #9C27B0; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .assignment-box {{ background-color: #fff; border: 2px solid #9C27B0; padding: 20px; margin: 20px 0; border-radius: 5px; text-align: center; }}
        .assignment-value {{ font-size: 22px; font-weight: bold; color: #9C27B0; margin: 10px 0; }}
        .info {{ background-color: #e1bee7; padding: 12px; margin: 20px 0; border-radius: 5px; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 Nova {assignmentType} Atribuída</h1>
        </div>
        <div class=""content"">
            <p>Olá <strong>{userName}</strong>,</p>
            
            <p>Você recebeu uma nova {assignmentType.ToLower()} no sistema SalesApp!</p>
            
            <div class=""assignment-box"">
                <p style=""margin: 0; font-size: 14px; color: #666;"">{assignmentType}:</p>
                <p class=""assignment-value"">{assignmentValue}</p>
            </div>
            
            <div class=""info"">
                <p style=""margin: 0;""><strong>Atribuído por:</strong> {assignedBy}</p>
                <p style=""margin: 5px 0 0 0;""><strong>Data:</strong> {DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC</p>
            </div>
            
            <p>Esta {assignmentType.ToLower()} já está ativa em sua conta. Faça login no sistema para ver suas novas permissões e funcionalidades.</p>
            
            <p>Atenciosamente,<br>
            <strong>Equipe SalesApp</strong></p>
        </div>
        <div class=""footer"">
            <p>Este é um email automático, por favor não responda.</p>
        </div>
    </div>
</body>
</html>";

            return new EmailMessage
            {
                To = userEmail,
                Subject = subject,
                Body = body,
                IsHtml = true
            };
        }
    }
}
