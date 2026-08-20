using Amazon.DynamoDBv2;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Repositories;
using Infrastructure.Repositories.Base;

namespace Infrastructure.Repositories;

public class StaffRepository : DynamoRepository, IStaffRepository
{
    public StaffRepository(IAmazonDynamoDB dynamoDb) : base(dynamoDb)
    {
    }

    public async Task<StaffEntity?> GetByIdAsync(string staffId, CancellationToken cancellationToken = default)
    {
        return await GetAsync<StaffEntity>(StaffEntity.GetPk(), staffId, cancellationToken);
    }

    public async Task<StaffEntity?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var mappings = await GetAllAsync<StaffEmailMapEntity>(
            StaffEmailMapEntity.GetPk(email),
            cancellationToken);

        var staffId = mappings.FirstOrDefault()?.StaffId;
        if (string.IsNullOrWhiteSpace(staffId))
        {
            return null;
        }

        return await GetByIdAsync(staffId, cancellationToken);
    }

    public async Task<List<StaffEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await GetAllAsync<StaffEntity>(StaffEntity.GetPk(), cancellationToken);
    }

    public async Task SaveAsync(StaffEntity entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAt = DateTime.UtcNow;

        await BatchWriteAsync(
            new List<IEntity>
            {
                entity,
                new StaffEmailMapEntity { Email = entity.Email, StaffId = entity.Id }
            },
            new List<IEntity>(),
            cancellationToken);
    }

    protected override string GetTableName()
    {
        return GetEnvironmentTableName("auth");
    }
}
