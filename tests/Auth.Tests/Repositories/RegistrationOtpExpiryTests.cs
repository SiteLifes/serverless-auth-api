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

public class RegistrationOtpExpiryTests
{
    [Theory]
    [InlineData(-1, false)]
    [InlineData(60, true)]
    public async Task GetLoginOtpAsync_RejectsExpiredCodeEvenIfDynamoHasNotDeletedIt(int secondsFromNow, bool valid)
    {
        using var dynamo = new OtpDynamoDbClient(new OtpEntity
        {
            Key = "5551234567", Otp = "12345",
            Ttl = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + secondsFromNow
        });
        var repository = new AuthRepository(dynamo);

        var otp = await repository.GetLoginOtpAsync("5551234567", "12345");

        Assert.Equal(valid, otp is not null);
    }

    private sealed class OtpDynamoDbClient(OtpEntity otp) : AmazonDynamoDBClient(
        new AnonymousAWSCredentials(), RegionEndpoint.EUCentral1)
    {
        public override Task<GetItemResponse> GetItemAsync(GetItemRequest request,
            CancellationToken cancellationToken = default) => Task.FromResult(new GetItemResponse
        {
            HttpStatusCode = HttpStatusCode.OK,
            Item = request.Key["pk"].S == otp.Pk && request.Key["sk"].S == otp.Sk
                ? otp.ToAttributeMap()
                : new Dictionary<string, AttributeValue>()
        });
    }
}
