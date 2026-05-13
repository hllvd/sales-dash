using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace SalesApp.DataMigrator;

class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: SalesApp.DataMigrator <connection_string> [--dry-run]");
            Console.WriteLine("Example: SalesApp.DataMigrator \"Data Source=SalesApp.db\" --dry-run");
            return 1;
        }

        string connectionString = args[0];
        bool isDryRun = args.Contains("--dry-run");

        Console.WriteLine($"--- Data Migration Utility Started ---");
        if (isDryRun) Console.WriteLine("⚠️  RUNNING IN DRY-RUN MODE (No changes will be committed)");

        try 
        {
            using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // --- Step 1: Migrate Contract Statuses to Lookup Table ---
                Console.WriteLine("\n[Step 1] Populating ContractStatuses table...");
                
                int initialStatusCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ContractStatuses", transaction: transaction);
                
                // Get distinct strings from Contracts
                var distinctStatuses = (await connection.QueryAsync<string>(
                    "SELECT DISTINCT Status FROM Contracts WHERE Status IS NOT NULL AND Status != ''", 
                    transaction: transaction)).ToList();

                Console.WriteLine($"Found {distinctStatuses.Count} distinct status values in existing Contracts.");

                var statusMap = new Dictionary<string, int>();
                foreach (var statusName in distinctStatuses)
                {
                    // Check if exists (idempotency)
                    var existingId = await connection.QueryFirstOrDefaultAsync<int?>(
                        "SELECT Id FROM ContractStatuses WHERE Name = @Name", new { Name = statusName }, transaction: transaction);

                    if (existingId.HasValue)
                    {
                        statusMap[statusName] = existingId.Value;
                    }
                    else
                    {
                        int id = await connection.QuerySingleAsync<int>(
                            "INSERT INTO ContractStatuses (Name) VALUES (@Name) RETURNING Id", 
                            new { Name = statusName }, 
                            transaction: transaction);
                        
                        statusMap[statusName] = id;
                    }
                }

                int finalStatusCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM ContractStatuses", transaction: transaction);
                Console.WriteLine($"Step 1 complete. Lookup table now has {finalStatusCount} entries (+{finalStatusCount - initialStatusCount} new).");

                // --- Step 2: Update Contracts.ContractStatusId ---
                Console.WriteLine("\n[Step 2] Mapping Contracts to new Status IDs...");
                
                int totalContractsCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Contracts", transaction: transaction);
                int contractsUpdated = 0;
                
                foreach (var mapping in statusMap)
                {
                    contractsUpdated += await connection.ExecuteAsync(
                        "UPDATE Contracts SET ContractStatusId = @StatusId WHERE Status = @StatusName AND ContractStatusId IS NULL",
                        new { StatusId = mapping.Value, StatusName = mapping.Key },
                        transaction: transaction);
                }
                Console.WriteLine($"Step 2 complete. Updated {contractsUpdated} of {totalContractsCount} contracts.");

                // --- Final Validation ---
                Console.WriteLine("\n--- Validation Summary ---");
                int nullStatusCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Contracts WHERE ContractStatusId IS NULL", transaction: transaction);

                if (nullStatusCount > 0)
                {
                    Console.Error.WriteLine($"\n❌  VALIDATION FAILED: {nullStatusCount} contracts still have NULL ContractStatusId.");
                    Console.Error.WriteLine("Rolling back transaction...");
                    transaction.Rollback();
                    return 1;
                }

                Console.WriteLine("✅  VALIDATION PASSED: All contracts have a ContractStatusId.");

                if (isDryRun)
                {
                    Console.WriteLine("\n⚠️  DRY-RUN SUCCESSFUL. Rolling back changes as requested.");
                    transaction.Rollback();
                }
                else
                {
                    Console.WriteLine("\n🚀  COMMITTING CHANGES...");
                    transaction.Commit();
                    Console.WriteLine("Migration completed successfully.");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\n💥  FATAL ERROR during transaction: {ex.Message}");
                transaction.Rollback();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\n💥  CONNECTION ERROR: {ex.Message}");
            return 1;
        }
    }
}
