using System.Net;
using System.Text;
using Domain.Options;
using Domain.Services;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Providers;

public class NetGsmSmsProvider : ISmsProvider
{
    private readonly IOptionsSnapshot<NetGsmOptions> _netGsmOptionsSnapshot;

    public NetGsmSmsProvider(IOptionsSnapshot<NetGsmOptions> netGsmOptionsSnapshot)
    {
        _netGsmOptionsSnapshot = netGsmOptionsSnapshot;
    }

    public async Task<bool> SendSms(string phone, string message, CancellationToken cancellationToken)
    {
        if (!_netGsmOptionsSnapshot.Value.IsEnabled)
            return true;
        
        var url = "https://api.netgsm.com.tr/sms/send/otp";
        var xmlData = $"<mainbody><header><usercode>8508402875</usercode><password>3B73-6E</password><msgheader>SITELIFES</msgheader></header><body><msg>{message}</msg><no>{phone}</no></body></mainbody>";
        var content = new StringContent(xmlData, Encoding.UTF8, "application/xml");
        HttpClient _httpClient = new HttpClient();
        var response = await _httpClient.PostAsync(url, content);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();
        return true;
    }
}