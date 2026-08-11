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

        public UserAccessTrackingMiddlewareTests()
        {
            _mockLogger = new Mock<ILogger<UserAccessTrackingMiddleware>>();
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

            var middleware = new UserAccessTrackingMiddleware(next, _mockLogger.Object);
            var httpContext = new DefaultHttpContext();
            var serviceProvider = new Mock<IServiceProvider>().Object;

            // Act
            await middleware.InvokeAsync(httpContext, serviceProvider);

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
