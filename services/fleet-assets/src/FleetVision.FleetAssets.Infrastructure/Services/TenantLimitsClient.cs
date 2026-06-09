using FleetVision.FleetAssets.Application.Common;
using System.Net;
using System.Net.Http.Json;

namespace FleetVision.FleetAssets.Infrastructure.Services;

public sealed class TenantLimitsClient : ITenantLimitsClient
{
    private readonly HttpClient _http;

    public TenantLimitsClient(HttpClient http) => _http = http;

    public async Task<TenantLimitsResponse> GetLimitsAsync(Guid tenantId, CancellationToken ct = default)
    {
        try
        {
            var response = await _http.GetAsync($"/tenants/{tenantId}/limits", ct);

            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new TenantServiceUnavailableException($"Tenant '{tenantId}' not found in tenant management service.");

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TenantLimitsResponse>(ct);
            return result ?? throw new TenantServiceUnavailableException("Tenant management service returned empty response.");
        }
        catch (TenantServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TenantServiceUnavailableException(
                "Tenant management service is unavailable.", ex);
        }
    }
}
