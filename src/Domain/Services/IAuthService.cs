namespace Domain.Services;

public interface IAuthService
{
    Task<bool> SendLoginOtpAsync(string? userId, string phone, string culture,bool isRegistered, CancellationToken cancellationToken = default);
    Task<string?> FindUserByPhone(string phone, CancellationToken cancellationToken = default);
    Task<string?> FindUserByEmail(string email, CancellationToken cancellationToken = default);
    Task<bool> CheckUserPassword(string userId, string password, CancellationToken cancellationToken = default!);
    Task<bool> VerifyOtpAsync(string phone, string otp, CancellationToken cancellationToken = default);
    Task CreateRefreshTokenAsync(string userId, string token, CancellationToken cancellationToken = default);
    Task DeleteRefreshTokenAsync(string requestRefreshToken, CancellationToken cancellationToken = default);
    Task<bool> CreatePhoneUserMapping(string phone, string userId, CancellationToken cancellationToken = default);
    Task CreateEmailUserMapping(string email, string userId, CancellationToken cancellationToken);
    Task CreatePasswordUserMapping(string userId, string password, CancellationToken cancellationToken);
    Task SendForgetPasswordOtp(string userId, string email, CancellationToken cancellationToken);
    Task<bool> ResetPasswordAsync(string userId, string email, string otp, string password, CancellationToken cancellationToken);

    Task<bool> DeleteAllUserDataAsync(string userId, string email, string phone, CancellationToken cancellationToken);
    Task<bool> UpdateUserPhoneMappingAsync(string userId,string? oldPhone, string phone, CancellationToken cancellationToken);
    Task<bool> UpdateUserEmailMappingAsync(string userId,string? oldEmail, string email, CancellationToken cancellationToken);
}