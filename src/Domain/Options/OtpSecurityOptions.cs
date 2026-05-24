namespace Domain.Options;

public class OtpSecurityOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int MaxSendAttemptsPerPhoneWindow { get; set; } = 3;
    public int MaxSendAttemptsPerIpWindow { get; set; } = 20;
    public int SendWindowMinutes { get; set; } = 10;
    public bool OnReview { get; set; } = false;
}


