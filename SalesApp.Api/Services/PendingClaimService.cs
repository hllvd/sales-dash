using SalesApp.Repositories;
using SalesApp.Data;

namespace SalesApp.Services
{
    public class PendingClaimService : IPendingClaimService
    {
        private readonly IPendingContractClaimRepository _claimRepository;
        private readonly IContractRepository _contractRepository;
        private readonly AppDbContext _context;

        public PendingClaimService(
            IPendingContractClaimRepository claimRepository,
            IContractRepository contractRepository,
            AppDbContext context)
        {
            _claimRepository = claimRepository;
            _contractRepository = contractRepository;
            _context = context;
        }

        public async Task ResolvePendingClaimsAsync(List<string> newContractNumbers)
        {
            if (newContractNumbers == null || !newContractNumbers.Any())
            {
                return;
            }

            var claims = await _claimRepository.GetUnresolvedByContractNumbersAsync(newContractNumbers);
            if (!claims.Any())
            {
                return;
            }

            var contracts = await _contractRepository.GetByContractNumbersAsync(claims.Select(c => c.ContractNumber).ToList());
            var contractDict = contracts.ToDictionary(c => c.ContractNumber);

            var claimsToUpdate = new List<Models.PendingContractClaim>();

            foreach (var claim in claims)
            {
                if (contractDict.TryGetValue(claim.ContractNumber, out var contract))
                {
                    // Assign to the contract
                    contract.UserId = claim.UserId;
                    contract.MatriculaId = claim.MatriculaId;
                    
                    // Mark claim as resolved
                    claim.IsResolved = true;
                    claim.ResolvedAt = DateTime.UtcNow;
                    
                    claimsToUpdate.Add(claim);
                }
            }

            if (claimsToUpdate.Any())
            {
                await _claimRepository.UpdateBatchAsync(claimsToUpdate);
                // The contracts themselves are updated automatically via EF tracking
                // assuming they were loaded within the same context, but it's safer to ensure context saves.
                // Here we just call SaveChangesAsync on the context to persist the contract changes
                await _context.SaveChangesAsync();
            }
        }
    }
}
