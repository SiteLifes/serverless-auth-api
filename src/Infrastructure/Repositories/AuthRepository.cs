using Amazon.DynamoDBv2;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Repositories;
using Infrastructure.Repositories.Base;

namespace Infrastructure.Repositories;

public class AuthRepository : DynamoRepository, IAuthRepository
{
    public AuthRepository(IAmazonDynamoDB dynamoDb) : base(dynamoDb)
    {
    }


    public async Task<OtpEntity> CreateLoginOtpAsync(string? userId, string phone, CancellationToken cancellationToken = default)
    {
        var entity = new OtpEntity
        {
            UserId = userId,
            Otp = new Random().Next(10000, 99999).ToString(),
            Key = phone
        };

        await SaveAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<OtpEntity?> GetLoginOtpAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync<OtpEntity>(OtpEntity.GetPk(phone), code, cancellationToken);

        if (entity == null || entity.Otp != code)
        {
            return null;
        }

        return entity;
    }
    
    public async Task<OtpEntity?> GetLoginAsync(string phone, CancellationToken cancellationToken = default)
    {
        var entity = await GetAllAsync<OtpEntity>(OtpEntity.GetPk(phone), cancellationToken);
        return entity.FirstOrDefault();
    }

    public async Task<OtpEntity> CreateForgotPasswordOtpAsync(string? userId, string email, string otp, CancellationToken cancellationToken = default)
    {
        var entity = new OtpEntity
        {
            UserId = userId,
            Otp = otp,
            Key = email
        };

        await SaveAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<OtpEntity?> GetForgotPasswordOtpAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync<OtpEntity>(OtpEntity.GetPk(email), code, cancellationToken);

        if (entity == null || entity.Otp != code)
        {
            return null;
        }

        return entity;
    }

    public async Task<RefreshTokenEntity> CreateRefreshTokenAsync(RefreshTokenEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<UserPhoneMapEntity?> GetPhoneUserMapAsync(string phone, CancellationToken cancellationToken = default)
    {
        var entities = await GetAllAsync<UserPhoneMapEntity>(UserPhoneMapEntity.GetPk(phone), cancellationToken);

        return entities.FirstOrDefault();
    }

    public async Task<UserPhoneMapEntity> CreatePhoneUserMapAsync(UserPhoneMapEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<UserEmailMapEntity> CreateEmailUserMapAsync(UserEmailMapEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);
        return entity;
    }

    public async Task<UserPasswordMapEntity> CreatePasswordUserMapAsync(UserPasswordMapEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);
        return entity;
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await DeleteAsync(RefreshTokenEntity.GetPk(), refreshToken, cancellationToken);
    }

    public async Task<RefreshTokenEntity?> GetRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return await GetAsync<RefreshTokenEntity>(RefreshTokenEntity.GetPk(), refreshToken, cancellationToken);
    }

    public async Task<UserPasswordMapEntity?> GetPasswordUserMapAsync(string userId, CancellationToken cancellationToken)
    {
        var entities = await GetAllAsync<UserPasswordMapEntity>(UserPasswordMapEntity.GetPk(userId), cancellationToken);
        return entities.FirstOrDefault();
    }

    public async Task<UserEmailMapEntity?> GetEmailUserMapAsync(string email, CancellationToken cancellationToken)
    {
        var entities = await GetAllAsync<UserEmailMapEntity>(UserEmailMapEntity.GetPk(email), cancellationToken);
        return entities.FirstOrDefault();
    }

    public async Task<List<UserPasswordMapEntity>> GetUserPasswords(string userId, CancellationToken cancellationToken)
    {
        return await GetAllAsync<UserPasswordMapEntity>(UserPasswordMapEntity.GetPk(userId), cancellationToken);
    }

    public async Task DeletePasswords(List<UserPasswordMapEntity> olderPasswords, CancellationToken cancellationToken)
    {
        await BatchWriteAsync(new List<IEntity>(), olderPasswords.Select(q => (IEntity) q).ToList(), cancellationToken);
    }

    public async Task DeletePhoneUserMapAsync(UserPhoneMapEntity phoneUserMap, CancellationToken cancellationToken)
    {
        await DeleteAsync(UserPhoneMapEntity.GetPk(phoneUserMap.Phone), phoneUserMap.UserId, cancellationToken);
    }

    public async Task DeleteEmailUserMapAsync(UserEmailMapEntity emailUserMap, CancellationToken cancellationToken)
    {
        await DeleteAsync(UserEmailMapEntity.GetPk(emailUserMap.Email), emailUserMap.UserId, cancellationToken);
    }

    public async Task DeletePasswordUserMapAsync(UserPasswordMapEntity userPasswordMap, CancellationToken cancellationToken)
    {
        await DeleteAsync(UserPasswordMapEntity.GetPk(userPasswordMap.UserId), userPasswordMap.Password, cancellationToken);
    }

    public async Task BatchSaveAsync(List<IEntity> entities, CancellationToken cancellationToken)
    {
        await BatchWriteAsync(entities, new List<IEntity>(), cancellationToken);
    }

    public async Task<List<RefreshTokenUserMapping>> GetUserRefreshTokenMappingsAsync(string userId, CancellationToken cancellationToken)
    {
        return await GetAllAsync<RefreshTokenUserMapping>(RefreshTokenUserMapping.GetPk(userId), cancellationToken);
    }

    public async Task BatchDeleteAsync(List<IEntity> entities, CancellationToken cancellationToken)
    {
        await BatchWriteAsync(new List<IEntity>(), entities, cancellationToken);
    }

    protected override string GetTableName()
    {
        return $"auth";
    }
}