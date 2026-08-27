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
    public class ContractRepositoryTests
    {
        private readonly AppDbContext _context;
        private readonly ContractRepository _repository;

        public ContractRepositoryTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _repository = new ContractRepository(_context);
        }

        [Fact]
        public async Task CreateAsync_ShouldSaveContractTypeAndQuota()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User", Email = "test@test.com", RoleId = 1 };
            var group = new Group { Id = 1, Name = "Test Group" };
            
            _context.Users.Add(user);
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            var contract = new Contract
            {
                ContractNumber = "CTR-001",
                UserInternalId = user.InternalId,
                GroupId = group.Id,
                TotalAmount = 1000,
                ContractType = 1,
                Quota = 5
            };

            // Act
            var result = await _repository.CreateAsync(contract);

            // Assert
            result.Should().NotBeNull();
            result.ContractType.Should().Be(1);
            result.Quota.Should().Be(5);
            
            var savedContract = await _context.Contracts.FindAsync(result.Id);
            savedContract.Should().NotBeNull();
            savedContract!.ContractType.Should().Be(1);
            savedContract.Quota.Should().Be(5);
        }

        [Fact]
        public async Task UpdateAsync_ShouldUpdateContractTypeAndQuota()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Name = "Test User 2", Email = "test2@test.com", RoleId = 1 };
            var group = new Group { Id = 2, Name = "Test Group 2" };
            
            _context.Users.Add(user);
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();

            var contract = new Contract
            {
                ContractNumber = "CTR-002",
                UserInternalId = user.InternalId,
                GroupId = group.Id,
                TotalAmount = 2000,
                ContractType = 1,
                Quota = 10
            };
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            // Act
            contract.ContractType = 2;
            contract.Quota = 20;
            var result = await _repository.UpdateAsync(contract);

            // Assert
            result.ContractType.Should().Be(2);
            result.Quota.Should().Be(20);
            
            var updatedContract = await _context.Contracts.FindAsync(contract.Id);
            updatedContract!.ContractType.Should().Be(2);
            updatedContract.Quota.Should().Be(20);
        }

        [Fact]
        public async Task GetAllAsync_WithScope_FiltersByHierarchyAndAdminLinkedMatriculas()
        {
            // Arrange
            var activeStatus = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(activeStatus);

            var admin = new User { Id = Guid.NewGuid(), InternalId = 10, Name = "Admin User", Email = "admin@test.com", RoleId = 2 };
            var subordinate = new User { Id = Guid.NewGuid(), InternalId = 20, Name = "Subordinate User", Email = "sub@test.com", RoleId = 3, ParentUserId = admin.Id };
            var otherUser = new User { Id = Guid.NewGuid(), InternalId = 30, Name = "Other User", Email = "other@test.com", RoleId = 3 };

            var matriculaAdmin = new Matricula { Id = 100, MatriculaNumber = "MAT-ADM", Status = "active" };
            var matriculaOther = new Matricula { Id = 200, MatriculaNumber = "MAT-OTH", Status = "active" };

            _context.Users.AddRange(admin, subordinate, otherUser);
            _context.Matriculas.AddRange(matriculaAdmin, matriculaOther);

            // Contract 1: Subordinate's contract (in hierarchy)
            var c1 = new Contract
            {
                ContractNumber = "CTR-SUB",
                UserInternalId = subordinate.InternalId,
                User = subordinate,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                TotalAmount = 100,
                IsActive = true
            };

            // Contract 2: Unassigned contract linked to Admin's matricula
            var c2 = new Contract
            {
                ContractNumber = "CTR-UNASSIGNED-ADM-MAT",
                UserInternalId = null,
                User = null,
                MatriculaId = matriculaAdmin.Id,
                Matricula = matriculaAdmin,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                TotalAmount = 200,
                IsActive = true
            };

            // Contract 3: Subordinate contract linked to Admin's matricula
            var c3 = new Contract
            {
                ContractNumber = "CTR-SUB-ADM-MAT",
                UserInternalId = subordinate.InternalId,
                User = subordinate,
                MatriculaId = matriculaAdmin.Id,
                Matricula = matriculaAdmin,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                TotalAmount = 300,
                IsActive = true
            };

            // Contract 4: Other user's contract linked to Admin's matricula (other user NOT in admin hierarchy)
            var c4 = new Contract
            {
                ContractNumber = "CTR-OTHER-ADM-MAT",
                UserInternalId = otherUser.InternalId,
                User = otherUser,
                MatriculaId = matriculaAdmin.Id,
                Matricula = matriculaAdmin,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                TotalAmount = 400,
                IsActive = true
            };

            // Contract 5: Unrelated contract
            var c5 = new Contract
            {
                ContractNumber = "CTR-UNRELATED",
                UserInternalId = otherUser.InternalId,
                User = otherUser,
                MatriculaId = matriculaOther.Id,
                Matricula = matriculaOther,
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                TotalAmount = 500,
                IsActive = true
            };

            _context.Contracts.AddRange(c1, c2, c3, c4, c5);
            await _context.SaveChangesAsync();

            // Scope for admin:
            // AllowedUserIds = { admin.Id, subordinate.Id }
            // AllowedMatriculas = {} (or not containing MAT-ADM for testing the AdminLinkedMatriculas path)
            // AdminLinkedMatriculas = { "MAT-ADM" }
            var scope = new UserScopeContext
            {
                IsGlobal = false,
                AllowedUserIds = new HashSet<Guid> { admin.Id, subordinate.Id },
                AllowedMatriculas = new HashSet<string>(),
                AdminLinkedMatriculas = new HashSet<string> { "MAT-ADM" }
            };

            // Act
            var results = await _repository.GetAllAsync(scope: scope);

            // Assert
            var contractNumbers = results.Select(c => c.ContractNumber).ToList();
            contractNumbers.Should().Contain("CTR-SUB"); // via AllowedUserIds
            contractNumbers.Should().Contain("CTR-UNASSIGNED-ADM-MAT"); // via AdminLinkedMatriculas + unassigned
            contractNumbers.Should().Contain("CTR-SUB-ADM-MAT"); // via AdminLinkedMatriculas + descendant
            contractNumbers.Should().NotContain("CTR-OTHER-ADM-MAT"); // other user is not in hierarchy
            contractNumbers.Should().NotContain("CTR-UNRELATED");
        }
    }
}
