namespace SalesApp.Services
{
    public interface IPendingClaimService
    {
        Task ResolvePendingClaimsAsync(List<string> newContractNumbers);
    }
}
