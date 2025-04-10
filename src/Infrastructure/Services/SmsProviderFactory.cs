using Domain.Services;

namespace Infrastructure.Services;

public class SmsProviderFactory : ISmsProviderFactory
{
    private readonly IEnumerable<ISmsProvider> _otpProviders;

    public SmsProviderFactory(IEnumerable<ISmsProvider> otpProviders)
    {
        _otpProviders = otpProviders;
    }

    private ISmsProvider Instance()
    {
        return _otpProviders.ToList()[1];
    }

    public async Task<bool> SendSms(string phone, string message, CancellationToken cancellationToken)
    {
        var instance = Instance();
        return await instance.SendSms(phone, message, cancellationToken);
    }
}