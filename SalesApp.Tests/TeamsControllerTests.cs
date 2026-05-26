using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FluentAssertions;
using Moq;
using SalesApp.Controllers;
using SalesApp.DTOs;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests
{
    public class TeamsControllerTests
    {
        private readonly Mock<ITeamRepository> _mockTeamRepository;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IMessageService> _mockMessageService;
        private readonly TeamsController _controller;

        public TeamsControllerTests()
        {
            _mockTeamRepository = new Mock<ITeamRepository>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockMessageService = new Mock<IMessageService>();

            _controller = new TeamsController(
                _mockTeamRepository.Object,
                _mockUserRepository.Object,
                _mockMessageService.Object
            );

            // Mock message service basic formatting
            _mockMessageService.Setup(m => m.Get(It.IsAny<AppMessage>()))
                .Returns((AppMessage msg) => msg.ToString());
            _mockMessageService.Setup(m => m.Get(It.IsAny<AppMessage>(), It.IsAny<object[]>()))
                .Returns((AppMessage msg, object[] args) => string.Format(msg.ToString(), args));

            SetupUser(Guid.NewGuid().ToString(), "superadmin");
        }

        private void SetupUser(string userId, string role)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role),
                new Claim("perm", "teams:manage")
            };

            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task CreateTeam_WithOverlappingMemberships_AutoClosesOtherTeamsAndAddsWarnings()
        {
            // Arrange
            var userGuid = Guid.NewGuid();
            var user = new User
            {
                Id = userGuid,
                Name = "John Doe",
                InternalId = 123
            };

            var request = new CreateTeamRequest
            {
                Name = "Alpha Team",
                Members = new List<TeamMemberRequest>
                {
                    new TeamMemberRequest { UserId = userGuid }
                }
            };

            var createdTeam = new Team
            {
                Id = 1,
                Name = "Alpha Team"
            };

            var otherTeam = new Team
            {
                Id = 2,
                Name = "Beta Team"
            };

            var existingOverlap = new UserTeam
            {
                Id = 10,
                TeamId = 2,
                Team = otherTeam,
                UserInternalId = 123,
                User = user,
                StartDate = DateTime.UtcNow.AddYears(-1),
                EndDate = null
            };

            _mockTeamRepository.Setup(x => x.NameExistsAsync(request.Name, null)).ReturnsAsync(false);
            _mockTeamRepository.Setup(x => x.CreateAsync(It.IsAny<Team>())).ReturnsAsync(createdTeam);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userGuid)).ReturnsAsync(user);

            // Mock finding the overlap on another team
            _mockTeamRepository.Setup(x => x.FindOverlappingMembershipsAsync(user.InternalId, It.IsAny<DateTime>(), null))
                .ReturnsAsync(new List<UserTeam> { existingOverlap });

            // Set up reloaded team response
            var reloadedTeam = new Team
            {
                Id = 1,
                Name = "Alpha Team",
                UserTeams = new List<UserTeam>
                {
                    new UserTeam { TeamId = 1, UserInternalId = 123, User = user, StartDate = DateTime.UtcNow.AddYears(-8) }
                }
            };
            _mockTeamRepository.Setup(x => x.GetByIdAsync(createdTeam.Id)).ReturnsAsync(reloadedTeam);

            // Act
            var result = await _controller.CreateTeam(request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<TeamResponse>;

            response!.Success.Should().BeTrue();
            response.Data!.Warnings.Should().HaveCount(1);
            response.Data.Warnings[0].Should().Contain("John Doe");
            response.Data.Warnings[0].Should().Contain("Beta Team");

            // Verify the overlap EndDate was set to UtcNow
            existingOverlap.EndDate.Should().NotBeNull();
            existingOverlap.EndDate.Value.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            _mockTeamRepository.Verify(x => x.UpdateAsync(otherTeam), Times.Once);
            _mockTeamRepository.Verify(x => x.AddMemberAsync(It.Is<UserTeam>(ut => ut.TeamId == 1 && ut.UserInternalId == 123)), Times.Once);
        }

        [Fact]
        public async Task SetOwner_UserIsNotMember_ReturnsBadRequest()
        {
            // Arrange
            var teamId = 1;
            var nonMemberGuid = Guid.NewGuid();
            var user = new User
            {
                Id = nonMemberGuid,
                Name = "Stranger",
                InternalId = 999
            };

            var team = new Team
            {
                Id = teamId,
                Name = "Alpha Team",
                UserTeams = new List<UserTeam>() // No members
            };

            _mockTeamRepository.Setup(x => x.GetByIdAsync(teamId)).ReturnsAsync(team);
            _mockUserRepository.Setup(x => x.GetByIdAsync(nonMemberGuid)).ReturnsAsync(user);

            // Act
            var result = await _controller.SetOwner(teamId, nonMemberGuid);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
            var badRequest = result.Result as BadRequestObjectResult;
            var response = badRequest!.Value as ApiResponse<TeamResponse>;

            response!.Success.Should().BeFalse();
            response.Message.Should().Contain("O proprietário deve ser um membro ativo da equipe.");
        }

        [Fact]
        public async Task AddMembers_AutoClosesOverlapsOnOtherTeams()
        {
            // Arrange
            var teamId = 1;
            var userGuid = Guid.NewGuid();
            var user = new User
            {
                Id = userGuid,
                Name = "Jane Doe",
                InternalId = 456
            };

            var team = new Team
            {
                Id = teamId,
                Name = "Alpha Team",
                UserTeams = new List<UserTeam>()
            };

            var otherTeam = new Team
            {
                Id = 2,
                Name = "Gamma Team"
            };

            var existingOverlap = new UserTeam
            {
                Id = 20,
                TeamId = 2,
                Team = otherTeam,
                UserInternalId = 456,
                User = user,
                StartDate = DateTime.UtcNow.AddYears(-2),
                EndDate = null
            };

            var request = new AddMembersRequest
            {
                Members = new List<TeamMemberRequest>
                {
                    new TeamMemberRequest { UserId = userGuid }
                }
            };

            _mockTeamRepository.Setup(x => x.GetByIdAsync(teamId)).ReturnsAsync(team);
            _mockUserRepository.Setup(x => x.GetByIdAsync(userGuid)).ReturnsAsync(user);
            _mockTeamRepository.Setup(x => x.FindOverlappingMembershipsAsync(456, It.IsAny<DateTime>(), null))
                .ReturnsAsync(new List<UserTeam> { existingOverlap });

            var reloadedTeam = new Team
            {
                Id = teamId,
                Name = "Alpha Team",
                UserTeams = new List<UserTeam>
                {
                    new UserTeam { TeamId = teamId, UserInternalId = 456, User = user, StartDate = DateTime.UtcNow.AddYears(-8) }
                }
            };
            _mockTeamRepository.Setup(x => x.GetByIdAsync(teamId)).ReturnsAsync(reloadedTeam);

            // Act
            var result = await _controller.AddMembers(teamId, request);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = result.Result as OkObjectResult;
            var response = okResult!.Value as ApiResponse<TeamResponse>;

            response!.Success.Should().BeTrue();
            response.Data!.Warnings.Should().HaveCount(1);
            response.Data.Warnings[0].Should().Contain("Jane Doe");
            response.Data.Warnings[0].Should().Contain("Gamma Team");

            existingOverlap.EndDate.Should().NotBeNull();
            _mockTeamRepository.Verify(x => x.UpdateAsync(otherTeam), Times.Once);
            _mockTeamRepository.Verify(x => x.AddMemberAsync(It.Is<UserTeam>(ut => ut.TeamId == teamId && ut.UserInternalId == 456)), Times.Once);
        }
    }
}
