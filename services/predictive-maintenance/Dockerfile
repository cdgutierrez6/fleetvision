FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY proto/ proto/

COPY services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Domain/FleetVision.PredictiveMaintenance.Domain.csproj \
     services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Domain/
COPY services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Application/FleetVision.PredictiveMaintenance.Application.csproj \
     services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Application/
COPY services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Infrastructure/FleetVision.PredictiveMaintenance.Infrastructure.csproj \
     services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.Infrastructure/
COPY services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.API/FleetVision.PredictiveMaintenance.API.csproj \
     services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.API/

RUN dotnet restore services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.API/FleetVision.PredictiveMaintenance.API.csproj

COPY services/predictive-maintenance/src/ services/predictive-maintenance/src/

RUN dotnet publish services/predictive-maintenance/src/FleetVision.PredictiveMaintenance.API/FleetVision.PredictiveMaintenance.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.PredictiveMaintenance.API.dll"]
