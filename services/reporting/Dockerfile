FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/reporting/src/FleetVision.Reporting.Application/FleetVision.Reporting.Application.csproj \
     services/reporting/src/FleetVision.Reporting.Application/
COPY services/reporting/src/FleetVision.Reporting.Infrastructure/FleetVision.Reporting.Infrastructure.csproj \
     services/reporting/src/FleetVision.Reporting.Infrastructure/
COPY services/reporting/src/FleetVision.Reporting.API/FleetVision.Reporting.API.csproj \
     services/reporting/src/FleetVision.Reporting.API/

RUN dotnet restore services/reporting/src/FleetVision.Reporting.API/FleetVision.Reporting.API.csproj

COPY services/reporting/src/ services/reporting/src/

RUN dotnet publish services/reporting/src/FleetVision.Reporting.API/FleetVision.Reporting.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.Reporting.API.dll"]
