using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email template for password reset notifications
    /// </summary>
    public class PasswordResetEmailTemplate : IEmailTemplate
    {
        private readonly string _fromAddress;

        public PasswordResetEmailTemplate(string fromAddress)
        {
            _fromAddress = fromAddress;
        }

        public EmailMessage Build(Dictionary<string, string> parameters)
        {
            var userName = parameters.GetValueOrDefault("userName", "User");
            var newPassword = parameters.GetValueOrDefault("newPassword", "");
            var userEmail = parameters.GetValueOrDefault("userEmail", "");

            var subject = "Sua senha foi redefinida - SalesApp";
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
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 12px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Senha Redefinida</h1>
        </div>
        <div class=""content"">
            <p>Olá <strong>{userName}</strong>,</p>
            
            <p>Sua senha foi redefinida por um administrador do sistema.</p>
            
            <div class=""password-box"">
                <p style=""margin: 0; font-size: 14px; color: #666;"">Sua nova senha é:</p>
                <p class=""password"">{newPassword}</p>
            </div>
            
            <div class=""warning"">
                <strong>⚠️ Importante:</strong> Por motivos de segurança, recomendamos que você altere esta senha após o primeiro login.
            </div>
            
            <p>Se você não solicitou esta alteração ou tem dúvidas, entre em contato com o administrador do sistema imediatamente.</p>
            
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
