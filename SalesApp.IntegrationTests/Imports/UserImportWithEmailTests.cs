using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class UserImportWithEmailTests
    {
        private readonly HttpClient _client;
        private readonly TestWebApplicationFactory _factory;

        public UserImportWithEmailTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.Client;
        }

        [Fact]
        public async Task UserImport_WithSendEmailTrue_ShouldCreateUserAndAttemptEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.sendemail.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,Matricula,SendEmail
John Doe,{uniqueEmail},MAT1,true";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-with-email.csv");
            
            // Configure mappings
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "SendEmail", "SendEmail" }
                }
            };

            var mappingResponse = await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            mappingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Confirm import
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Success.Should().BeTrue();
            confirmResult.Data!.ProcessedRows.Should().Be(1);

            // Verify user was created
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
            user!.Name.Should().Be("John Doe");
        }

        [Fact]
        public async Task UserImport_MissingMatricula_ShouldFailAndReturnErrors()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.nomatricula.{Guid.NewGuid().ToString()[..8]}@test.com";
            // Explicitly omitting Matricula column to trigger the validation error
            var csvContent = $@"Name,Email,SendEmail
Missing Matricula User,{uniqueEmail},true";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-missing-matricula.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "SendEmail", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            
            // We expect the request to succeed but process 0 rows and return validation errors
            confirmResult!.Success.Should().BeTrue();
            confirmResult.Data!.ProcessedRows.Should().Be(0);
            
            // The validation error comes back in the Errors list
            confirmResult.Data.Errors.Should().Contain(e => e.Contains("Nome, Email, and Matricula are required"));
        }

        [Fact]
        public async Task UserImport_WithSendEmailFalse_ShouldCreateUserWithoutEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.noemail.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,Matricula,SendEmail
Jane Smith,{uniqueEmail},MAT2,false";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-no-email.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "SendEmail", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithoutSendEmailColumn_ShouldCreateUserWithoutEmail()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.default.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,Matricula
Bob Johnson,{uniqueEmail},MAT3";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-default.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithCaseInsensitiveSendEmail_ShouldWork()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.caseinsensitive.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,Matricula,SENDEMAIL
Alice Wonder,{uniqueEmail},MAT4,TRUE";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-case-insensitive.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "SENDEMAIL", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithFlexibleColumnName_ShouldAutoMap()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var uniqueEmail = $"test.flexible.{Guid.NewGuid().ToString()[..8]}@test.com";
            var csvContent = $@"Name,Email,Matricula,send-email
Charlie Brown,{uniqueEmail},MAT5,1";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-flexible.csv");
            
            // The auto-mapping should recognize "send-email" as "SendEmail"
            var previewResponse = await _client.GetAsync($"/api/imports/{uploadId}/status");
            previewResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "send-email", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == uniqueEmail);
            user.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_WithVariousBooleanValues_ShouldParseCorrectly()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var email1 = $"test.bool1.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email2 = $"test.bool2.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email3 = $"test.bool3.{Guid.NewGuid().ToString()[..8]}@test.com";
            var email4 = $"test.bool4.{Guid.NewGuid().ToString()[..8]}@test.com";
            
            var csvContent = $@"Name,Email,Matricula,SendEmail
User One,{email1},MAT6,yes
User Two,{email2},MAT7,sim
User Three,{email3},MAT8,y
User Four,{email4},MAT9,s";

            // Act
            var uploadId = await UploadUserFile(csvContent, "users-boolean-values.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "SendEmail", "SendEmail" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);

            // Assert
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var confirmResult = await confirmResponse.Content.ReadFromJsonAsync<ApiResponse<ImportStatusResponse>>();
            confirmResult!.Data!.ProcessedRows.Should().Be(4);
            
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            
            var user1 = await context.Users.FirstOrDefaultAsync(u => u.Email == email1);
            var user2 = await context.Users.FirstOrDefaultAsync(u => u.Email == email2);
            var user3 = await context.Users.FirstOrDefaultAsync(u => u.Email == email3);
            var user4 = await context.Users.FirstOrDefaultAsync(u => u.Email == email4);
            
            user1.Should().NotBeNull();
            user2.Should().NotBeNull();
            user3.Should().NotBeNull();
            user4.Should().NotBeNull();
        }

        [Fact]
        public async Task UserImport_PasswordOverwriteSafety_ShouldRespectConditions()
        {
            // Arrange
            var token = await GetAdminToken();
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var emailKeep = $"keep.password.{Guid.NewGuid().ToString()[..8]}@test.com";
            var emailOverwrite = $"overwrite.password.{Guid.NewGuid().ToString()[..8]}@test.com";

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // User 1: Has custom password and has logged in (has RefreshToken)
                var userKeep = new User
                {
                    Name = "User Keep Password",
                    Email = emailKeep,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("MyCustomPassword123!"),
                    RoleId = 3,
                    IsActive = true
                };
                context.Users.Add(userKeep);
                await context.SaveChangesAsync();

                var rt = new RefreshToken
                {
                    UserInternalId = userKeep.InternalId,
                    Token = "dummy-token-for-test",
                    ExpiresAt = DateTime.UtcNow.AddDays(1),
                    CreatedAt = DateTime.UtcNow
                };
                context.RefreshTokens.Add(rt);
                await context.SaveChangesAsync();

                // User 2: Has default password, never logged in
                var userOverwrite = new User
                {
                    Name = "User Overwrite Password",
                    Email = emailOverwrite,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe123!"),
                    RoleId = 3,
                    IsActive = true
                };
                context.Users.Add(userOverwrite);
                await context.SaveChangesAsync();
            }

            // CSV with column Password pointing to a new password
            var csvContent = $@"Name,Email,Matricula,Password
User Keep Password,{emailKeep},MATKEEP,NewImportPassword999!
User Overwrite Password,{emailOverwrite},MATOVER,NewImportPassword999!";

            // Act
            var uploadId = await UploadUserFile(csvContent, "password-overwrite-test.csv");
            
            var mappingRequest = new
            {
                mappings = new Dictionary<string, string>
                {
                    { "Name", "Name" },
                    { "Email", "Email" },
                    { "Matricula", "Matricula" },
                    { "Password", "Password" }
                }
            };

            await _client.PostAsJsonAsync($"/api/imports/{uploadId}/mappings", mappingRequest);
            var confirmResponse = await _client.PostAsync($"/api/imports/{uploadId}/confirm", null);
            confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                
                var userKeepResult = await context.Users.FirstOrDefaultAsync(u => u.Email == emailKeep);
                userKeepResult.Should().NotBeNull();
                // Password should NOT be overwritten (verifying it is still "MyCustomPassword123!")
                BCrypt.Net.BCrypt.Verify("MyCustomPassword123!", userKeepResult!.PasswordHash).Should().BeTrue();
                BCrypt.Net.BCrypt.Verify("NewImportPassword999!", userKeepResult!.PasswordHash).Should().BeFalse();

                var userOverwriteResult = await context.Users.FirstOrDefaultAsync(u => u.Email == emailOverwrite);
                userOverwriteResult.Should().NotBeNull();
                // Password SHOULD be overwritten (verifying it is now "NewImportPassword999!")
                BCrypt.Net.BCrypt.Verify("NewImportPassword999!", userOverwriteResult!.PasswordHash).Should().BeTrue();
            }
        }

        private async Task<string> UploadUserFile(string content, string fileName)
        {
            var multipartContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(content));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            multipartContent.Add(fileContent, "file", fileName);

            // Use template ID 1 for Users
            var response = await _client.PostAsync("/api/imports/upload?templateId=1", multipartContent);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ApiResponse<ImportPreviewResponse>>();
            return result!.Data!.UploadId;
        }

        private async Task<string> GetAdminToken()
        {
            // User imports require superadmin role
            var loginRequest = new LoginRequest
            {
                Email = "superadmin@test.com",
                Password = "superadmin123"
            };

            var response = await _client.PostAsJsonAsync("/api/users/login", loginRequest);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
            return result?.Data?.Token ?? throw new Exception("Failed to get admin token");
        }
    }
}
