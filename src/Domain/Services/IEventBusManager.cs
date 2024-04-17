namespace Domain.Services;

public interface IEventBusManager
{
    Task<bool> LoginOtpRequestedAsync(string? userId, string phone, string code,bool isRegistered, CancellationToken cancellationToken);
    Task<bool> ForgetPasswordOtpRequestedAsync(string userId, string code, CancellationToken cancellationToken);
    Task<bool> LogoutUserAsync(string userId, string deviceId, CancellationToken cancellationToken);
}