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
        private readonly IConfiguration _configuration;
        private readonly string? _fromAddress;
        private readonly bool _isConfigured;

        public SesEmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
            _fromAddress = _configuration["Email:FromAddress"];
            
            // Check if AWS credentials are configured
            var accessKey = _configuration["AWS:AccessKeyId"];
            var secretKey = _configuration["AWS:SecretAccessKey"];
            var region = _configuration["AWS:Region"];
            
            _isConfigured = !string.IsNullOrEmpty(accessKey) && 
                           !string.IsNullOrEmpty(secretKey) && 
                           !string.IsNullOrEmpty(region) &&
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
                var accessKey = _configuration["AWS:AccessKeyId"]!;
                var secretKey = _configuration["AWS:SecretAccessKey"]!;
                var regionName = _configuration["AWS:Region"]!;
                
                var region = RegionEndpoint.GetBySystemName(regionName);
                
                using var client = new AmazonSimpleEmailServiceClient(accessKey, secretKey, region);
                
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
