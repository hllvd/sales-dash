using System.Security.Claims;
using System.Threading.Tasks;
using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IUserScopeService
    {
        Task<UserScopeContext> GetContractScopeAsync(ClaimsPrincipal user);
    }
}
