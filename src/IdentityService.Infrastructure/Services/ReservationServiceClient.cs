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
            // If reservation-service is unreachable, fail-open with a warning.
            // This prevents the identity service from being permanently blocked
            // when reservation-service is down. Adjust to fail-closed if desired.
            logger.LogWarning(ex,
                "Could not reach reservation-service to verify deletion for user {UserId}. Allowing deletion.",
                userId);
            return (true, null);
        }
    }

    private sealed record CanDeleteDto(
        bool CanDelete,
        int ActiveCount,
        string? Reason
    );
}
