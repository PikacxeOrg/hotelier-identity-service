namespace IdentityService.Domain;

/// <summary>
/// Abstraction for calling reservation-service to verify whether
/// a user can safely delete their account.
/// </summary>
public interface IReservationServiceClient
{
    /// <summary>
    /// Returns (canDelete, reason). If canDelete is false, reason explains why.
    /// </summary>
    Task<(bool CanDelete, string? Reason)> CanDeleteUserAsync(Guid userId, string userType);
}
