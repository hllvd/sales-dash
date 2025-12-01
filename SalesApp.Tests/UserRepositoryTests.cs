using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;

namespace SalesApp.Tests.Repositories
{
    public class UserRepositoryTests
    {
        private readonly AppDbContext _context;
        private readonly UserRepository _repository;

        public UserRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _repository = new UserRepository(_context);
        }

        [Fact]
        public async Task GetByMatricula_ExistingMatricula_ShouldReturnUser()
        {
            // Arrange
            var role = new Role { Id = 1, Name = "user" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            var matricula = "MAT123";
            var user = new User
            {
                Name = "John Doe",
                Email = "john@test.com",
                Matricula = matricula,
                IsActive = true,
                PasswordHash = "hash",
                RoleId = role.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByMatriculaAsync(matricula);

            // Assert
            result.Should().HaveCount(1);
            result.First().Name.Should().Be("John Doe");
            result.First().Matricula.Should().Be(matricula);
        }

        [Fact]
        public async Task GetByMatricula_NonExistentMatricula_ShouldReturnEmptyList()
        {
            // Act
            var result = await _repository.GetByMatriculaAsync("NON_EXISTENT");

            // Assert
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByMatricula_MultipleUsersWithSameMatricula_ShouldReturnAll()
        {
            // Arrange
            var role = new Role { Id = 2, Name = "user" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            var matricula = "MAT_DUPLICATE";
            var user1 = new User
            {
                Name = "User 1",
                Email = "user1@test.com",
                Matricula = matricula,
                IsActive = true,
                PasswordHash = "hash",
                RoleId = role.Id
            };
            var user2 = new User
            {
                Name = "User 2",
                Email = "user2@test.com",
                Matricula = matricula,
                IsActive = true,
                PasswordHash = "hash",
                RoleId = role.Id
            };
            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByMatriculaAsync(matricula);

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain(u => u.Name == "User 1");
            result.Should().Contain(u => u.Name == "User 2");
        }

        [Fact]
        public async Task GetByMatricula_InactiveUser_ShouldNotReturn()
        {
            // Arrange
            var role = new Role { Id = 3, Name = "user" };
            _context.Roles.Add(role);
            await _context.SaveChangesAsync();

            var matricula = "MAT_INACTIVE";
            var user = new User
            {
                Name = "Inactive User",
                Email = "inactive@test.com",
                Matricula = matricula,
                IsActive = false,
                PasswordHash = "hash",
                RoleId = role.Id
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByMatriculaAsync(matricula);

            // Assert
            result.Should().BeEmpty();
        }
    }
}
