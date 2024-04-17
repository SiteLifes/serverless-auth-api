using Domain.Constants;
using Domain.Entities;
using Domain.Entities.Base;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IAuthRepository _authRepository;
    private readonly IOptionsSnapshot<JwtOptions> _jwtOptionsSnapshot;
    private readonly IOptionsSnapshot<AllowedPhonesOptions> _allowedPhonesOptions;
    private readonly IMessageService _messageService;
    private readonly ISmsProviderFactory _smsProviderFactory;
    private readonly ICryptoService _cryptoService;
    private readonly IEventBusManager _eventBusManager;

    public AuthService(IAuthRepository authRepository, IOptionsSnapshot<JwtOptions> jwtOptionsSnapshot,
        IMessageService messageService, ISmsProviderFactory smsProviderFactory, ICryptoService cryptoService,
        IOptionsSnapshot<AllowedPhonesOptions> allowedPhonesOptions, IEventBusManager eventBusManager)
    {
        _authRepository = authRepository;
        _jwtOptionsSnapshot = jwtOptionsSnapshot;
        _messageService = messageService;
        _smsProviderFactory = smsProviderFactory;
        _cryptoService = cryptoService;
        _allowedPhonesOptions = allowedPhonesOptions;
        _eventBusManager = eventBusManager;
    }

    public async Task<bool> SendLoginOtpAsync(string? userId, string phone, string culture,bool isRegistered,
        CancellationToken cancellationToken = default)
    {
        var otpEntity = await _authRepository.CreateLoginOtpAsync(userId, phone, cancellationToken);
        var message = await _messageService.GetMessageAsync(culture, MessageKeys.OTPSms, cancellationToken);

        var messagePayload = $"Code {otpEntity.Otp}";
        if (message != null)
        {
            messagePayload = string.Format(message.Message, otpEntity.Otp);
        }

        await _eventBusManager.LoginOtpRequestedAsync(userId, phone, otpEntity.Otp,isRegistered, cancellationToken);
        return await _smsProviderFactory.SendSms(phone, messagePayload, cancellationToken);
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

    public async Task<bool> VerifyOtpAsync(string phone, string otp, CancellationToken cancellationToken)
    {
        var entity = await _authRepository.GetLoginOtpAsync(phone, otp, cancellationToken);
        if (entity == null)
        {
            if (_allowedPhonesOptions.Value.Phones.Contains(phone) || _allowedPhonesOptions.Value.AllowAll)
            {
                return otp == _allowedPhonesOptions.Value.Code;
            }
        }

        return entity != null;
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
        var otpEntity = await _authRepository.GetForgotPasswordOtpAsync(email, otp, cancellationToken);
        if (otpEntity == null)
        {
            return false;
        }
        await CreatePasswordUserMapping(userId, password, cancellationToken);
        return true;
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
}