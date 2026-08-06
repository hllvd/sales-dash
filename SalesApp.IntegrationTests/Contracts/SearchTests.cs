using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
using Xunit;

namespace SalesApp.IntegrationTests.Contracts
{
    [Collection("Contracts Tests")]
    public class SearchTests 
    {
        private readonly HttpClient _client;
        private readonly ContractsTestFactory _factory;

        public SearchTests(ContractsTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = "superadmin@test.com",
                password = "superadmin123"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data.Token;
        }

        [Fact]
        public async Task SearchContracts_ByContractNumber_ShouldReturnCorrectContract()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"SEARCH-{Guid.NewGuid().ToString()[..8]}";
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var contract = new Contract
                {
                    ContractNumber = contractNumber,
                    TotalAmount = 1500,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.Contracts.Add(contract);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/contracts?contractNumber={contractNumber}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result!.Data!.Should().HaveCount(1);
            result!.Data!.First().ContractNumber.Should().Be(contractNumber);
        }

        [Fact]
        public async Task SearchUsers_ByContractNumber_ShouldReturnOwnerUser()
        {
            // Arrange
            var token = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var contractNumber = $"USER-SEARCH-{Guid.NewGuid().ToString()[..8]}";
            var userEmail = $"searchuser-{Guid.NewGuid().ToString()[..8]}@test.com";
            
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var user = new User 
                { 
                    Id = Guid.NewGuid(), 
                    Name = "Search Test User", 
                    Email = userEmail, 
                    RoleId = 3,
                    IsActive = true,
                    PasswordHash = "fake-hash"
                };
                context.Users.Add(user);
                await context.SaveChangesAsync();

                var contract = new Contract
                {
                    ContractNumber = contractNumber,
                    UserInternalId = user.InternalId,
                    TotalAmount = 2500,
                    ContractStatusId = 1,
                    SaleStartDate = DateTime.UtcNow,
                    IsActive = true
                };
                context.Contracts.Add(contract);
                await context.SaveChangesAsync();
            }

            // Act
            var response = await _client.GetAsync($"/api/users?contractNumber={contractNumber}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result!.Data!.Items.Should().HaveCount(1);
            result!.Data!.Items.First().Email.Should().Be(userEmail);
        }
    }
}
