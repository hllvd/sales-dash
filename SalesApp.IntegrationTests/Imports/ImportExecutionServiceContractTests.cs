using Microsoft.Extensions.DependencyInjection;
using SalesApp.Models;
using SalesApp.Services;
using SalesApp.Data;
using SalesApp.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using FluentAssertions;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Integration Tests")]
    public class ImportExecutionServiceContractTests
    {
        private readonly TestWebApplicationFactory _factory;

        public ImportExecutionServiceContractTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task ImportContracts_WithAllFields_ShouldSucceed()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test group
            var group = new Group
            {
                Name = $"Test Group {Guid.NewGuid().ToString()[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            // Create test user
            var user = new User
            {
                Name = "John Doe",
                Email = $"john_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "150050" },
                    { "GroupId", group.Id.ToString() },
                    { "Status", "active" },
                    { "SaleStartDate", "2024-01-01" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "Status", "Status" },
                { "SaleStartDate", "SaleStartDate" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts.Should().HaveCount(1);

            var contract = result.CreatedContracts[0];
            contract.UserId.Should().Be(user.Id);
            contract.TotalAmount.Should().Be(150050m); // Stored as cents (no decimals)
            contract.GroupId.Should().Be(group.Id);
            contract.Status.Should().Be("active");
            contract.SaleStartDate.Should().BeCloseTo(new DateTime(2024, 1, 1), TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task ImportContracts_WithRequiredFieldsOnly_ShouldSucceed()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test group
            var group = new Group
            {
                Name = $"Test Group {Guid.NewGuid().ToString()[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            // Create test user
            var user = new User
            {
                Name = "Jane Smith",
                Email = $"jane_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "200000" },
                    { "GroupId", group.Id.ToString() }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts.Should().HaveCount(1);

            var contract = result.CreatedContracts[0];
            contract.UserId.Should().Be(user.Id);
            contract.TotalAmount.Should().Be(200000m); // Stored as cents (no decimals)
            contract.GroupId.Should().Be(group.Id);
            contract.Status.Should().Be("active"); // Default status
        }

        [Fact]
        public async Task ImportContracts_MissingRequiredField_ShouldFail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();

            var uploadId = Guid.NewGuid().ToString();

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    // Missing UserEmail
                    { "TotalAmount", "1500.50" },
                    { "GroupId", "1" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(1);
            result.Errors.Should().NotBeEmpty();
            result.Errors[0].Should().Contain("Missing required fields");
        }

        [Fact]
        public async Task ImportContracts_InvalidAmount_ShouldFail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test group
            var group = new Group
            {
                Name = $"Test Group {Guid.NewGuid().ToString()[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            // Create test user
            var user = new User
            {
                Name = "Test User",
                Email = $"test_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "invalid_amount" },
                    { "GroupId", group.Id.ToString() }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(1);
            result.Errors.Should().NotBeEmpty();
            result.Errors[0].Should().Contain("Invalid total amount");
        }

        [Fact]
        public async Task ImportContracts_NonExistentGroup_ShouldCreateGroupAndSucceed()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var uploadId = Guid.NewGuid().ToString();
            var groupName = "NEW-GROUP-999";

            // Create test user
            var user = new User
            {
                Name = "Test User",
                Email = $"test_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "1000.00" },
                    { "GroupId", groupName } 
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY", allowAutoCreateGroups: true);

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedGroups.Should().Contain(groupName);
            
            // Verify group exists in DB
            var createdGroup = await groupRepo.GetByNameAsync(groupName);
            createdGroup.Should().NotBeNull();
            createdGroup!.Name.Should().Be(groupName);
            
            var contract = result.CreatedContracts[0];
            contract.GroupId.Should().Be(createdGroup.Id);
        }

        [Fact]
        public async Task ImportContracts_NonExistentEmail_ShouldFail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test group
            var group = new Group
            {
                Name = $"Test Group {Guid.NewGuid().ToString()[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", "nonexistent@test.com" },
                    { "TotalAmount", "1000.00" },
                    { "GroupId", group.Id.ToString() }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(1);
            result.Errors.Should().NotBeEmpty();
            result.Errors[0].Should().Contain("User not found or inactive");
        }

        [Fact]
        public async Task ImportContracts_MultipleRows_ShouldProcessAll()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test group
            var group = new Group
            {
                Name = $"Test Group {Guid.NewGuid().ToString()[..8]}",
                IsActive = true
            };
            await groupRepo.CreateAsync(group);

            // Create test users
            var user1 = new User
            {
                Name = "Alice Johnson",
                Email = $"alice_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user1);

            var user2 = new User
            {
                Name = "Bob Williams",
                Email = $"bob_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user2);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user1.Email },
                    { "TotalAmount", "1500.00" },
                    { "GroupId", group.Id.ToString() }
                },
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user2.Email },
                    { "TotalAmount", "2500.00" },
                    { "GroupId", group.Id.ToString() }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(2);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts.Should().HaveCount(2);
        }

        [Fact]
        public async Task ImportContracts_MissingGroupId_ShouldDefaultToNull()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();

            // Create test user
            var user = new User
            {
                Name = "Default Group User",
                Email = $"default_{Guid.NewGuid().ToString()[..8]}@test.com",
                PasswordHash = "hash",
                RoleId = 3,
                IsActive = true
            };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "1000.00" }
                    // Missing GroupId
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts[0].GroupId.Should().BeNull();
        }
    }
}
