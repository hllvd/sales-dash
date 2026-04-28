using Amazon;
using Amazon.CloudWatchLogs;
using Amazon.CloudWatchLogs.Model;

namespace SalesApp.Services
{
    public static class CloudWatchRetentionInitializer
    {
        public static async Task EnsureCloudWatchRetentionAsync(string logGroup, string region, string? accessKey = null, string? secretKey = null)
        {
            try
            {
                AmazonCloudWatchLogsClient client;
                if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
                {
                    client = new AmazonCloudWatchLogsClient(accessKey, secretKey, RegionEndpoint.GetBySystemName(region));
                }
                else
                {
                    client = new AmazonCloudWatchLogsClient(RegionEndpoint.GetBySystemName(region));
                }

                // Create group if it doesn't exist yet
                try
                {
                    await client.CreateLogGroupAsync(new CreateLogGroupRequest { LogGroupName = logGroup });
                    Console.WriteLine($"[CloudWatch] Created log group '{logGroup}'");
                }
                catch (ResourceAlreadyExistsException)
                {
                    // already exists, fine
                }

                // Enforce 30-day retention
                await client.PutRetentionPolicyAsync(new PutRetentionPolicyRequest
                {
                    LogGroupName = logGroup,
                    RetentionInDays = 30
                });
                
                Console.WriteLine($"[CloudWatch] Ensured 30-day retention for '{logGroup}' in {region}");
            }
            catch (Exception ex)
            {
                // We don't want to crash the app if CloudWatch setup fails (e.g. permission issues)
                // but we should log it to console.
                Console.WriteLine($"[CloudWatch] ERROR: Failed to initialize retention policy: {ex.Message}");
            }
        }
    }
}
