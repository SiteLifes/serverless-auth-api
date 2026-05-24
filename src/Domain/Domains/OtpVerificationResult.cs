namespace Domain.Domains;

public class OtpVerificationResult
{
    public static OtpVerificationResult Success() => new(true, false);
    public static OtpVerificationResult Invalid() => new(false, false);
    public static OtpVerificationResult Locked() => new(false, true);

    public OtpVerificationResult(bool isSuccess, bool isLocked)
    {
        IsSuccess = isSuccess;
        IsLocked = isLocked;
    }

    public bool IsSuccess { get; }
    public bool IsLocked { get; }
}

