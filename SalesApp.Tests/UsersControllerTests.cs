using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using System.Security.Claims;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests
{
    public class UsersControllerTests
    {
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IJwtService> _mockJwtService;
        private readonly Mock<IUserHierarchyService> _mockHierarchyService;
        private readonly Mock<IContractRepository> _mockContractRepository;
        private readonly UsersController _controller;

        public UsersControllerTests()
        {
            _mockUserRepository = new Mock<IUserRepository>();
            _mockJwtService = new Mock<IJwtService>();
            _mockHierarchyService = new Mock<IUserHierarchyService>();
            _mockContractRepository = new Mock<IContractRepository>();
            _controller = new UsersController(
                _mockUserRepository.Object, 
                _mockJwtService.Object, 
                _mockHierarchyService.Object,
                _mockContractRepository.Object);
        }

        private void SetupUser(string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task Register_ValidRequest_ReturnsOkResult()
        {
            // Arrange
            SetupUser(Guid.NewGuid().ToString(), "admin");
            var request = new RegisterRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = "password123",
                Role = "user"
            };

            _mockUserRepository.Setup(x => x.EmailExistsAsync(request.Email, null)).ReturnsAsync(false);
            _mockHierarchyService.Setup(x => x.ValidateHierarchyChangeAsync(It.IsAny<Guid>(), request.ParentUserId))
                .ReturnsAsync((string?)null);
            _mockUserRepository.Setup(x => x.CreateAsync(It.IsAny<User>())).ReturnsAsync(It.IsAny<User>());

            // Act
            var result = await _controller.Register(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeTrue();
            response.Data!.Name.Should().Be(request.Name);
            response.Data.Email.Should().Be(request.Email);
        }

        [Fact]
        public async Task Register_InvalidRole_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(Guid.NewGuid().ToString(), "admin");
            var request = new RegisterRequest
            {
                Name = "Test User",
                Email = "test@example.com",
                Password = "password123",
                Role = "invalid_role"
            };

            // Act
            var result = await _controller.Register(request);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badResult = result.Result as BadRequestObjectResult;
            var response = badResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Contain("Invalid role");
        }

        [Fact]
        public async Task Register_EmailExists_ReturnsBadRequest()
        {
            // Arrange
            SetupUser(Guid.NewGuid().ToString(), "admin");
            var request = new RegisterRequest
            {
                Name = "Test User",
                Email = "existing@example.com",
                Password = "password123",
                Role = "user"
            };

            _mockUserRepository.Setup(x => x.EmailExistsAsync(request.Email, null)).ReturnsAsync(true);

            // Act
            var result = await _controller.Register(request);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badResult = result.Result as BadRequestObjectResult;
            var response = badResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("Email already exists");
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkWithToken()
        {
            // Arrange
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "test@example.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                Name = "Test User",
                Role = new Role { Name = "user" }
            };

            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "password123"
            };

            _mockUserRepository.Setup(x => x.GetByEmailAsync(request.Email)).ReturnsAsync(user);
            _mockJwtService.Setup(x => x.GenerateToken(user)).Returns("test_token");

            // Act
            var result = await _controller.Login(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<LoginResponse>;
            response!.Success.Should().BeTrue();
            response.Data!.Token.Should().Be("test_token");
            response.Data.User.Email.Should().Be(user.Email);
        }

        [Fact]
        public async Task Login_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var request = new LoginRequest
            {
                Email = "test@example.com",
                Password = "wrong_password"
            };

            _mockUserRepository.Setup(x => x.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);

            // Act
            var result = await _controller.Login(request);

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
            var unauthorizedResult = result.Result as UnauthorizedObjectResult;
            var response = unauthorizedResult!.Value as ApiResponse<LoginResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("Invalid credentials");
        }

        [Fact]
        public async Task GetUser_AdminRole_ReturnsUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            var user = new User
            {
                Id = targetUserId,
                Name = "Target User",
                Email = "target@example.com",
                Role = new Role { Name = "user" }
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(targetUserId)).ReturnsAsync(user);

            // Act
            var result = await _controller.GetUser(targetUserId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeTrue();
            response.Data!.Id.Should().Be(targetUserId);
        }

        [Fact]
        public async Task GetUser_NonAdminAccessingOtherUser_ReturnsForbid()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            SetupUser(userId.ToString(), "user");

            // Act
            var result = await _controller.GetUser(targetUserId);

            // Assert
            result.Result.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task GetUser_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync((User?)null);

            // Act
            var result = await _controller.GetUser(userId);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
            var notFoundResult = result.Result as NotFoundObjectResult;
            var response = notFoundResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("User not found");
        }

        [Fact]
        public async Task UpdateUser_ValidRequest_ReturnsUpdatedUser()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            var existingUser = new User
            {
                Id = userId,
                Name = "Old Name",
                Email = "old@example.com",
                Role = new Role { Name = "user" }
            };

            var updateRequest = new UpdateUserRequest
            {
                Name = "New Name",
                Email = "new@example.com"
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockUserRepository.Setup(x => x.EmailExistsAsync(updateRequest.Email!, userId)).ReturnsAsync(false);
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(It.IsAny<User>());

            // Act
            var result = await _controller.UpdateUser(userId, updateRequest);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeTrue();
            response.Data!.Name.Should().Be(updateRequest.Name);
            response.Data.Email.Should().Be(updateRequest.Email);
        }

        [Fact]
        public async Task UpdateUser_EmailAlreadyExists_ReturnsBadRequest()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            var existingUser = new User
            {
                Id = userId,
                Name = "Test User",
                Email = "test@example.com",
                Role = new Role { Name = "user" }
            };

            var updateRequest = new UpdateUserRequest
            {
                Email = "existing@example.com"
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockUserRepository.Setup(x => x.EmailExistsAsync(updateRequest.Email!, userId)).ReturnsAsync(true);

            // Act
            var result = await _controller.UpdateUser(userId, updateRequest);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badResult = result.Result as BadRequestObjectResult;
            var response = badResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("Email already exists");
        }

        [Fact]
        public async Task UpdateUser_HierarchyValidationFails_ReturnsBadRequest()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            var existingUser = new User
            {
                Id = userId,
                Name = "Test User",
                Email = "test@example.com",
                Role = new Role { Name = "user" }
            };

            var updateRequest = new UpdateUserRequest
            {
                ParentUserId = parentId
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(existingUser);
            _mockHierarchyService.Setup(x => x.ValidateHierarchyChangeAsync(userId, parentId))
                .ReturnsAsync("Circular reference detected");

            // Act
            var result = await _controller.UpdateUser(userId, updateRequest);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badResult = result.Result as BadRequestObjectResult;
            var response = badResult!.Value as ApiResponse<UserResponse>;
            response!.Success.Should().BeFalse();
            response.Message.Should().Be("Circular reference detected");
        }

        [Fact]
        public async Task DeleteUser_ValidRequest_SetsUserInactive()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "admin");

            var user = new User
            {
                Id = userId,
                Name = "Test User",
                Email = "test@example.com",
                IsActive = true
            };

            _mockUserRepository.Setup(x => x.GetByIdAsync(userId)).ReturnsAsync(user);
            _mockUserRepository.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(It.IsAny<User>());

            // Act
            var result = await _controller.DeleteUser(userId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<object>;
            response!.Success.Should().BeTrue();
            response.Message.Should().Be("User deleted successfully");
            
            _mockUserRepository.Verify(x => x.UpdateAsync(It.Is<User>(u => u.IsActive == false)), Times.Once);
        }

        [Fact]
        public async Task GetParent_ValidRequest_ReturnsParent()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "user");

            var parent = new User
            {
                Id = Guid.NewGuid(),
                Name = "Parent User",
                Email = "parent@example.com",
                Role = new Role { Name = "admin" }
            };

            _mockHierarchyService.Setup(x => x.GetParentAsync(userId)).ReturnsAsync(parent);

            // Act
            var result = await _controller.GetParent(userId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserHierarchyResponse?>;
            response!.Success.Should().BeTrue();
            response.Data.Should().NotBeNull();
            response.Data!.Name.Should().Be(parent.Name);
        }

        [Fact]
        public async Task GetChildren_ValidRequest_ReturnsChildren()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "user");

            var children = new List<User>
            {
                new User { Id = Guid.NewGuid(), Name = "Child 1", Email = "child1@example.com" },
                new User { Id = Guid.NewGuid(), Name = "Child 2", Email = "child2@example.com" }
            };

            _mockHierarchyService.Setup(x => x.GetChildrenAsync(userId)).ReturnsAsync(children);

            // Act
            var result = await _controller.GetChildren(userId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<List<UserHierarchyResponse>>;
            response!.Success.Should().BeTrue();
            response.Data!.Should().HaveCount(2);
            response.Data[0].Name.Should().Be("Child 1");
            response.Data[1].Name.Should().Be("Child 2");
        }

        [Fact]
        public async Task GetTree_ValidRequest_ReturnsTree()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "user");

            var treeUsers = new List<User>
            {
                new User { Id = userId, Name = "Root", Level = 0 },
                new User { Id = Guid.NewGuid(), Name = "Child 1", Level = 1 },
                new User { Id = Guid.NewGuid(), Name = "Child 2", Level = 1 }
            };

            _mockHierarchyService.Setup(x => x.GetTreeAsync(userId, -1)).ReturnsAsync(treeUsers);

            // Act
            var result = await _controller.GetTree(userId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserTreeResponse>;
            response!.Success.Should().BeTrue();
            response.Data!.Users.Should().HaveCount(3);
            response.Data.TotalUsers.Should().Be(3);
            response.Data.MaxDepth.Should().Be(1);
        }

        [Fact]
        public async Task GetLevel_ValidRequest_ReturnsLevel()
        {
            // Arrange
            var userId = Guid.NewGuid();
            SetupUser(userId.ToString(), "user");

            _mockHierarchyService.Setup(x => x.GetLevelAsync(userId)).ReturnsAsync(2);

            // Act
            var result = await _controller.GetLevel(userId);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<int>;
            response!.Success.Should().BeTrue();
            response.Data.Should().Be(2);
        }

        [Fact]
        public async Task GetRoot_ValidRequest_ReturnsRootUser()
        {
            // Arrange
            SetupUser(Guid.NewGuid().ToString(), "user");

            var rootUser = new User
            {
                Id = Guid.NewGuid(),
                Name = "Root User",
                Email = "root@example.com",
                Role = new Role { Name = "superadmin" },
                Level = 0
            };

            _mockHierarchyService.Setup(x => x.GetRootUserAsync()).ReturnsAsync(rootUser);

            // Act
            var result = await _controller.GetRoot();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<UserHierarchyResponse?>;
            response!.Success.Should().BeTrue();
            response.Data.Should().NotBeNull();
            response.Data!.Name.Should().Be(rootUser.Name);
            response.Data.Level.Should().Be(0);
        }

        [Fact]
        public async Task GetUsers_AdminRole_ReturnsPagedUsers()
        {
            // Arrange
            SetupUser(Guid.NewGuid().ToString(), "admin");

            var users = new List<User>
            {
                new User { Id = Guid.NewGuid(), Name = "User 1", Email = "user1@example.com" },
                new User { Id = Guid.NewGuid(), Name = "User 2", Email = "user2@example.com" }
            };

            _mockUserRepository.Setup(x => x.GetAllAsync(1, 10, It.IsAny<string?>())).ReturnsAsync((users, 2));

            // Act
            var result = await _controller.GetUsers();

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<PagedResponse<UserResponse>>;
            response!.Success.Should().BeTrue();
            response.Data!.Items.Should().HaveCount(2);
            response.Data.TotalCount.Should().Be(2);
            response.Data.Page.Should().Be(1);
            response.Data.PageSize.Should().Be(10);
        }
    }
}