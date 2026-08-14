using System.Net;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Repositories;
using Infrastructure.Extensions;
using Infrastructure.Repositories.Base;

namespace Infrastructure.Repositories;

public class AuthRepository : DynamoRepository, IAuthRepository
{
    private const int MaxTransactionItems = 100;
    private readonly IAmazonDynamoDB _dynamoDb;

    public AuthRepository(IAmazonDynamoDB dynamoDb) : base(dynamoDb)
    {
        _dynamoDb = dynamoDb;
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
    
    public async Task<RefreshTokenUserMapping?> GetLoginAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entity = await GetAllAsync<RefreshTokenUserMapping>(RefreshTokenUserMapping.GetPk(userId), cancellationToken);
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

    public async Task<OtpAttemptEntity?> GetOtpAttemptAsync(string key, CancellationToken cancellationToken = default)
    {
        return await GetAsync<OtpAttemptEntity>(OtpAttemptEntity.GetPk(key), OtpAttemptEntity.GetSk(), cancellationToken);
    }

    public async Task<OtpAttemptEntity> UpsertOtpAttemptAsync(OtpAttemptEntity entity,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);
        return entity;
    }

    public async Task DeleteOtpAttemptAsync(string key, CancellationToken cancellationToken = default)
    {
        await DeleteAsync(OtpAttemptEntity.GetPk(key), OtpAttemptEntity.GetSk(), cancellationToken);
    }

    public async Task<RefreshTokenEntity> CreateRefreshTokenAsync(RefreshTokenEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);

        return entity;
    }

    public async Task<UserPhoneMapEntity?> GetPhoneUserMapAsync(string phone, CancellationToken cancellationToken = default)
    {
        var entities = await GetPhoneUserMapsAsync(phone, cancellationToken);
        var userIds = entities
            .Select(entity => entity.UserId)
            .Where(userId => !string.IsNullOrWhiteSpace(userId))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (userIds.Count != 1)
        {
            return null;
        }

        return entities.FirstOrDefault(entity => entity.Sk == UserPhoneMapEntity.CanonicalSortKey)
               ?? entities.FirstOrDefault();
    }

    public async Task<IReadOnlyList<UserPhoneMapEntity>> GetPhoneUserMapsAsync(string phone,
        CancellationToken cancellationToken = default)
    {
        var entities = await GetAllAsync<UserPhoneMapEntity>(UserPhoneMapEntity.GetPk(phone), cancellationToken);
        return entities
            .Where(entity => !string.IsNullOrWhiteSpace(entity.UserId))
            .ToList();
    }

    public Task<bool> TryCreatePhoneUserMapAsync(UserPhoneMapEntity entity,
        CancellationToken cancellationToken = default)
    {
        return TryReplacePhoneUserMapAsync(entity.UserId, entity.Phone, entity.Phone, cancellationToken);
    }

    public async Task<bool> TryReplacePhoneUserMapAsync(string userId, string? oldPhone, string phone,
        CancellationToken cancellationToken = default)
    {
        var newMappings = await GetPhoneUserMapsAsync(phone, cancellationToken);
        if (newMappings.Any(mapping => !string.Equals(mapping.UserId, userId, StringComparison.Ordinal)))
        {
            return false;
        }

        IReadOnlyList<UserPhoneMapEntity> oldMappings = Array.Empty<UserPhoneMapEntity>();
        if (!string.IsNullOrWhiteSpace(oldPhone))
        {
            oldMappings = string.Equals(oldPhone, phone, StringComparison.Ordinal)
                ? newMappings
                : await GetPhoneUserMapsAsync(oldPhone, cancellationToken);
        }

        var canonicalMapping = new UserPhoneMapEntity
        {
            Phone = phone,
            UserId = userId,
            Sk = UserPhoneMapEntity.CanonicalSortKey
        };

        var transactionItems = new List<TransactWriteItem>
        {
            CreateCanonicalPhoneMappingPut(canonicalMapping)
        };

        transactionItems.AddRange(oldMappings
            .Where(mapping => string.Equals(mapping.UserId, userId, StringComparison.Ordinal))
            .Where(mapping => !IsSameKey(mapping, canonicalMapping))
            .Select(CreatePhoneMappingDelete));

        ValidateTransactionSize(transactionItems.Count);

        try
        {
            var response = await _dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems = transactionItems
            }, cancellationToken);

            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (TransactionCanceledException exception) when (HasConditionalCheckFailure(exception))
        {
            return false;
        }
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

    public async Task DeletePhoneUserMapsAsync(string phone, string userId, CancellationToken cancellationToken)
    {
        var mappings = await GetPhoneUserMapsAsync(phone, cancellationToken);
        var transactionItems = mappings
            .Where(mapping => string.Equals(mapping.UserId, userId, StringComparison.Ordinal))
            .Select(CreatePhoneMappingDelete)
            .ToList();

        if (transactionItems.Count == 0)
        {
            return;
        }

        ValidateTransactionSize(transactionItems.Count);
        await _dynamoDb.TransactWriteItemsAsync(new TransactWriteItemsRequest
        {
            TransactItems = transactionItems
        }, cancellationToken);
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
    
    public async Task UserLoginAsync(UserLoginEntity entity, CancellationToken cancellationToken = default)
    {
        await SaveAsync(entity, cancellationToken);
    }
    
    public async Task<List<UserLoginEntity>> GetUserLoginAsync(string userId, CancellationToken cancellationToken)
    {
        return await GetAllAsync<UserLoginEntity>(UserLoginEntity.GetPk(userId), cancellationToken);
    }

    private TransactWriteItem CreateCanonicalPhoneMappingPut(UserPhoneMapEntity mapping)
    {
        return new TransactWriteItem
        {
            Put = new Put
            {
                TableName = GetTableName(),
                Item = mapping.ToAttributeMap(),
                ConditionExpression = "attribute_not_exists(#pk) OR #userId = :userId",
                ExpressionAttributeNames = new Dictionary<string, string>
                {
                    ["#pk"] = "pk",
                    ["#userId"] = "userId"
                },
                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    [":userId"] = new() { S = mapping.UserId }
                }
            }
        };
    }

    private TransactWriteItem CreatePhoneMappingDelete(UserPhoneMapEntity mapping)
    {
        var delete = new Delete
        {
            TableName = GetTableName(),
            Key = new Dictionary<string, AttributeValue>
            {
                ["pk"] = new() { S = UserPhoneMapEntity.GetPk(mapping.Phone) },
                ["sk"] = new() { S = mapping.Sk }
            }
        };

        if (mapping.Sk == UserPhoneMapEntity.CanonicalSortKey)
        {
            delete.ConditionExpression = "attribute_not_exists(#pk) OR #userId = :userId";
            delete.ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#pk"] = "pk",
                ["#userId"] = "userId"
            };
            delete.ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":userId"] = new() { S = mapping.UserId }
            };
        }

        return new TransactWriteItem { Delete = delete };
    }

    private static bool IsSameKey(UserPhoneMapEntity first, UserPhoneMapEntity second)
    {
        return string.Equals(first.Phone, second.Phone, StringComparison.Ordinal)
               && string.Equals(first.Sk, second.Sk, StringComparison.Ordinal);
    }

    private static void ValidateTransactionSize(int itemCount)
    {
        if (itemCount > MaxTransactionItems)
        {
            throw new InvalidOperationException(
                $"Phone mapping repair requires {itemCount} transaction items; DynamoDB supports at most {MaxTransactionItems}.");
        }
    }

    private static bool HasConditionalCheckFailure(TransactionCanceledException exception)
    {
        if (exception.CancellationReasons?.Any(reason => string.Equals(
                reason.Code,
                "ConditionalCheckFailed",
                StringComparison.Ordinal)) == true)
        {
            return true;
        }

        return exception.Message.Contains("ConditionalCheckFailed", StringComparison.Ordinal);
    }

    protected override string GetTableName()
    {
        return GetEnvironmentTableName("auth");
    }
}
