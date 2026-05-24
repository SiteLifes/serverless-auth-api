namespace Domain.Options;

public class OtpSecurityOptions
{
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public bool OnReview { get; set; } = false;
}


