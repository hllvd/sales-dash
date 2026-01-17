using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// Email template for admin notifications
    /// </summary>
    public class AdminNotificationEmailTemplate : IEmailTemplate
    {
        public EmailMessage Build(Dictionary<string, string> parameters)
        {
            var adminEmail = parameters.GetValueOrDefault("adminEmail", "");
            var notificationType = parameters.GetValueOrDefault("notificationType", "Notificação");
            var message = parameters.GetValueOrDefault("message", "");
            var details = parameters.GetValueOrDefault("details", "");

            var subject = $"[SalesApp] {notificationType}";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2196F3; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-radius: 0 0 5px 5px; }}
        .info-box {{ background-color: #fff; border-left: 4px solid #2196F3; padding: 15px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>{notificationType}</h1>
        </div>
        <div class=""content"">
            <p>{message}</p>
            
            {(string.IsNullOrEmpty(details) ? "" : $@"
            <div class=""info-box"">
                <h3>Detalhes:</h3>
                <p>{details}</p>
            </div>
            ")}
            
            <p>Atenciosamente,<br>
            <strong>Sistema SalesApp</strong></p>
        </div>
        <div class=""footer"">
            <p>Este é um email automático, por favor não responda.</p>
        </div>
    </div>
</body>
</html>";

            return new EmailMessage
            {
                To = adminEmail,
                Subject = subject,
                Body = body,
                IsHtml = true
            };
        }
    }
}
