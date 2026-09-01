using Domain.Entities;
using Domain.Entities.Base;

namespace Domain.Repositories;

public interface IAuthRepository
{
    /// <param name="issuedByStaffId">
    /// Set when a staff member had the code minted without it being sent anywhere. Marks the code
    /// as theirs and, while it lasts, suppresses delivery of any further login code to that phone.
    /// </param>
    Task<OtpEntity> CreateLoginOtpAsync(string? userId, string phone, string? issuedByStaffId = null,
        CancellationToken cancellationToken = default);

    Task<OtpEntity?> GetLoginOtpAsync(string phone, string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// The live staff-issued login code for this phone, or null when there is none. Codes that have
    /// passed their expiry are ignored even if Dynamo has not swept them yet.
    /// </summary>
    Task<OtpEntity?> GetActiveStaffIssuedLoginOtpAsync(string phone, CancellationToken cancellationToken = default);
    Task<RefreshTokenUserMapping?> GetLoginAsync(string userId, CancellationToken cancellationToken = default);

    Task<OtpEntity> CreateForgotPasswordOtpAsync(string? userId, string email, string otp,
        CancellationToken cancellationToken = default);

    Task<OtpEntity?> GetForgotPasswordOtpAsync(string email, string code,
        CancellationToken cancellationToken = default);

    Task<OtpAttemptEntity?> GetOtpAttemptAsync(string key, CancellationToken cancellationToken = default);
    Task<OtpAttemptEntity> UpsertOtpAttemptAsync(OtpAttemptEntity entity, CancellationToken cancellationToken = default);
    Task DeleteOtpAttemptAsync(string key, CancellationToken cancellationToken = default);

    Task<RefreshTokenEntity> CreateRefreshTokenAsync(RefreshTokenEntity entity,
        CancellationToken cancellationToken = default);

    Task<UserPhoneMapEntity?> GetPhoneUserMapAsync(string phone, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserPhoneMapEntity>> GetPhoneUserMapsAsync(string phone,
        CancellationToken cancellationToken = default);

    Task<bool> TryCreatePhoneUserMapAsync(UserPhoneMapEntity entity,
        CancellationToken cancellationToken = default);

    Task<bool> TryReplacePhoneUserMapAsync(string userId, string? oldPhone, string phone,
        CancellationToken cancellationToken = default);

    Task<UserEmailMapEntity> CreateEmailUserMapAsync(UserEmailMapEntity entity,
        CancellationToken cancellationToken = default);

    Task<UserPasswordMapEntity> CreatePasswordUserMapAsync(UserPasswordMapEntity entity,
        CancellationToken cancellationToken = default);

    Task DeleteRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<RefreshTokenEntity?> GetRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<UserPasswordMapEntity?> GetPasswordUserMapAsync(string userId, CancellationToken cancellationToken);
    Task<UserEmailMapEntity?> GetEmailUserMapAsync(string email, CancellationToken cancellationToken);
    Task<List<UserPasswordMapEntity>> GetUserPasswords(string userId, CancellationToken cancellationToken);
    Task DeletePasswords(List<UserPasswordMapEntity> olderPasswords, CancellationToken cancellationToken);
    Task DeletePhoneUserMapsAsync(string phone, string userId, CancellationToken cancellationToken);
    Task DeleteEmailUserMapAsync(UserEmailMapEntity emailUserMap, CancellationToken cancellationToken);
    Task DeletePasswordUserMapAsync(UserPasswordMapEntity userPasswordMap, CancellationToken cancellationToken);
    Task BatchSaveAsync(List<IEntity> entities, CancellationToken cancellationToken);

    Task<List<RefreshTokenUserMapping>> GetUserRefreshTokenMappingsAsync(string userId,
        CancellationToken cancellationToken);

    Task BatchDeleteAsync(List<IEntity> entities, CancellationToken cancellationToken);
    Task UserLoginAsync(UserLoginEntity entity, CancellationToken cancellationToken = default);
    Task<List<UserLoginEntity>> GetUserLoginAsync(string userId, CancellationToken cancellationToken = default);
}
