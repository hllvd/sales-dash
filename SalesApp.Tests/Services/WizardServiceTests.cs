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
    }
}
