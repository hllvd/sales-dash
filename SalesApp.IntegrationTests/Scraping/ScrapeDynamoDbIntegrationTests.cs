using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SalesApp.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Scraping
{
    [Collection("Integration Tests")]
    public class ScrapeDynamoDbIntegrationTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private IAmazonDynamoDB _dynamoDb;
        private readonly string _tableName = "pbi_scrape_logs";

        public ScrapeDynamoDbIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        public async Task InitializeAsync()
        {
            // Dynamically resolve real local DynamoDB 
            var scope = _factory.Services.CreateScope();
            
            // Reconfigure to connect to Local DynamoDB (host.docker.internal for Mac, localhost for others)
            var dynamoUrl = Environment.GetEnvironmentVariable("DYNAMODB_URL") ?? "http://host.docker.internal:8000";
            var config = new AmazonDynamoDBConfig { ServiceURL = dynamoUrl };
            _dynamoDb = new AmazonDynamoDBClient("dummyAccessKey", "dummySecretKey", config);

            // Re-create the table for testing
            await PrepareDynamoDbTableAsync();
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task PrepareDynamoDbTableAsync()
        {
            try
            {
                await _dynamoDb.DeleteTableAsync(_tableName);
                // Simple wait for deletion
                await Task.Delay(2000);
            }
            catch (ResourceNotFoundException) { }

            var createRequest = new CreateTableRequest
            {
                TableName = _tableName,
                KeySchema = new List<KeySchemaElement>
                {
                    new KeySchemaElement("PK", KeyType.HASH),
                    new KeySchemaElement("SK", KeyType.RANGE)
                },
                AttributeDefinitions = new List<AttributeDefinition>
                {
                    new AttributeDefinition("PK", ScalarAttributeType.S),
                    new AttributeDefinition("SK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1PK", ScalarAttributeType.S),
                    new AttributeDefinition("GSI1SK", ScalarAttributeType.S)
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new GlobalSecondaryIndex
                    {
                        IndexName = "GSI1",
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement("GSI1PK", KeyType.HASH),
                            new KeySchemaElement("GSI1SK", KeyType.RANGE)
                        },
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                        ProvisionedThroughput = new ProvisionedThroughput(5, 5)
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput(5, 5)
            };

            await _dynamoDb.CreateTableAsync(createRequest);
            await Task.Delay(2000); // Give it a sec to be active
        }

        [Fact]
        public async Task WriteJobStatusAsync_And_GetAllJobs_ShouldReturnExpectedRecords()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            
            // Force the ScrapeDynamoLogService to use our real local instance instead of mock
            var dynamoDbService = new ScrapeDynamoLogService(
                _dynamoDb, 
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScrapeDynamoLogService>>()
            );

            var jobId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var store = "Test Store";
            var matricula = "55555";

            // Act - Log Pending
            await dynamoDbService.WriteJobStatusAsync(jobId, userId.ToString(), "Pending", store, matricula);

            // Fetch after Pending
            var initialJobs = await dynamoDbService.GetAllJobsAsync();
            var myInitialJobs = await dynamoDbService.GetJobsByUserAsync(userId);
            
            // Act - Log Completed with RowCount
            await dynamoDbService.WriteJobStatusAsync(jobId, userId.ToString(), "Succeeded", store, matricula, new { RowCount = 42, FileRelativePath = "data.csv" });

            // Fetch after Completed
            var finalJobs = await dynamoDbService.GetAllJobsAsync();
            var myFinalJobs = await dynamoDbService.GetJobsByUserAsync(userId);

            // Assert
            initialJobs.Should().ContainSingle();
            initialJobs[0].Status.Should().Be("Pending");
            initialJobs[0].Store.Should().Be(store);

            myInitialJobs.Should().ContainSingle();
            myInitialJobs[0].Status.Should().Be("Pending");

            finalJobs.Should().ContainSingle();
            finalJobs[0].Status.Should().Be("Succeeded");
            finalJobs[0].RowCount.Should().Be(42);
            finalJobs[0].FileRelativePath.Should().Be("data.csv");

            myFinalJobs.Should().ContainSingle();
            myFinalJobs[0].Status.Should().Be("Succeeded");
        }

        [Fact]
        public async Task HandleCallbackAsync_Via_Orchestrator_UpdatesDynamoDb_And_TriggersImport()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            
            var dynamoDbService = new ScrapeDynamoLogService(
                _dynamoDb, 
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>(),
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ScrapeDynamoLogService>>()
            );

            // We mock the Import Service to ensure AutoImport is called when successful
            var mockImportService = new Mock<IScrapeImportService>();
            mockImportService.Setup(s => s.AutoImportAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                             .ReturnsAsync(new SalesApp.Services.ImportResult { ProcessedRows = 10 });
                             
            var orchestrator = new ScrapeOrchestrator(
                scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>(),
                scope.ServiceProvider.GetRequiredService<PbiScraperClient>(),
                dynamoDbService,
                mockImportService.Object,
                scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()
            );

            var jobId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();

            // First, log the job manually as Pending (as if it was just triggered)
            await dynamoDbService.WriteJobStatusAsync(jobId, userId.ToString(), "Pending", "Store X", "123");

            // Act - The Scraper completes and sends a Callback to orchestrator
            var callbackData = new SalesApp.Models.ScrapeResult 
            { 
                JobId = jobId, 
                UserId = userId.ToString(), 
                Status = "Succeeded", 
                Store = "Store X", 
                Matricula = "123", 
                RowCount = 50, 
                FileRelativePath = "outputs/store_x_123.csv" 
            };

            await orchestrator.HandleCallbackAsync(callbackData);

            // Assert
            var jobs = await dynamoDbService.GetJobsByUserAsync(userId);
            jobs.Should().HaveCount(1);
            jobs[0].Status.Should().Be("Succeeded");
            jobs[0].RowCount.Should().Be(50);
            
            // Verify that auto trigger import was called
            mockImportService.Verify(m => m.AutoImportAsync(It.Is<string>(s => s.Contains("store_x_123.csv")), userId), Times.Once);
        }
    }
}
