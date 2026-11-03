using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Users
{
    [Collection("Users Tests")]
    public class SuperAdminPasswordEnvTests
    {
        private readonly HttpClient _client;
        private readonly UsersTestFactory _factory;

        public SuperAdminPasswordEnvTests(UsersTestFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        private async Task EnsureSuperAdminExists(AppDbContext context, string corruptedHash = null)
        {
            var superAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "superadmin@salesapp.com");
            if (superAdmin == null)
            {
                superAdmin = new User
                {
                    Id = Guid.NewGuid(),
                    Name = "Super Admin",
                    Email = "superadmin@salesapp.com",
                    PasswordHash = corruptedHash ?? BCrypt.Net.BCrypt.HashPassword("string"),
                    RoleId = (int)RoleId.SuperAdmin,
                    IsActive = true
                };
                context.Users.Add(superAdmin);
            }
            else if (corruptedHash != null)
            {
                superAdmin.PasswordHash = corruptedHash;
            }

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task SuperAdmin_Login_SucceedsWithEnvPassword_EvenWhenDatabaseHashIsCorrupted()
        {
            // Arrange: corrupt the SQLite database hash for superadmin
            var corruptedPassword = "random_old_or_corrupted_hash_987654";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await EnsureSuperAdminExists(context, BCrypt.Net.BCrypt.HashPassword(corruptedPassword));
            }

            // Act: login using the environment / default password ("string")
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@salesapp.com",
                Password = "string"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);

            // Assert: login succeeds despite DB hash mismatch
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            result.Should().NotBeNull();
            result!.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Token.Should().NotBeNullOrEmpty();

            // Verify SQLite DB hash was synchronized
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var updatedUser = await context.Users.FirstAsync(u => u.Email == "superadmin@salesapp.com");
                BCrypt.Net.BCrypt.Verify("string", updatedUser.PasswordHash).Should().BeTrue();
            }
        }

        [Fact]
        public async Task SuperAdmin_Login_WithWrongPassword_ReturnsUnauthorized()
        {
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await EnsureSuperAdminExists(context);
            }

            var loginRequest = new LoginRequest
            {
                Email = "superadmin@salesapp.com",
                Password = "totally_incorrect_password"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task SuperAdmin_ForgotPassword_DoesNotOverwritePasswordHash()
        {
            string originalHash;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await EnsureSuperAdminExists(context);
                var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@salesapp.com");
                originalHash = superAdmin.PasswordHash;
            }

            // Act: trigger public forgot password for superadmin
            var forgotRequest = new ForgotPasswordRequest
            {
                Email = "superadmin@salesapp.com"
            };

            var response = await _client.PostAsJsonAsync("/api/users/forgot-password", forgotRequest);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert: hash in SQLite was NOT modified
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@salesapp.com");
                superAdmin.PasswordHash.Should().Be(originalHash);
            }
        }

        [Fact]
        public async Task SuperAdmin_ResetPasswordEndpoint_IsBlocked()
        {
            Guid superAdminId;
            Guid adminUserId;
            string adminToken;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await EnsureSuperAdminExists(context);
                var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@salesapp.com");
                superAdminId = superAdmin.Id;

                var testAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == "testadmin_reset@test.com");
                if (testAdmin == null)
                {
                    testAdmin = new User
                    {
                        Id = Guid.NewGuid(),
                        Name = "Admin Reset Tester",
                        Email = "testadmin_reset@test.com",
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                        RoleId = (int)RoleId.Admin,
                        IsActive = true
                    };
                    context.Users.Add(testAdmin);
                    await context.SaveChangesAsync();
                }
                adminUserId = testAdmin.Id;
            }

            // Login as Admin to get token
            var loginRes = await _client.PostAsJsonAsync("/api/users/login", new LoginRequest
            {
                Email = "testadmin_reset@test.com",
                Password = "admin123"
            });
            var loginData = await loginRes.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            adminToken = loginData!.Data!.Token;

            // Admin tries to reset superadmin's password
            var clientWithAuth = _factory.CreateClient();
            clientWithAuth.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminToken);

            var resetRequest = new ResetPasswordRequest { SendEmail = false };
            var response = await clientWithAuth.PostAsJsonAsync($"/api/users/{superAdminId}/reset-password", resetRequest);

            // Cannot reset superadmin password
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var err = await response.Content.ReadAsStringAsync();
            err.Should().Contain("SUPERADMIN_PASSWORD");
        }

        [Fact]
        public async Task SuperAdmin_UpdateUser_PasswordChange_IsBlocked()
        {
            Guid superAdminId;
            string superadminToken;

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await EnsureSuperAdminExists(context);
                var superAdmin = await context.Users.FirstAsync(u => u.Email == "superadmin@salesapp.com");
                superAdminId = superAdmin.Id;
            }

            var loginRes = await _client.PostAsJsonAsync("/api/users/login", new LoginRequest
            {
                Email = "superadmin@salesapp.com",
                Password = "string"
            });
            var loginData = await loginRes.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            superadminToken = loginData!.Data!.Token;

            var clientWithAuth = _factory.CreateClient();
            clientWithAuth.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", superadminToken);

            var updateRequest = new UpdateUserRequest
            {
                Password = "NewAttemptedPassword123"
            };

            var response = await clientWithAuth.PutAsJsonAsync($"/api/users/{superAdminId}", updateRequest);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var err = await response.Content.ReadAsStringAsync();
            err.Should().Contain("SUPERADMIN_PASSWORD");
        }
    }
}
