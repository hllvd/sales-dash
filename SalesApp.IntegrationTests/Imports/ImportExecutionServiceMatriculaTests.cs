using Microsoft.Extensions.DependencyInjection;
using SalesApp.Models;
using SalesApp.Services;
using SalesApp.Data;
using SalesApp.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class ImportExecutionServiceMatriculaTests
    {
        private readonly TestWebApplicationFactory _factory;

        public ImportExecutionServiceMatriculaTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ImportUsers_WithMatriculaFields_ShouldSucceed()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var uploadId = Guid.NewGuid().ToString();
            var matricula = $"IMP-{Guid.NewGuid().ToString()[..8]}";
            
            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Name", "Imported User" },
                    { "Email", $"imported_{Guid.NewGuid().ToString()[..8]}@test.com" },
                    { "Matricula", matricula },
                    { "IsOwner", "true" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "Name", "Name" },
                { "Email", "Email" },
                { "Matricula", "Matricula" },
                { "IsOwner", "IsMatriculaOwner" }
            };

            // Act
            var result = await service.ExecuteUserImportAsync(uploadId, rows, mappings);

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedUsers.Should().HaveCount(1);
            
            var user = result.CreatedUsers[0];
            user.Matricula.Should().Be(matricula);
            user.IsMatriculaOwner.Should().BeTrue();

            // Verify in DB
            var dbUser = await context.Users.FindAsync(user.Id);
            dbUser.Should().NotBeNull();
            dbUser!.Matricula.Should().Be(matricula);
            dbUser.IsMatriculaOwner.Should().BeTrue();
        }

        [Fact]
        public async Task ImportUsers_SecondOwnerSameMatricula_ShouldFail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var uploadId = Guid.NewGuid().ToString();
            var matricula = $"IMP-DUP-{Guid.NewGuid().ToString()[..8]}";
            
            // Create first owner manually
            var owner1 = new User
            {
                Name = "Existing Owner",
                Email = $"existing_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                Matricula = matricula,
                IsMatriculaOwner = true,
                IsActive = true,
                RoleId = 3
            };
            context.Users.Add(owner1);
            await context.SaveChangesAsync();

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "Name", "Second Owner" },
                    { "Email", $"second_{Guid.NewGuid().ToString()[..8]}@test.com" },
                    { "Matricula", matricula },
                    { "IsOwner", "true" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "Name", "Name" },
                { "Email", "Email" },
                { "Matricula", "Matricula" },
                { "IsOwner", "IsMatriculaOwner" }
            };

            // Act
            var result = await service.ExecuteUserImportAsync(uploadId, rows, mappings);

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(1);
            result.Errors.Should().ContainMatch("*already has an owner*");
        }
    }
}
