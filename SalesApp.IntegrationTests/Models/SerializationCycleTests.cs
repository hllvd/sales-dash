using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Data;
using SalesApp.Models;
using Xunit;

namespace SalesApp.IntegrationTests
{
    [Collection("Integration Tests")]
    public class SerializationCycleTests
    {
        private readonly TestWebApplicationFactory _factory;

        public SerializationCycleTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task UserSerialization_WithMatriculaInclude_ShouldNotThrowCircularReferenceException()
        {
            // Arrange: Create a user with a matricula
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var uniqueId = Guid.NewGuid().ToString()[..8];
            var testUser = new User
            {
                Name = $"Serialization Test User {uniqueId}",
                Email = $"serialization.test.{uniqueId}@test.com",
                PasswordHash = "hash123"
            };

            var testMatricula = new Matricula
            {
                MatriculaNumber = $"MAT-SER-{uniqueId}",
                StartDate = DateTime.UtcNow,
                Status = "active"
            };

            context.Users.Add(testUser);
            context.Matriculas.Add(testMatricula);
            await context.SaveChangesAsync();

            var userMatricula = new UserMatricula
            {
                UserInternalId = testUser.InternalId,
                MatriculaId = testMatricula.Id,
                IsOwner = true
            };
            context.UserMatriculas.Add(userMatricula);
            await context.SaveChangesAsync();

            // Act: Fetch exactly how the UserRepository fetches it
            var fetchedUser = await context.Users
                .Include(u => u.Role)
                .Include(u => u.UserMatriculas).ThenInclude(um => um.Matricula)
                .FirstOrDefaultAsync(u => u.Id == testUser.Id);

            fetchedUser.Should().NotBeNull();
            fetchedUser!.UserMatriculas.Should().HaveCount(1);
            fetchedUser.UserMatriculas.First().Matricula.Should().NotBeNull();

            // Assert: Serializing the fetched user should NOT throw an exception
            var options = new JsonSerializerOptions
            {
                ReferenceHandler = ReferenceHandler.IgnoreCycles // Adding this just in case, but [JsonIgnore] should handle it natively
            };

            // This line will throw a JsonException if there's an unhandled circular reference (e.g. if [JsonIgnore] is missing)
            var action = () => JsonSerializer.Serialize(fetchedUser);
            
            action.Should().NotThrow<JsonException>("because [JsonIgnore] attributes on back-references should prevent infinite loops");
            
            // Clean up
            context.UserMatriculas.Remove(userMatricula);
            context.Matriculas.Remove(testMatricula);
            context.Users.Remove(testUser);
            await context.SaveChangesAsync();
        }
    }
}
