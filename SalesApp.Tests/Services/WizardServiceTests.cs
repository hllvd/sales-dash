using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Moq;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;
using SalesApp.Services;
using System.Text.Json;
using Xunit;
using OfficeOpenXml;

namespace SalesApp.Tests.Services
{
    public class WizardServiceTests
    {
        private readonly AppDbContext _context;
        private readonly Mock<IImportSessionRepository> _mockSessionRepository;
        private readonly Mock<IImportTemplateRepository> _mockTemplateRepository;
        private readonly Mock<IFileParserService> _mockFileParser;
        private readonly Mock<IAutoMappingService> _mockAutoMapping;
        private readonly Mock<IImportExecutionService> _mockImportExecution;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<IWizardHeaderValidator> _mockHeaderValidator;
        private readonly WizardService _service;

        public WizardServiceTests()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new AppDbContext(options, new Mock<IHttpContextAccessor>().Object);
            _mockSessionRepository = new Mock<IImportSessionRepository>();
            _mockTemplateRepository = new Mock<IImportTemplateRepository>();
            _mockFileParser = new Mock<IFileParserService>();
            _mockAutoMapping = new Mock<IAutoMappingService>();
            _mockImportExecution = new Mock<IImportExecutionService>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockHeaderValidator = new Mock<IWizardHeaderValidator>();

            _service = new WizardService(
                _mockSessionRepository.Object,
                _mockTemplateRepository.Object,
                _mockFileParser.Object,
                _mockAutoMapping.Object,
                _mockImportExecution.Object,
                _mockUserRepository.Object,
                _mockHeaderValidator.Object,
                _context
            );
        }

        [Fact]
        public async Task GenerateEnrichedContractsAsync_ShouldResolveExactMatchCorrectly()
        {
            // Arrange
            var uploadId = "test-upload";
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "test.xlsx",
                FileType = "xlsx",
                Status = "preview"
            };
            _context.ImportSessions.Add(session);

            var user = new User { Id = Guid.NewGuid(), Name = "Glayse Santos", Email = "glayse@test.com", IsActive = true };
            var matriculaObj = new Matricula { MatriculaNumber = "13126" };
            user.UserMatriculas = new List<UserMatricula>
            {
                new UserMatricula { Matricula = matriculaObj, IsActive = true, IsOwner = true }
            };
            _context.Users.Add(user);

            var rowData = new Dictionary<string, string>
            {
                { "Consultor", "Glayse Santos" },
                { "Matrícula", "13126" },
                { "Contrato", "CTR-001" }
            };
            var importRow = new ImportRow
            {
                ImportSession = session,
                RowIndex = 0,
                RowData = JsonSerializer.Serialize(rowData)
            };
            _context.ImportRows.Add(importRow);
            await _context.SaveChangesAsync();

            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            // Act
            var excelBytes = await _service.GenerateEnrichedContractsAsync(uploadId, Guid.NewGuid());

            // Assert
            excelBytes.Should().NotBeNull();
        }

        [Fact]
        public async Task GenerateEnrichedContractsAsync_ShouldSkipAmbiguousNames()
        {
            // Arrange
            var uploadId = "test-upload-ambiguous-name";
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "test.xlsx",
                FileType = "xlsx",
                Status = "preview"
            };
            _context.ImportSessions.Add(session);

            // Two users sharing the exact same name
            var user1 = new User { Id = Guid.NewGuid(), Name = "Witoria", Email = "witoria1@test.com", IsActive = true };
            var user2 = new User { Id = Guid.NewGuid(), Name = "Witoria", Email = "witoria2@test.com", IsActive = true };
            _context.Users.AddRange(user1, user2);

            var rowData = new Dictionary<string, string>
            {
                { "Consultor", "Witoria" },
                { "Matrícula", "" },
                { "Contrato", "CTR-001" }
            };
            var importRow = new ImportRow
            {
                ImportSession = session,
                RowIndex = 0,
                RowData = JsonSerializer.Serialize(rowData)
            };
            _context.ImportRows.Add(importRow);
            await _context.SaveChangesAsync();

            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            // Act
            var excelBytes = await _service.GenerateEnrichedContractsAsync(uploadId, Guid.NewGuid());

            // Assert
            excelBytes.Should().NotBeNull();
            using var ms = new System.IO.MemoryStream(excelBytes);
            using var package = new ExcelPackage(ms);
            var sheet = package.Workbook.Worksheets[0];
            var emailVal = sheet.Cells[2, 4].Text; // 2nd row, 4th column is Email
            emailVal.Should().BeNullOrEmpty(); // Ambiguous name should skip name-only match
        }

        [Fact]
        public async Task GenerateEnrichedContractsAsync_ShouldSkipAmbiguousMatriculas()
        {
            // Arrange
            var uploadId = "test-upload-ambiguous-mat";
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "test.xlsx",
                FileType = "xlsx",
                Status = "preview"
            };
            _context.ImportSessions.Add(session);

            var matriculaObj = new Matricula { MatriculaNumber = "8724" };
            _context.Matriculas.Add(matriculaObj);

            // Two owners for same matricula
            var user1 = new User { Id = Guid.NewGuid(), Name = "User One", Email = "user1@test.com", IsActive = true };
            user1.UserMatriculas = new List<UserMatricula> { new UserMatricula { Matricula = matriculaObj, IsActive = true, IsOwner = true } };
            
            var user2 = new User { Id = Guid.NewGuid(), Name = "User Two", Email = "user2@test.com", IsActive = true };
            user2.UserMatriculas = new List<UserMatricula> { new UserMatricula { Matricula = matriculaObj, IsActive = true, IsOwner = true } };

            _context.Users.AddRange(user1, user2);

            var rowData = new Dictionary<string, string>
            {
                { "Consultor", "Some Other Name" },
                { "Matrícula", "8724" },
                { "Contrato", "CTR-001" }
            };
            var importRow = new ImportRow
            {
                ImportSession = session,
                RowIndex = 0,
                RowData = JsonSerializer.Serialize(rowData)
            };
            _context.ImportRows.Add(importRow);
            await _context.SaveChangesAsync();

            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            // Act
            var excelBytes = await _service.GenerateEnrichedContractsAsync(uploadId, Guid.NewGuid());

            // Assert
            excelBytes.Should().NotBeNull();
            using var ms = new System.IO.MemoryStream(excelBytes);
            using var package = new ExcelPackage(ms);
            var sheet = package.Workbook.Worksheets[0];
            var emailVal = sheet.Cells[2, 4].Text;
            emailVal.Should().BeNullOrEmpty(); // Ambiguous matricula should skip fallback match
        }

        [Fact]
        public async Task GenerateEnrichedContractsAsync_ShouldResolveNameMatchesOrIsSimilar()
        {
            // Arrange
            var uploadId = "test-upload-similar-name";
            var session = new ImportSession
            {
                UploadId = uploadId,
                FileName = "test.xlsx",
                FileType = "xlsx",
                Status = "preview"
            };
            _context.ImportSessions.Add(session);

            var user = new User { Id = Guid.NewGuid(), Name = "Glayse Santos", Email = "glayse@test.com", IsActive = true };
            var matriculaObj = new Matricula { MatriculaNumber = "13126" };
            user.UserMatriculas = new List<UserMatricula>
            {
                new UserMatricula { Matricula = matriculaObj, IsActive = true, IsOwner = true }
            };
            _context.Users.Add(user);

            var rowData = new Dictionary<string, string>
            {
                { "Consultor", "Glayse S." }, // similar name, first word matches
                { "Matrícula", "13126" },
                { "Contrato", "CTR-001" }
            };
            var importRow = new ImportRow
            {
                ImportSession = session,
                RowIndex = 0,
                RowData = JsonSerializer.Serialize(rowData)
            };
            _context.ImportRows.Add(importRow);
            await _context.SaveChangesAsync();

            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            // Act
            var excelBytes = await _service.GenerateEnrichedContractsAsync(uploadId, Guid.NewGuid());

            // Assert
            excelBytes.Should().NotBeNull();
            using var ms = new System.IO.MemoryStream(excelBytes);
            using var package = new ExcelPackage(ms);
            var sheet = package.Workbook.Worksheets[0];
            var emailVal = sheet.Cells[2, 4].Text;
            emailVal.Should().Be("glayse@test.com"); // first word match "Glayse" matches "Glayse Santos"
        }

        [Fact]
        public async Task ProcessStep2ImportAsync_ShouldThrowException_WhenDuplicateEmailWithDifferentNamesExist()
        {
            // Arrange
            var uploadId = "test-upload";
            var session = new ImportSession { Id = 1, UploadId = uploadId, Status = "wizard_step1" };
            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Name", "John Doe" }, { "Email", "john@test.com" } },
                new Dictionary<string, string> { { "Name", "Jane Smith" }, { "Email", "john@test.com" } }
            };

            var mockFile = new Mock<IFormFile>();
            _mockFileParser.Setup(p => p.ParseFileAsync(mockFile.Object)).ReturnsAsync(rows);

            // Act
            Func<Task> act = async () => await _service.ProcessStep2ImportAsync(uploadId, mockFile.Object, Guid.NewGuid());

            // Assert
            var exception = await act.Should().ThrowAsync<ArgumentException>();
            exception.WithMessage("*O e-mail 'john@test.com' está associado a múltiplos usuários: Jane Smith, John Doe.*");
        }

        [Fact]
        public async Task ProcessStep2ImportAsync_ShouldProceed_WhenDuplicateEmailWithSameNameExists()
        {
            // Arrange
            var uploadId = "test-upload";
            var session = new ImportSession { Id = 1, UploadId = uploadId, Status = "wizard_step1" };
            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Name", "John Doe" }, { "Email", "john@test.com" } },
                // Same email and name (casing/spaces aside)
                new Dictionary<string, string> { { "Name", " JOHN DOE " }, { "Email", "john@test.com" } }
            };

            var mockFile = new Mock<IFormFile>();
            _mockFileParser.Setup(p => p.ParseFileAsync(mockFile.Object)).ReturnsAsync(rows);

            var importResult = new ImportResult
            {
                ProcessedRows = 2,
                FailedRows = 0,
                Errors = new List<string>(),
                FailedRowsDetails = new List<Dictionary<string, string>>()
            };

            _mockImportExecution.Setup(e => e.ExecuteUserImportAsync(
                uploadId,
                session.Id,
                rows,
                It.IsAny<Dictionary<string, string>>()
            )).ReturnsAsync(importResult);

            var usersTemplate = new ImportTemplate { Id = 10, Name = "Users" };
            _mockTemplateRepository.Setup(r => r.GetByNameAsync("Users")).ReturnsAsync(usersTemplate);

            // Act
            var result = await _service.ProcessStep2ImportAsync(uploadId, mockFile.Object, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result.Status.Should().Be("completed");
            result.ProcessedRows.Should().Be(2);
        }

        [Fact]
        public async Task ProcessStep2ImportAsync_ShouldBeCaseInsensitive_ForEmails()
        {
            // Arrange
            var uploadId = "test-upload";
            var session = new ImportSession { Id = 1, UploadId = uploadId, Status = "wizard_step1" };
            _mockSessionRepository.Setup(r => r.GetByUploadIdAsync(uploadId)).ReturnsAsync(session);

            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Name", "John Doe" }, { "Email", "JOHN@TEST.COM" } },
                new Dictionary<string, string> { { "Name", "Jane Smith" }, { "Email", "john@test.com" } }
            };

            var mockFile = new Mock<IFormFile>();
            _mockFileParser.Setup(p => p.ParseFileAsync(mockFile.Object)).ReturnsAsync(rows);

            // Act
            Func<Task> act = async () => await _service.ProcessStep2ImportAsync(uploadId, mockFile.Object, Guid.NewGuid());

            // Assert
            var exception = await act.Should().ThrowAsync<ArgumentException>();
            exception.WithMessage("*O e-mail 'john@test.com' está associado a múltiplos usuários: Jane Smith, John Doe.*");
        }

        private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                yield return item;
                await Task.Yield();
            }
        }

        [Fact]
        public async Task AnalyzeFileAsync_ShouldDetectAmbiguousTotalAmount_AndCalculateLikelyInterpretationUsingMedian()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test_outliers.xlsx");

            var columns = new List<string> { "Contrato", "Total" };
            _mockFileParser.Setup(p => p.GetColumnsAsync(mockFile.Object)).ReturnsAsync(columns);
            _mockFileParser.Setup(p => p.GetFileType(mockFile.Object)).Returns("xlsx");

            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Contrato", "CTR-001" }, { "Total", "200.000,00" } },
                new Dictionary<string, string> { { "Contrato", "CTR-002" }, { "Total", "250.000,00" } },
                new Dictionary<string, string> { { "Contrato", "CTR-003" }, { "Total", "300.000,00" } },
                new Dictionary<string, string> { { "Contrato", "CTR-004" }, { "Total", "80.000.00" } }
            };

            _mockFileParser.Setup(p => p.ParseFileStreamedAsync(mockFile.Object))
                .Returns(ToAsyncEnumerable(rows));

            _mockHeaderValidator.Setup(v => v.Validate(It.IsAny<List<string>>()))
                .Returns(new HeaderValidationResult { IsValid = true });

            var mockUser = new User { Id = Guid.NewGuid(), InternalId = 1, Name = "Test Admin", Email = "admin@test.com" };
            _mockUserRepository.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(mockUser);

            // Act
            var result = await _service.AnalyzeFileAsync(mockFile.Object, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result.OutlierAmounts.Should().HaveCount(1);

            var entry = result.OutlierAmounts.Single();
            entry.RowNumber.Should().Be(4); // 4th row
            entry.RawValue.Should().Be("80.000.00");
            entry.LikelyValue.Should().Be(80000m); // 80,000.00 is closer to median 250,000 than 8,000,000.00
            entry.AltValue.Should().Be(8000000m);
            entry.FileMedian.Should().Be(250000m);
        }

        [Fact]
        public async Task AnalyzeFileAsync_ShouldReturnEmptyOutlierAmounts_WhenAllTotalsAreUnambiguous()
        {
            // Arrange
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns("test_clean.xlsx");

            var columns = new List<string> { "Contrato", "Total" };
            _mockFileParser.Setup(p => p.GetColumnsAsync(mockFile.Object)).ReturnsAsync(columns);
            _mockFileParser.Setup(p => p.GetFileType(mockFile.Object)).Returns("xlsx");

            var rows = new List<Dictionary<string, string>>
            {
                new Dictionary<string, string> { { "Contrato", "CTR-001" }, { "Total", "225.000,00" } },
                new Dictionary<string, string> { { "Contrato", "CTR-002" }, { "Total", "150.000,00" } }
            };

            _mockFileParser.Setup(p => p.ParseFileStreamedAsync(mockFile.Object))
                .Returns(ToAsyncEnumerable(rows));

            _mockHeaderValidator.Setup(v => v.Validate(It.IsAny<List<string>>()))
                .Returns(new HeaderValidationResult { IsValid = true });

            var mockUser = new User { Id = Guid.NewGuid(), InternalId = 1, Name = "Test Admin", Email = "admin@test.com" };
            _mockUserRepository.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(mockUser);

            // Act
            var result = await _service.AnalyzeFileAsync(mockFile.Object, Guid.NewGuid());

            // Assert
            result.Should().NotBeNull();
            result.OutlierAmounts.Should().BeEmpty();
        }
    }
}
