using Domain.Entities;

namespace Domain.Repositories;

public interface IStaffRepository
{
    Task<StaffEntity?> GetByIdAsync(string staffId, CancellationToken cancellationToken = default);

    Task<StaffEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<List<StaffEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the account and its email lookup together.</summary>
    Task SaveAsync(StaffEntity entity, CancellationToken cancellationToken = default);
}
