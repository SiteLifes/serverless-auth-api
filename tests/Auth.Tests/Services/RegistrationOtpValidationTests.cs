using Api.Endpoints.V1.Register;
using Xunit;

namespace Auth.Tests.Services;

public class RegistrationOtpValidationTests
{
    [Fact]
    public void RequestOtp_RequiresCaptchaAndValidMobileNumber()
    {
        var validator = new RequestOtp.RequestOtpModelValidator();

        Assert.True(validator.Validate(new RequestOtp.RequestOtpModel("+90 555 123 45 67", "captcha-token")).IsValid);
        Assert.False(validator.Validate(new RequestOtp.RequestOtpModel("123", "captcha-token")).IsValid);
        Assert.False(validator.Validate(new RequestOtp.RequestOtpModel("5551234567", "")).IsValid);
    }
}
