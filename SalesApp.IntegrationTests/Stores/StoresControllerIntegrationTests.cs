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

namespace SalesApp.IntegrationTests.Stores
{
    [Collection("Misc Tests")]
    public class StoresControllerIntegrationTests
    {
        private readonly HttpClient _client;
        private readonly MiscTestFactory _factory;

        public StoresControllerIntegrationTests(MiscTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task StoreCrudOperations_AsSuperadmin_ShouldWork()
        {
            var superadminClient = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            // 1. Create a Store
            var storeName = $"Store {Guid.NewGuid().ToString()[..8]}";
            var createReq = new CreateStoreRequest
            {
                Name = storeName,
                State = "PR"
            };

            var createRes = await superadminClient.PostAsJsonAsync("/api/stores", createReq);
            createRes.StatusCode.Should().Be(HttpStatusCode.Created);

            var createResult = await createRes.Content.ReadFromJsonAsync<ApiResponse<StoreResponse>>();
            createResult.Should().NotBeNull();
            createResult!.Success.Should().BeTrue();
            createResult.Data!.Name.Should().Be(storeName);
            createResult.Data.State.Should().Be("PR");
            var storeId = createResult.Data.Id;

            // 2. Get Store by ID
            var getRes = await superadminClient.GetAsync($"/api/stores/{storeId}");
            getRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 3. Update Store
            var updatedName = $"{storeName} Updated";
            var updateReq = new UpdateStoreRequest
            {
                Name = updatedName,
                State = "SC",
                IsActive = true
            };

            var updateRes = await superadminClient.PutAsJsonAsync($"/api/stores/{storeId}", updateReq);
            updateRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var updateResult = await updateRes.Content.ReadFromJsonAsync<ApiResponse<StoreResponse>>();
            updateResult!.Data!.Name.Should().Be(updatedName);
            updateResult.Data.State.Should().Be("SC");

            // 4. GetAllStores (Superadmin)
            var getAllRes = await superadminClient.GetAsync("/api/stores");
            getAllRes.StatusCode.Should().Be(HttpStatusCode.OK);
            var getAllResult = await getAllRes.Content.ReadFromJsonAsync<ApiResponse<List<StoreResponse>>>();
            getAllResult!.Data.Should().Contain(s => s.Id == storeId && s.Name == updatedName);

            // 5. Delete Store
            var deleteRes = await superadminClient.DeleteAsync($"/api/stores/{storeId}");
            deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify store is deleted
            var getDeletedRes = await superadminClient.GetAsync($"/api/stores/{storeId}");
            getDeletedRes.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CreateStore_DuplicateName_ShouldReturnBadRequest()
        {
            var superadminClient = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            var storeName = $"DupStore {Guid.NewGuid().ToString()[..8]}";
            var createReq = new CreateStoreRequest { Name = storeName, State = "SP" };

            var firstRes = await superadminClient.PostAsJsonAsync("/api/stores", createReq);
            firstRes.StatusCode.Should().Be(HttpStatusCode.Created);

            // Try creating with duplicate name
            var secondRes = await superadminClient.PostAsJsonAsync("/api/stores", createReq);
            secondRes.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetActiveStores_AsPublicUser_ShouldSucceed()
        {
            var adminClient = await GetAuthenticatedClient("admin@test.com", "admin123");

            var res = await adminClient.GetAsync("/api/stores/all");
            res.StatusCode.Should().Be(HttpStatusCode.OK);

            var result = await res.Content.ReadFromJsonAsync<ApiResponse<List<StoreResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
        }

        [Fact]
        public async Task TeamStoreAssignment_RolePermissions_ShouldBeEnforced()
        {
            var superadminClient = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");
            var adminClient = await GetAuthenticatedClient("admin@test.com", "admin123");

            // 1. Create a store
            var storeReq = new CreateStoreRequest { Name = $"TeamStore {Guid.NewGuid().ToString()[..8]}", State = "PR" };
            var storeRes = await superadminClient.PostAsJsonAsync("/api/stores", storeReq);
            var storeData = (await storeRes.Content.ReadFromJsonAsync<ApiResponse<StoreResponse>>())!.Data!;

            // 2. Create a team as superadmin
            var teamReq = new CreateTeamRequest { Name = $"StoreTeam {Guid.NewGuid().ToString()[..8]}" };
            var teamRes = await superadminClient.PostAsJsonAsync("/api/teams", teamReq);
            var teamData = (await teamRes.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>())!.Data!;

            // 3. Superadmin sets team store -> Should succeed
            var updateStoreReq = new UpdateTeamRequest { StoreId = storeData.Id };
            var setStoreRes = await superadminClient.PutAsJsonAsync($"/api/teams/{teamData.Id}", updateStoreReq);
            setStoreRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var updatedTeam = (await setStoreRes.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>())!.Data!;
            updatedTeam.StoreId.Should().Be(storeData.Id);
            updatedTeam.StoreName.Should().Be(storeData.Name);

            // 4. Admin non-owner tries to update team store -> Should return 403 Forbidden
            var adminUpdateReq = new UpdateTeamRequest { StoreId = storeData.Id };
            var adminRes = await adminClient.PutAsJsonAsync($"/api/teams/{teamData.Id}", adminUpdateReq);
            adminRes.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // 5. Superadmin clears team store
            var clearStoreReq = new UpdateTeamRequest { ClearStore = true };
            var clearRes = await superadminClient.PutAsJsonAsync($"/api/teams/{teamData.Id}", clearStoreReq);
            clearRes.StatusCode.Should().Be(HttpStatusCode.OK);

            var clearedTeam = (await clearRes.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>())!.Data!;
            clearedTeam.StoreId.Should().BeNull();
        }

        [Fact]
        public async Task DeleteStore_ClearsAssociatedTeamStoreId()
        {
            var superadminClient = await GetAuthenticatedClient("superadmin@test.com", "superadmin123");

            // 1. Create a store
            var storeReq = new CreateStoreRequest { Name = $"DeleteStore {Guid.NewGuid().ToString()[..8]}", State = "SC" };
            var storeRes = await superadminClient.PostAsJsonAsync("/api/stores", storeReq);
            var storeData = (await storeRes.Content.ReadFromJsonAsync<ApiResponse<StoreResponse>>())!.Data!;

            // 2. Create team linked to store
            var teamReq = new CreateTeamRequest { Name = $"LinkedTeam {Guid.NewGuid().ToString()[..8]}", StoreId = storeData.Id };
            var teamRes = await superadminClient.PostAsJsonAsync("/api/teams", teamReq);
            var teamData = (await teamRes.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>())!.Data!;
            teamData.StoreId.Should().Be(storeData.Id);

            // 3. Delete store
            var deleteRes = await superadminClient.DeleteAsync($"/api/stores/{storeData.Id}");
            deleteRes.StatusCode.Should().Be(HttpStatusCode.OK);

            // 4. Verify team store is now null
            var getTeamRes = await superadminClient.GetAsync($"/api/teams/{teamData.Id}");
            var reloadedTeam = (await getTeamRes.Content.ReadFromJsonAsync<ApiResponse<TeamResponse>>())!.Data!;
            reloadedTeam.StoreId.Should().BeNull();
            reloadedTeam.StoreName.Should().BeNull();
        }

        private async Task<HttpClient> GetAuthenticatedClient(string email, string password)
        {
            var loginRequest = new LoginRequest { Email = email, Password = password };
            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            var token = result?.Data?.Token ?? throw new Exception($"Failed to authenticate {email}");

            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return client;
        }
    }
}
