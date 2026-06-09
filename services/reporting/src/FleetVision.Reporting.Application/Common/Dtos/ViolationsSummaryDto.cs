namespace FleetVision.Reporting.Application.Common.Dtos;

public sealed record ViolationsSummaryDto(
    int TotalViolations,
    IReadOnlyList<ViolationByTypeDto>    ByType,
    IReadOnlyList<ViolationByVehicleDto> TopVehicles);

public sealed record ViolationByTypeDto(
    string ViolationType,
    int    Count,
    double Percentage);

public sealed record ViolationByVehicleDto(
    Guid   VehicleId,
    int    Count);
