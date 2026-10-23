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
    public class AdminCheckContractsTests
    {
        private readonly AppDbContext _context;
        private readonly AdminToolsController _controller;

        public AdminCheckContractsTests()
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
        public async Task CheckContracts_ForbiddenForNonSuperAdmin()
        {
            SetupUser("seller@salesapp.com");

            var request = new AdminCheckContractsRequest
            {
                ContractNumbers = new List<string> { "CTR-001" }
            };

            var result = await _controller.CheckContracts(request);
            result.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task CheckContracts_ReturnsCorrectResults_ForActiveInactiveAndMissingContracts()
        {
            // Seed User
            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = "Maria Silva",
                Email = "maria@test.com",
                PasswordHash = "hash",
                RoleId = 3
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Seed active contract with assigned user
            var activeContract = new Contract
            {
                ContractNumber = "CTR-100",
                UserInternalId = user.InternalId,
                TotalAmount = 50000m,
                SaleStartDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                ContractStatusId = 1
            };

            // Seed deleted/inactive contract without assigned user
            var deletedContract = new Contract
            {
                ContractNumber = "CTR-200",
                UserInternalId = null,
                TotalAmount = 75000m,
                SaleStartDate = new DateTime(2026, 2, 20, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false,
                ContractStatusId = 1
            };

            _context.Contracts.AddRange(activeContract, deletedContract);
            await _context.SaveChangesAsync();

            // Request with active, deleted, missing, and duplicate contracts
            var request = new AdminCheckContractsRequest
            {
                ContractNumbers = new List<string>
                {
                    "CTR-100",
                    "CTR-200",
                    "CTR-999", // Not in DB
                    "CTR-100", // Duplicate in input
                    "  CTR-200  ", // Duplicate with whitespace
                    ""
                }
            };

            var actionResult = await _controller.CheckContracts(request);
            var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
            var response = okResult.Value.Should().BeOfType<ApiResponse<AdminCheckContractsResponse>>().Subject;

            response.Success.Should().BeTrue();
            var data = response.Data;
            data.Should().NotBeNull();

            // Verification of summary counts
            data.TotalInput.Should().Be(5);
            data.UniqueInput.Should().Be(3);
            data.FoundCount.Should().Be(2);
            data.NotFoundCount.Should().Be(1);
            data.DeletedCount.Should().Be(1);

            // Verification of duplicates in input
            data.DuplicatesInList.Should().Contain(new[] { "CTR-100", "CTR-200" });

            // Verification of CTR-100 (Active, assigned)
            var item100 = data.Results.FirstOrDefault(r => r.ContractNumber == "CTR-100");
            item100.Should().NotBeNull();
            item100!.ExistsInSystem.Should().BeTrue();
            item100.IsDeleted.Should().BeFalse();
            item100.AssignedTo.Should().Be("Maria Silva");
            item100.TotalAmount.Should().Be(50000m);
            item100.SaleStartDate.Should().Be(new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));

            // Verification of CTR-200 (Deleted/Inactive, not assigned)
            var item200 = data.Results.FirstOrDefault(r => r.ContractNumber == "CTR-200");
            item200.Should().NotBeNull();
            item200!.ExistsInSystem.Should().BeTrue();
            item200.IsDeleted.Should().BeTrue();
            item200.AssignedTo.Should().Be("Não atribuído");
            item200.TotalAmount.Should().Be(75000m);

            // Verification of CTR-999 (Not in DB)
            var item999 = data.Results.FirstOrDefault(r => r.ContractNumber == "CTR-999");
            item999.Should().NotBeNull();
            item999!.ExistsInSystem.Should().BeFalse();
            item999.IsDeleted.Should().BeFalse();
            item999.AssignedTo.Should().BeNull();
            item999.TotalAmount.Should().BeNull();
            item999.SaleStartDate.Should().BeNull();
        }

        [Fact]
        public async Task ExportCheckContractsXlsx_ReturnsFileResult()
        {
            var request = new AdminCheckContractsRequest
            {
                ContractNumbers = new List<string> { "CTR-1", "CTR-2", "CTR-1" }
            };

            var actionResult = await _controller.ExportCheckContractsXlsx(request);
            var fileResult = actionResult.Should().BeOfType<FileContentResult>().Subject;

            fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            fileResult.FileDownloadName.Should().StartWith("conferencia_contratos_");
            fileResult.FileContents.Length.Should().BeGreaterThan(0);
        }
    }
}
