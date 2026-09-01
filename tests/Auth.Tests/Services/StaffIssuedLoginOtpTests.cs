using System.Collections.Concurrent;
using System.Net;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Domain.Domains;
using Domain.Entities;
using Domain.Extensions;
using Domain.Options;
using Domain.Services;
using Infrastructure.Extensions;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Auth.Tests.Services;

/// <summary>
/// A staff member can have a login code minted from the back office without it being delivered, so
/// support can read it out to whoever is on the phone. These cover the half of that promise that is
/// easy to lose: for as long as the issued code lives, the ordinary login endpoint must stay silent,
/// or typing the number into a login screen texts the person the panel deliberately did not text.
/// </summary>
public sealed class StaffIssuedLoginOtpTests
{
    private const string Phone = "5077663021";

    [Fact]
    public async Task SendLoginOtpAsync_WhileAStaffIssuedCodeIsValid_ShouldDeliverNothing()
    {
        using var dynamoDb = new LoginOtpDynamoDbClient();
        dynamoDb.AddLoginOtp(Phone, "12345", TimeSpan.FromMinutes(4), issuedByStaffId: "staff-1");
        var sms = new RecordingSmsProviderFactory();
        var eventBus = new RecordingEventBusManager();
        var service = CreateService(dynamoDb, sms, eventBus);

        var result = await service.SendLoginOtpAsync("user-1", Phone, "tr-TR", true, "1.2.3.4");

        Assert.True(result.IsSuccess);
        Assert.Empty(sms.Sent);
        Assert.Empty(eventBus.Published);

        // Nor is a second code written: the one the caller is holding stays the only one anybody
        // in the conversation knows about.
        Assert.Empty(dynamoDb.PutRequests);
    }

    [Fact]
    public async Task SendLoginOtpAsync_OnceTheStaffIssuedCodeHasExpired_ShouldSendAgain()
    {
        using var dynamoDb = new LoginOtpDynamoDbClient();
        // Dynamo sweeps expired items whenever it gets round to it, so an hours-dead code can still
        // be sitting in the partition. The window is what the stored expiry says, not what is there.
        dynamoDb.AddLoginOtp(Phone, "12345", TimeSpan.FromMinutes(-1), issuedByStaffId: "staff-1");
        var sms = new RecordingSmsProviderFactory();
        var eventBus = new RecordingEventBusManager();
        var service = CreateService(dynamoDb, sms, eventBus);

        var result = await service.SendLoginOtpAsync("user-1", Phone, "tr-TR", true, "1.2.3.4");

        Assert.True(result.IsSuccess);
        Assert.Single(sms.Sent);
        Assert.Single(eventBus.Published);
    }

    [Fact]
    public async Task SendLoginOtpAsync_WhenTheLiveCodesAreTheResidentsOwn_ShouldSend()
    {
        using var dynamoDb = new LoginOtpDynamoDbClient();
        dynamoDb.AddLoginOtp(Phone, "12345", TimeSpan.FromMinutes(4), issuedByStaffId: null);
        var sms = new RecordingSmsProviderFactory();
        var eventBus = new RecordingEventBusManager();
        var service = CreateService(dynamoDb, sms, eventBus);

        await service.SendLoginOtpAsync("user-1", Phone, "tr-TR", true, "1.2.3.4");

        Assert.Single(sms.Sent);
    }

    [Fact]
    public async Task CreateLoginOtpAsync_WhenStaffIssuesIt_ShouldStoreTheStaffIdAndAnExpiryThatSurvivesAReadBack()
    {
        using var dynamoDb = new LoginOtpDynamoDbClient();
        var repository = new AuthRepository(dynamoDb);

        var created = await repository.CreateLoginOtpAsync("user-1", Phone, "staff-1", CancellationToken.None);

        var stored = Assert.Single(dynamoDb.PutRequests);
        Assert.Equal("staff-1", stored.Item["issuedByStaffId"].S);

        var expiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(stored.Item["ttl"].N)).UtcDateTime;
        Assert.InRange(expiry, DateTime.UtcNow.AddMinutes(4), DateTime.UtcNow.AddMinutes(6));

        var active = await repository.GetActiveStaffIssuedLoginOtpAsync(Phone, CancellationToken.None);
        Assert.NotNull(active);
        Assert.Equal(created.Otp, active.Otp);
    }

    private static AuthService CreateService(
        IAmazonDynamoDB dynamoDb,
        ISmsProviderFactory smsProviderFactory,
        IEventBusManager eventBusManager)
    {
        return new AuthService(
            new AuthRepository(dynamoDb),
            new TestOptionsSnapshot<JwtOptions>(new JwtOptions()),
            new NullMessageService(),
            smsProviderFactory,
            new NoopCryptoService(),
            new TestOptionsSnapshot<AllowedPhonesOptions>(new AllowedPhonesOptions()),
            eventBusManager,
            new HttpClient(),
            new TestOptionsSnapshot<OtpSecurityOptions>(new OtpSecurityOptions()),
            NullLogger<AuthService>.Instance);
    }

    private sealed class LoginOtpDynamoDbClient : AmazonDynamoDBClient
    {
        private readonly ConcurrentDictionary<string, List<Dictionary<string, AttributeValue>>> _queryItems =
            new(StringComparer.Ordinal);

        public LoginOtpDynamoDbClient()
            : base(new AnonymousAWSCredentials(), RegionEndpoint.EUCentral1)
        {
        }

        public List<PutItemRequest> PutRequests { get; } = [];

        public void AddLoginOtp(string phone, string otp, TimeSpan expiresIn, string? issuedByStaffId)
        {
            var entity = new OtpEntity
            {
                Key = phone,
                Otp = otp,
                UserId = "user-1",
                IssuedByStaffId = issuedByStaffId,
                Ttl = DateTime.UtcNow.Add(expiresIn).ToUnixTimeSeconds()
            };

            var items = _queryItems.GetOrAdd(entity.Pk, _ => []);
            lock (items)
            {
                items.Add(entity.ToAttributeMap());
            }
        }

        public override Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken cancellationToken = default)
        {
            var pk = request.ExpressionAttributeValues[":pk"].S;
            var items = _queryItems.TryGetValue(pk, out var storedItems) ? storedItems.ToList() : [];

            return Task.FromResult(new QueryResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Items = items,
                LastEvaluatedKey = new Dictionary<string, AttributeValue>()
            });
        }

        public override Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
        {
            // The send path reads OTP attempt counters through this; nothing is stored for them.
            return Task.FromResult(new GetItemResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Item = new Dictionary<string, AttributeValue>()
            });
        }

        public override Task<PutItemResponse> PutItemAsync(PutItemRequest request, CancellationToken cancellationToken = default)
        {
            // Attempt counters share the table; only the codes themselves matter here.
            if (request.Item["pk"].S.StartsWith("LoginOtp#", StringComparison.Ordinal))
            {
                lock (PutRequests)
                {
                    PutRequests.Add(request);
                }

                // Written items have to come back from a query, or nothing can be read back the way
                // the service reads it.
                var items = _queryItems.GetOrAdd(request.Item["pk"].S, _ => []);
                lock (items)
                {
                    items.Add(request.Item);
                }
            }

            return Task.FromResult(new PutItemResponse { HttpStatusCode = HttpStatusCode.OK });
        }
    }

    private sealed class RecordingSmsProviderFactory : ISmsProviderFactory
    {
        public List<(string Phone, string Message)> Sent { get; } = [];

        public Task<bool> SendSms(string phone, string message, CancellationToken cancellationToken)
        {
            Sent.Add((phone, message));
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingEventBusManager : IEventBusManager
    {
        public List<string> Published { get; } = [];

        public Task<bool> LoginOtpRequestedAsync(string? userId, string phone, string code, bool isRegistered,
            CancellationToken cancellationToken)
        {
            Published.Add(code);
            return Task.FromResult(true);
        }

        public Task<bool> ForgetPasswordOtpRequestedAsync(string userId, string code, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<bool> LogoutUserAsync(string userId, string deviceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class NullMessageService : IMessageService
    {
        public Task<MessageDto?> GetMessageAsync(string culture, string key, CancellationToken cancellationToken = default)
            => Task.FromResult<MessageDto?>(null);
    }

    private sealed class NoopCryptoService : ICryptoService
    {
        public string HashPassword(string password) => password;
    }

    private sealed class TestOptionsSnapshot<T> : IOptionsSnapshot<T> where T : class
    {
        public TestOptionsSnapshot(T value) => Value = value;

        public T Value { get; }

        public T Get(string? name) => Value;
    }
}
