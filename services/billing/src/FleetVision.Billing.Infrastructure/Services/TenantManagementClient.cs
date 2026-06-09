using System.Text;
using System.Text.Json;
using FleetVision.Billing.Application.Common.Interfaces;
using FleetVision.Billing.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FleetVision.Billing.Infrastructure.Services;

public sealed class TenantManagementClient : ITenantManagementClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TenantManagementClient> _logger;

    public TenantManagementClient(
        HttpClient http,
        IConfiguration config,
        ILogger<TenantManagementClient> logger)
    {
        _http   = http;
        _config = config;
        _logger = logger;
    }

    public async Task UpdateTenantPlanAsync(Guid tenantId, PlanTier plan, CancellationToken ct)
    {
        var internalKey = _config["TenantManagement:InternalApiKey"]
            ?? throw new InvalidOperationException("TenantManagement:InternalApiKey is required.");

        var body = JsonSerializer.Serialize(new { plan = plan.ToString() });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/internal/tenants/{tenantId}/plan")
        {
            Content = content
        };

        request.Headers.Add("X-Internal-Key", internalKey);

        var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "TenantManagement plan update failed for tenant {TenantId}: {Status} — {Error}",
                tenantId, response.StatusCode, error);

            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation(
            "Updated TenantManagement plan to {Plan} for tenant {TenantId}", plan, tenantId);
    }
}
