using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Domain.Options;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Providers;

public class NetGsmSmsProvider : ISmsProvider
{
    private const string OtpUrl = "https://api.netgsm.com.tr/sms/send/otp";
    private const string SmsUrl = "https://api.netgsm.com.tr/sms/rest/v2/send";
    private const string BasicAuthToken = "ODUwODQwMjg3NTozQjczLTZF";

    private readonly IOptionsSnapshot<NetGsmOptions> _netGsmOptionsSnapshot;

    public NetGsmSmsProvider(IOptionsSnapshot<NetGsmOptions> netGsmOptionsSnapshot)
    {
        _netGsmOptionsSnapshot = netGsmOptionsSnapshot;
    }

    public async Task<bool> SendSms(string phone, string message, CancellationToken cancellationToken)
    {
        if (!_netGsmOptionsSnapshot.Value.IsEnabled)
            return true;

        // Yabancı sakin numarası +ülke koduyla saklanır (+447911123456). NetGsm'in OTP ucu yalnız
        // yurt içine gönderdiği için bu numaralar normal SMS ucundan 00'lı biçimde gider; bunun için
        // NetGsm hesabında yurt dışı SMS paketinin açık olması gerekir.
        if (IsInternational(phone))
        {
            return await SendInternationalAsync(phone, message, cancellationToken);
        }

        var xmlData = $"<mainbody><header><usercode>8508402875</usercode><password>3B73-6E</password><msgheader>SITELIFES</msgheader></header><body><msg>{message}</msg><no>{phone}</no></body></mainbody>";
        var content = new StringContent(xmlData, Encoding.UTF8, "application/xml");
        HttpClient _httpClient = new HttpClient();
        var response = await _httpClient.PostAsync(OtpUrl, content);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        return true;
    }

    internal static bool IsInternational(string? phone)
    {
        var trimmed = phone?.Trim();
        return !string.IsNullOrEmpty(trimmed) &&
               trimmed.StartsWith('+') &&
               !trimmed.StartsWith("+90", StringComparison.Ordinal);
    }

    internal static string ToNetGsmRecipient(string phone) =>
        "00" + new string(phone.Where(char.IsAsciiDigit).ToArray());

    private static async Task<bool> SendInternationalAsync(string phone, string message,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            msgheader = "SITELIFES",
            messages = new[] { new { msg = message, no = ToNetGsmRecipient(phone) } }
        };

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", BasicAuthToken);
        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(SmsUrl, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return true;
    }
}
