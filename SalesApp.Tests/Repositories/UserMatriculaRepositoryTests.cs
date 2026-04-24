using Xunit;
using FluentAssertions;
using Moq;
using SalesApp.Repositories;
using SalesApp.Models;
using SalesApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

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

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _repository = new UserMatriculaRepository(_context);
        }

        [Fact]
        public async Task CreateAsync_ShouldCreateUserMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var matricula = new Matricula { MatriculaNumber = "MAT001", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(matricula);
            await _context.SaveChangesAsync();

            var userMatricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = matricula.Id,
                IsActive = true
            };

            // Act
            var result = await _repository.CreateAsync(userMatricula);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().BeGreaterThan(0);
            result.MatriculaId.Should().Be(matricula.Id);
            result.UserId.Should().Be(user.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var matricula = new Matricula { MatriculaNumber = "MAT002", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(matricula);
            await _context.SaveChangesAsync();

            var userMatricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = matricula.Id
            };
            _context.UserMatriculas.Add(userMatricula);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(userMatricula.Id);

            // Assert
            result.Should().NotBeNull();
            result!.MatriculaId.Should().Be(matricula.Id);
        }

        [Fact]
        public async Task GetByUserIdAsync_ShouldReturnUserMatriculas()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var mat1 = new Matricula { MatriculaNumber = "MAT003", StartDate = DateTime.UtcNow, Status = "active" };
            var mat2 = new Matricula { MatriculaNumber = "MAT004", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.AddRange(mat1, mat2);
            await _context.SaveChangesAsync();

            var userMatricula1 = new UserMatricula { UserId = user.Id, MatriculaId = mat1.Id };
            var userMatricula2 = new UserMatricula { UserId = user.Id, MatriculaId = mat2.Id };
            _context.UserMatriculas.AddRange(userMatricula1, userMatricula2);
            await _context.SaveChangesAsync();

            // Act
            var results = await _repository.GetByUserIdAsync(user.Id);

            // Assert
            results.Should().HaveCount(2);
            results.Should().Contain(m => m.MatriculaId == mat1.Id);
            results.Should().Contain(m => m.MatriculaId == mat2.Id);
        }

        [Fact]
        public async Task GetActiveByUserIdAsync_ShouldReturnOnlyActiveMatriculas()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var mat1 = new Matricula { MatriculaNumber = "MAT005", StartDate = DateTime.UtcNow, Status = "active" };
            var mat2 = new Matricula { MatriculaNumber = "MAT006", StartDate = DateTime.UtcNow, Status = "active" };
            var mat3 = new Matricula { MatriculaNumber = "MAT007", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.AddRange(mat1, mat2, mat3);
            await _context.SaveChangesAsync();

            var activeMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaId = mat1.Id, 
                IsActive = true
            };
            var inactiveMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaId = mat2.Id, 
                IsActive = false
            };
            var expiredMatricula = new UserMatricula 
            { 
                UserId = user.Id, 
                MatriculaId = mat3.Id, 
                EndDate = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };
            
            _context.UserMatriculas.AddRange(activeMatricula, inactiveMatricula, expiredMatricula);
            await _context.SaveChangesAsync();

            // Act
            var results = await _repository.GetActiveByUserIdAsync(user.Id);

            // Assert
            results.Should().HaveCount(1);
            results[0].MatriculaId.Should().Be(mat1.Id);
        }

        [Fact]
        public async Task IsMatriculaValidForUser_ShouldReturnTrueForValidMatricula()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var mat = new Matricula { MatriculaNumber = "MAT008", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat.Id,
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

            var mat = new Matricula { MatriculaNumber = "MAT009", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user1.Id,
                MatriculaId = mat.Id,
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

            var mat = new Matricula { MatriculaNumber = "MAT010", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat.Id,
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

            var mat = new Matricula { MatriculaNumber = "MAT011", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat.Id
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
        public async Task CreateAsync_ShouldThrowException_WhenDuplicateMatriculaForSameUser()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "duplicate@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var mat = new Matricula { MatriculaNumber = "DUP001", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula1 = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat.Id,
                IsActive = true
            };
            await _repository.CreateAsync(matricula1);

            var matricula2 = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat.Id,
                IsActive = true
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.CreateAsync(matricula2));
        }

        [Fact]
        public async Task UpdateAsync_ShouldThrowException_WhenDuplicateMatriculaForSameUser()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "update-dup@test.com", PasswordHash = "hash" };
            _context.Users.Add(user);

            var mat1 = new Matricula { MatriculaNumber = "ORIGINAL", StartDate = DateTime.UtcNow, Status = "active" };
            var mat2 = new Matricula { MatriculaNumber = "OTHER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.AddRange(mat1, mat2);
            await _context.SaveChangesAsync();

            var matricula1 = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat1.Id,
                IsActive = true
            };
            await _repository.CreateAsync(matricula1);

            var matricula2 = new UserMatricula
            {
                UserId = user.Id,
                MatriculaId = mat2.Id,
                IsActive = true
            };
            await _repository.CreateAsync(matricula2);

            // Act
            matricula2.MatriculaId = mat1.Id;

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.UpdateAsync(matricula2));
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
