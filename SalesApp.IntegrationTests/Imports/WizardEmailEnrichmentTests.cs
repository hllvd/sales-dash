using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Services;
using SalesApp.DTOs;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using Xunit;

namespace SalesApp.IntegrationTests.Imports
{
    [Collection("Database Collection")]
    public class WizardEmailEnrichmentTests : IClassFixture<TestWebApplicationFactory>
    {
        private readonly TestWebApplicationFactory _factory;

        public WizardEmailEnrichmentTests(TestWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task SetupTestDataAsync(AppDbContext context)
        {
            // Clear existing data in correct order (child to parent) to avoid FK constraint violations
            context.AuditLogs.RemoveRange(context.AuditLogs);
            context.Contracts.RemoveRange(context.Contracts);
            context.ImportRows.RemoveRange(context.ImportRows);
            context.ImportSessions.RemoveRange(context.ImportSessions);
            context.Groups.RemoveRange(context.Groups);
            context.PVs.RemoveRange(context.PVs);
            context.UserMatriculas.RemoveRange(context.UserMatriculas);
            context.Matriculas.RemoveRange(context.Matriculas);
            context.RefreshTokens.RemoveRange(context.RefreshTokens);
            context.ScrapeConfigs.RemoveRange(context.ScrapeConfigs);
            
            var users = await context.Users
                .Where(u => u.Email != "superadmin@test.com" && u.Email != "admin@test.com" && u.Email != "user@test.com")
                .ToListAsync();
            context.Users.RemoveRange(users);
            await context.SaveChangesAsync();
            
            var role = await context.Roles.FirstAsync();

            // Ensure we have an admin user for the tests
            if (!await context.Users.AnyAsync(u => u.Email == "admin@test.com"))
            {
                context.Users.Add(new User 
                { 
                    Name = "Admin", 
                    Email = "admin@test.com", 
                    PasswordHash = "hash", 
                    RoleId = role.Id, 
                    IsActive = true 
                });
                await context.SaveChangesAsync();
            }

            var carlos = new User { Name = "Carlos Eduardo Pereira", Email = "carlos@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var anthony = new User { Name = "Anthony Francys Bryan Pereira", Email = "anthony@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var vini = new User { Name = "Vinicius Silva Ornelas", Email = "vini@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var valeria = new User { Name = "Valeria de Lima Abicalaf", Email = "valeria@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            
            context.Users.AddRange(carlos, anthony, vini, valeria);
            await context.SaveChangesAsync();

            // Set up Matriculas exactly simulating the bug's environment
            // 6241 is shared. Anthony is owner. Carlos and Vini are NOT owners.
            var m6241 = new Matricula { MatriculaNumber = "6241", StartDate = DateTime.UtcNow, Status = "active" };
            var m10979 = new Matricula { MatriculaNumber = "10979", StartDate = DateTime.UtcNow, Status = "active" };
            context.Matriculas.AddRange(m6241, m10979);
            await context.SaveChangesAsync();

            context.UserMatriculas.AddRange(
                new UserMatricula { UserId = anthony.Id, MatriculaId = m6241.Id, IsOwner = true, IsActive = true },
                new UserMatricula { UserId = carlos.Id, MatriculaId = m6241.Id, IsOwner = false, IsActive = true },
                new UserMatricula { UserId = vini.Id, MatriculaId = m6241.Id, IsOwner = false, IsActive = true },
                // 10979 is uniquely owned by Valeria
                new UserMatricula { UserId = valeria.Id, MatriculaId = m10979.Id, IsOwner = true, IsActive = true }
            );
            
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task GenerateEnrichedContracts_PrioritizesExactNameAndMatriculaMatch()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetupTestDataAsync(context);

            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();
            
            var adminUser = await context.Users.FirstAsync(u => u.Email.Contains("admin"));
            var session = new ImportSession 
            { 
                UploadId = Guid.NewGuid().ToString(), 
                FileName = "test.csv", 
                FileType = "csv",
                UploadedByUserId = adminUser.Id,
                Status = "ready", 
                TotalRows = 2 
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            // 2. Add ImportRows representing contracts.csv exactly as the wizard parses it
            // Carlos row (shared matricula, but exact name matches)
            var row1 = new Dictionary<string, string>
            {
                { "Comissionado", "Carlos Eduardo Pereira" },
                { "Matrícula", "6241" },
                { "Contrato", "AAA" },
                { "Status", "" }
            };
            
            // Vini row (shared matricula, exact name matches)
            var row2 = new Dictionary<string, string>
            {
                { "Comissionado", "Vinicius Silva Ornelas" },
                { "Matrícula", "6241" },
                { "Contrato", "BBB" },
                { "Status", "" }
            };

            context.ImportRows.AddRange(
                new ImportRow { ImportSessionId = session.Id, RowIndex = 0, RowData = JsonSerializer.Serialize(row1) },
                new ImportRow { ImportSessionId = session.Id, RowIndex = 1, RowData = JsonSerializer.Serialize(row2) }
            );
            await context.SaveChangesAsync();

            // 3. Execute enrichment
            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId, adminUser.Id);
            
            // 4. Validate output
            var parsedRows = ParseXlsxBytes(csvBytes);
            Assert.Equal(2, parsedRows.Count);
            
            // Before fix, Carlos would have gotten vini@test.com
            Assert.Equal("carlos@test.com", parsedRows[0]["Email"]);
            Assert.Equal("vini@test.com", parsedRows[1]["Email"]);
        }

        [Fact]
        public async Task GenerateEnrichedContracts_FallsBackToName_WhenMatriculaDoesNotMatch()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetupTestDataAsync(context);

            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();
            
            var adminUser = await context.Users.FirstAsync(u => u.Email.Contains("admin"));
            var session = new ImportSession 
            { 
                UploadId = Guid.NewGuid().ToString(), 
                FileName = "test.csv", 
                FileType = "csv",
                UploadedByUserId = adminUser.Id,
                Status = "ready", 
                TotalRows = 1 
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var row1 = new Dictionary<string, string>
            {
                { "Comissionado", "Vinicius Silva Ornelas" },  // Exists
                { "Matrícula", "999999" },                     // Doesn't exist
                { "Contrato", "CCC" },
                { "Status", "" }
            };

            context.ImportRows.Add(new ImportRow { ImportSessionId = session.Id, RowIndex = 0, RowData = JsonSerializer.Serialize(row1) });
            await context.SaveChangesAsync();

            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId, adminUser.Id);
            var parsedRows = ParseXlsxBytes(csvBytes);
            
            Assert.Single(parsedRows);
            Assert.Equal("vini@test.com", parsedRows[0]["Email"]);
        }

        [Fact]
        public async Task GenerateEnrichedContracts_FallsBackToOwner_WhenNameMismatchedButMatriculaMatches()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetupTestDataAsync(context);

            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();
            
            var adminUser = await context.Users.FirstAsync(u => u.Email.Contains("admin"));
            var session = new ImportSession 
            { 
                UploadId = Guid.NewGuid().ToString(), 
                FileName = "test.csv", 
                FileType = "csv",
                UploadedByUserId = adminUser.Id,
                Status = "ready", 
                TotalRows = 1 
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var row1 = new Dictionary<string, string>
            {
                { "Comissionado", "Typo In Name" }, // Mismatched name
                { "Matrícula", "6241" },            // Shared matricula
                { "Contrato", "DDD" },
                { "Status", "" }
            };

            context.ImportRows.Add(new ImportRow { ImportSessionId = session.Id, RowIndex = 0, RowData = JsonSerializer.Serialize(row1) });
            await context.SaveChangesAsync();

            var csvBytes2 = await wizardService.GenerateEnrichedContractsAsync(session.UploadId, adminUser.Id);
            var parsedRows2 = ParseXlsxBytes(csvBytes2);
            
            Assert.Single(parsedRows2);
            // Because name missed, it falls back to matricula. Since it's shared, it MUST pick the Owner=true (Anthony)
            Assert.Equal("anthony@test.com", parsedRows2[0]["Email"]);
        }

        [Fact]
        public async Task GenerateEnrichedContracts_EmptyEmail_WhenNothingMatches()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetupTestDataAsync(context);

            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();
            
            var adminUser = await context.Users.FirstAsync(u => u.Email.Contains("admin"));
            var session = new ImportSession 
            { 
                UploadId = Guid.NewGuid().ToString(), 
                FileName = "test.csv", 
                FileType = "csv",
                UploadedByUserId = adminUser.Id,
                Status = "ready", 
                TotalRows = 1 
            };
            context.ImportSessions.Add(session);
            await context.SaveChangesAsync();

            var row1 = new Dictionary<string, string>
            {
                { "Comissionado", "Ghost Salesperson" },
                { "Matrícula", "00000" },
                { "Contrato", "EEE" },
                { "Status", "" }
            };

            context.ImportRows.Add(new ImportRow { ImportSessionId = session.Id, RowIndex = 0, RowData = JsonSerializer.Serialize(row1) });
            await context.SaveChangesAsync();

            var csvBytes3 = await wizardService.GenerateEnrichedContractsAsync(session.UploadId, adminUser.Id);
            var parsedRows3 = ParseXlsxBytes(csvBytes3);
            
            Assert.Single(parsedRows3);
            Assert.Equal("", parsedRows3[0]["Email"]); // Must be empty, properly written to CSV
        }

        private List<Dictionary<string, string>> ParseXlsxBytes(byte[] xlsxBytes)
        {
            OfficeOpenXml.ExcelPackage.License.SetNonCommercialOrganization("SalesApp");
            using var memoryStream = new MemoryStream(xlsxBytes);
            using var package = new OfficeOpenXml.ExcelPackage(memoryStream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return new List<Dictionary<string, string>>();

            var rows = new List<Dictionary<string, string>>();
            int colCount = worksheet.Dimension.Columns;
            int rowCount = worksheet.Dimension.Rows;

            var headers = new List<string>();
            for (int col = 1; col <= colCount; col++)
            {
                headers.Add(worksheet.Cells[1, col].Text);
            }

            for (int r = 2; r <= rowCount; r++)
            {
                var row = new Dictionary<string, string>();
                for (int col = 1; col <= colCount; col++)
                {
                    row[headers[col - 1]] = worksheet.Cells[r, col].Text;
                }
                rows.Add(row);
            }
            return rows;
        }
    }
}
