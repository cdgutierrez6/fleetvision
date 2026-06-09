namespace FleetVision.Reporting.Application.Common.Dtos;

public sealed record FleetKpisDto(
    int    ActiveVehicles,
    double TotalDistanceKm,
    double AvgSpeedKmh,
    double MaxSpeedKmh,
    long   TotalPositionRecords,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);
