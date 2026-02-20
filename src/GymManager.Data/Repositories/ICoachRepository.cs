using GymManager.Domain.Entities;

namespace GymManager.Data.Repositories;

public interface ICoachRepository
{
    Task<List<Coach>> GetListAsync(string? keyword, CancellationToken cancellationToken = default);
    Task<Coach?> GetByEmployeeNoAsync(string employeeNo, CancellationToken cancellationToken = default);
    Task AddAsync(Coach coach, CancellationToken cancellationToken = default);
    Task UpdateAsync(Coach coach, CancellationToken cancellationToken = default);
    Task DeleteAsync(string employeeNo, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string employeeNo, CancellationToken cancellationToken = default);
}

