FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/fleet-assets/src/FleetVision.FleetAssets.Domain/FleetVision.FleetAssets.Domain.csproj \
     services/fleet-assets/src/FleetVision.FleetAssets.Domain/
COPY services/fleet-assets/src/FleetVision.FleetAssets.Application/FleetVision.FleetAssets.Application.csproj \
     services/fleet-assets/src/FleetVision.FleetAssets.Application/
COPY services/fleet-assets/src/FleetVision.FleetAssets.Infrastructure/FleetVision.FleetAssets.Infrastructure.csproj \
     services/fleet-assets/src/FleetVision.FleetAssets.Infrastructure/
COPY services/fleet-assets/src/FleetVision.FleetAssets.API/FleetVision.FleetAssets.API.csproj \
     services/fleet-assets/src/FleetVision.FleetAssets.API/

RUN dotnet restore services/fleet-assets/src/FleetVision.FleetAssets.API/FleetVision.FleetAssets.API.csproj

COPY services/fleet-assets/src/ services/fleet-assets/src/

RUN dotnet publish services/fleet-assets/src/FleetVision.FleetAssets.API/FleetVision.FleetAssets.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN adduser -D -u 1001 appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FleetVision.FleetAssets.API.dll"]
