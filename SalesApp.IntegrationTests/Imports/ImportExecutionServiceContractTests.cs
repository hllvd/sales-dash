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
    [Collection("Imports Tests")]
    public class ImportExecutionServiceContractTests
    {
        private readonly ImportsTestFactory _factory;

        public ImportExecutionServiceContractTests(ImportsTestFactory factory)
        {
            _factory = factory;
        }

        private async Task<ImportSession> CreateTestSessionAsync(AppDbContext context, string uploadId, string fileName = "test.csv")
        {
            var admin = await context.Users.FirstOrDefaultAsync(u => u.Role.Name == "superadmin");
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = fileName,
                Status = "preview",
                UploadedByUserInternalId = admin?.InternalId ?? 1,
                CreatedAt = DateTime.UtcNow
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();
            return session;
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
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "Status", "Active" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-123" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "Status", "Status" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts.Should().HaveCount(1);

            var contract = result.CreatedContracts[0];
            contract.UserInternalId.Should().Be(user.InternalId);
            contract.TotalAmount.Should().Be(150050m); // Stored as cents (no decimals)
            contract.GroupId.Should().Be(group.Id);
            contract.ContractStatusId.Should().Be(1);
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
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "GroupId", group.Id.ToString() },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-456" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts.Should().HaveCount(1);

            var contract = result.CreatedContracts[0];
            contract.UserInternalId.Should().Be(user.InternalId);
            contract.TotalAmount.Should().Be(200000m); // Stored as cents (no decimals)
            contract.GroupId.Should().Be(group.Id);
            contract.ContractStatusId.Should().Be(1); // Default status
        }

        [Fact]
        public async Task ImportContracts_MissingRequiredField_ShouldFail()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var uploadId = Guid.NewGuid().ToString();
            var session = await CreateTestSessionAsync(context, uploadId);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", "test@test.com" },
                    // Missing TotalAmount
                    { "GroupId", "1" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-789" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "GroupId", "GroupId" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "TotalAmount", "invalid_amount" }, // INVALID
                    { "GroupId", group.Id.ToString() },
                    { "SaleStartDate", "2024-01-01" }, // VALID
                    { "MatriculaNumber", "MAT-INV" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(1);
            result.Errors.Should().NotBeEmpty();
            result.Errors[0].Should().Contain("Invalid total amount");
        }

        [Fact]
        public async Task ImportContracts_MissingSaleStartDate_ShouldSkipSilently()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await CreateTestSessionAsync(context, uploadId);

            // Create test user
            var user = new User { Name = "Test", Email = "test@test.com", RoleId = 3, IsActive = true };
            await userRepo.CreateAsync(user);

            var rows = new List<Dictionary<string, string>>
            {
                new()
                {
                    { "ContractNumber", "SKIP-1" },
                    { "UserEmail", user.Email },
                    { "TotalAmount", "1000" },
                    { "SaleStartDate", "" }, // MISSING
                    { "MatriculaNumber", "MAT-SKIP" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(0);
            result.FailedRows.Should().Be(0); // SILENT SKIP
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
            var session = await CreateTestSessionAsync(context, uploadId);
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
                    { "TotalAmount", "100000" },
                    { "GroupId", groupName },
                    { "Status", "" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-GROUP" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "Status", "Status" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY", allowAutoCreateGroups: true);

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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "UserEmail", "nonexistent@test.com" }, // NON-EXISTENT
                    { "TotalAmount", "150000" },
                    { "GroupId", group.Id.ToString() },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-NONEXIST" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "GroupId", group.Id.ToString() },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-MULTI-1" }
                },
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "UserEmail", user2.Email },
                    { "TotalAmount", "2500.00" },
                    { "GroupId", group.Id.ToString() },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-MULTI-2" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "GroupId", "GroupId" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

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
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "TotalAmount", "1000.00" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-NULLGRP" }
                    // Missing GroupId
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "UserEmail", "UserEmail" },
                { "TotalAmount", "TotalAmount" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(1);
            result.FailedRows.Should().Be(0);
            result.CreatedContracts[0].GroupId.Should().BeNull();
        }

        [Fact]
        public async Task ImportContracts_WithUnknownStatus_ShouldMapToNaoDefinidoAndSaveRawStatus()
        {
            // Arrange
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IImportExecutionService>();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var groupRepo = scope.ServiceProvider.GetRequiredService<IGroupRepository>();

            var uploadId = Guid.NewGuid().ToString();
            var session = await CreateTestSessionAsync(context, uploadId);

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
                    { "TotalAmount", "1000.00" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-STATUS-1" },
                    { "GroupId", group.Name },
                    { "Status", "COBRANCA ADMINISTRATIVA" }
                },
                new()
                {
                    { "ContractNumber", $"CNT-{Guid.NewGuid().ToString()[..8]}" },
                    { "TotalAmount", "1000.00" },
                    { "SaleStartDate", "2024-01-01" },
                    { "MatriculaNumber", "MAT-STATUS-2" },
                    { "GroupId", group.Name },
                    { "Status", "FOO_BAR_STATUS" }
                }
            };

            var mappings = new Dictionary<string, string>
            {
                { "ContractNumber", "ContractNumber" },
                { "TotalAmount", "TotalAmount" },
                { "SaleStartDate", "SaleStartDate" },
                { "MatriculaNumber", "MatriculaNumber" },
                { "GroupId", "GroupId" },
                { "Status", "Status" }
            };

            // Act
            var result = await service.ExecuteContractImportAsync(uploadId, session.Id, rows, mappings, "MM/DD/YYYY");

            // Assert
            result.ProcessedRows.Should().Be(2);
            result.FailedRows.Should().Be(0);
            
            // Retrieve contracts from DB to inspect status name
            var dbContracts = await context.Contracts
                .Include(c => c.ContractStatus)
                .Where(c => c.UploadId == uploadId)
                .ToListAsync();

            dbContracts.Should().HaveCount(2);

            var contract1 = dbContracts.FirstOrDefault(c => c.Matricula?.MatriculaNumber == "MAT-STATUS-1");
            contract1.Should().NotBeNull();
            contract1!.ContractStatus.Name.Should().Be(ContractStatus.NaoDefinido.ToApiString());
            contract1.RawStatus.Should().Be("COBRANCA ADMINISTRATIVA");

            var contract2 = dbContracts.FirstOrDefault(c => c.Matricula?.MatriculaNumber == "MAT-STATUS-2");
            contract2.Should().NotBeNull();
            contract2!.ContractStatus.Name.Should().Be(ContractStatus.NaoDefinido.ToApiString());
            contract2.RawStatus.Should().Be("FOO_BAR_STATUS");

            result.Warnings.Should().Contain(w => w.Contains("COBRANCA ADMINISTRATIVA") && w.Contains("Nao definido"));
            result.Warnings.Should().Contain(w => w.Contains("FOO_BAR_STATUS") && w.Contains("Nao definido"));
        }
    }
}
