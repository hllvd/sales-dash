using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SalesApp.Controllers;
using SalesApp.Data;
using SalesApp.DTOs;
using SalesApp.Models;
using Xunit;

namespace SalesApp.Tests
{
    public class AdminDeletedContractsTests
    {
        private readonly AppDbContext _context;
        private readonly AdminToolsController _controller;

        public AdminDeletedContractsTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _controller = new AdminToolsController(_context);

            SetupUser("superadmin@salesapp.com");
        }

        private void SetupUser(string email, string? permission = null)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Email, email),
            };

            if (!string.IsNullOrEmpty(permission))
            {
                claims.Add(new Claim("perm", permission));
            }

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task GetDeletedContracts_ReturnsOnlyInactiveContracts()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            _context.Contracts.AddRange(
                new Contract { Id = 1, ContractNumber = "ACT-001", IsActive = true, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 2, ContractNumber = "DEL-001", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 3, ContractNumber = "DEL-002", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetDeletedContracts();

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>().Subject;

            response.Success.Should().BeTrue();
            response.Data.Should().NotBeNull();
            response.Data!.TotalCount.Should().Be(2);
            response.Data.Items.Should().HaveCount(2);
            response.Data.Items.Select(c => c.ContractNumber).Should().Contain(new[] { "DEL-001", "DEL-002" });
            response.Data.Items.Select(c => c.ContractNumber).Should().NotContain("ACT-001");
        }

        [Fact]
        public async Task GetDeletedContracts_FiltersByContractNumber_ExactMatch()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            _context.Contracts.AddRange(
                new Contract { Id = 1, ContractNumber = "12345", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 2, ContractNumber = "123456", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 3, ContractNumber = "99999", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            // Act - exact match
            var result = await _controller.GetDeletedContracts(contractNumber: "12345", exactMatch: true);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>().Subject;

            response.Data!.TotalCount.Should().Be(1);
            response.Data.Items.Single().ContractNumber.Should().Be("12345");
        }

        [Fact]
        public async Task GetDeletedContracts_FiltersByContractNumber_PartialMatch()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            _context.Contracts.AddRange(
                new Contract { Id = 1, ContractNumber = "ABC-123", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 2, ContractNumber = "XYZ-123-99", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow },
                new Contract { Id = 3, ContractNumber = "ABC-999", IsActive = false, ContractStatusId = 1, ContractStatus = status, SaleStartDate = DateTime.UtcNow }
            );
            await _context.SaveChangesAsync();

            // Act - partial match
            var result = await _controller.GetDeletedContracts(contractNumber: "123", exactMatch: false);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>().Subject;

            response.Data!.TotalCount.Should().Be(2);
            response.Data.Items.Select(c => c.ContractNumber).Should().Contain(new[] { "ABC-123", "XYZ-123-99" });
        }

        [Fact]
        public async Task GetDeletedContracts_FiltersByTeam()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            var teamA = new Team { Id = 10, Name = "Team Alfa" };
            var teamB = new Team { Id = 20, Name = "Team Beta" };
            _context.Teams.AddRange(teamA, teamB);

            var userAlfa = new User { Id = Guid.NewGuid(), InternalId = 100, Name = "User Alfa", Email = "alfa@test.com" };
            var userBeta = new User { Id = Guid.NewGuid(), InternalId = 200, Name = "User Beta", Email = "beta@test.com" };
            _context.Users.AddRange(userAlfa, userBeta);

            var saleDate = new DateTime(2026, 1, 15);

            _context.UserTeams.AddRange(
                new UserTeam { Id = 1, TeamId = 10, UserInternalId = 100, StartDate = new DateTime(2026, 1, 1), EndDate = null, Team = teamA },
                new UserTeam { Id = 2, TeamId = 20, UserInternalId = 200, StartDate = new DateTime(2026, 1, 1), EndDate = null, Team = teamB }
            );

            _context.Contracts.AddRange(
                new Contract { Id = 1, ContractNumber = "CTR-ALFA", IsActive = false, UserInternalId = 100, ContractStatusId = 1, ContractStatus = status, SaleStartDate = saleDate },
                new Contract { Id = 2, ContractNumber = "CTR-BETA", IsActive = false, UserInternalId = 200, ContractStatusId = 1, ContractStatus = status, SaleStartDate = saleDate }
            );
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.GetDeletedContracts(teamId: 10);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<PagedContractResponse>>().Subject;

            response.Data!.TotalCount.Should().Be(1);
            response.Data.Items.Single().ContractNumber.Should().Be("CTR-ALFA");
            response.Data.Items.Single().TeamName.Should().Be("Team Alfa");
        }

        [Fact]
        public async Task GetDeletedContracts_PaginationWorksCorrectly()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            for (int i = 1; i <= 60; i++)
            {
                _context.Contracts.Add(new Contract
                {
                    Id = i,
                    ContractNumber = $"CTR-{i:D3}",
                    IsActive = false,
                    ContractStatusId = 1,
                    ContractStatus = status,
                    SaleStartDate = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow.AddMinutes(i)
                });
            }
            await _context.SaveChangesAsync();

            // Act - Page 1
            var page1Result = await _controller.GetDeletedContracts(page: 1, pageSize: 50);
            // Act - Page 2
            var page2Result = await _controller.GetDeletedContracts(page: 2, pageSize: 50);

            // Assert
            var p1 = ((OkObjectResult)page1Result.Result!).Value as ApiResponse<PagedContractResponse>;
            p1!.Data!.TotalCount.Should().Be(60);
            p1.Data.Items.Should().HaveCount(50);
            p1.Data.Page.Should().Be(1);

            var p2 = ((OkObjectResult)page2Result.Result!).Value as ApiResponse<PagedContractResponse>;
            p2!.Data!.TotalCount.Should().Be(60);
            p2.Data.Items.Should().HaveCount(10);
            p2.Data.Page.Should().Be(2);
        }

        [Fact]
        public async Task GetDeletedContracts_NonSuperadmin_ReturnsForbid()
        {
            // Arrange
            SetupUser("regular@user.com");

            // Act
            var result = await _controller.GetDeletedContracts();

            // Assert
            result.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task RestoreDeletedContract_Success()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            var contract = new Contract
            {
                Id = 10,
                ContractNumber = "CTR-TO-RESTORE",
                IsActive = false,
                ContractStatusId = 1,
                ContractStatus = status,
                SaleStartDate = DateTime.UtcNow
            };
            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.RestoreDeletedContract(10);

            // Assert
            var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

            response.Success.Should().BeTrue();

            var updated = await _context.Contracts.FindAsync(10);
            updated!.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task RestoreDeletedContract_Conflict_WhenActiveDuplicateExists()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            // Existing active contract
            _context.Contracts.Add(new Contract
            {
                Id = 1,
                ContractNumber = "CTR-DUPLICATE",
                IsActive = true,
                ContractStatusId = 1,
                ContractStatus = status,
                SaleStartDate = DateTime.UtcNow
            });

            // Inactive contract with same number
            _context.Contracts.Add(new Contract
            {
                Id = 2,
                ContractNumber = "CTR-DUPLICATE",
                IsActive = false,
                ContractStatusId = 1,
                ContractStatus = status,
                SaleStartDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.RestoreDeletedContract(2);

            // Assert
            var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
            var response = conflictResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;

            response.Success.Should().BeFalse();
            response.Message.Should().Contain("já existe um contrato ativo com o número 'CTR-DUPLICATE'");

            // Verify contract remained inactive
            var contract2 = await _context.Contracts.FindAsync(2);
            contract2!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task RestoreDeletedContract_NotFound_WhenContractDoesNotExist()
        {
            // Act
            var result = await _controller.RestoreDeletedContract(999);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task RestoreDeletedContract_AlreadyActive_ReturnsBadRequest()
        {
            // Arrange
            var status = new ContractStatusEntity { Id = 1, Name = "Active" };
            _context.ContractStatuses.Add(status);

            _context.Contracts.Add(new Contract
            {
                Id = 1,
                ContractNumber = "ALREADY-ACTIVE",
                IsActive = true,
                ContractStatusId = 1,
                ContractStatus = status,
                SaleStartDate = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            // Act
            var result = await _controller.RestoreDeletedContract(1);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }
    }
}
