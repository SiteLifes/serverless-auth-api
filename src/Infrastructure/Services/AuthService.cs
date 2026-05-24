using System.Net.Http.Headers;
using System.Text;
using Domain.Constants;
using Domain.Domains;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private const string ReviewBypassPhone = "5555555555";
    private const string ReviewBypassOtp = "11111";

    private readonly IAuthRepository _authRepository;
    private readonly IOptionsSnapshot<JwtOptions> _jwtOptionsSnapshot;
    private readonly IOptionsSnapshot<AllowedPhonesOptions> _allowedPhonesOptions;
    private readonly IMessageService _messageService;
    private readonly ISmsProviderFactory _smsProviderFactory;
    private readonly ICryptoService _cryptoService;
    private readonly IEventBusManager _eventBusManager;
    private readonly HttpClient _httpClient;
    private readonly IOptionsSnapshot<OtpSecurityOptions> _otpSecurityOptions;


    public AuthService(IAuthRepository authRepository, IOptionsSnapshot<JwtOptions> jwtOptionsSnapshot,
        IMessageService messageService, ISmsProviderFactory smsProviderFactory, ICryptoService cryptoService,
        IOptionsSnapshot<AllowedPhonesOptions> allowedPhonesOptions, IEventBusManager eventBusManager,
        HttpClient httpClient, IOptionsSnapshot<OtpSecurityOptions> otpSecurityOptions)
    {
        _authRepository = authRepository;
        _jwtOptionsSnapshot = jwtOptionsSnapshot;
        _messageService = messageService;
        _smsProviderFactory = smsProviderFactory;
        _cryptoService = cryptoService;
        _allowedPhonesOptions = allowedPhonesOptions;
        _eventBusManager = eventBusManager;
        _httpClient = httpClient;
        _otpSecurityOptions = otpSecurityOptions;
    }

    public async Task<OtpSendResult> SendLoginOtpAsync(string? userId, string phone, string culture, bool isRegistered,
        string? ipAddress, CancellationToken cancellationToken = default)
    {
        var sendRateLimitResult = await EnforceOtpSendRateLimitAsync(phone, ipAddress, cancellationToken);
        if (!sendRateLimitResult.IsSuccess)
        {
            return sendRateLimitResult;
        }

        var otpEntity = await _authRepository.CreateLoginOtpAsync(userId, phone, cancellationToken);
        var message = await _messageService.GetMessageAsync(culture, MessageKeys.OTPSms, cancellationToken);

        var messagePayload = $"SiteLifes giriş şifreniz : {otpEntity.Otp}";
        if (message != null)
        {
            messagePayload = string.Format(message.Message, otpEntity.Otp);
        }

        await _eventBusManager.LoginOtpRequestedAsync(userId, phone, otpEntity.Otp, isRegistered, cancellationToken);
        var smsSent = await _smsProviderFactory.SendSms(phone, messagePayload, cancellationToken);
        return smsSent ? OtpSendResult.Success() : new OtpSendResult { IsSuccess = false };
    }

    public async Task<string?> FindUserByPhone(string phone, CancellationToken cancellationToken)
    {
        var entity = await _authRepository.GetPhoneUserMapAsync(phone, cancellationToken);
        return entity?.UserId;
    }

    public async Task<string?> FindUserByEmail(string email, CancellationToken cancellationToken = default)
    {
        var entity = await _authRepository.GetEmailUserMapAsync(email, cancellationToken);
        return entity?.UserId;
    }

    public async Task<bool> CheckUserPassword(string userId, string password,
        CancellationToken cancellationToken = default)
    {
        var entity = await _authRepository.GetPasswordUserMapAsync(userId, cancellationToken);

        return entity?.Password == _cryptoService.HashPassword(password);
    }

    public async Task<OtpVerificationResult> VerifyOtpAsync(string phone, string otp, CancellationToken cancellationToken)
    {
        if (IsReviewBypass(phone, otp))
        {
            return OtpVerificationResult.Success();
        }

        return await VerifyOtpCoreAsync($"login:{phone}",
            () => _authRepository.GetLoginOtpAsync(phone, otp, cancellationToken), cancellationToken);
    }

    public async Task<OtpVerificationResult> VerifyForgotPasswordOtpAsync(string email, string otp,
        CancellationToken cancellationToken = default)
    {
        return await VerifyOtpCoreAsync($"password-reset:{email}",
            () => _authRepository.GetForgotPasswordOtpAsync(email, otp, cancellationToken), cancellationToken);
    }

    public async Task CreateRefreshTokenAsync(string userId, string token, CancellationToken cancellationToken)
    {
        var entity = new RefreshTokenEntity
        {
            UserId = userId,
            RefreshToken = token,
            ExpireAt = DateTime.UtcNow.AddDays(_jwtOptionsSnapshot.Value.RefreshExpireDays)
        };

        await _authRepository.CreateRefreshTokenAsync(entity, cancellationToken);
    }

    public async Task DeleteRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await _authRepository.DeleteRefreshTokenAsync(refreshToken, cancellationToken);
    }

    public async Task<bool> CreatePhoneUserMapping(string phone, string userId,
        CancellationToken cancellationToken = default)
    {
        var entity = new UserPhoneMapEntity()
        {
            Phone = phone,
            UserId = userId
        };

        await _authRepository.CreatePhoneUserMapAsync(entity, cancellationToken);
        return true;
    }

    public async Task CreateEmailUserMapping(string email, string userId, CancellationToken cancellationToken)
    {
        await _authRepository.CreateEmailUserMapAsync(new UserEmailMapEntity
        {
            UserId = userId,
            Email = email
        }, cancellationToken);
    }

    public async Task CreatePasswordUserMapping(string userId, string password, CancellationToken cancellationToken)
    {
        List<UserPasswordMapEntity> olderPasswords = await _authRepository.GetUserPasswords(userId, cancellationToken);
        await _authRepository.DeletePasswords(olderPasswords, cancellationToken);
        await _authRepository.CreatePasswordUserMapAsync(new UserPasswordMapEntity
        {
            UserId = userId,
            Password = _cryptoService.HashPassword(password)
        }, cancellationToken);
    }

    public async Task SendForgetPasswordOtp(string userId, string requestEmail, CancellationToken cancellationToken)
    {
        var otp = new Random().Next(11111, 55555);
        var otpEntity =
            await _authRepository.CreateForgotPasswordOtpAsync(userId, requestEmail, otp.ToString(), cancellationToken);

        await _eventBusManager.ForgetPasswordOtpRequestedAsync(userId, otpEntity.Otp, cancellationToken);
    }

    public async Task<bool> ResetPasswordAsync(string userId, string email, string otp, string password,
        CancellationToken cancellationToken)
    {
        var otpVerificationResult = await VerifyForgotPasswordOtpAsync(email, otp, cancellationToken);
        if (!otpVerificationResult.IsSuccess)
        {
            return false;
        }
        await CreatePasswordUserMapping(userId, password, cancellationToken);
        return true;
    }

    private async Task<OtpVerificationResult> VerifyOtpCoreAsync(string attemptKey,
        Func<Task<OtpEntity?>> verifyOtpFunc, CancellationToken cancellationToken)
    {
        var attempts = await _authRepository.GetOtpAttemptAsync(attemptKey, cancellationToken) ?? new OtpAttemptEntity
        {
            Key = attemptKey,
            FailedAttempts = 0
        };

        var now = DateTime.UtcNow;
        if (attempts.LockedUntilUtc.HasValue && attempts.LockedUntilUtc.Value > now)
        {
            return OtpVerificationResult.Locked();
        }

        if (attempts.LockedUntilUtc.HasValue && attempts.LockedUntilUtc.Value <= now)
        {
            attempts.LockedUntilUtc = null;
            attempts.FailedAttempts = 0;
            await _authRepository.UpsertOtpAttemptAsync(attempts, cancellationToken);
        }

        var otpEntity = await verifyOtpFunc();
        if (otpEntity != null)
        {
            await _authRepository.DeleteOtpAttemptAsync(attemptKey, cancellationToken);
            return OtpVerificationResult.Success();
        }

        attempts.FailedAttempts++;
        if (attempts.FailedAttempts >= _otpSecurityOptions.Value.MaxFailedAttempts)
        {
            attempts.LockedUntilUtc = now.AddMinutes(_otpSecurityOptions.Value.LockoutMinutes);
            await _authRepository.UpsertOtpAttemptAsync(attempts, cancellationToken);
            return OtpVerificationResult.Locked();
        }

        await _authRepository.UpsertOtpAttemptAsync(attempts, cancellationToken);
        return OtpVerificationResult.Invalid();
    }

    public async Task<bool> DeleteAllUserDataAsync(string userId, string email, string phone,
        CancellationToken cancellationToken)
    {
        var phoneUserMapAsync = _authRepository.GetPhoneUserMapAsync(phone, cancellationToken);
        var emailUserMapAsync = _authRepository.GetEmailUserMapAsync(email, cancellationToken);
        var userPasswordMapAsync = _authRepository.GetPasswordUserMapAsync(userId, cancellationToken);
        var userRefreshTokenMappingAsync = _authRepository.GetUserRefreshTokenMappingsAsync(userId, cancellationToken);

        await Task.WhenAll(phoneUserMapAsync, emailUserMapAsync, userPasswordMapAsync, userRefreshTokenMappingAsync);
        var phoneUserMap = await phoneUserMapAsync;
        var emailUserMap = await emailUserMapAsync;
        var userPasswordMap = await userPasswordMapAsync;
        var userRefreshTokenMapping = await userRefreshTokenMappingAsync;

        var entities = new List<IEntity>();
        if (phoneUserMap != null && phoneUserMap.UserId == userId)
        {
            entities.Add(phoneUserMap);
        }

        if (emailUserMap != null && emailUserMap.UserId == userId)
        {
            entities.Add(emailUserMap);
        }

        if (userPasswordMap != null && userPasswordMap.UserId == userId)
        {
            entities.Add(userPasswordMap);
        }

        if (userRefreshTokenMapping.Any())
        {
            entities.AddRange(userRefreshTokenMapping);
            entities.AddRange(userRefreshTokenMapping.Where(q => q.ExpireAt > DateTime.UtcNow).Select(q =>
                new RefreshTokenEntity
                {
                    RefreshToken = q.RefreshToken
                }));
        }

        await _authRepository.BatchDeleteAsync(entities, cancellationToken);
        return true;
    }

    private bool IsReviewBypass(string phone, string otp)
    {
        return _otpSecurityOptions.Value.OnReview &&
               phone == ReviewBypassPhone &&
               otp == ReviewBypassOtp;
    }

    private async Task<OtpSendResult> EnforceOtpSendRateLimitAsync(string phone, string? ipAddress,
        CancellationToken cancellationToken)
    {
        var phoneAttemptResult = await CheckOtpSendAttemptAsync(
            $"otp-send:phone:{phone}",
            _otpSecurityOptions.Value.MaxSendAttemptsPerPhoneWindow,
            _otpSecurityOptions.Value.SendWindowMinutes,
            cancellationToken);

        if (!phoneAttemptResult.IsSuccess)
        {
            return phoneAttemptResult;
        }

        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return OtpSendResult.Success();
        }

        return await CheckOtpSendAttemptAsync(
            $"otp-send:ip:{ipAddress}",
            _otpSecurityOptions.Value.MaxSendAttemptsPerIpWindow,
            _otpSecurityOptions.Value.SendWindowMinutes,
            cancellationToken);
    }

    private async Task<OtpSendResult> CheckOtpSendAttemptAsync(string key, int maxAttempts, int windowMinutes,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var attempt = await _authRepository.GetOtpAttemptAsync(key, cancellationToken);

        if (attempt == null || !attempt.LockedUntilUtc.HasValue || attempt.LockedUntilUtc.Value <= now)
        {
            await _authRepository.UpsertOtpAttemptAsync(new OtpAttemptEntity
            {
                Key = key,
                FailedAttempts = 1,
                LockedUntilUtc = now.AddMinutes(windowMinutes)
            }, cancellationToken);

            return OtpSendResult.Success();
        }

        if (attempt.FailedAttempts < maxAttempts)
        {
            attempt.FailedAttempts++;
            await _authRepository.UpsertOtpAttemptAsync(attempt, cancellationToken);
            return OtpSendResult.Success();
        }

        var retryAfter = (int)Math.Ceiling((attempt.LockedUntilUtc.Value - now).TotalSeconds);
        return OtpSendResult.RateLimited(Math.Max(1, retryAfter));
    }

    public async Task<bool> UpdateUserPhoneMappingAsync(string userId, string? oldPhone, string phone,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(oldPhone))
        {
            var userPhoneMapping = await _authRepository.GetPhoneUserMapAsync(oldPhone, cancellationToken);
            if (userPhoneMapping != null && userPhoneMapping.UserId == userId)
            {
                await _authRepository.DeletePhoneUserMapAsync(userPhoneMapping, cancellationToken);
            }
        }


        await _authRepository.CreatePhoneUserMapAsync(new UserPhoneMapEntity()
        {
            UserId = userId,
            Phone = phone
        }, cancellationToken);
        return true;
    }

    public async Task<bool> UpdateUserEmailMappingAsync(string userId, string? oldEmail, string email,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(oldEmail))
        {
            var mapping = await _authRepository.GetEmailUserMapAsync(oldEmail, cancellationToken);
            if (mapping != null && mapping.UserId == userId)
            {
                await _authRepository.DeleteEmailUserMapAsync(mapping, cancellationToken);
            }
        }


        await _authRepository.CreateEmailUserMapAsync(new UserEmailMapEntity
        {
            UserId = userId,
            Email = email
        }, cancellationToken);
        return true;
    }

    public async Task<string> SendSms(string PhoneNumber, string message,CancellationToken cancellationToken = default)
    {
        var smsRequest = new
        {
            username = "902162351556",
            password = "LVF!856qxy", // Şifrenizi buraya ekleyin
            valid_for = "00:01",
            messages = new[]
                {
                    new
                    {
                        msg = message,
                        dest = PhoneNumber,
                    },
                }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(smsRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using (var httpClient = new HttpClient())
        {
            var apiUrl = "https://sms.verimor.com.tr/v2/send.json";
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                return "";
            }

            var errorResponse = await response.Content.ReadAsStringAsync();

            return errorResponse;
        }
    }
}