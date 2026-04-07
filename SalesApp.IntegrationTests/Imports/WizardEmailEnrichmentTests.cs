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
            // Clear existing users and matriculas
            context.UserMatriculas.RemoveRange(context.UserMatriculas);
            var users = await context.Users.Where(u => u.Email != "superadmin@test.com" && u.Email != "admin@test.com" && u.Email != "user@test.com").ToListAsync();
            context.Users.RemoveRange(users);
            await context.SaveChangesAsync();

            var role = await context.Roles.FirstAsync();

            var carlos = new User { Name = "Carlos Eduardo Pereira", Email = "carlos@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var anthony = new User { Name = "Anthony Francys Bryan Pereira", Email = "anthony@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var vini = new User { Name = "Vinicius Silva Ornelas", Email = "vini@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            var valeria = new User { Name = "Valeria de Lima Abicalaf", Email = "valeria@test.com", PasswordHash = "hash", RoleId = role.Id, IsActive = true };
            
            context.Users.AddRange(carlos, anthony, vini, valeria);
            await context.SaveChangesAsync();

            // Set up Matriculas exactly simulating the bug's environment
            // 6241 is shared. Anthony is owner. Carlos and Vini are NOT owners.
            context.UserMatriculas.AddRange(
                new UserMatricula { UserId = anthony.Id, MatriculaNumber = "6241", IsOwner = true, IsActive = true, StartDate = DateTime.UtcNow },
                new UserMatricula { UserId = carlos.Id, MatriculaNumber = "6241", IsOwner = false, IsActive = true, StartDate = DateTime.UtcNow },
                new UserMatricula { UserId = vini.Id, MatriculaNumber = "6241", IsOwner = false, IsActive = true, StartDate = DateTime.UtcNow },
                // 10979 is uniquely owned by Valeria
                new UserMatricula { UserId = valeria.Id, MatriculaNumber = "10979", IsOwner = true, IsActive = true, StartDate = DateTime.UtcNow }
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
            
            // 1. Create a dummy session
            var session = new ImportSession { UploadId = Guid.NewGuid().ToString(), FileName = "test.csv", Status = "ready", TotalRows = 2 };
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
            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId);
            
            // 4. Validate output
            var parsedRows = ParseCsvBytes(csvBytes);
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
            
            var session = new ImportSession { UploadId = Guid.NewGuid().ToString(), FileName = "test.csv", Status = "ready", TotalRows = 1 };
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

            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId);
            var parsedRows = ParseCsvBytes(csvBytes);
            
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
            
            var session = new ImportSession { UploadId = Guid.NewGuid().ToString(), FileName = "test.csv", Status = "ready", TotalRows = 1 };
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

            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId);
            var parsedRows = ParseCsvBytes(csvBytes);
            
            Assert.Single(parsedRows);
            // Because name missed, it falls back to matricula. Since it's shared, it MUST pick the Owner=true (Anthony)
            Assert.Equal("anthony@test.com", parsedRows[0]["Email"]);
        }

        [Fact]
        public async Task GenerateEnrichedContracts_EmptyEmail_WhenNothingMatches()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await SetupTestDataAsync(context);

            var wizardService = scope.ServiceProvider.GetRequiredService<IWizardService>();
            
            var session = new ImportSession { UploadId = Guid.NewGuid().ToString(), FileName = "test.csv", Status = "ready", TotalRows = 1 };
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

            var csvBytes = await wizardService.GenerateEnrichedContractsAsync(session.UploadId);
            var parsedRows = ParseCsvBytes(csvBytes);
            
            Assert.Single(parsedRows);
            Assert.Equal("", parsedRows[0]["Email"]); // Must be empty, properly written to CSV
        }

        private List<Dictionary<string, string>> ParseCsvBytes(byte[] csvBytes)
        {
            using var memoryStream = new MemoryStream(csvBytes);
            using var reader = new StreamReader(memoryStream);
            using var csvReader = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { IgnoreBlankLines = true });
            
            var records = new List<Dictionary<string, string>>();
            csvReader.Read();
            csvReader.ReadHeader();
            
            while (csvReader.Read())
            {
                var record = new Dictionary<string, string>();
                foreach (var header in csvReader.HeaderRecord!)
                {
                    record[header] = csvReader.GetField(header);
                }
                records.Add(record);
            }
            return records;
        }
    }
}
