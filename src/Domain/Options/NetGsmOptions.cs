namespace Domain.Options;

public class NetGsmOptions
{
    public bool IsEnabled { get; set; } = true;
    public string Username { get; set; }
    public string Password { get; set; } 
    public string From { get; set; }
}