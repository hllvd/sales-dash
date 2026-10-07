using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using SalesApp.Helpdesk.DTOs;
using SalesApp.Helpdesk.Models;
using SalesApp.Helpdesk.Repositories;
using SalesApp.Helpdesk.Services;
using Xunit;

namespace SalesApp.IntegrationTests.Helpdesk
{
    public class HelpdeskIntegrationTests
    {
        private readonly Mock<IHelpdeskRepository> _mockRepo;
        private readonly IHelpdeskService _service;

        public HelpdeskIntegrationTests()
        {
            _mockRepo = new Mock<IHelpdeskRepository>();
            _service = new HelpdeskService(_mockRepo.Object);
        }

        [Fact]
        public async Task CreateQuestionAsync_SetsDefaultStatusToNaoLido()
        {
            // Arrange
            HelpdeskQuestion? saved = null;
            _mockRepo.Setup(r => r.CreateQuestionAsync(It.IsAny<HelpdeskQuestion>()))
                     .Callback<HelpdeskQuestion>(q => saved = q)
                     .Returns(Task.CompletedTask);

            var req = new CreateHelpdeskQuestionRequest
            {
                Title = "Como calcular comissao?",
                Content = "Gostaria de entender o total ativo.",
                SourcePageUrl = "#/my-contracts"
            };

            // Act
            var res = await _service.CreateQuestionAsync("user-1", "admin1@test.com", "Admin 1", req);

            // Assert
            Assert.NotNull(res);
            Assert.Equal("Não lido", res.Status);
            Assert.Equal("Como calcular comissao?", res.Title);
            Assert.NotNull(saved);
            Assert.Equal("Não lido", saved!.Status);
        }

        [Fact]
        public async Task UpdateQuestionContentAsync_ArchivesPreviousContentInEditHistory()
        {
            // Arrange
            var existing = new HelpdeskQuestion
            {
                QuestionId = "q1",
                UserId = "user-1",
                CurrentContent = "Versao 1",
                EditHistory = new List<HelpdeskEditHistory>()
            };

            _mockRepo.Setup(r => r.GetQuestionByIdAsync("q1")).ReturnsAsync(existing);
            _mockRepo.Setup(r => r.UpdateQuestionContentAsync("q1", "Versao 2", "Versao 1", 1))
                     .Callback(() =>
                     {
                         existing.CurrentContent = "Versao 2";
                         existing.EditHistory.Add(new HelpdeskEditHistory { Version = 1, PreviousContent = "Versao 1" });
                     })
                     .Returns(Task.CompletedTask);

            // Act
            var res = await _service.UpdateQuestionContentAsync("q1", "user-1", new UpdateHelpdeskQuestionRequest { Content = "Versao 2" });

            // Assert
            Assert.Equal("Versao 2", res.CurrentContent);
            Assert.Single(res.EditHistory);
            Assert.Equal("Versao 1", res.EditHistory[0].PreviousContent);
        }

        [Fact]
        public async Task AdminCannotAccessOthersQuestion()
        {
            // Arrange
            var question = new HelpdeskQuestion
            {
                QuestionId = "q1",
                UserId = "admin-A",
                Title = "Duvida do admin A"
            };
            _mockRepo.Setup(r => r.GetQuestionByIdAsync("q1")).ReturnsAsync(question);

            // Act & Assert (admin-B should be denied)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _service.GetQuestionByIdAsync("q1", "admin-B", isSuperAdmin: false));

            // SuperAdmin should succeed
            var res = await _service.GetQuestionByIdAsync("q1", "superadmin-1", isSuperAdmin: true);
            Assert.NotNull(res);
            Assert.Equal("q1", res.QuestionId);
        }

        [Fact]
        public async Task UpdateStatusAsync_ValidatesStatusValues()
        {
            // Arrange
            var question = new HelpdeskQuestion { QuestionId = "q1", UserId = "admin-1", Status = "Não lido" };
            _mockRepo.Setup(r => r.GetQuestionByIdAsync("q1")).ReturnsAsync(question);
            _mockRepo.Setup(r => r.UpdateStatusAsync("q1", "Resolvido"))
                     .Callback(() => question.Status = "Resolvido")
                     .Returns(Task.CompletedTask);

            // Act & Assert - Invalid status
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _service.UpdateStatusAsync("q1", "admin-1", false, new UpdateHelpdeskStatusRequest { Status = "Invalido" }));

            // Valid status transition
            var res = await _service.UpdateStatusAsync("q1", "admin-1", false, new UpdateHelpdeskStatusRequest { Status = "Resolvido" });
            Assert.Equal("Resolvido", res.Status);
        }
    }
}
