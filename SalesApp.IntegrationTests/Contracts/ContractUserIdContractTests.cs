using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
using Xunit;
using Microsoft.Extensions.DependencyInjection;


namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Integration Tests")]
    public class ContractUserIdContractTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public ContractUserIdContractTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task GetContracts_ShouldUseGuidUserIdAndNotLeakUserInternalId()
        {
            var superAdminToken = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);

            // Act
            var response = await client.GetAsync("/api/contracts");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rawJson = await response.Content.ReadAsStringAsync();
            rawJson.Should().NotContainEquivalentOf("userInternalId");
            rawJson.Should().NotContainEquivalentOf("user_internal_id");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();

            // If there is any contract with a user, check its UserId is Guid
            var assignedContract = result.Data!.FirstOrDefault(c => c.UserId.HasValue);
            if (assignedContract != null)
            {
                assignedContract.UserId.Should().NotBe(Guid.Empty);
            }
        }

        [Fact]
        public async Task AssignContract_ShouldCorrectlyMapGuidUserIdInResponseAndNotLeakUserInternalId()
        {
            // Arrange
            var userToken = await GetUserToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            // Create a contract assigned to superadmin (which regular user can assign to themselves)
            var superAdminUserId = await GetUserIdByEmail("superadmin@test.com");
            var contract = await CreateContract(superAdminUserId);

            // Act
            var response = await client.PostAsync($"/api/users/assign-contract/{contract.ContractNumber}", null);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rawJson = await response.Content.ReadAsStringAsync();
            rawJson.Should().NotContainEquivalentOf("userInternalId");
            rawJson.Should().NotContainEquivalentOf("user_internal_id");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.ContractNumber.Should().Be(contract.ContractNumber);

            var regularUserId = await GetUserIdByEmail("user@test.com");
            result.Data.UserId.Should().Be(regularUserId);
        }

        private async Task<string> GetSuperAdminToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token");
        }

        private async Task<string> GetUserToken()
        {
            var loginRequest = new LoginRequest
            {
                Email = "user@test.com",
                Password = "user123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get user token");
        }

        private async Task<Guid> GetUserIdByEmail(string email)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(context.Users, u => u.Email == email);
            return user.Id;
        }

        private async Task<SalesApp.Models.Contract> CreateContract(Guid userId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SalesApp.Data.AppDbContext>();
            
            var group = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(context.Groups);
            if (group == null)
            {
                group = new SalesApp.Models.Group { Name = "Test Group", Description = "Test Group" };
                context.Groups.Add(group);
                await context.SaveChangesAsync();
            }
            
            var user = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync(context.Users, u => u.Id == userId);
            var contract = new SalesApp.Models.Contract
            {
                ContractNumber = $"CN-{Guid.NewGuid().ToString()[..8]}",
                UserInternalId = user.InternalId,
                TotalAmount = 1000,
                GroupId = group.Id,
                ContractStatusId = 1,
                IsActive = true
            };
            
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();
            
            return contract;
        }
    }
}
