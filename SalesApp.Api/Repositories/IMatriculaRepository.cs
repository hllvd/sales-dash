using SalesApp.Models;

namespace SalesApp.Repositories
{
    public interface IMatriculaRepository
    {
        Task<Matricula?> GetByIdAsync(int id);
        Task<Matricula?> GetByMatriculaNumberAsync(string matriculaNumber);
        Task<IEnumerable<Matricula>> GetAllAsync();
        Task<Matricula> CreateAsync(Matricula matricula);
        Task<Matricula> UpdateAsync(Matricula matricula);
        Task DeleteAsync(int id);
        Task<IEnumerable<Matricula>> GetByImportSessionIdAsync(int sessionId);
    }
}
