using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email template for welcoming new users with their credentials
    /// </summary>
    public class WelcomeEmailTemplate : IEmailTemplate
    {
        public EmailMessage Build(Dictionary<string, string> parameters)
        {
            if (!parameters.ContainsKey("toEmail") || !parameters.ContainsKey("userName") || !parameters.ContainsKey("password"))
            {
                throw new ArgumentException("Missing required parameters: toEmail, userName, password");
            }

            var toEmail = parameters["toEmail"];
            var userName = parameters["userName"];
            var password = parameters["password"];

            var subject = "Bem-vindo ao SalesApp - Suas Credenciais de Acesso";

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #4CAF50; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .credentials {{ background-color: white; padding: 20px; margin: 20px 0; border-left: 4px solid #4CAF50; }}
        .credential-item {{ margin: 10px 0; }}
        .credential-label {{ font-weight: bold; color: #555; }}
        .credential-value {{ font-family: monospace; background-color: #f0f0f0; padding: 5px 10px; border-radius: 3px; display: inline-block; }}
        .warning {{ background-color: #fff3cd; border-left: 4px solid #ffc107; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #777; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Bem-vindo ao SalesApp!</h1>
        </div>
        <div class='content'>
            <p>Olá <strong>{userName}</strong>,</p>
            
            <p>Sua conta foi criada com sucesso no SalesApp. Abaixo estão suas credenciais de acesso:</p>
            
            <div class='credentials'>
                <div class='credential-item'>
                    <span class='credential-label'>Email:</span>
                    <span class='credential-value'>{toEmail}</span>
                </div>
                <div class='credential-item'>
                    <span class='credential-label'>Senha:</span>
                    <span class='credential-value'>{password}</span>
                </div>
            </div>
            
            <div class='warning'>
                <strong>⚠️ Importante:</strong>
                <ul style='margin: 10px 0; padding-left: 20px;'>
                    <li>Mantenha suas credenciais em segurança</li>
                    <li>Não compartilhe sua senha com ninguém</li>
                    <li>Recomendamos alterar sua senha no primeiro acesso</li>
                    <li>Este email contém informações confidenciais</li>
                </ul>
            </div>
            
            <p>Você já pode acessar o sistema usando as credenciais acima.</p>
            
            <p>Se você tiver alguma dúvida ou precisar de ajuda, entre em contato com o administrador do sistema.</p>
            
            <p>Atenciosamente,<br>
            <strong>Equipe SalesApp</strong></p>
        </div>
        <div class='footer'>
            <p>Este é um email automático, por favor não responda.</p>
        </div>
    </div>
</body>
</html>";

            return new EmailMessage
            {
                To = toEmail,
                Subject = subject,
                Body = body,
                IsHtml = true
            };
        }
    }
}
