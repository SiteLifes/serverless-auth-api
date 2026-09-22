using Domain.Domains;
using Domain.Entities;

namespace Domain.Services;

public interface IJwtService
{
    Task<JwtDto> CreateJwtAsync(string userId, CancellationToken cancellationToken = default,
        string? replacesRefreshToken = null);
    Task<JwtDto> CreateStaffJwtAsync(StaffEntity staff, CancellationToken cancellationToken = default,
        string? replacesRefreshToken = null);
    Task<RefreshTokenValidation> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
}
