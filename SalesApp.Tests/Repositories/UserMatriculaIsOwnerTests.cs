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
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com" };
            _context.Users.Add(user);
            var mat = new Matricula { MatriculaNumber = "MAT-001", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserInternalId = user.InternalId,
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
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com" };
            _context.Users.Add(user);
            var mat = new Matricula { MatriculaNumber = "MAT-002", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            var matricula = new UserMatricula
            {
                UserInternalId = user.InternalId,
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
            var user1 = new User { Id = user1Id, Name = "User 1", Email = "user1@test.com" };
            var user2 = new User { Id = user2Id, Name = "User 2", Email = "user2@test.com" };
            _context.Users.AddRange(user1, user2);
            var mat = new Matricula { MatriculaNumber = "MAT-TRANSFER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            // Create first matricula with IsOwner = true
            var matricula1 = new UserMatricula
            {
                UserInternalId = user1.InternalId,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            };
            await _repository.CreateAsync(matricula1);

            // Act - Create second matricula with same number and IsOwner = true
            var matricula2 = new UserMatricula
            {
                UserInternalId = user2.InternalId,
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
            var user1 = new User { Id = user1Id, Name = "User 1", Email = "user1@test.com" };
            var user2 = new User { Id = user2Id, Name = "User 2", Email = "user2@test.com" };
            _context.Users.AddRange(user1, user2);
            var mat = new Matricula { MatriculaNumber = "MAT-UPDATE", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            // Create two matriculas with same number, first one is owner
            var matricula1 = await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user1.InternalId,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            var matricula2 = await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user2.InternalId,
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
            var ownerUser = new User { Id = ownerId, Name = "Owner User", Email = "owner@test.com" };
            var nonOwnerUser = new User { Id = nonOwnerId, Name = "Non Owner User", Email = "nonowner@test.com" };
            _context.Users.AddRange(ownerUser, nonOwnerUser);
            var mat = new Matricula { MatriculaNumber = "MAT-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = ownerUser.InternalId,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = nonOwnerUser.InternalId,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            // Act
            var owner = await _repository.GetOwnerByMatriculaIdAsync(mat.Id);

            // Assert
            owner.Should().NotBeNull();
            owner!.UserInternalId.Should().Be(ownerUser.InternalId);
            owner.IsOwner.Should().BeTrue();
        }

        [Fact]
        public async Task GetOwnerByMatriculaIdAsync_NoOwner_ShouldReturnNull()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com" };
            _context.Users.Add(user);
            var mat = new Matricula { MatriculaNumber = "MAT-NO-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user.InternalId,
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
            var user1 = new User { Id = user1Id, Name = "User 1", Email = "user1@test.com" };
            var user2 = new User { Id = user2Id, Name = "User 2", Email = "user2@test.com" };
            var user3 = new User { Id = user3Id, Name = "User 3", Email = "user3@test.com" };
            _context.Users.AddRange(user1, user2, user3);
            var mat = new Matricula { MatriculaNumber = "MAT-SET-OWNER", StartDate = DateTime.UtcNow, Status = "active" };
            _context.Matriculas.Add(mat);
            await _context.SaveChangesAsync();

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user1.InternalId,
                MatriculaId = mat.Id,
                IsOwner = true,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user2.InternalId,
                MatriculaId = mat.Id,
                IsOwner = false,
                IsActive = true
            });

            await _repository.CreateAsync(new UserMatricula
            {
                UserInternalId = user3.InternalId,
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

            var userGuids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var users = new List<User>();
            for (int i = 0; i < userGuids.Length; i++)
            {
                var user = new User { Id = userGuids[i], Name = $"User {i}", Email = $"user{i}@test.com" };
                _context.Users.Add(user);
                users.Add(user);
            }
            await _context.SaveChangesAsync();

            // Create matriculas for all users, last one is owner
            for (int i = 0; i < users.Count; i++)
            {
                await _repository.CreateAsync(new UserMatricula
                {
                    UserInternalId = users[i].InternalId,
                    MatriculaId = mat.Id,
                    IsOwner = (i == users.Count - 1), // Last user is owner
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
