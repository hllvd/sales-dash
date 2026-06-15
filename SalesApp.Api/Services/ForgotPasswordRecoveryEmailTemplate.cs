using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email template for self-service password recovery notifications
    /// </summary>
    public class ForgotPasswordRecoveryEmailTemplate : IEmailTemplate
    {
        private readonly string _fromAddress;

        public ForgotPasswordRecoveryEmailTemplate(string fromAddress)
        {
            _fromAddress = fromAddress;
        }

        public EmailMessage Build(Dictionary<string, string> parameters)
        {
            var userName = parameters.GetValueOrDefault("userName", "Usuário");
            var newPassword = parameters.GetValueOrDefault("newPassword", "");
            var userEmail = parameters.GetValueOrDefault("userEmail", "");
            var baseUrl = parameters.GetValueOrDefault("baseUrl", "http://localhost");
            var loginUrl = baseUrl;

            var subject = "Recuperação de senha - SalesApp";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .password-box {{ background-color: #fff; border: 2px solid #4CAF50; padding: 15px; margin: 20px 0; border-radius: 5px; text-align: center; }}
        .password {{ font-size: 24px; font-weight: bold; color: #4CAF50; letter-spacing: 2px; }}
        .button-container {{ text-align: center; margin: 30px 0; }}
        .btn {{ background-color: #4CAF50; color: white !important; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold; display: inline-block; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 12px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Recuperação de Senha</h1>
        </div>
        <div class=""content"">
            <p>Olá <strong>{userName}</strong>,</p>
            
            <p>Você solicitou a recuperação da sua senha no SalesApp. Uma nova senha temporária foi gerada para a sua conta.</p>
            
            <div class=""password-box"">
                <p style=""margin: 0; font-size: 14px; color: #666;"">Sua nova senha é:</p>
                <p class=""password"">{newPassword}</p>
            </div>
            
            <div class=""button-container"">
                <a href=""{loginUrl}"" class=""btn"">Acessar o SalesApp</a>
            </div>
            
            <div class=""warning"">
                <strong>⚠️ Importante:</strong> Por motivos de segurança, recomendamos que você altere esta senha após o primeiro login.
            </div>
            
            <p>Se você não solicitou esta redefinição, recomendamos que acesse o sistema com esta nova senha e altere-a imediatamente para garantir a segurança da sua conta.</p>
            
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
