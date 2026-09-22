using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Enum;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Extensions;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace Auth.Tests.Services;

/// <summary>
/// A refresh used to cut the token it consumed to one hour. When the client never received the
/// replacement — a lost response, an app suspended while the auth Lambda cold-started — its next
/// launch sent that old token and the user was logged out. These pin the rotation that replaced it:
/// a token stays valid until the token issued in exchange for it is first used.
/// </summary>
public class RefreshTokenRotationTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task Using_a_token_does_not_shorten_it()
    {
        var (service, tokens) = Create();
        var expireAt = DateTime.UtcNow.AddDays(30);
        tokens.Seed("A", UserId, expireAt);

        Assert.Equal(UserId, (await service.ValidateRefreshTokenAsync("A")).UserId);

        Assert.Equal(expireAt, tokens.Get("A")!.ExpireAt);
    }

    [Fact]
    public async Task Old_token_still_works_when_the_replacement_never_reached_the_client()
    {
        var (service, tokens) = Create();
        tokens.Seed("A", UserId, DateTime.UtcNow.AddDays(30));

        await service.ValidateRefreshTokenAsync("A");
        await service.CreateJwtAsync(UserId, replacesRefreshToken: "A");
        // The response is lost, so the client comes back with the only token it has.

        Assert.Equal(UserId, (await service.ValidateRefreshTokenAsync("A")).UserId);
    }

    [Fact]
    public async Task Using_the_replacement_revokes_the_token_it_replaced()
    {
        var (service, tokens) = Create();
        tokens.Seed("A", UserId, DateTime.UtcNow.AddDays(30));

        await service.ValidateRefreshTokenAsync("A");
        var issued = await service.CreateJwtAsync(UserId, replacesRefreshToken: "A");

        Assert.Equal(UserId, (await service.ValidateRefreshTokenAsync(issued.RefreshToken)).UserId);
        Assert.Equal(RefreshTokenFailure.NotFound, (await service.ValidateRefreshTokenAsync("A")).Failure);
    }

    [Fact]
    public async Task Staff_refresh_records_the_token_it_replaces()
    {
        var (service, tokens) = Create();
        var staff = new StaffEntity
        {
            Id = "staff-1",
            Email = "someone@sitelifes.com",
            FullName = "Someone",
            Roles = [StaffRole.Admin]
        };

        var issued = await service.CreateStaffJwtAsync(staff, replacesRefreshToken: "A");

        Assert.Equal("A", tokens.Get(issued.RefreshToken)!.ReplacesRefreshToken);
    }

    [Fact]
    public async Task Expired_token_is_still_refused()
    {
        var (service, tokens) = Create();
        tokens.Seed("A", UserId, DateTime.UtcNow.AddMinutes(-10));

        Assert.Equal(RefreshTokenFailure.Expired, (await service.ValidateRefreshTokenAsync("A")).Failure);
    }

    [Fact]
    public void Replaced_token_survives_the_attribute_map_round_trip()
    {
        var entity = new RefreshTokenEntity
        {
            RefreshToken = "B",
            UserId = UserId,
            ExpireAt = DateTime.UtcNow.AddDays(30),
            ReplacesRefreshToken = "A"
        };

        var read = entity.ToAttributeMap().ToEntity<RefreshTokenEntity>();

        Assert.Equal("A", read.ReplacesRefreshToken);
    }

    [Fact]
    public void Token_stored_before_the_field_existed_reads_without_a_predecessor()
    {
        // Every token issued before this change looks like this, and must keep working.
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = "RefreshToken" },
            ["sk"] = new AttributeValue { S = "A" },
            ["refreshToken"] = new AttributeValue { S = "A" },
            ["userId"] = new AttributeValue { S = UserId },
            ["expireAt"] = new AttributeValue { S = "2026-10-01T00:00:00Z" },
            ["ttl"] = new AttributeValue { N = "1790812800" }
        };

        Assert.Null(item.ToEntity<RefreshTokenEntity>().ReplacesRefreshToken);
    }

    private static (IJwtService Service, InMemoryRefreshTokens Tokens) Create()
    {
        var tokens = new InMemoryRefreshTokens();
        var repository = DispatchProxy.Create<IAuthRepository, RefreshTokenOnlyRepository>();
        ((RefreshTokenOnlyRepository)(object)repository).Tokens = tokens;

        var service = new JwtService(
            repository,
            new Snapshot<JwtOptions>(new JwtOptions
            {
                Secret = new string('k', 64),
                Issuer = "test",
                Audience = "test",
                ExpireMinutes = 60,
                RefreshExpireDays = 30
            }),
            new Snapshot<StaffAuthOptions>(new StaffAuthOptions()));

        return (service, tokens);
    }

    internal sealed class InMemoryRefreshTokens
    {
        private readonly Dictionary<string, RefreshTokenEntity> _items = new();

        public void Seed(string token, string userId, DateTime expireAt) =>
            _items[token] = new RefreshTokenEntity { RefreshToken = token, UserId = userId, ExpireAt = expireAt };

        public RefreshTokenEntity? Get(string token) => _items.GetValueOrDefault(token);
        public void Save(RefreshTokenEntity entity) => _items[entity.RefreshToken] = entity;
        public void Delete(string token) => _items.Remove(token);
    }

    // IAuthRepository has thirty members and these tests only touch refresh-token storage, so any
    // other call throws instead of quietly returning a default.
    public class RefreshTokenOnlyRepository : DispatchProxy
    {
        internal InMemoryRefreshTokens Tokens = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod!.Name)
            {
                case nameof(IAuthRepository.GetRefreshTokenAsync):
                    return Task.FromResult(Tokens.Get((string)args![0]!));
                case nameof(IAuthRepository.CreateRefreshTokenAsync):
                    var entity = (RefreshTokenEntity)args![0]!;
                    Tokens.Save(entity);
                    return Task.FromResult(entity);
                case nameof(IAuthRepository.DeleteRefreshTokenAsync):
                    Tokens.Delete((string)args![0]!);
                    return Task.CompletedTask;
                case nameof(IAuthRepository.BatchSaveAsync):
                    foreach (var saved in ((List<IEntity>)args![0]!).OfType<RefreshTokenEntity>())
                        Tokens.Save(saved);
                    return Task.CompletedTask;
                default:
                    throw new NotSupportedException(targetMethod.Name);
            }
        }
    }

    private sealed class Snapshot<T>(T value) : IOptionsSnapshot<T> where T : class
    {
        public T Value => value;
        public T Get(string? name) => value;
    }
}
