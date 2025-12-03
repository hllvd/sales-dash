using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Data;
using SalesApp.IntegrationTests;
using Xunit;

namespace SalesApp.IntegrationTests.PointOfSale
{
    public class PointOfSaleControllerTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public PointOfSaleControllerTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task<string> GetToken(string role)
        {
            var email = role switch
            {
                "admin" => "admin@test.com",
                "superadmin" => "superadmin@test.com",
                _ => "user@test.com"
            };

            var password = role switch
            {
                "admin" => "admin123",
                "superadmin" => "superadmin123",
                _ => "user123"
            };

            var loginResponse = await _client.PostAsJsonAsync("/api/users/login", new
            {
                email = email,
                password = password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await loginResponse.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result!.Data!.Token;
        }

        [Fact]
        public async Task GetAll_AsSuperAdmin_ShouldReturn200_WithPVList()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/point-of-sale");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<PVResponse>>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task GetAll_AsAdmin_ShouldReturn403()
        {
            // Arrange
            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/point-of-sale");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetAll_AsUser_ShouldReturn403()
        {
            // Arrange
            var token = await GetToken("user");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/point-of-sale");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetAll_Unauthenticated_ShouldReturn401()
        {
            // Arrange
            _client.DefaultRequestHeaders.Authorization = null;

            // Act
            var response = await _client.GetAsync("/api/point-of-sale");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task GetById_ExistingPV_AsSuperAdmin_ShouldReturn200()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/point-of-sale/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PVResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(1);
        }

        [Fact]
        public async Task GetById_NonExistingPV_ShouldReturn404()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync("/api/point-of-sale/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Create_ValidPV_AsSuperAdmin_ShouldReturn201()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var newPV = new PVRequest
            {
                Id = 100,
                Name = "Nova Loja Teste"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/point-of-sale", newPV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PVResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Id.Should().Be(100);
            result.Data.Name.Should().Be("Nova Loja Teste");
        }

        [Fact]
        public async Task Create_DuplicateId_ShouldReturn400()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var duplicatePV = new PVRequest
            {
                Id = 1, // Already exists in seed data
                Name = "Duplicate Test"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/point-of-sale", duplicatePV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Create_AsAdmin_ShouldReturn403()
        {
            // Arrange
            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var newPV = new PVRequest
            {
                Id = 101,
                Name = "Test Loja"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/point-of-sale", newPV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task Update_ExistingPV_AsSuperAdmin_ShouldReturn200()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var updatePV = new PVRequest
            {
                Id = 1,
                Name = "Loja Centro Atualizada"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/point-of-sale/1", updatePV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<PVResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data!.Name.Should().Be("Loja Centro Atualizada");
        }

        [Fact]
        public async Task Update_IdMismatch_ShouldReturn400()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var updatePV = new PVRequest
            {
                Id = 2,
                Name = "Test"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/point-of-sale/1", updatePV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Update_NonExistingPV_ShouldReturn404()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var updatePV = new PVRequest
            {
                Id = 999,
                Name = "Non Existing"
            };

            // Act
            var response = await _client.PutAsJsonAsync("/api/point-of-sale/999", updatePV);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_ExistingPV_AsSuperAdmin_ShouldReturn200()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // First create a PV to delete
            var newPV = new PVRequest { Id = 200, Name = "To Delete" };
            await _client.PostAsJsonAsync("/api/point-of-sale", newPV);

            // Act
            var response = await _client.DeleteAsync("/api/point-of-sale/200");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Delete_NonExistingPV_ShouldReturn404()
        {
            // Arrange
            var token = await GetToken("superadmin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.DeleteAsync("/api/point-of-sale/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Delete_AsAdmin_ShouldReturn403()
        {
            // Arrange
            var token = await GetToken("admin");
            _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.DeleteAsync("/api/point-of-sale/1");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
    }
}
