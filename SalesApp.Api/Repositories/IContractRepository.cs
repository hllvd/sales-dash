using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IContractRepository
    {
        Task<Contract?> GetByIdAsync(int id);
        Task<Contract?> GetByContractNumberAsync(string contractNumber);
        Task<List<Contract>> GetByContractNumbersAsync(List<string> contractNumbers);
        Task<List<Contract>> GetAllAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false, bool? awaitingPayment = null);
        Task<List<Contract>> GetAllAsync(Guid? userId, int? groupId, DateTime? startDate, DateTime? endDate, string? contractNumber, bool? showUnassigned, List<string>? matriculaNumbers, string? userEmail, UserScopeContext? scope, List<int>? teamIds, List<Guid>? userIds, List<string>? statuses, bool isSuperAdmin, bool? awaitingPayment, List<int>? userInternalIds, bool asNoTracking = false);
        Task<(List<Contract> Items, int TotalCount)> GetPagedAsync(int page, int pageSize, Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false, bool? awaitingPayment = null);
        Task<ContractAggregation> GetAggregationAsync(Guid? userId = null, int? groupId = null, DateTime? startDate = null, DateTime? endDate = null, string? contractNumber = null, bool? showUnassigned = null, List<string>? matriculaNumbers = null, string? userEmail = null, UserScopeContext? scope = null, List<int>? teamIds = null, List<Guid>? userIds = null, List<string>? statuses = null, bool isSuperAdmin = false, bool? awaitingPayment = null);
        Task<List<Contract>> GetByUserIdAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, string? matriculaNumber = null, List<int>? teamIds = null, List<string>? matriculaNumbers = null);
        Task<List<Contract>> GetByUploadIdAsync(string uploadId);
        Task<Contract> CreateAsync(Contract contract);
        Task<List<Contract>> CreateBatchAsync(List<Contract> contracts);
        Task<Contract> UpdateAsync(Contract contract);
        Task<List<MonthlyProduction>> GetMonthlyProductionAsync(Guid? userId, DateTime? startDate, DateTime? endDate, bool? showUnassigned = null);
        Task<List<Contract>> GetContractsForMigrationAsync(Guid userId);
    }
}