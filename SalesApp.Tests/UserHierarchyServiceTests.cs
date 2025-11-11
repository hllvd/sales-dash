using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests
{
    public class UserHierarchyServiceTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly UserHierarchyService _service;

        public UserHierarchyServiceTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _service = new UserHierarchyService(_mockUserRepository.Object);
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_ValidParent_ReturnsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            _mockUserRepository.Setup(x => x.GetByIdAsync(parentId)).ReturnsAsync(new User { Id = parentId });
            _mockUserRepository.Setup(x => x.WouldCreateCycleAsync(userId, parentId)).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.GetRootUserAsync()).ReturnsAsync(new User { Id = Guid.NewGuid() });

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, parentId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_ParentNotFound_ReturnsError()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            _mockUserRepository.Setup(x => x.GetByIdAsync(parentId)).ReturnsAsync((User?)null);

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, parentId);

            // Assert
            result.Should().Be("Parent user does not exist or is inactive");
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_WouldCreateCycle_ReturnsError()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            _mockUserRepository.Setup(x => x.GetByIdAsync(parentId)).ReturnsAsync(new User { Id = parentId });
            _mockUserRepository.Setup(x => x.WouldCreateCycleAsync(userId, parentId)).ReturnsAsync(true);

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, parentId);

            // Assert
            result.Should().Be("This change would create a circular reference in the hierarchy");
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_SelfAsParent_ReturnsError()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, userId);

            // Assert
            result.Should().Be("A user cannot be their own parent");
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_NoRootUserAndNullParent_ReturnsNull()
        {
            // Arrange
            var userId = Guid.NewGuid();

            _mockUserRepository.Setup(x => x.GetRootUserAsync()).ReturnsAsync((User?)null);

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, null);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task ValidateHierarchyChangeAsync_HasRootUserAndNullParent_ReturnsError()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var existingRoot = new User { Id = Guid.NewGuid(), Name = "Existing Root" };

            _mockUserRepository.Setup(x => x.GetRootUserAsync()).ReturnsAsync(existingRoot);

            // Act
            var result = await _service.ValidateHierarchyChangeAsync(userId, null);

            // Assert
            result.Should().Be("Only one root user is allowed in the system");
        }

        [Fact]
        public async Task GetParentAsync_ValidUserId_ReturnsParent()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var parent = new User { Id = Guid.NewGuid(), Name = "Parent" };

            _mockUserRepository.Setup(x => x.GetParentAsync(userId)).ReturnsAsync(parent);

            // Act
            var result = await _service.GetParentAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Parent");
        }

        [Fact]
        public async Task GetChildrenAsync_ValidUserId_ReturnsChildren()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var children = new List<User>
            {
                new User { Id = Guid.NewGuid(), Name = "Child 1" },
                new User { Id = Guid.NewGuid(), Name = "Child 2" }
            };

            _mockUserRepository.Setup(x => x.GetChildrenAsync(userId)).ReturnsAsync(children);

            // Act
            var result = await _service.GetChildrenAsync(userId);

            // Assert
            result.Should().HaveCount(2);
            result[0].Name.Should().Be("Child 1");
            result[1].Name.Should().Be("Child 2");
        }

        [Fact]
        public async Task GetTreeAsync_ValidUserId_ReturnsTree()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var treeUsers = new List<User>
            {
                new User { Id = userId, Name = "Root", Level = 0 },
                new User { Id = Guid.NewGuid(), Name = "Child 1", Level = 1 },
                new User { Id = Guid.NewGuid(), Name = "Grandchild", Level = 2 }
            };

            _mockUserRepository.Setup(x => x.GetTreeAsync(userId, -1)).ReturnsAsync(treeUsers);

            // Act
            var result = await _service.GetTreeAsync(userId, -1);

            // Assert
            result.Should().HaveCount(3);
            result.Should().Contain(u => u.Name == "Root" && u.Level == 0);
            result.Should().Contain(u => u.Name == "Child 1" && u.Level == 1);
            result.Should().Contain(u => u.Name == "Grandchild" && u.Level == 2);
        }

        [Fact]
        public async Task GetTreeAsync_WithDepthLimit_ReturnsLimitedTree()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var treeUsers = new List<User>
            {
                new User { Id = userId, Name = "Root", Level = 0 },
                new User { Id = Guid.NewGuid(), Name = "Child 1", Level = 1 }
            };

            _mockUserRepository.Setup(x => x.GetTreeAsync(userId, 1)).ReturnsAsync(treeUsers);

            // Act
            var result = await _service.GetTreeAsync(userId, 1);

            // Assert
            result.Should().HaveCount(2);
            result.Max(u => u.Level).Should().Be(1);
        }

        [Fact]
        public async Task GetLevelAsync_ValidUserId_ReturnsLevel()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var expectedLevel = 3;

            _mockUserRepository.Setup(x => x.GetLevelAsync(userId)).ReturnsAsync(expectedLevel);

            // Act
            var result = await _service.GetLevelAsync(userId);

            // Assert
            result.Should().Be(expectedLevel);
        }

        [Fact]
        public async Task GetRootUserAsync_RootExists_ReturnsRootUser()
        {
            // Arrange
            var rootUser = new User 
            { 
                Id = Guid.NewGuid(), 
                Name = "Root User", 
                Level = 0,
                ParentUserId = null 
            };

            _mockUserRepository.Setup(x => x.GetRootUserAsync()).ReturnsAsync(rootUser);

            // Act
            var result = await _service.GetRootUserAsync();

            // Assert
            result.Should().NotBeNull();
            result!.Name.Should().Be("Root User");
            result.Level.Should().Be(0);
            result.ParentUserId.Should().BeNull();
        }

        [Fact]
        public async Task GetRootUserAsync_NoRoot_ReturnsNull()
        {
            // Arrange
            _mockUserRepository.Setup(x => x.GetRootUserAsync()).ReturnsAsync((User?)null);

            // Act
            var result = await _service.GetRootUserAsync();

            // Assert
            result.Should().BeNull();
        }
    }
}