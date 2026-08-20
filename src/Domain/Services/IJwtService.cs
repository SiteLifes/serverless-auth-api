using Domain.Domains;
using Domain.Entities;

namespace Domain.Services;

public interface IJwtService
{
    Task<JwtDto> CreateJwtAsync(string userId, CancellationToken cancellationToken = default);
    Task<JwtDto> CreateStaffJwtAsync(StaffEntity staff, CancellationToken cancellationToken = default);
    Task<string?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken= default);
}