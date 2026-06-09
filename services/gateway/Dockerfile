FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY services/gateway/src/FleetVision.Gateway/FleetVision.Gateway.csproj \
     services/gateway/src/FleetVision.Gateway/

RUN dotnet restore services/gateway/src/FleetVision.Gateway/FleetVision.Gateway.csproj

COPY services/gateway/src/ services/gateway/src/

RUN dotnet publish services/gateway/src/FleetVision.Gateway/FleetVision.Gateway.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN adduser -D -u 1001 appuser
COPY --from=build --chown=appuser:appuser /app/publish .
USER appuser
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "FleetVision.Gateway.dll"]
