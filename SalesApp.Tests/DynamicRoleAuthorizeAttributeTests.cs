using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SalesApp.Attributes;
using SalesApp.Services;
using System.Security.Claims;

namespace SalesApp.Tests
{
    public class DynamicRoleAuthorizeAttributeTests
    {
        [Fact]
        public async Task OnAuthorizationAsync_UserWithValidRole_ShouldAllowAccess()
        {
            // Arrange
            var mockAuthService = new Mock<IDynamicRoleAuthorizationService>();
            mockAuthService.Setup(s => s.HasPermissionAsync("admin", new[] { "admin", "superadmin" }))
                .ReturnsAsync(true);

            var services = new ServiceCollection();
            services.AddSingleton(mockAuthService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = serviceProvider;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "admin")
            }, "test"));

            var context = new AuthorizationFilterContext(
                new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                new List<IFilterMetadata>()
            );

            var attribute = new DynamicRoleAuthorizeAttribute("admin", "superadmin");

            // Act
            await attribute.OnAuthorizationAsync(context);

            // Assert
            context.Result.Should().BeNull();
        }

        [Fact]
        public async Task OnAuthorizationAsync_UserWithInvalidRole_ShouldForbid()
        {
            // Arrange
            var mockAuthService = new Mock<IDynamicRoleAuthorizationService>();
            mockAuthService.Setup(s => s.HasPermissionAsync("user", new[] { "admin", "superadmin" }))
                .ReturnsAsync(false);

            var services = new ServiceCollection();
            services.AddSingleton(mockAuthService.Object);
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = serviceProvider;
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Role, "user")
            }, "test"));

            var context = new AuthorizationFilterContext(
                new ActionContext(httpContext, new Microsoft.AspNetCore.Routing.RouteData(), new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor()),
                new List<IFilterMetadata>()
            );

            var attribute = new DynamicRoleAuthorizeAttribute("admin", "superadmin");

            // Act
            await attribute.OnAuthorizationAsync(context);

            // Assert
            context.Result.Should().BeOfType<ForbidResult>();
        }
    }
}