using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SalesApp.DTOs;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Users Tests")]
    public class UserApiContractTests
    {
        private readonly HttpClient _client;
        private readonly UsersTestFactory _factory;

        public UserApiContractTests(UsersTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task RegisterAndMeContract_ShouldUseGuidAndNotLeakInternalId()
        {
            // 1. POST /api/users/register
            var superAdminToken = await GetSuperAdminToken();
            
            // Get parent user ID
            var parentUser = await GetCurrentUserWithToken(superAdminToken);
            var parentUserId = parentUser.Id;

            var registerRequest = new
            {
                Name = "Contract Test User",
                Email = $"contract_test_{Guid.NewGuid().ToString()[..8]}@test.com",
                Password = "Password123!",
                ParentUserId = parentUserId
            };

            var registerResponse = await _client.PostAsJsonAsync("/api/users/register", registerRequest);
            registerResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var registerRawJson = await registerResponse.Content.ReadAsStringAsync();
            registerRawJson.Should().NotContainEquivalentOf("internalId");
            registerRawJson.Should().NotContainEquivalentOf("internal_id");

            var registerResult = await registerResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            registerResult.Should().NotBeNull();
            registerResult!.Success.Should().BeTrue();
            registerResult.Data.Should().NotBeNull();
            registerResult.Data!.Id.Should().NotBe(Guid.Empty);

            var newUserId = registerResult.Data.Id;

            // 2. POST /api/users/login
            var loginRequest = new LoginRequest
            {
                Email = registerRequest.Email,
                Password = registerRequest.Password
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginRawJson = await loginResponse.Content.ReadAsStringAsync();
            loginRawJson.Should().NotContainEquivalentOf("internalId");
            loginRawJson.Should().NotContainEquivalentOf("internal_id");

            var loginResult = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            loginResult.Should().NotBeNull();
            loginResult!.Success.Should().BeTrue();
            loginResult.Data!.User.Id.Should().Be(newUserId);

            var userToken = loginResult.Data.Token;

            // 3. GET /api/users/me
            var clientWithToken = _factory.Client;
            clientWithToken.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", userToken);

            var meResponse = await clientWithToken.GetAsync("/api/users/me");
            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var meRawJson = await meResponse.Content.ReadAsStringAsync();
            meRawJson.Should().NotContainEquivalentOf("internalId");
            meRawJson.Should().NotContainEquivalentOf("internal_id");

            var meResult = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            meResult.Should().NotBeNull();
            meResult!.Success.Should().BeTrue();
            meResult.Data!.Id.Should().Be(newUserId);
        }

        [Fact]
        public async Task GetUsersAndGetById_ShouldUseGuidAndNotLeakInternalId()
        {
            var superAdminToken = await GetSuperAdminToken();
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superAdminToken);

            // 1. GET /api/users
            var response = await client.GetAsync("/api/users");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var rawJson = await response.Content.ReadAsStringAsync();
            rawJson.Should().NotContainEquivalentOf("internalId");
            rawJson.Should().NotContainEquivalentOf("internal_id");

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PagedResponse<UserResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.Items.Should().NotBeEmpty();

            var firstUser = result.Data.Items.First();
            firstUser.Id.Should().NotBe(Guid.Empty);

            // 2. GET /api/users/{id}
            var getByIdResponse = await client.GetAsync($"/api/users/{firstUser.Id}");
            getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var getByIdRawJson = await getByIdResponse.Content.ReadAsStringAsync();
            getByIdRawJson.Should().NotContainEquivalentOf("internalId");
            getByIdRawJson.Should().NotContainEquivalentOf("internal_id");

            var getByIdResult = await getByIdResponse.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            getByIdResult.Should().NotBeNull();
            getByIdResult!.Success.Should().BeTrue();
            getByIdResult.Data!.Id.Should().Be(firstUser.Id);
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

        private async Task<UserResponse> GetCurrentUserWithToken(string token)
        {
            var client = _factory.Client;
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var response = await client.GetAsync("/api/users/me");
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserResponse>>();
            return result!.Data!;
        }
    }
}
