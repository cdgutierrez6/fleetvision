FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY proto/ proto/

COPY services/notifications/src/FleetVision.Notifications.API/FleetVision.Notifications.API.csproj \
     services/notifications/src/FleetVision.Notifications.API/

RUN dotnet restore services/notifications/src/FleetVision.Notifications.API/FleetVision.Notifications.API.csproj

COPY services/notifications/src/ services/notifications/src/

RUN dotnet publish services/notifications/src/FleetVision.Notifications.API/FleetVision.Notifications.API.csproj \
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
ENTRYPOINT ["dotnet", "FleetVision.Notifications.API.dll"]
