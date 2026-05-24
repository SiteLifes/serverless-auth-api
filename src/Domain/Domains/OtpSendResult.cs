namespace Domain.Domains;

public class OtpSendResult
{
    public bool IsSuccess { get; init; }
    public bool IsRateLimited { get; init; }
    public int RetryAfterSeconds { get; init; }

    public static OtpSendResult Success()
    {
        return new OtpSendResult { IsSuccess = true };
    }

    public static OtpSendResult RateLimited(int retryAfterSeconds)
    {
        return new OtpSendResult
        {
            IsSuccess = false,
            IsRateLimited = true,
            RetryAfterSeconds = retryAfterSeconds
        };
    }
}

