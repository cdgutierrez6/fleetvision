FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/identity/src/FleetVision.Identity.Domain/FleetVision.Identity.Domain.csproj \
     services/identity/src/FleetVision.Identity.Domain/
COPY services/identity/src/FleetVision.Identity.Application/FleetVision.Identity.Application.csproj \
     services/identity/src/FleetVision.Identity.Application/
COPY services/identity/src/FleetVision.Identity.Infrastructure/FleetVision.Identity.Infrastructure.csproj \
     services/identity/src/FleetVision.Identity.Infrastructure/
COPY services/identity/src/FleetVision.Identity.API/FleetVision.Identity.API.csproj \
     services/identity/src/FleetVision.Identity.API/

RUN dotnet restore services/identity/src/FleetVision.Identity.API/FleetVision.Identity.API.csproj

COPY services/identity/src/ services/identity/src/

RUN dotnet publish services/identity/src/FleetVision.Identity.API/FleetVision.Identity.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.Identity.API.dll"]
