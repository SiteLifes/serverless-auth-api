using Amazon.DynamoDBv2;
using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Repositories.Base;

namespace Infrastructure.Repositories;

public class MessageRepository : DynamoRepository, IMessageRepository
{
    public MessageRepository(IAmazonDynamoDB dynamoDb) : base(dynamoDb)
    {
    }

    public async Task<MessagesEntity?> GetMessageAsync(string culture, string key, CancellationToken cancellationToken = default)
    {
        var entity = await GetAsync<MessagesEntity>(MessagesEntity.GetPk(culture), key, cancellationToken);

        return entity;
    }

    public async Task<bool> SaveMessageAsync(MessagesEntity entity, CancellationToken cancellationToken = default)
    {
        return await SaveAsync(entity, cancellationToken);
    }
    protected override string GetTableName()
    {
        return GetEnvironmentTableName("auth");
    }
}