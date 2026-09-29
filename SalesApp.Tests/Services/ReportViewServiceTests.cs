using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.ReportViews.DTOs;
using SalesApp.ReportViews.Models;
using SalesApp.ReportViews.Repositories;
using SalesApp.ReportViews.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class ReportViewServiceTests
    {
        private readonly Mock<IReportViewRepository> _repositoryMock;
        private readonly Mock<IUserRepository> _userRepositoryMock;
        private readonly ReportViewService _service;

        public ReportViewServiceTests()
        {
            _repositoryMock = new Mock<IReportViewRepository>();
            _userRepositoryMock = new Mock<IUserRepository>();

            _service = new ReportViewService(
                _repositoryMock.Object,
                _userRepositoryMock.Object
            );
        }

        [Fact]
        public async Task ListAsync_WhenMasterSuperAdmin_ShouldCallListAllAsync()
        {
            // Arrange
            var masterUserId = Guid.NewGuid();
            var masterUser = new User
            {
                Id = masterUserId,
                Email = "superadmin@salesapp.com",
                Role = new Role { Name = "superadmin" }
            };
            _userRepositoryMock.Setup(u => u.GetByIdAsync(masterUserId)).ReturnsAsync(masterUser);

            var allViews = new List<ReportView>
            {
                new ReportView { ViewId = "view-1", UserId = Guid.NewGuid().ToString(), Name = "Other User Private View", Scope = "private" },
                new ReportView { ViewId = "view-2", UserId = masterUserId.ToString(), Name = "My View", Scope = "shared" }
            };
            _repositoryMock.Setup(r => r.ListAllAsync()).ReturnsAsync(allViews);

            // Act
            var result = await _service.ListAsync(masterUserId.ToString());

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().HaveCount(2);
            _repositoryMock.Verify(r => r.ListAllAsync(), Times.Once);
            _repositoryMock.Verify(r => r.ListForUserAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetAsync_WhenMasterSuperAdminAndNotFoundInPartition_ShouldCallGetAnyByIdAsync()
        {
            // Arrange
            var masterUserId = Guid.NewGuid();
            var masterUser = new User
            {
                Id = masterUserId,
                Email = "superadmin@salesapp.com",
                Role = new Role { Name = "superadmin" }
            };
            _userRepositoryMock.Setup(u => u.GetByIdAsync(masterUserId)).ReturnsAsync(masterUser);

            var otherOwnerId = Guid.NewGuid().ToString();
            var otherView = new ReportView { ViewId = "view-other", UserId = otherOwnerId, Name = "Other Private View", Scope = "private" };

            _repositoryMock.Setup(r => r.GetByIdAsync(masterUserId.ToString(), "view-other")).ReturnsAsync((ReportView?)null);
            _repositoryMock.Setup(r => r.GetAnyByIdAsync("view-other")).ReturnsAsync(otherView);

            // Act
            var result = await _service.GetAsync(masterUserId.ToString(), "view-other");

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.ViewId.Should().Be("view-other");
            _repositoryMock.Verify(r => r.GetAnyByIdAsync("view-other"), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenMasterSuperAdmin_ShouldAllowUpdatingOtherUserView()
        {
            // Arrange
            var masterUserId = Guid.NewGuid();
            var masterUser = new User
            {
                Id = masterUserId,
                Email = "superadmin@salesapp.com",
                Role = new Role { Name = "superadmin" }
            };
            _userRepositoryMock.Setup(u => u.GetByIdAsync(masterUserId)).ReturnsAsync(masterUser);

            var otherOwnerId = Guid.NewGuid().ToString();
            var existingView = new ReportView
            {
                ViewId = "view-other",
                UserId = otherOwnerId,
                Name = "Original View Name",
                Scope = "private"
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(masterUserId.ToString(), "view-other")).ReturnsAsync((ReportView?)null);
            _repositoryMock.Setup(r => r.GetAnyByIdAsync("view-other")).ReturnsAsync(existingView);

            var updateRequest = new UpdateReportViewRequest
            {
                Name = "Updated View Name by Master",
                Scope = "private",
                Rows = new List<ViewRow>()
            };

            // Act
            var result = await _service.UpdateAsync(masterUserId.ToString(), "view-other", updateRequest);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Name.Should().Be("Updated View Name by Master");
            _repositoryMock.Verify(r => r.UpdateAsync(It.Is<ReportView>(v => v.UserId == otherOwnerId && v.Name == "Updated View Name by Master")), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenRegularSuperAdmin_ShouldForbidUpdatingOtherUserView()
        {
            // Arrange
            var regularAdminId = Guid.NewGuid();
            var regularAdmin = new User
            {
                Id = regularAdminId,
                Email = "otheradmin@salesapp.com",
                Role = new Role { Name = "superadmin" }
            };
            _userRepositoryMock.Setup(u => u.GetByIdAsync(regularAdminId)).ReturnsAsync(regularAdmin);

            var otherOwnerId = Guid.NewGuid().ToString();
            var existingView = new ReportView
            {
                ViewId = "view-other",
                UserId = otherOwnerId,
                Name = "Original View Name",
                Scope = "shared"
            };

            _repositoryMock.Setup(r => r.GetByIdAsync(regularAdminId.ToString(), "view-other")).ReturnsAsync(existingView);

            var updateRequest = new UpdateReportViewRequest
            {
                Name = "Unauthorized Edit",
                Scope = "shared",
                Rows = new List<ViewRow>()
            };

            // Act
            var result = await _service.UpdateAsync(regularAdminId.ToString(), "view-other", updateRequest);

            // Assert
            result.Success.Should().BeFalse();
            result.StatusCode.Should().Be(403);
            _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<ReportView>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsync_WhenMasterSuperAdmin_ShouldDeleteWithOriginalOwnerUserId()
        {
            // Arrange
            var masterUserId = Guid.NewGuid();
            var masterUser = new User
            {
                Id = masterUserId,
                Email = "superadmin@salesapp.com",
                Role = new Role { Name = "superadmin" }
            };
            _userRepositoryMock.Setup(u => u.GetByIdAsync(masterUserId)).ReturnsAsync(masterUser);

            var otherOwnerId = Guid.NewGuid().ToString();
            var otherView = new ReportView { ViewId = "view-other", UserId = otherOwnerId, Name = "Other Private View", Scope = "private" };

            _repositoryMock.Setup(r => r.GetByIdAsync(masterUserId.ToString(), "view-other")).ReturnsAsync((ReportView?)null);
            _repositoryMock.Setup(r => r.GetAnyByIdAsync("view-other")).ReturnsAsync(otherView);

            // Act
            var result = await _service.DeleteAsync(masterUserId.ToString(), "view-other");

            // Assert
            result.Success.Should().BeTrue();
            _repositoryMock.Verify(r => r.DeleteAsync(otherOwnerId, "view-other"), Times.Once);
        }
    }
}
