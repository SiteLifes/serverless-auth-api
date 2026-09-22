using Api.Endpoints.V1.Register;
using Xunit;

namespace Auth.Tests.Services;

public class RegistrationOtpValidationTests
{
    [Fact]
    public void RequestOtp_RequiresValidMobileNumber()
    {
        var validator = new RequestOtp.RequestOtpModelValidator();

        Assert.True(validator.Validate(new RequestOtp.RequestOtpModel("+90 555 123 45 67")).IsValid);
        Assert.False(validator.Validate(new RequestOtp.RequestOtpModel("123")).IsValid);
    }
}
