using FleetVision.Geofencing.Application.Common;
using System.Net;
using System.Net.Http.Json;

namespace FleetVision.Geofencing.Infrastructure.Services;

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
                throw new TenantServiceUnavailableException($"Tenant '{tenantId}' not found in tenant management.");

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TenantLimitsResponse>(ct)
                ?? throw new TenantServiceUnavailableException("Empty response from tenant management.");
        }
        catch (TenantServiceUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new TenantServiceUnavailableException($"Tenant management unreachable: {ex.Message}");
        }
    }
}
