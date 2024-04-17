using Amazon.DynamoDBv2;
using Amazon.Extensions.Configuration.SystemsManager;
using Amazon.SimpleNotificationService;
using Domain.External;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Context;
using Infrastructure.External;
using Infrastructure.Repositories;
using Infrastructure.Services;
using Infrastructure.Services.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Extensions;

public static class StartupExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection service, ConfigurationManager configuration)
    {
        service.AddScoped<IAuthService, AuthService>();
        service.AddSingleton<IAuthRepository, AuthRepository>();
        service.AddScoped<IMessageService, MessageService>();
        service.AddSingleton<IMessageRepository, MessageRepository>();
        service.AddScoped<ICaptchaService, CaptchaService>();
        service.AddScoped<ICryptoService, CryptoService>();
        service.AddScoped<ISmsProviderFactory, SmsProviderFactory>();
        service.AddScoped<ISmsProvider, MockSmsProvider>();
        service.AddScoped<ISmsProvider, NetGsmSmsProvider>();
        service.AddScoped<ISmsProvider, TwilioSmsProvider>();
        service.AddScoped<IJwtService, JwtService>();
        service.AddScoped<IEventBusManager, EventBusManager>();
        service.AddAWSService<IAmazonDynamoDB>();
        service.AddAWSLambdaHosting(Environment.GetEnvironmentVariable("ApiGatewayType") == "RestApi" ? LambdaEventSource.RestApi : LambdaEventSource.HttpApi);
        service.AddScoped<IApiContext, ApiContext>();
        
        service.AddHttpClient<IGoogleHttpClient, GoogleHttpClient>();
        service.AddHttpClient<IMicrosoftHttpClient, MicrosoftHttpClient>();

        service.AddAWSService<IAmazonSimpleNotificationService>();
        service.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        service.Configure<PasswordSalt>(configuration.GetSection("PasswordSalt"));
        service.Configure<CaptchaOptions>(configuration.GetSection("CaptchaSettings"));
        var smsSection = configuration.GetSection("SmsProviders");
        service.Configure<NetGsmOptions>(smsSection.GetSection("NetGsm"));
        service.Configure<TwilioOptions>(smsSection.GetSection("Twilio"));
        service.Configure<ApiKeyValidationSettings>(configuration.GetSection("ApiKeyValidationSettings"));
        service.Configure<AllowedPhonesOptions>(configuration.GetSection("AllowedPhones"));
        service.Configure<EventBusSettings>(configuration.GetSection("EventBusSettings"));
        service.Configure<GoogleAuthOptions>(configuration.GetSection("GoogleAuthOptions"));
        service.Configure<MicrosoftAuthOptions>(configuration.GetSection("MicrosoftAuthOptions"));


        configuration.AddSystemsManager(config =>
        {
            config.Path = $"/auth-api";
            config.ReloadAfter = TimeSpan.FromMinutes(5);
            config.ParameterProcessor = new JsonParameterProcessor();
        });

        return service;
    }
}