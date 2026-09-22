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

    public async Task<JwtDto> CreateJwtAsync(string userId, CancellationToken cancellationToken = default,
        string? replacesRefreshToken = null)
    {
        var jwt = GenerateJwt(userId);
        var refreshToken = Guid.NewGuid().ToString("N");

        var entities = new List<IEntity>();

        var expireAt = DateTime.UtcNow.AddDays(_jwtOptionsSnapshot.Value.RefreshExpireDays);
        entities.Add(new RefreshTokenEntity
        {
            UserId = userId,
            RefreshToken = refreshToken,
            ExpireAt = expireAt,
            ReplacesRefreshToken = replacesRefreshToken
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

    public async Task<JwtDto> CreateStaffJwtAsync(StaffEntity staff, CancellationToken cancellationToken = default,
        string? replacesRefreshToken = null)
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
                ExpireAt = expireAt,
                ReplacesRefreshToken = replacesRefreshToken
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

    public async Task<RefreshTokenValidation> ValidateRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var refreshTokenEntity = await _authRepository.GetRefreshTokenAsync(refreshToken, cancellationToken);
        if (refreshTokenEntity == null)
        {
            return RefreshTokenValidation.Invalid(RefreshTokenFailure.NotFound);
        }

        if (refreshTokenEntity.ExpireAt.AddMinutes(5) < DateTime.UtcNow)
        {
            return RefreshTokenValidation.Invalid(RefreshTokenFailure.Expired);
        }

        // The token being used is not shortened: the client may never receive the replacement this
        // refresh issues (a lost response, an app suspended mid-request), and cutting this token to an
        // hour used to log those users out on their next launch. Rotation happens one step later:
        // using a token proves the client holds it, so the token it replaced is revoked now.
        if (!string.IsNullOrEmpty(refreshTokenEntity.ReplacesRefreshToken))
        {
            await _authRepository.DeleteRefreshTokenAsync(refreshTokenEntity.ReplacesRefreshToken, cancellationToken);
        }

        return RefreshTokenValidation.Valid(refreshTokenEntity.UserId);
    }

    private string GenerateStaffJwt(StaffEntity staff)
    {
        var jwtOptions = _jwtOptionsSnapshot.Value;
        var staffOptions = _staffAuthOptionsSnapshot.Value;

        var claims = StaffTokenClaims.Build(staff);

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
