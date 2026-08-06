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
    [Collection("Imports Tests")]
    public class WizardEmailEnrichmentTests 
    {
        private readonly ImportsTestFactory _factory;

        public WizardEmailEnrichmentTests(ImportsTestFactory factory)
        {
            _factory = factory;
        }

        private async Task SetupTestDataAsync(AppDbContext context)
        {
            // Instead of deleting users which might have foreign key references (AuditLogs, etc),
            // we just ensure our test users exist or are updated.
            
            var role = await context.Roles.FirstAsync(r => r.Name == "admin" || r.Name == "superadmin");

            async Task<User> EnsureUser(string name, string email)
            {
                var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                {
                    user = new User { Id = Guid.NewGuid(), Name = name, Email = email, PasswordHash = "hash", RoleId = role.Id, IsActive = true };
                    context.Users.Add(user);
                }
                else
                {
                    user.Name = name;
                    user.IsActive = true;
                    context.Users.Update(user);
                }
                return user;
            }

            var admin = await EnsureUser("Admin", "admin@test.com");
            var carlos = await EnsureUser("Carlos Eduardo Pereira", "carlos@test.com");
            var anthony = await EnsureUser("Anthony Francys Bryan Pereira", "anthony@test.com");
            var vini = await EnsureUser("Vinicius Silva Ornelas", "vini@test.com");
            var valeria = await EnsureUser("Valeria de Lima Abicalaf", "valeria@test.com");
            
            await context.SaveChangesAsync();

            // Clear old test matriculas to avoid "already exists" errors if running multiple times
            // but safely (using truncate or careful deletion)
            var testMatriculaNumbers = new[] { "6241", "10979" };
            var existingMatriculas = await context.Matriculas
                .Include(m => m.UserMatriculas)
                .Where(m => testMatriculaNumbers.Contains(m.MatriculaNumber))
                .ToListAsync();
            
            foreach (var m in existingMatriculas)
            {
                context.UserMatriculas.RemoveRange(m.UserMatriculas);
            }
            context.Matriculas.RemoveRange(existingMatriculas);
            await context.SaveChangesAsync();

            var m6241 = new Matricula { MatriculaNumber = "6241", StartDate = DateTime.UtcNow, Status = "active" };
            var m10979 = new Matricula { MatriculaNumber = "10979", StartDate = DateTime.UtcNow, Status = "active" };
            context.Matriculas.AddRange(m6241, m10979);
            await context.SaveChangesAsync();

            context.UserMatriculas.AddRange(
                new UserMatricula { UserInternalId = anthony.InternalId, MatriculaId = m6241.Id, IsOwner = true, IsActive = true },
                new UserMatricula { UserInternalId = carlos.InternalId, MatriculaId = m6241.Id, IsOwner = false, IsActive = true },
                new UserMatricula { UserInternalId = vini.InternalId, MatriculaId = m6241.Id, IsOwner = false, IsActive = true },
                new UserMatricula { UserInternalId = valeria.InternalId, MatriculaId = m10979.Id, IsOwner = true, IsActive = true }
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
                UploadedByUserInternalId = adminUser.InternalId,
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
                UploadedByUserInternalId = adminUser.InternalId,
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
                UploadedByUserInternalId = adminUser.InternalId,
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
                UploadedByUserInternalId = adminUser.InternalId,
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
