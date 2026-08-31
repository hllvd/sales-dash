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

        [Fact]
        public async Task GetAllAsync_WithTeamIds_ShouldReturnContractsSoldDuringTeamTenure()
        {
            // Arrange
            var userA = new User { Id = Guid.NewGuid(), Name = "User A", Email = "usera@test.com", RoleId = 3 };
            var userB = new User { Id = Guid.NewGuid(), Name = "User B", Email = "userb@test.com", RoleId = 3 };
            _context.Users.AddRange(userA, userB);
            await _context.SaveChangesAsync();

            var team1 = new Team { Id = 10, Name = "Team Alpha" };
            var team2 = new Team { Id = 20, Name = "Team Beta" };
            _context.Teams.AddRange(team1, team2);
            await _context.SaveChangesAsync();

            // User A in Team 1 (Jan 2024 to May 2024), then Team 2 (Jun 2024 onwards)
            var utA1 = new UserTeam
            {
                TeamId = team1.Id,
                UserInternalId = userA.InternalId,
                StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2024, 5, 31, 23, 59, 59, DateTimeKind.Utc)
            };
            var utA2 = new UserTeam
            {
                TeamId = team2.Id,
                UserInternalId = userA.InternalId,
                StartDate = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = null
            };

            // User B in Team 1 (Mar 2024 onwards)
            var utB1 = new UserTeam
            {
                TeamId = team1.Id,
                UserInternalId = userB.InternalId,
                StartDate = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = null
            };
            _context.UserTeams.AddRange(utA1, utA2, utB1);
            await _context.SaveChangesAsync();

            var activeStatus = new ContractStatus { Id = 101, Name = "Ativo" };
            _context.ContractStatuses.Add(activeStatus);
            await _context.SaveChangesAsync();

            // Contracts
            var cA_before = new Contract
            {
                ContractNumber = "CTR-A-BEFORE",
                UserInternalId = userA.InternalId,
                User = userA,
                SaleStartDate = new DateTime(2023, 11, 15, 0, 0, 0, DateTimeKind.Utc),
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true
            };
            var cA_team1 = new Contract
            {
                ContractNumber = "CTR-A-TEAM1",
                UserInternalId = userA.InternalId,
                User = userA,
                SaleStartDate = new DateTime(2024, 2, 20, 0, 0, 0, DateTimeKind.Utc),
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true
            };
            var cA_team2 = new Contract
            {
                ContractNumber = "CTR-A-TEAM2",
                UserInternalId = userA.InternalId,
                User = userA,
                SaleStartDate = new DateTime(2024, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true
            };
            var cB_team1 = new Contract
            {
                ContractNumber = "CTR-B-TEAM1",
                UserInternalId = userB.InternalId,
                User = userB,
                SaleStartDate = new DateTime(2024, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                ContractStatusId = activeStatus.Id,
                ContractStatus = activeStatus,
                IsActive = true
            };

            _context.Contracts.AddRange(cA_before, cA_team1, cA_team2, cB_team1);
            await _context.SaveChangesAsync();

            // Act 1: Query by Team 1
            var team1Results = await _repository.GetAllAsync(teamIds: new List<int> { team1.Id });
            var team1Numbers = team1Results.Select(c => c.ContractNumber).ToList();

            // Assert 1: Only contracts during Team 1 period
            team1Numbers.Should().Contain("CTR-A-TEAM1");
            team1Numbers.Should().Contain("CTR-B-TEAM1");
            team1Numbers.Should().NotContain("CTR-A-TEAM2");
            team1Numbers.Should().NotContain("CTR-A-BEFORE");

            // Act 2: Query by Team 2
            var team2Results = await _repository.GetAllAsync(teamIds: new List<int> { team2.Id });
            var team2Numbers = team2Results.Select(c => c.ContractNumber).ToList();

            // Assert 2: Only contracts during Team 2 period
            team2Numbers.Should().Contain("CTR-A-TEAM2");
            team2Numbers.Should().NotContain("CTR-A-TEAM1");
            team2Numbers.Should().NotContain("CTR-A-BEFORE");
            team2Numbers.Should().NotContain("CTR-B-TEAM1");
        }
    }
}
