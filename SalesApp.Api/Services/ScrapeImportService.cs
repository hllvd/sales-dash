using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using SalesApp.Data;
using SalesApp.Models;
using SalesApp.Repositories;

namespace SalesApp.Services
{
    public interface IScrapeImportService
    {
        Task<ImportResult> AutoImportAsync(string filePath, Guid userId);
    }

    public class ScrapeImportService : IScrapeImportService
    {
        private readonly AppDbContext _context;
        private readonly IContractRepository _contractRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPVRepository _pvRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IContractMetadataRepository _metadataRepository;

        public ScrapeImportService(
            AppDbContext context,
            IContractRepository contractRepository,
            IUserRepository userRepository,
            IPVRepository pvRepository,
            IGroupRepository groupRepository,
            IContractMetadataRepository metadataRepository)
        {
            _context = context;
            _contractRepository = contractRepository;
            _userRepository = userRepository;
            _pvRepository = pvRepository;
            _groupRepository = groupRepository;
            _metadataRepository = metadataRepository;
        }

        public async Task<ImportResult> AutoImportAsync(string filePath, Guid userId)
        {
            var result = new ImportResult();
            
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            
            var records = csv.GetRecords<dynamic>().ToList();
            result.TotalRows = records.Count;

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                result.Errors.Add($"User {userId} not found");
                return result;
            }

            foreach (var record in records)
            {
                try
                {
                    var dict = (IDictionary<string, object>)record;
                    await ProcessRowAsync(dict, user, result);
                    result.ProcessedRows++;
                }
                catch (Exception ex)
                {
                    result.FailedRows++;
                    result.Errors.Add($"Error processing row: {ex.Message}");
                }
            }

            await _context.SaveChangesAsync();
            return result;
        }

        private async Task ProcessRowAsync(IDictionary<string, object> row, User user, ImportResult result)
        {
            // Map headers to properties
            var contractNumber = row.ContainsKey("Cota") ? row["Cota"]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(contractNumber)) return;

            var existing = await _contractRepository.GetByContractNumberAsync(contractNumber);
            var contract = existing ?? new Contract { CreatedAt = DateTime.UtcNow };

            contract.ContractNumber = contractNumber;
            contract.UserId = user.Id;
            contract.Status = MapStatus(row.ContainsKey("Situação Cobrança") ? row["Situação Cobrança"]?.ToString() : null);
            
            if (row.ContainsKey("Crédito Venda") && decimal.TryParse(row["Crédito Venda"]?.ToString(), out var amount))
            {
                contract.TotalAmount = amount;
            }

            if (row.ContainsKey("Dt Produção") && DateTime.TryParse(row["Dt Produção"]?.ToString(), out var prodDate))
            {
                contract.SaleStartDate = prodDate;
            }

            contract.CustomerName = row.ContainsKey("Consultor") ? row["Consultor"]?.ToString() : null;
            contract.UpdatedAt = DateTime.UtcNow;
            contract.IsActive = true;

            // Resolve PV
            var pvName = row.ContainsKey("PV") ? row["PV"]?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(pvName))
            {
                var pv = await _context.PVs.FirstOrDefaultAsync(p => p.Name == pvName);
                if (pv == null)
                {
                    pv = new PV { Id = GeneratePvId(pvName), Name = pvName, CreatedAt = DateTime.UtcNow };
                    _context.PVs.Add(pv);
                }
                contract.PvId = pv.Id;
            }

            if (existing == null)
            {
                _context.Contracts.Add(contract);
            }
        }

        private string MapStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return "active";
            // Simple mapping for now
            return status.ToLower().Contains("cancel") ? "cancelled" : "active";
        }

        private int GeneratePvId(string name)
        {
            // Simple hash for ID if not provided by PBI
            return Math.Abs(name.GetHashCode() % 1000000);
        }
    }
}
