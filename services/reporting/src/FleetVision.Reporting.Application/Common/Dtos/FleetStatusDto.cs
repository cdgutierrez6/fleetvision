namespace FleetVision.Reporting.Application.Common.Dtos;

public sealed record FleetStatusDto(
    int Total,
    int Active,
    int Maintenance,
    int Inactive);
