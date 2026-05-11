using System.Net;
using System.Net.Http.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.ReportFilters.DTOs;
using SalesApp.ReportFilters.Validators;
using Xunit;

namespace SalesApp.IntegrationTests.ReportFilters
{
    [Collection("Integration Tests")]
    public class ReportFiltersIntegrationTests : IAsyncLifetime
    {
        private readonly TestWebApplicationFactory _factory;
        private HttpClient _client;
        private IAmazonDynamoDB? _dynamoDb;
        private readonly string _tableName = "ReportFilters";
        private bool _isDynamoAvailable = false;

        public ReportFiltersIntegrationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client; // Default client
        }

        public async Task InitializeAsync()
        {
            try 
            {
                var dynamoUrl = Environment.GetEnvironmentVariable("DYNAMODB_URL") ?? "http://host.docker.internal:8000";
                var config = new AmazonDynamoDBConfig 
                { 
                    ServiceURL = dynamoUrl,
                    Timeout = TimeSpan.FromSeconds(2),
                    MaxErrorRetry = 0
                };
                _dynamoDb = new AmazonDynamoDBClient("dummyAccessKey", "dummySecretKey", config);

                // Ping DynamoDB
                await _dynamoDb.ListTablesAsync(new ListTablesRequest { Limit = 1 });
                
                await PrepareDynamoDbTableAsync();
                _isDynamoAvailable = true;

                // Re-create the client with the real local DynamoDB instance
                _client = await _factory.CreateClientWithServicesAsync(services =>
                {
                    services.AddSingleton<IAmazonDynamoDB>(_dynamoDb);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Warning] Skipping DynamoDB integration tests: {ex.Message}");
                _isDynamoAvailable = false;
            }
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task PrepareDynamoDbTableAsync()
        {
            if (_dynamoDb == null) return;

            try
            {
                await _dynamoDb.DeleteTableAsync(_tableName);
                await Task.Delay(1000);
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
                    new AttributeDefinition("scope", ScalarAttributeType.S),
                    new AttributeDefinition("createdAt", ScalarAttributeType.S)
                },
                GlobalSecondaryIndexes = new List<GlobalSecondaryIndex>
                {
                    new GlobalSecondaryIndex
                    {
                        IndexName = "scope-createdAt-index",
                        KeySchema = new List<KeySchemaElement>
                        {
                            new KeySchemaElement("scope", KeyType.HASH),
                            new KeySchemaElement("createdAt", KeyType.RANGE)
                        },
                        Projection = new Projection { ProjectionType = ProjectionType.ALL },
                        ProvisionedThroughput = new ProvisionedThroughput(5, 5)
                    }
                },
                ProvisionedThroughput = new ProvisionedThroughput(5, 5)
            };

            await _dynamoDb.CreateTableAsync(createRequest);
            await Task.Delay(1000);
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "superadmin@test.com",
                password = "superadmin123"
            });
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        private class CustomApiResponse<T>
        {
            public bool Success { get; set; }
            public T? Data { get; set; }
            public string? Message { get; set; }
            public List<ValidationError>? Errors { get; set; }
        }

        [Fact]
        public async Task EndToEnd_ReportFilterLifecycle()
        {
            if (!_isDynamoAvailable) return; // Skip if DynamoDB isn't running locally

            // 1. Arrange & Authenticate
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var createRequest = new CreateReportFilterRequest
            {
                Name = "Integration Test Report",
                Description = "A report created by integration test",
                Scope = "private",
                FilterConfig = new FilterConfigRequest
                {
                    Matriculas = new List<string> { "99999" },
                    StartDate = DateTime.UtcNow.AddDays(-7),
                    EndDate = DateTime.UtcNow
                },
                OutputColumns = new List<OutputColumnRequest>
                {
                    new OutputColumnRequest { Source = "Contracts", Field = "contractNumber", Label = "Contract #", Order = 1 },
                    new OutputColumnRequest { Source = "Contracts", Field = "totalAmount", Label = "Amount", Order = 2 }
                }
            };

            // 2. CREATE
            var createResponse = await _client.PostAsJsonAsync("/api/report-filters", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var createResult = await createResponse.Content.ReadFromJsonAsync<CustomApiResponse<ReportFilterResponse>>();
            createResult!.Success.Should().BeTrue();
            var filterId = createResult.Data!.FilterId;
            filterId.Should().NotBeNullOrEmpty();
            createResult.Data.Name.Should().Be("Integration Test Report");
            createResult.Data.Scope.Should().Be("private");

            // 3. GET SINGLE
            var getResponse = await _client.GetAsync($"/api/report-filters/{filterId}");
            getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getResult = await getResponse.Content.ReadFromJsonAsync<CustomApiResponse<ReportFilterResponse>>();
            getResult!.Success.Should().BeTrue();
            getResult.Data!.FilterId.Should().Be(filterId);
            getResult.Data.OutputColumns.Should().HaveCount(2);

            // 4. LIST (Should contain at least our newly created report)
            var listResponse = await _client.GetAsync("/api/report-filters");
            listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var listResult = await listResponse.Content.ReadFromJsonAsync<CustomApiResponse<List<ReportFilterResponse>>>();
            listResult!.Success.Should().BeTrue();
            listResult.Data.Should().Contain(f => f.FilterId == filterId);

            // 5. GET AVAILABLE COLUMNS
            var columnsResponse = await _client.GetAsync("/api/report-filters/columns/available");
            columnsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var columnsResult = await columnsResponse.Content.ReadFromJsonAsync<CustomApiResponse<AvailableColumnsResponse>>();
            columnsResult!.Success.Should().BeTrue();
            columnsResult.Data!.Sources.Should().NotBeEmpty();

            // 6. GET RESULTS (Contracts Projection)
            var resultsResponse = await _client.GetAsync($"/api/report-filters/{filterId}/results");
            resultsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var resultsResult = await resultsResponse.Content.ReadFromJsonAsync<CustomApiResponse<ReportResultsResponse>>();
            resultsResult!.Success.Should().BeTrue();
            resultsResult.Data!.Page.Should().BeGreaterThan(0);
            resultsResult.Data.Columns.Should().HaveCount(2);

            // 7. UPDATE
            var updateRequest = new UpdateReportFilterRequest
            {
                Name = "Integration Test Report Updated",
                Scope = "shared",
                FilterConfig = createRequest.FilterConfig,
                OutputColumns = createRequest.OutputColumns
            };
            var updateResponse = await _client.PutAsJsonAsync($"/api/report-filters/{filterId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var updateResult = await updateResponse.Content.ReadFromJsonAsync<CustomApiResponse<ReportFilterResponse>>();
            updateResult!.Data!.Name.Should().Be("Integration Test Report Updated");
            updateResult.Data.Scope.Should().Be("shared");

            // 8. DELETE
            var deleteResponse = await _client.DeleteAsync($"/api/report-filters/{filterId}");
            deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify deletion
            var getDeletedResponse = await _client.GetAsync($"/api/report-filters/{filterId}");
            getDeletedResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
