FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY proto/ proto/

COPY services/geofencing/src/FleetVision.Geofencing.Domain/FleetVision.Geofencing.Domain.csproj \
     services/geofencing/src/FleetVision.Geofencing.Domain/
COPY services/geofencing/src/FleetVision.Geofencing.Application/FleetVision.Geofencing.Application.csproj \
     services/geofencing/src/FleetVision.Geofencing.Application/
COPY services/geofencing/src/FleetVision.Geofencing.Infrastructure/FleetVision.Geofencing.Infrastructure.csproj \
     services/geofencing/src/FleetVision.Geofencing.Infrastructure/
COPY services/geofencing/src/FleetVision.Geofencing.API/FleetVision.Geofencing.API.csproj \
     services/geofencing/src/FleetVision.Geofencing.API/

RUN dotnet restore services/geofencing/src/FleetVision.Geofencing.API/FleetVision.Geofencing.API.csproj

COPY services/geofencing/src/ services/geofencing/src/

RUN dotnet publish services/geofencing/src/FleetVision.Geofencing.API/FleetVision.Geofencing.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.Geofencing.API.dll"]
