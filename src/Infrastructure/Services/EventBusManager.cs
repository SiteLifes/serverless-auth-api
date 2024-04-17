using System.Net;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Domain.Domains;
using Domain.Options;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class EventBusManager : IEventBusManager
{
    private readonly IAmazonSimpleNotificationService _amazonSimpleNotificationService;
    private readonly IOptionsSnapshot<EventBusSettings> _eventBusSettingsOptions;

    public EventBusManager(IAmazonSimpleNotificationService amazonSimpleNotificationService,
        IOptionsSnapshot<EventBusSettings> eventBusSettingsOptions)
    {
        _amazonSimpleNotificationService = amazonSimpleNotificationService;
        _eventBusSettingsOptions = eventBusSettingsOptions;
    }


    public async Task<bool> LoginOtpRequestedAsync(string? userId, string phone, string code, bool isRegistered,
        CancellationToken cancellationToken)
    {
        return await PublishAsync(
            new EventModel<object>("LoginOtpRequested",
                new { UserId = userId, Code = code, Phone = phone, IsRegistered = isRegistered }),
            cancellationToken);
    }

    public async Task<bool> ForgetPasswordOtpRequestedAsync(string userId, string code,
        CancellationToken cancellationToken)
    {
        return await PublishAsync(
            new EventModel<object>("ForgetPasswordOtpRequested", new { UserId = userId, Code = code }),
            cancellationToken);
    }

    public async Task<bool> LogoutUserAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        return await PublishAsync(
            new EventModel<object>("UserLoggedOut", new { UserId = userId, deviceId = deviceId }),
            cancellationToken);
    }

    private async Task<bool> PublishAsync(EventModel<object> eventModel, CancellationToken cancellationToken = default)
    {
        if (!_eventBusSettingsOptions.Value.IsEnabled)
            return true;

        var message = JsonSerializer.Serialize(eventModel);
        var snsResponse = await _amazonSimpleNotificationService.PublishAsync(_eventBusSettingsOptions.Value.TopicArn,
            message, cancellationToken);
        return snsResponse.HttpStatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted or HttpStatusCode.Created;
    }
}