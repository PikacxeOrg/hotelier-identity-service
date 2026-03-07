using System.Net.Http.Json;
using System.Text.Json;

using IdentityService.Domain;

using Microsoft.Extensions.Logging;

namespace IdentityService.Infrastructure;

public class ReservationServiceClient(
    HttpClient httpClient,
    ILogger<ReservationServiceClient> logger)
    : IReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<(bool CanDelete, string? Reason)> CanDeleteUserAsync(Guid userId, string userType)
    {
        try
        {
            var url = $"/api/reservations/internal/can-delete/{userId}?userType={userType}";
            logger.LogDebug("Checking deletion eligibility: {Url}", url);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CanDeleteDto>(JsonOptions);

            return (body?.CanDelete ?? true, body?.Reason);
        }
        catch (Exception ex)
        {
            // Fail-closed: if reservation-service is unreachable we cannot confirm
            // it is safe to delete, so we block the operation.
            logger.LogError(ex,
                "Could not reach reservation-service to verify deletion for user {UserId}. Blocking deletion.",
                userId);
            return (false, "Account deletion is temporarily unavailable. Please try again in a moment.");
        }
    }

    private sealed record CanDeleteDto(
        bool CanDelete,
        int ActiveCount,
        string? Reason
    );
}
