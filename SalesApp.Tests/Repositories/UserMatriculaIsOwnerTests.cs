using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;

namespace SalesApp.Tests.Repositories
{
    public class UserMatriculaIsOwnerTests
    {
        private readonly AppDbContext _context;
        private readonly UserMatriculaRepository _repository;

        public UserMatriculaIsOwnerTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options);
            _repository = new UserMatriculaRepository(_context);
        }

        [Fact]
        public async Task CreateMatricula_WithIsOwnerTrue_ShouldSetAsOwner()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaNumber = "MAT-001",
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            };

            // Act
            var created = await _repository.CreateAsync(matricula);

            // Assert
            created.IsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task CreateMatricula_WithIsOwnerFalse_ShouldNotSetAsOwner()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaNumber = "MAT-002",
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            };

            // Act
            var created = await _repository.CreateAsync(matricula);

            // Assert
            created.IsOwner.Should().BeFalse();
        }

        [Fact]
        public async Task CreateMatricula_SecondUserWithSameNumberAndIsOwnerTrue_ShouldTransferOwnership()
        {
            // Arrange
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            var matriculaNumber = "MAT-TRANSFER";

            // Create first matricula with IsOwner = true
            var matricula1 = new UserMatricula
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            };
            await _repository.CreateAsync(matricula1);

            // Act - Create second matricula with same number and IsOwner = true
            var matricula2 = new UserMatricula
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            };
            await _repository.CreateAsync(matricula2);

            // Assert
            var user1Matriculas = await _repository.GetByUserIdAsync(user1Id);
            var user2Matriculas = await _repository.GetByUserIdAsync(user2Id);

            user1Matriculas.First().IsOwner.Should().BeFalse("User 1 should no longer be owner");
            user2Matriculas.First().IsOwner.Should().BeTrue("User 2 should now be owner");
        }

        [Fact]
        public async Task UpdateMatricula_SetIsOwnerToTrue_ShouldTransferOwnership()
        {
            // Arrange
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            var matriculaNumber = "MAT-UPDATE";

            // Create two matriculas with same number, first one is owner
            var matricula1 = await _repository.CreateAsync(new UserMatricula
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            });

            var matricula2 = await _repository.CreateAsync(new UserMatricula
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            });

            // Act - Update second matricula to be owner
            matricula2.IsOwner = true;
            await _repository.UpdateAsync(matricula2);

            // Assert
            var updatedMatricula1 = await _repository.GetByIdAsync(matricula1.Id);
            var updatedMatricula2 = await _repository.GetByIdAsync(matricula2.Id);

            updatedMatricula1!.IsOwner.Should().BeFalse("User 1 should no longer be owner");
            updatedMatricula2!.IsOwner.Should().BeTrue("User 2 should now be owner");
        }

        [Fact]
        public async Task GetOwnerByMatriculaNumberAsync_ShouldReturnOwner()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var nonOwnerId = Guid.NewGuid();
            var matriculaNumber = "MAT-OWNER";

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = ownerId,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = nonOwnerId,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            });

            // Act
            var owner = await _repository.GetOwnerByMatriculaNumberAsync(matriculaNumber);

            // Assert
            owner.Should().NotBeNull();
            owner!.UserId.Should().Be(ownerId);
            owner.IsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task GetOwnerByMatriculaNumberAsync_NoOwner_ShouldReturnNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var matriculaNumber = "MAT-NO-OWNER";

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = userId,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            });

            // Act
            var owner = await _repository.GetOwnerByMatriculaNumberAsync(matriculaNumber);

            // Assert
            owner.Should().BeNull();
        }

        [Fact]
        public async Task SetOwnerAsync_ShouldTransferOwnershipCorrectly()
        {
            // Arrange
            var user1Id = Guid.NewGuid();
            var user2Id = Guid.NewGuid();
            var user3Id = Guid.NewGuid();
            var matriculaNumber = "MAT-SET-OWNER";

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user1Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user2Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user3Id,
                MatriculaNumber = matriculaNumber,
                StartDate = DateTime.UtcNow,
                IsOwner = false,
                IsActive = true
            });

            // Act
            await _repository.SetOwnerAsync(matriculaNumber, user2Id);

            // Assert
            var user1Matriculas = await _repository.GetByUserIdAsync(user1Id);
            var user2Matriculas = await _repository.GetByUserIdAsync(user2Id);
            var user3Matriculas = await _repository.GetByUserIdAsync(user3Id);

            user1Matriculas.First().IsOwner.Should().BeFalse();
            user2Matriculas.First().IsOwner.Should().BeTrue();
            user3Matriculas.First().IsOwner.Should().BeFalse();
        }

        [Fact]
        public async Task MultipleUsers_SameMatricula_OnlyOneOwner()
        {
            // Arrange
            var matriculaNumber = "MAT-MULTI";
            var users = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            // Create matriculas for all users, last one is owner
            for (int i = 0; i < users.Length; i++)
            {
                await _repository.CreateAsync(new UserMatricula
                {
                    UserId = users[i],
                    MatriculaNumber = matriculaNumber,
                    StartDate = DateTime.UtcNow,
                    IsOwner = (i == users.Length - 1), // Last user is owner
                    IsActive = true
                });
            }

            // Act
            var allMatriculas = await _context.UserMatriculas
                .Where(m => m.MatriculaNumber == matriculaNumber)
                .ToListAsync();

            // Assert
            allMatriculas.Should().HaveCount(4);
            allMatriculas.Count(m => m.IsOwner).Should().Be(1, "Only one user should be owner");
            allMatriculas.Last().IsOwner.Should().BeTrue("Last user should be owner");
        }
    }
}
