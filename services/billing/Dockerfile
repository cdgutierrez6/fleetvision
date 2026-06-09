FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/billing/src/FleetVision.Billing.Domain/FleetVision.Billing.Domain.csproj \
     services/billing/src/FleetVision.Billing.Domain/
COPY services/billing/src/FleetVision.Billing.Application/FleetVision.Billing.Application.csproj \
     services/billing/src/FleetVision.Billing.Application/
COPY services/billing/src/FleetVision.Billing.Infrastructure/FleetVision.Billing.Infrastructure.csproj \
     services/billing/src/FleetVision.Billing.Infrastructure/
COPY services/billing/src/FleetVision.Billing.API/FleetVision.Billing.API.csproj \
     services/billing/src/FleetVision.Billing.API/

RUN dotnet restore services/billing/src/FleetVision.Billing.API/FleetVision.Billing.API.csproj

COPY services/billing/src/ services/billing/src/

RUN dotnet publish services/billing/src/FleetVision.Billing.API/FleetVision.Billing.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.Billing.API.dll"]
