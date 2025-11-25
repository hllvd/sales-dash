using SalesApp.DTOs;
using SalesApp.Models;

namespace SalesApp.Services
{
    public interface IUserMatchingService
    {
        Task<List<UserResponse>> FindUserMatchesAsync(string name, string surname);
        Task<User> CreateUserFromImportAsync(string name, string surname, string email, Guid createdByUserId);
    }
}
