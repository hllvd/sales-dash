using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class UserScopeServiceTests
    {
        private readonly AppDbContext _context;
        private readonly UserScopeService _service;

        public UserScopeServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _service = new UserScopeService(_context);
        }

        [Fact]
        public async Task GetContractScopeAsync_UnauthenticatedUser_ReturnsEmptyScope()
        {
            // Arrange
            var principal = new ClaimsPrincipal();

            // Act
            var scope = await _service.GetContractScopeAsync(principal);

            // Assert
            scope.Should().NotBeNull();
            scope.IsGlobal.Should().BeFalse();
            scope.AllowedUserIds.Should().BeEmpty();
            scope.AllowedMatriculas.Should().BeEmpty();
            scope.AdminLinkedMatriculas.Should().BeEmpty();
        }

        [Fact]
        public async Task GetContractScopeAsync_SuperAdmin_ReturnsGlobalScope()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("role_id", "1")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            // Act
            var scope = await _service.GetContractScopeAsync(principal);

            // Assert
            scope.Should().NotBeNull();
            scope.IsGlobal.Should().BeTrue();
            scope.AllowedUserIds.Should().BeEmpty();
            scope.AllowedMatriculas.Should().BeEmpty();
            scope.AdminLinkedMatriculas.Should().BeEmpty();
        }

        [Fact]
        public async Task GetContractScopeAsync_AdminWithHierarchyAndMatriculas_PopulatesCorrectly()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var subordinateId = Guid.NewGuid();
            var unrelatedUserId = Guid.NewGuid();

            var admin = new User { Id = adminId, InternalId = 1, Name = "Admin", Email = "admin@test.com", RoleId = 2, IsActive = true };
            var subordinate = new User { Id = subordinateId, InternalId = 2, Name = "Subordinate", Email = "sub@test.com", RoleId = 3, IsActive = true, ParentUserId = adminId };
            var unrelatedUser = new User { Id = unrelatedUserId, InternalId = 3, Name = "Other", Email = "other@test.com", RoleId = 2, IsActive = true };

            var matricula1 = new Matricula { Id = 1, MatriculaNumber = "MAT-001", Status = "active" };
            var matricula2 = new Matricula { Id = 2, MatriculaNumber = "MAT-002", Status = "active" };
            var matriculaExpired = new Matricula { Id = 3, MatriculaNumber = "MAT-EXP", Status = "active" };

            // Admin is linked to MAT-001 (as member) and MAT-EXP (expired)
            var umAdmin1 = new UserMatricula { Id = 1, UserInternalId = admin.InternalId, User = admin, MatriculaId = matricula1.Id, Matricula = matricula1, IsActive = true, IsOwner = false };
            var umAdminExp = new UserMatricula { Id = 2, UserInternalId = admin.InternalId, User = admin, MatriculaId = matriculaExpired.Id, Matricula = matriculaExpired, IsActive = true, EndDate = DateTime.UtcNow.AddDays(-1) };

            // Subordinate is linked to MAT-002
            var umSub = new UserMatricula { Id = 3, UserInternalId = subordinate.InternalId, User = subordinate, MatriculaId = matricula2.Id, Matricula = matricula2, IsActive = true, IsOwner = true };

            _context.Users.AddRange(admin, subordinate, unrelatedUser);
            _context.Matriculas.AddRange(matricula1, matricula2, matriculaExpired);
            _context.UserMatriculas.AddRange(umAdmin1, umAdminExp, umSub);
            await _context.SaveChangesAsync();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                new Claim("role_id", "2")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);

            // Act
            var scope = await _service.GetContractScopeAsync(principal);

            // Assert
            scope.IsGlobal.Should().BeFalse();
            scope.AllowedUserIds.Should().Contain(new[] { adminId, subordinateId });
            scope.AllowedUserIds.Should().NotContain(unrelatedUserId);

            // AllowedMatriculas contains matriculas of admin and subordinates (MAT-001, MAT-002)
            scope.AllowedMatriculas.Should().Contain("MAT-001");
            scope.AllowedMatriculas.Should().Contain("MAT-002");
            scope.AllowedMatriculas.Should().NotContain("MAT-EXP");

            // AdminLinkedMatriculas contains only active, non-expired matriculas linked directly to the admin (MAT-001)
            scope.AdminLinkedMatriculas.Should().Contain("MAT-001");
            scope.AdminLinkedMatriculas.Should().NotContain("MAT-002"); // Subordinate's matricula, not directly linked to admin
            scope.AdminLinkedMatriculas.Should().NotContain("MAT-EXP"); // Expired
        }
    }
}
