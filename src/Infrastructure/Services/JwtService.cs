using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Domains;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly IAuthRepository _authRepository;
    private readonly IOptionsSnapshot<JwtOptions> _jwtOptionsSnapshot;

    public JwtService(IAuthRepository authRepository, IOptionsSnapshot<JwtOptions> jwtOptionsSnapshot)
    {
        _authRepository = authRepository;
        _jwtOptionsSnapshot = jwtOptionsSnapshot;
    }

    public async Task<JwtDto> CreateJwtAsync(string userId, CancellationToken cancellationToken = default)
    {
        var jwt = GenerateJwt(userId);
        var refreshToken = Guid.NewGuid().ToString("N");

        var entities = new List<IEntity>();

        var expireAt = DateTime.UtcNow.AddDays(_jwtOptionsSnapshot.Value.RefreshExpireDays);
        entities.Add(new RefreshTokenEntity
        {
            UserId = userId,
            RefreshToken = refreshToken,
            ExpireAt = expireAt
        });
        entities.Add(new RefreshTokenUserMapping
        {
            UserId = userId,
            RefreshToken = refreshToken,
            ExpireAt = expireAt
        });

        await _authRepository.BatchSaveAsync(entities, cancellationToken);


        return new JwtDto(jwt, refreshToken);
    }

    public async Task<string?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var refreshTokenEntity = await _authRepository.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (refreshTokenEntity == null || refreshTokenEntity.ExpireAt < DateTime.UtcNow)
        {
            return null;
        }
        refreshTokenEntity.ExpireAt = DateTime.UtcNow.AddMinutes(_jwtOptionsSnapshot.Value.ExpireMinutes);
        return refreshTokenEntity?.UserId;
    }

    private string GenerateJwt(string userId)
    {
        var jwtOptions = _jwtOptionsSnapshot.Value;
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtOptions.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("userId", userId),
                new Claim(ClaimTypes.Actor, "Login"),
                new Claim(ClaimTypes.Authentication, "Login"),
                new Claim(ClaimTypes.UserData, userId),
            }),
            Expires = DateTime.UtcNow.AddMinutes(jwtOptions.ExpireMinutes),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Audience = jwtOptions.Audience,
            Issuer = jwtOptions.Issuer
        };

        var jwtHandler = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(jwtHandler);

        return jwt;
    }
}