using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;

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

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _repository = new UserMatriculaRepository(_context);
        }

        [Fact]
        public async Task CreateMatricula_WithIsOwnerTrue_ShouldSetAsOwner()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mat = new Matricula { MatriculaNumber = "MAT-001", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaId = mat.Id,
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
            var mat = new Matricula { MatriculaNumber = "MAT-002", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserId = userId,
                MatriculaId = mat.Id,
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
            var mat = new Matricula { MatriculaNumber = "MAT-TRANSFER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            // Create first matricula with IsOwner = true
            var matricula1 = new UserMatricula
            {
                UserId = user1Id,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            };
            await _repository.CreateAsync(matricula1);

            // Act - Create second matricula with same number and IsOwner = true
            var matricula2 = new UserMatricula
            {
                UserId = user2Id,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            };
            await _repository.CreateAsync(matricula2);
            _context.ChangeTracker.Clear();

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
            var mat = new Matricula { MatriculaNumber = "MAT-UPDATE", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            // Create two matriculas with same number, first one is owner
            var matricula1 = await _repository.CreateAsync(new UserMatricula
            {
                UserId = user1Id,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            var matricula2 = await _repository.CreateAsync(new UserMatricula
            {
                UserId = user2Id,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            // Act - Update second matricula to be owner
            matricula2.IsOwner = true;
            await _repository.UpdateAsync(matricula2);
            _context.ChangeTracker.Clear();

            // Assert - query IsOwner directly to avoid Include navigation issues with InMemory DB
            var isOwner1 = await _context.UserMatriculas
                .AsNoTracking()
                .Where(m => m.Id == matricula1.Id)
                .Select(m => m.IsOwner)
                .FirstOrDefaultAsync();

            var isOwner2 = await _context.UserMatriculas
                .AsNoTracking()
                .Where(m => m.Id == matricula2.Id)
                .Select(m => m.IsOwner)
                .FirstOrDefaultAsync();

            isOwner1.Should().BeFalse("User 1 should no longer be owner");
            isOwner2.Should().BeTrue("User 2 should now be owner");
        }

        [Fact]
        public async Task GetOwnerByMatriculaIdAsync_ShouldReturnOwner()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var nonOwnerId = Guid.NewGuid();
            var mat = new Matricula { MatriculaNumber = "MAT-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = ownerId,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = nonOwnerId,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            // Act
            var owner = await _repository.GetOwnerByMatriculaIdAsync(mat.Id);

            // Assert
            owner.Should().NotBeNull();
            owner!.UserId.Should().Be(ownerId);
            owner.IsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task GetOwnerByMatriculaIdAsync_NoOwner_ShouldReturnNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var mat = new Matricula { MatriculaNumber = "MAT-NO-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = userId,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            // Act
            var owner = await _repository.GetOwnerByMatriculaIdAsync(mat.Id);

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
            var mat = new Matricula { MatriculaNumber = "MAT-SET-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user1Id,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user2Id,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserId = user3Id,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            // Act
            await _repository.SetOwnerAsync(mat.Id, user2Id);
            _context.ChangeTracker.Clear();

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
            var mat = new Matricula { MatriculaNumber = "MAT-MULTI", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var users = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            // Create matriculas for all users, last one is owner
            for (int i = 0; i < users.Length; i++)
            {
                await _repository.CreateAsync(new UserMatricula
                {
                    UserId = users[i],
                    MatriculaId = mat.Id,
                    IsOwner = (i == users.Length - 1), // Last user is owner
                    IsActive = true
                });
            }

            // Act
            _context.ChangeTracker.Clear();
            var allMatriculas = await _context.UserMatriculas
                .Where(m => m.MatriculaId == mat.Id)
                .ToListAsync();

            // Assert
            allMatriculas.Should().HaveCount(4);
            allMatriculas.Count(m => m.IsOwner).Should().Be(1, "Only one user should be owner");
            allMatriculas.Last().IsOwner.Should().BeTrue("Last user should be owner");
        }
    }
}
