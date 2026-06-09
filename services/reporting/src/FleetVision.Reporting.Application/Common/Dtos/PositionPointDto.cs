namespace FleetVision.Reporting.Application.Common.Dtos;

public sealed record PositionPointDto(
    double         Latitude,
    double         Longitude,
    double         Speed,
    int            Heading,
    double         Odometer,
    DateTimeOffset Timestamp);
