FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/tenant-management/src/FleetVision.TenantManagement.Domain/FleetVision.TenantManagement.Domain.csproj \
     services/tenant-management/src/FleetVision.TenantManagement.Domain/
COPY services/tenant-management/src/FleetVision.TenantManagement.Application/FleetVision.TenantManagement.Application.csproj \
     services/tenant-management/src/FleetVision.TenantManagement.Application/
COPY services/tenant-management/src/FleetVision.TenantManagement.Infrastructure/FleetVision.TenantManagement.Infrastructure.csproj \
     services/tenant-management/src/FleetVision.TenantManagement.Infrastructure/
COPY services/tenant-management/src/FleetVision.TenantManagement.API/FleetVision.TenantManagement.API.csproj \
     services/tenant-management/src/FleetVision.TenantManagement.API/

RUN dotnet restore services/tenant-management/src/FleetVision.TenantManagement.API/FleetVision.TenantManagement.API.csproj

COPY services/tenant-management/src/ services/tenant-management/src/

RUN dotnet publish services/tenant-management/src/FleetVision.TenantManagement.API/FleetVision.TenantManagement.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN adduser -D -u 1001 appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FleetVision.TenantManagement.API.dll"]
