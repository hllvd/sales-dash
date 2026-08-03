using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IUserMatriculaRepository
    {
        Task<List<UserMatricula>> GetAllAsync();
        Task<UserMatricula?> GetByIdAsync(int id);
        Task<List<UserMatricula>> GetByUserIdAsync(Guid userId);
        Task<List<UserMatricula>> GetActiveByUserIdAsync(Guid userId);
        Task<UserMatricula?> GetByMatriculaIdAsync(int matriculaId);
        Task<List<UserMatricula>> GetAllByMatriculaIdAsync(int matriculaId);
        Task<UserMatricula?> GetByMatriculaNumberAndUserIdAsync(string matriculaNumber, Guid userId);
        Task<UserMatricula> CreateAsync(UserMatricula matricula);
        Task<UserMatricula> UpdateAsync(UserMatricula matricula);
        Task DeleteAsync(int id);
        Task<bool> IsMatriculaValidForUser(Guid userId, int matriculaId);
        Task<UserMatricula?> GetOwnerByMatriculaIdAsync(int matriculaId);
        Task SetOwnerAsync(int matriculaId, Guid newOwnerId);
        Task<Dictionary<string, DateTime>> GetLastUpdateByMatriculaNumberAsync();
    }
}
