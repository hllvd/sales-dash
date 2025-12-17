using Xunit;
using FluentAssertions;
using Moq;
using SalesApp.Repositories;
using SalesApp.Models;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;

namespace SalesApp.Tests.Repositories
{
    public class UserMatriculaRepositoryTests : IDisposable
    {
        private readonly AppDbContext _context;
        private readonly UserMatriculaRepository _repository;

        public UserMatriculaRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _repository = new UserMatriculaRepository(_context);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateUserMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT001",
                StartDate = DateTime.UtcNow,
                IsActive = true
            };

            // Act
            var result = await _repository.CreateAsync(matricula);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.MatriculaNumber.Should().Be("MAT001");
            result.UserId.Should().Be(user.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT002",
                StartDate = DateTime.UtcNow
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(matricula.Id);

            // Assert
            result.Should().NotBeNull();
            result!.MatriculaNumber.Should().Be("MAT002");
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnUserMatriculas()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula1 = new UserMatricula { UserId = user.Id, MatriculaNumber = "MAT003", StartDate = DateTime.UtcNow };
            var matricula2 = new UserMatricula { UserId = user.Id, MatriculaNumber = "MAT004", StartDate = DateTime.UtcNow.AddDays(-1) };
            _context.UserMatriculas.AddRange(matricula1, matricula2);
            await _context.SaveChangesAsync();

            // Act
            var results = await _repository.GetByUserIdAsync(user.Id);

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(m => m.MatriculaNumber == "MAT003");
            results.Should().Contain(m => m.MatriculaNumber == "MAT004");
        }

        [Fact]
        public async Task GetActiveByUserIdAsync_ShouldReturnOnlyActiveMatriculas()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var activeMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaNumber = "MAT005", 
                StartDate = DateTime.UtcNow,
                IsActive = true
            };
            var inactiveMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaNumber = "MAT006", 
                StartDate = DateTime.UtcNow,
                IsActive = false
            };
            var expiredMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaNumber = "MAT007", 
                StartDate = DateTime.UtcNow.AddDays(-10),
                EndDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };
            
            _context.UserMatriculas.AddRange(activeMatricula, inactiveMatricula, expiredMatricula);
            await _context.SaveChangesAsync();

            // Act
            var results = await _repository.GetActiveByUserIdAsync(user.Id);

            // Assert
            results.Should().HaveCount(1);
            results[0].MatriculaNumber.Should().Be("MAT005");
        }

        [Fact]
        public async Task IsMatriculaValidForUser_ShouldReturnTrueForValidMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT008",
                StartDate = DateTime.UtcNow,
                IsActive = true
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.IsMatriculaValidForUser(user.Id, matricula.Id);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task IsMatriculaValidForUser_ShouldReturnFalseForDifferentUser()
        {
            // Arrange
            var user1 = new User { Id = Guid.NewGuid(), Name = "User 1", Email = "user1@test.com", PasswordHash = "hash" };
            var user2 = new User { Id = Guid.NewGuid(), Name = "User 2", Email = "user2@test.com", PasswordHash = "hash" };
            _context.Users.AddRange(user1, user2);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user1.Id,
                MatriculaNumber = "MAT009",
                StartDate = DateTime.UtcNow,
                IsActive = true
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.IsMatriculaValidForUser(user2.Id, matricula.Id);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT010",
                StartDate = DateTime.UtcNow,
                IsActive = true
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();

            // Act
            matricula.IsActive = false;
            matricula.EndDate = DateTime.UtcNow;
            var result = await _repository.UpdateAsync(matricula);

            // Assert
            result.IsActive.Should().BeFalse();
            result.EndDate.Should().NotBeNull();
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT011",
                StartDate = DateTime.UtcNow
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();
            var matriculaId = matricula.Id;

            // Act
            await _repository.DeleteAsync(matriculaId);

            // Assert
            var deleted = await _repository.GetByIdAsync(matriculaId);
            deleted.Should().BeNull();
        }

        [Fact]
        public async Task MatriculaExistsAsync_ShouldReturnTrueWhenExists()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaNumber = "MAT012",
                StartDate = DateTime.UtcNow
            };
            _context.UserMatriculas.Add(matricula);
            await _context.SaveChangesAsync();

            // Act
            var exists = await _repository.MatriculaExistsAsync("MAT012");
            var notExists = await _repository.MatriculaExistsAsync("MAT999");

            // Assert
            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
