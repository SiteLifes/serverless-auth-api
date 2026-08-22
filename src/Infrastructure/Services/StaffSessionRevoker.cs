using Domain.Entities;
using Domain.Entities.Base;
using Domain.Repositories;
using Domain.Services;

namespace Infrastructure.Services;

public class StaffSessionRevoker : IStaffSessionRevoker
{
    private readonly IAuthRepository _authRepository;

    public StaffSessionRevoker(IAuthRepository authRepository)
    {
        _authRepository = authRepository;
    }

    public async Task<int> RevokeAllAsync(string staffId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return 0;
        }

        var mappings = await _authRepository.GetUserRefreshTokenMappingsAsync(staffId, cancellationToken);
        if (mappings.Count == 0)
        {
            return 0;
        }

        // A refresh token is stored twice: once under the token, once under the user. Deleting only
        // the mapping would leave the token itself usable.
        var entities = new List<IEntity>();
        foreach (var mapping in mappings)
        {
            entities.Add(mapping);
            entities.Add(new RefreshTokenEntity
            {
                UserId = staffId,
                RefreshToken = mapping.RefreshToken,
                ExpireAt = mapping.ExpireAt
            });
        }

        await _authRepository.BatchDeleteAsync(entities, cancellationToken);

        return mappings.Count;
    }
}
