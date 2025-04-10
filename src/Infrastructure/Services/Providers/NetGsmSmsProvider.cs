using System.Net;
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
        var userName = _netGsmOptionsSnapshot.Value.Username;
        var password = _netGsmOptionsSnapshot.Value.Password;
        var sender = _netGsmOptionsSnapshot.Value.From;
        var url = @$"https://api.netgsm.com.tr/sms/send/get/?usercode=08503053487&password=Kisshe1234*&gsmno={phone}&message={message}&msgheader=08503053487";
        var request = (HttpWebRequest) WebRequest.Create(url);
        request.AutomaticDecompression = DecompressionMethods.GZip;
        using var response = (HttpWebResponse) request.GetResponse();
        await using var stream = response.GetResponseStream();
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        return html.StartsWith("00");
    }
}