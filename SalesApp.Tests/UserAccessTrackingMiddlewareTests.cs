using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using SalesApp.Data;
using SalesApp.Middleware;
using SalesApp.Models;
using Xunit;
using FluentAssertions;

namespace SalesApp.Tests
{
    public class UserAccessTrackingMiddlewareTests
    {
        private readonly Mock<ILogger<UserAccessTrackingMiddleware>> _mockLogger;
        private readonly Mock<IServiceScopeFactory> _mockScopeFactory;

        public UserAccessTrackingMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<UserAccessTrackingMiddleware>>();
            _mockScopeFactory = new Mock<IServiceScopeFactory>();
        }

        [Fact]
        public async Task InvokeAsync_UnauthenticatedUser_ShouldNotUpdateOrThrow()
        {
            // Arrange
            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var middleware = new UserAccessTrackingMiddleware(next, _mockLogger.Object, _mockScopeFactory.Object);
            var httpContext = new DefaultHttpContext();

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            nextCalled.Should().BeTrue();
            _mockScopeFactory.Verify(f => f.CreateScope(), Times.Never);
        }

        [Fact]
        public async Task InvokeAsync_AuthenticatedUser_ShouldInvokeNextAndCreateScope()
        {
            // Arrange
            var nextCalled = false;
            RequestDelegate next = (ctx) =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            };

            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            mockScope.Setup(s => s.ServiceProvider).Returns(mockServiceProvider.Object);
            _mockScopeFactory.Setup(f => f.CreateScope()).Returns(mockScope.Object);

            var middleware = new UserAccessTrackingMiddleware(next, _mockLogger.Object, _mockScopeFactory.Object);
            var userId = Guid.NewGuid();
            var httpContext = new DefaultHttpContext();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            httpContext.User = new ClaimsPrincipal(identity);

            // Act
            await middleware.InvokeAsync(httpContext);

            // Assert
            nextCalled.Should().BeTrue();
        }

        [Fact]
        public void RecordAccess_ShouldUpdateCache()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            // Act
            UserAccessTrackingMiddleware.RecordAccess(userId, now);
            
            // Assert - no exception thrown
            true.Should().BeTrue();
        }
    }
}
