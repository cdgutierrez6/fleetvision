FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY proto/ proto/

COPY services/telemetry/src/FleetVision.Telemetry.Domain/FleetVision.Telemetry.Domain.csproj \
     services/telemetry/src/FleetVision.Telemetry.Domain/
COPY services/telemetry/src/FleetVision.Telemetry.Application/FleetVision.Telemetry.Application.csproj \
     services/telemetry/src/FleetVision.Telemetry.Application/
COPY services/telemetry/src/FleetVision.Telemetry.Infrastructure/FleetVision.Telemetry.Infrastructure.csproj \
     services/telemetry/src/FleetVision.Telemetry.Infrastructure/
COPY services/telemetry/src/FleetVision.Telemetry.API/FleetVision.Telemetry.API.csproj \
     services/telemetry/src/FleetVision.Telemetry.API/

RUN dotnet restore services/telemetry/src/FleetVision.Telemetry.API/FleetVision.Telemetry.API.csproj

COPY services/telemetry/src/ services/telemetry/src/

RUN dotnet publish services/telemetry/src/FleetVision.Telemetry.API/FleetVision.Telemetry.API.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN adduser -D -u 1001 appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FleetVision.Telemetry.API.dll"]
