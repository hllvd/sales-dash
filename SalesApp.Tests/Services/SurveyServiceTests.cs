using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using Xunit;

namespace SalesApp.Tests.Services
{
    public class SurveyServiceTests
    {
        private readonly Mock<ISurveyRepository> _surveyRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly SurveyService _surveyService;

        public SurveyServiceTests()
        {
            _surveyRepoMock = new Mock<ISurveyRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _surveyService = new SurveyService(_surveyRepoMock.Object, _userRepoMock.Object);
        }

        [Fact]
        public async Task GetSurveyResultsAsync_OrdersResponses_AnsweredFirstDesc_ThenNonAnsweredSentAtDesc_ThenUserName()
        {
            // Arrange
            var surveyId = Guid.NewGuid();
            var survey = new Survey
            {
                Id = surveyId,
                Title = "Pesquisa de Teste",
                QuestionText = "Você aprova?",
                QuestionType = "yesno",
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Assignments = new List<SurveyAssignment>
                {
                    new SurveyAssignment
                    {
                        Id = 1,
                        UserId = Guid.NewGuid(),
                        User = new User { Name = "Ana", Email = "ana@test.com" },
                        Status = "answered",
                        SentAt = DateTime.UtcNow.AddDays(-5),
                        ExpiresAt = DateTime.UtcNow.AddDays(-3),
                        Response = new SurveyResponse
                        {
                            Answer = "Sim",
                            AnsweredAt = new DateTime(2026, 9, 10, 10, 0, 0, DateTimeKind.Utc)
                        }
                    },
                    new SurveyAssignment
                    {
                        Id = 2,
                        UserId = Guid.NewGuid(),
                        User = new User { Name = "Carlos", Email = "carlos@test.com" },
                        Status = "answered",
                        SentAt = DateTime.UtcNow.AddDays(-5),
                        ExpiresAt = DateTime.UtcNow.AddDays(-3),
                        Response = new SurveyResponse
                        {
                            Answer = "Não",
                            AnsweredAt = new DateTime(2026, 9, 15, 14, 0, 0, DateTimeKind.Utc)
                        }
                    },
                    new SurveyAssignment
                    {
                        Id = 3,
                        UserId = Guid.NewGuid(),
                        User = new User { Name = "Bruno", Email = "bruno@test.com" },
                        Status = "pending",
                        SentAt = new DateTime(2026, 9, 9, 10, 0, 0, DateTimeKind.Utc),
                        ExpiresAt = DateTime.UtcNow.AddDays(1)
                    },
                    new SurveyAssignment
                    {
                        Id = 4,
                        UserId = Guid.NewGuid(),
                        User = new User { Name = "Daniel", Email = "daniel@test.com" },
                        Status = "pending",
                        SentAt = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc),
                        ExpiresAt = DateTime.UtcNow.AddDays(1)
                    },
                    new SurveyAssignment
                    {
                        Id = 5,
                        UserId = Guid.NewGuid(),
                        User = new User { Name = "Amanda", Email = "amanda@test.com" },
                        Status = "expired",
                        SentAt = new DateTime(2026, 9, 12, 10, 0, 0, DateTimeKind.Utc),
                        ExpiresAt = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc)
                    }
                }
            };

            _surveyRepoMock.Setup(r => r.GetByIdAsync(surveyId)).ReturnsAsync(survey);

            // Act
            var result = await _surveyService.GetSurveyResultsAsync(surveyId);

            // Assert
            result.Should().NotBeNull();
            var namesInOrder = result.Responses.Select(r => r.UserName).ToList();

            // Expected order:
            // 1. Carlos (answered, 2026-09-15)
            // 2. Ana (answered, 2026-09-10)
            // 3. Amanda (expired, sent 2026-09-12, 'Amanda' < 'Daniel')
            // 4. Daniel (pending, sent 2026-09-12)
            // 5. Bruno (pending, sent 2026-09-09)
            namesInOrder.Should().Equal("Carlos", "Ana", "Amanda", "Daniel", "Bruno");
        }
    }
}
