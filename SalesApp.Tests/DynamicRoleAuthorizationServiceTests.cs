using Xunit;
using Moq;
using FluentAssertions;
using SalesApp.Services;
using SalesApp.Repositories;
using SalesApp.Models;

namespace SalesApp.Tests
{
    public class DynamicRoleAuthorizationServiceTests
    {
        private readonly Mock<IRoleRepository> _mockRoleRepository;
        private readonly DynamicRoleAuthorizationService _service;

        public DynamicRoleAuthorizationServiceTests()
        {
            _mockRoleRepository = new Mock<IRoleRepository>();
            _service = new DynamicRoleAuthorizationService(_mockRoleRepository.Object);
        }

        [Fact]
        public async Task HasPermissionAsync_UserWithAdminRole_ShouldReturnTrue()
        {
            // Arrange
            var userRoleName = "admin";
            var requiredRoles = new[] { "admin", "superadmin" };
            
            _mockRoleRepository.Setup(r => r.GetByNameAsync(userRoleName))
                .ReturnsAsync(new Role { Name = "admin", IsActive = true });

            // Act
            var result = await _service.HasPermissionAsync(userRoleName, requiredRoles);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task HasPermissionAsync_UserWithUserRole_ShouldReturnFalse()
        {
            // Arrange
            var userRoleName = "user";
            var requiredRoles = new[] { "admin", "superadmin" };
            
            _mockRoleRepository.Setup(r => r.GetByNameAsync(userRoleName))
                .ReturnsAsync(new Role { Name = "user", IsActive = true });

            // Act
            var result = await _service.HasPermissionAsync(userRoleName, requiredRoles);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasPermissionAsync_InactiveRole_ShouldReturnFalse()
        {
            // Arrange
            var userRoleName = "admin";
            var requiredRoles = new[] { "admin" };
            
            _mockRoleRepository.Setup(r => r.GetByNameAsync(userRoleName))
                .ReturnsAsync(new Role { Name = "admin", IsActive = false });

            // Act
            var result = await _service.HasPermissionAsync(userRoleName, requiredRoles);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task HasPermissionAsync_NonExistentRole_ShouldReturnFalse()
        {
            // Arrange
            var userRoleName = "invalid";
            var requiredRoles = new[] { "admin" };
            
            _mockRoleRepository.Setup(r => r.GetByNameAsync(userRoleName))
                .ReturnsAsync((Role?)null);

            // Act
            var result = await _service.HasPermissionAsync(userRoleName, requiredRoles);

            // Assert
            result.Should().BeFalse();
        }
    }
}