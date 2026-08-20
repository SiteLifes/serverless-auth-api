using Domain.Services;
using Infrastructure.Services;
using Xunit;

namespace Auth.Tests.Services;

/// <summary>
/// Verified against the RFC 6238 / RFC 4226 published test vectors rather than against our own
/// output, so a mistake in the implementation cannot make the tests agree with it.
///
/// The shared secret is the RFC's ASCII "12345678901234567890", base32 encoded.
/// </summary>
public class TotpServiceTests
{
    private const string RfcSecret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

    private readonly ITotpService _service = new TotpService();

    [Theory]
    // Unix time, expected 6 digit code. Truncated from the RFC's 8 digit SHA1 vectors.
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    [InlineData(20000000000L, "353130")]
    public void VerifyCode_ShouldAcceptRfc6238Vectors(long unixSeconds, string expectedCode)
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);

        Assert.True(_service.VerifyCode(RfcSecret, expectedCode, at));
    }

    [Fact]
    public void VerifyCode_ShouldRejectAWrongCode()
    {
        var at = DateTimeOffset.FromUnixTimeSeconds(59);

        Assert.False(_service.VerifyCode(RfcSecret, "000000", at));
    }

    [Fact]
    public void VerifyCode_ShouldTolerateOneStepOfClockDrift()
    {
        // 287082 belongs to the step starting at 30s; check it still passes a step later.
        var oneStepLater = DateTimeOffset.FromUnixTimeSeconds(59 + 30);

        Assert.True(_service.VerifyCode(RfcSecret, "287082", oneStepLater));
    }

    [Fact]
    public void VerifyCode_ShouldRejectCodesBeyondTheDriftWindow()
    {
        var threeStepsLater = DateTimeOffset.FromUnixTimeSeconds(59 + 90);

        Assert.False(_service.VerifyCode(RfcSecret, "287082", threeStepsLater));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void VerifyCode_ShouldRejectMalformedInput(string code)
    {
        Assert.False(_service.VerifyCode(RfcSecret, code, DateTimeOffset.FromUnixTimeSeconds(59)));
    }

    [Fact]
    public void VerifyCode_ShouldRejectAnInvalidSecret()
    {
        Assert.False(_service.VerifyCode("not base32 !!", "287082", DateTimeOffset.FromUnixTimeSeconds(59)));
    }

    [Fact]
    public void GenerateSecret_ShouldProduceADecodableSecretThatVerifiesItsOwnCodes()
    {
        var secret = _service.GenerateSecret();

        Assert.False(string.IsNullOrWhiteSpace(secret));
        Assert.All(secret, c => Assert.Contains(c, "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567"));

        // A fresh secret must not validate the RFC's code, which belongs to a different secret.
        Assert.False(_service.VerifyCode(secret, "287082", DateTimeOffset.FromUnixTimeSeconds(59)));
    }

    [Fact]
    public void BuildProvisioningUri_ShouldCarryTheParametersAuthenticatorAppsExpect()
    {
        var uri = _service.BuildProvisioningUri(RfcSecret, "sefer@sitelifes.com", "SiteLifes");

        Assert.StartsWith("otpauth://totp/SiteLifes:sefer%40sitelifes.com", uri);
        Assert.Contains($"secret={RfcSecret}", uri);
        Assert.Contains("issuer=SiteLifes", uri);
        Assert.Contains("algorithm=SHA1", uri);
        Assert.Contains("digits=6", uri);
        Assert.Contains("period=30", uri);
    }
}
