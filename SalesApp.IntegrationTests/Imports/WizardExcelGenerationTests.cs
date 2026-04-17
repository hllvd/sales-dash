using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using OfficeOpenXml;
using Xunit;
using System.Text.Json;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Database Collection")]
    public class WizardExcelGenerationTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public WizardExcelGenerationTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task GenerateUsersTemplate_ReturnsValidXlsxWithExtractedUsers()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();

            // 1. Setup a session with some rows containing user data
            var adminUser = await context.Users.FirstAsync(u => u.Email == "superadmin@test.com");
            var session = new ImportSession 
            { 
                UploadId = "xlsx-gen-test-" + Guid.NewGuid().ToString().Substring(0, 8), 
                FileName = "contracts.csv", 
                FileType = "csv",
                Status = "ready", 
                TotalRows = 2,
                UploadedByUserId = adminUser.Id
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var row1 = new Dictionary<string, string>
            {
                { "Consultor", "Joao Silva" },
                { "Matricula", "MAT-001" },
                { "Cota", "123;456;789;Customer A;Contract 1" }
            };
            var row2 = new Dictionary<string, string>
            {
                { "Consultor", "Maria Souza" },
                { "Matricula", "MAT-002" },
                { "Cota", "999;888;777;Customer B;Contract 2" }
            };

            context.ImportRows.AddRange(
                new ImportRow { ImportSessionId = session.Id, RowIndex = 0, RowData = JsonSerializer.Serialize(row1) },
                new ImportRow { ImportSessionId = session.Id, RowIndex = 1, RowData = JsonSerializer.Serialize(row2) }
            );
            await context.SaveChangesAsync();

            // 2. Generate the template
            var xlsxBytes = await wizardService.GenerateUsersTemplateAsync(session.UploadId);

            // 3. Verify the Excel content using EPPlus
            ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var ms = new MemoryStream(xlsxBytes);
            using var package = new ExcelPackage(ms);
            
            var worksheet = package.Workbook.Worksheets["Users"];
            Assert.NotNull(worksheet);

            // Verify Headers
            Assert.Equal("Name", worksheet.Cells[1, 1].Value);
            Assert.Equal("Email", worksheet.Cells[1, 2].Value);
            Assert.Equal("ParentEmail", worksheet.Cells[1, 3].Value);
            Assert.Equal("Matricula", worksheet.Cells[1, 4].Value);
            Assert.Equal("Owner_Matricula", worksheet.Cells[1, 5].Value);
            Assert.Equal("Password", worksheet.Cells[1, 6].Value);

            // Verify Data (Sorted by Name)
            Assert.Equal("Joao Silva", worksheet.Cells[2, 1].Value);
            Assert.Equal("MAT-001", worksheet.Cells[2, 4].Value);
            Assert.Equal("0", worksheet.Cells[2, 5].Value);

            Assert.Equal("Maria Souza", worksheet.Cells[3, 1].Value);
            Assert.Equal("MAT-002", worksheet.Cells[3, 4].Value);
            Assert.Equal("0", worksheet.Cells[3, 5].Value);
        }
    }
}
