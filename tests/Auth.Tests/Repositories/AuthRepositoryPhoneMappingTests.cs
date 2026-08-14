using System.Collections.Concurrent;
using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Domain.Entities;
using Infrastructure.Extensions;
using Infrastructure.Repositories;
using Xunit;

namespace Auth.Tests.Repositories;

public sealed class AuthRepositoryPhoneMappingTests
{
    [Fact]
    public async Task GetPhoneUserMapAsync_WhenPhoneHasDifferentOwners_ShouldFailClosed()
    {
        using var dynamoDb = new PhoneMappingDynamoDbClient();
        dynamoDb.AddMapping("5077663021", "user-a", "user-a");
        dynamoDb.AddMapping("5077663021", "user-b", "user-b");
        var repository = new AuthRepository(dynamoDb);

        var mapping = await repository.GetPhoneUserMapAsync("5077663021", CancellationToken.None);

        Assert.Null(mapping);
    }

    [Fact]
    public async Task TryCreatePhoneUserMapAsync_ShouldMigrateOwnedLegacyMappingToCanonicalKey()
    {
        using var dynamoDb = new PhoneMappingDynamoDbClient();
        dynamoDb.AddMapping("5077663021", "user-a", "user-a");
        var repository = new AuthRepository(dynamoDb);

        var created = await repository.TryCreatePhoneUserMapAsync(new UserPhoneMapEntity
        {
            Phone = "5077663021",
            UserId = "user-a"
        }, CancellationToken.None);

        Assert.True(created);
        var request = Assert.Single(dynamoDb.TransactionRequests);
        var put = Assert.Single(request.TransactItems, item => item.Put is not null).Put;
        Assert.Equal(UserPhoneMapEntity.CanonicalSortKey, put.Item["sk"].S);
        Assert.Contains("attribute_not_exists", put.ConditionExpression);

        var delete = Assert.Single(request.TransactItems, item => item.Delete is not null).Delete;
        Assert.Equal("user-a", delete.Key["sk"].S);
    }

    [Fact]
    public async Task ConcurrentCreates_ForSamePhone_ShouldAllowOnlyOneOwner()
    {
        using var dynamoDb = new PhoneMappingDynamoDbClient();
        var firstRepository = new AuthRepository(dynamoDb);
        var secondRepository = new AuthRepository(dynamoDb);

        var results = await Task.WhenAll(
            firstRepository.TryCreatePhoneUserMapAsync(new UserPhoneMapEntity
            {
                Phone = "5077663021",
                UserId = "user-a"
            }, CancellationToken.None),
            secondRepository.TryCreatePhoneUserMapAsync(new UserPhoneMapEntity
            {
                Phone = "5077663021",
                UserId = "user-b"
            }, CancellationToken.None));

        Assert.Single(results, result => result);
        Assert.Single(results, result => !result);
    }

    [Fact]
    public async Task TryReplacePhoneUserMapAsync_ShouldDeleteOnlyOldMappingsOwnedByUser()
    {
        using var dynamoDb = new PhoneMappingDynamoDbClient();
        dynamoDb.AddMapping("5077663021", "user-a", "user-a");
        dynamoDb.AddMapping("5077663021", "user-b", "user-b");
        var repository = new AuthRepository(dynamoDb);

        var updated = await repository.TryReplacePhoneUserMapAsync(
            "user-a",
            "5077663021",
            "5348731666",
            CancellationToken.None);

        Assert.True(updated);
        var request = Assert.Single(dynamoDb.TransactionRequests);
        var put = Assert.Single(request.TransactItems, item => item.Put is not null).Put;
        Assert.Equal("5348731666", put.Item["phone"].S);
        Assert.Equal("user-a", put.Item["userId"].S);

        var delete = Assert.Single(request.TransactItems, item => item.Delete is not null).Delete;
        Assert.Equal(UserPhoneMapEntity.GetPk("5077663021"), delete.Key["pk"].S);
        Assert.Equal("user-a", delete.Key["sk"].S);
    }

    private sealed class PhoneMappingDynamoDbClient : AmazonDynamoDBClient
    {
        private readonly ConcurrentDictionary<string, List<Dictionary<string, AttributeValue>>> _queryItems =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _canonicalOwners = new(StringComparer.Ordinal);

        public PhoneMappingDynamoDbClient()
            : base(new AnonymousAWSCredentials(), RegionEndpoint.EUCentral1)
        {
        }

        public List<TransactWriteItemsRequest> TransactionRequests { get; } = [];

        public void AddMapping(string phone, string userId, string sortKey)
        {
            var mapping = new UserPhoneMapEntity
            {
                Phone = phone,
                UserId = userId,
                Sk = sortKey
            };

            var items = _queryItems.GetOrAdd(mapping.Pk, _ => []);
            lock (items)
            {
                items.Add(mapping.ToAttributeMap());
            }
        }

        public override Task<QueryResponse> QueryAsync(
            QueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var pk = request.ExpressionAttributeValues[":pk"].S;
            var items = _queryItems.TryGetValue(pk, out var storedItems)
                ? storedItems.ToList()
                : [];

            return Task.FromResult(new QueryResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Items = items,
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });
        }

        public override Task<TransactWriteItemsResponse> TransactWriteItemsAsync(
            TransactWriteItemsRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (TransactionRequests)
            {
                TransactionRequests.Add(request);
                var put = request.TransactItems.Single(item => item.Put is not null).Put;
                var key = $"{put.Item["pk"].S}|{put.Item["sk"].S}";
                var userId = put.Item["userId"].S;

                if (_canonicalOwners.TryGetValue(key, out var owner)
                    && !string.Equals(owner, userId, StringComparison.Ordinal))
                {
                    return Task.FromException<TransactWriteItemsResponse>(
                        new TransactionCanceledException("ConditionalCheckFailed"));
                }

                _canonicalOwners[key] = userId;
            }

            return Task.FromResult(new TransactWriteItemsResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            });
        }
    }
}
