using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Integration Tests")]
    public class UserMatriculaFieldsTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UserMatriculaFieldsTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }
        [Fact]
        public async Task GetUsers_ShouldReturnMatriculaFields_WhenUserHasOwnerMatricula()
        {
            // Arrange
            var superAdminToken = await GetSuperAdminTokenAsync();
            
            // Create a test user
            var createUserRequest = new
            {
                name = "Test User With Matricula",
                email = $"matricula-test-{Guid.NewGuid()}@test.com",
                password = "Test123!",
                role = "user"
            };
            
            var createUserResponse = await _client.PostAsJsonAsync("/api/users/register", createUserRequest);
            createUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createUserResult = await createUserResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var userId = createUserResult!.Data!.Id;
            
            // Create a matricula for the user
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);
            
            var createMatriculaRequest = new
            {
                userId = userId,
                matriculaNumber = "MAT-TEST-001",
                startDate = DateTime.UtcNow,
                isOwner = true,
                isActive = true
            };
            
            var createMatriculaResponse = await _client.PostAsJsonAsync("/api/usermatriculas", createMatriculaRequest);
            createMatriculaResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var matriculaResult = await createMatriculaResponse.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            var matriculaId = matriculaResult!.Data!.Id;
            
            // Act - Get all users
            var getUsersResponse = await _client.GetAsync("/api/users?page=1&pageSize=1000");
            
            // Assert
            getUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var usersResult = await getUsersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            
            usersResult.Should().NotBeNull();
            usersResult!.Success.Should().BeTrue();
            usersResult.Data.Should().NotBeNull();
            
            var testUser = usersResult.Data!.Items.FirstOrDefault(u => u.Id == userId);
            testUser.Should().NotBeNull();
            
            // Verify matricula fields are populated
            testUser!.MatriculaId.Should().Be(matriculaId);
            testUser.MatriculaNumber.Should().Be("MAT-TEST-001");
            testUser.IsMatriculaOwner.Should().BeTrue();
        }
        
        [Fact]
        public async Task GetUsers_ShouldReturnNullMatriculaFields_WhenUserHasNoOwnerMatricula()
        {
            // Arrange
            var superAdminToken = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);
            
            // Create a test user without matricula
            var createUserRequest = new
            {
                name = "Test User Without Matricula",
                email = $"no-matricula-{Guid.NewGuid()}@test.com",
                password = "Test123!",
                role = "user"
            };
            
            var createUserResponse = await _client.PostAsJsonAsync("/api/users/register", createUserRequest);
            createUserResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createUserResult = await createUserResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var userId = createUserResult!.Data!.Id;
            
            // Act - Get all users
            var getUsersResponse = await _client.GetAsync("/api/users?page=1&pageSize=1000");
            
            // Assert
            getUsersResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var usersResult = await getUsersResponse.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            
            var testUser = usersResult!.Data!.Items.FirstOrDefault(u => u.Id == userId);
            testUser.Should().NotBeNull();
            
            // Verify matricula fields are null
            testUser!.MatriculaId.Should().BeNull();
            testUser.MatriculaNumber.Should().BeNull();
            testUser.IsMatriculaOwner.Should().BeFalse();
        }
        
        [Fact]
        public async Task GetContracts_ShouldReturnMatriculaNumber_WhenContractHasMatricula()
        {
            // Arrange
            var superAdminToken = await GetSuperAdminTokenAsync();
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);
            
            // Create a test user
            var createUserRequest = new
            {
                name = "Test User For Contract",
                email = $"contract-user-{Guid.NewGuid()}@test.com",
                password = "Test123!",
                role = "user"
            };
            
            var createUserResponse = await _client.PostAsJsonAsync("/api/users/register", createUserRequest);
            var createUserResult = await createUserResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            var userId = createUserResult!.Data!.Id;
            
            // Create a matricula
            var createMatriculaRequest = new
            {
                userId = userId,
                matriculaNumber = "MAT-CONTRACT-001",
                startDate = DateTime.UtcNow,
                isOwner = true,
                isActive = true
            };
            
            var createMatriculaResponse = await _client.PostAsJsonAsync("/api/usermatriculas", createMatriculaRequest);
            var matriculaResult = await createMatriculaResponse.Content.ReadFromJsonAsync<ApiResponse<UserMatriculaResponse>>();
            var matriculaId = matriculaResult!.Data!.Id;
            
            // Create a contract with the matricula
            var createContractRequest = new
            {
                contractNumber = $"CONTRACT-{Guid.NewGuid()}",
                userId = userId,
                totalAmount = 1000.00m,
                status = "Active",
                contractStartDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                matriculaId = matriculaId
            };
            
            var createContractResponse = await _client.PostAsJsonAsync("/api/contracts", createContractRequest);
            createContractResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var contractResult = await createContractResponse.Content.ReadFromJsonAsync<ApiResponse<ContractResponse>>();
            
            // Act - Get all contracts
            var getContractsResponse = await _client.GetAsync("/api/contracts");
            
            // Assert
            getContractsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var contractsResult = await getContractsResponse.Content.ReadFromJsonAsync<ApiResponse<List<ContractResponse>>>();
            
            contractsResult.Should().NotBeNull();
            contractsResult!.Success.Should().BeTrue();
            
            var testContract = contractsResult.Data!.FirstOrDefault(c => c.Id == contractResult!.Data!.Id);
            testContract.Should().NotBeNull();
            
            // Verify matricula fields are populated in contract response
            testContract!.MatriculaId.Should().Be(matriculaId);
            testContract.MatriculaNumber.Should().Be("MAT-CONTRACT-001");
        }
        private async Task<string> GetSuperAdminTokenAsync()
        {
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"SuperAdmin login failed: {response.StatusCode} - {content}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get superadmin token from login response");
        }
    }
}
