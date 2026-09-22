namespace Domain.Services;

public enum RefreshTokenFailure
{
    None,
    // The record may have been revoked, deleted by DynamoDB TTL, or never issued.
    NotFound,
    Expired
}

public sealed record RefreshTokenValidation(string? UserId, RefreshTokenFailure Failure)
{
    public static RefreshTokenValidation Valid(string userId) => new(userId, RefreshTokenFailure.None);
    public static RefreshTokenValidation Invalid(RefreshTokenFailure reason) => new(null, reason);
}
