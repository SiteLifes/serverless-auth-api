namespace Domain.Services;

/// <summary>
/// Drops every refresh token a staff account holds.
///
/// Whenever a staff password changes, the sessions that were established under the old one have to
/// go with it: otherwise whoever knew that password — the colleague who provisioned the account, or
/// whoever prompted the change — keeps a way back in.
/// </summary>
public interface IStaffSessionRevoker
{
    /// <returns>How many sessions were dropped.</returns>
    Task<int> RevokeAllAsync(string staffId, CancellationToken cancellationToken = default);
}
