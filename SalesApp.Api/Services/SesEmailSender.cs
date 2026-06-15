using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using SalesApp.Models;

namespace SalesApp.Services
{
    /// <summary>
    /// AWS SES implementation of IEmailSender
    /// Can be replaced with any other email provider (SendGrid, SMTP, etc.)
    /// </summary>
    public class SesEmailSender : IEmailSender
    {
        private readonly string? _fromAddress;
        private readonly string? _accessKey;
        private readonly string? _secretKey;
        private readonly string? _regionName;
        private readonly bool _isConfigured;

        public SesEmailSender(IConfiguration configuration)
        {
            _fromAddress = configuration["Email:FromAddress"];
            
            // Check config first, then environment variables
            _accessKey = configuration["AWS:AccessKeyId"];
            if (string.IsNullOrEmpty(_accessKey))
            {
                _accessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
            }

            _secretKey = configuration["AWS:SecretAccessKey"];
            if (string.IsNullOrEmpty(_secretKey))
            {
                _secretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");
            }

            _regionName = configuration["AWS:Region"];
            if (string.IsNullOrEmpty(_regionName))
            {
                _regionName = Environment.GetEnvironmentVariable("AWS_REGION") ?? "us-east-1";
            }
            
            _isConfigured = !string.IsNullOrEmpty(_accessKey) && 
                           !string.IsNullOrEmpty(_secretKey) && 
                           !string.IsNullOrEmpty(_regionName) &&
                           !string.IsNullOrEmpty(_fromAddress);
        }

        public async Task<bool> SendEmailAsync(EmailMessage message)
        {
            // Graceful failure when AWS credentials are not configured
            if (!_isConfigured)
            {
                Console.WriteLine($"[SesEmailSender] Email not sent - AWS SES not configured. Would send to: {message.To}");
                return false;
            }

            try
            {
                var region = RegionEndpoint.GetBySystemName(_regionName);
                
                using var client = new AmazonSimpleEmailServiceClient(_accessKey, _secretKey, region);
                
                var sendRequest = new SendEmailRequest
                {
                    Source = _fromAddress,
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { message.To }
                    },
                    Message = new Message
                    {
                        Subject = new Content(message.Subject),
                        Body = new Body
                        {
                            Html = message.IsHtml ? new Content(message.Body) : null,
                            Text = !message.IsHtml ? new Content(message.Body) : null
                        }
                    }
                };

                var response = await client.SendEmailAsync(sendRequest);
                return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SesEmailSender] Error sending email: {ex.Message}");
                return false;
            }
        }
    }
}
