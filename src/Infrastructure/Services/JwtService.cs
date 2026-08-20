using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Domain.Constants;
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
    private readonly IOptionsSnapshot<StaffAuthOptions> _staffAuthOptionsSnapshot;

    public JwtService(
        IAuthRepository authRepository,
        IOptionsSnapshot<JwtOptions> jwtOptionsSnapshot,
        IOptionsSnapshot<StaffAuthOptions> staffAuthOptionsSnapshot)
    {
        _authRepository = authRepository;
        _jwtOptionsSnapshot = jwtOptionsSnapshot;
        _staffAuthOptionsSnapshot = staffAuthOptionsSnapshot;
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

    public async Task<JwtDto> CreateStaffJwtAsync(StaffEntity staff, CancellationToken cancellationToken = default)
    {
        var jwt = GenerateStaffJwt(staff);
        var refreshToken = Guid.NewGuid().ToString("N");

        var expireAt = DateTime.UtcNow.AddDays(_jwtOptionsSnapshot.Value.RefreshExpireDays);
        var entities = new List<IEntity>
        {
            new RefreshTokenEntity
            {
                UserId = staff.Id,
                RefreshToken = refreshToken,
                ExpireAt = expireAt
            },
            new RefreshTokenUserMapping
            {
                UserId = staff.Id,
                RefreshToken = refreshToken,
                ExpireAt = expireAt
            }
        };

        await _authRepository.BatchSaveAsync(entities, cancellationToken);

        return new JwtDto(jwt, refreshToken);
    }

    public async Task<string?> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var refreshTokenEntity = await _authRepository.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (refreshTokenEntity == null || refreshTokenEntity.ExpireAt.AddMinutes(5) < DateTime.UtcNow)
        {
            return null;
        }
        refreshTokenEntity.ExpireAt = DateTime.UtcNow.AddMinutes(_jwtOptionsSnapshot.Value.ExpireMinutes);
        await _authRepository.CreateRefreshTokenAsync(refreshTokenEntity, cancellationToken);
        return refreshTokenEntity?.UserId;
    }

    private string GenerateStaffJwt(StaffEntity staff)
    {
        var jwtOptions = _jwtOptionsSnapshot.Value;
        var staffOptions = _staffAuthOptionsSnapshot.Value;

        var claims = new List<Claim>
        {
            new(AuthClaims.UserId, staff.Id),
            new(AuthClaims.UserType, AuthClaims.UserTypes.Staff),
            new(AuthClaims.FullName, staff.FullName),
            new(ClaimTypes.Actor, "StaffLogin"),
            new(ClaimTypes.Authentication, "Login"),
            new(ClaimTypes.UserData, staff.Id)
        };

        claims.AddRange(staff.Roles.Select(role => new Claim(AuthClaims.Role, role.ToString())));

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(jwtOptions.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            // Staff tokens expire sooner than resident tokens: they can reach every site.
            Expires = DateTime.UtcNow.AddMinutes(staffOptions.ExpireMinutes),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
            Audience = jwtOptions.Audience,
            Issuer = jwtOptions.Issuer
        };

        return tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
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
                new Claim(AuthClaims.UserId, userId),
                new Claim(AuthClaims.UserType, AuthClaims.UserTypes.Resident),
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