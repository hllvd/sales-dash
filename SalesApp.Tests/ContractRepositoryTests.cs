using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using Xunit;

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

            _context = new AppDbContext(options);
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
                UserId = user.Id,
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
                UserId = user.Id,
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
    }
}
