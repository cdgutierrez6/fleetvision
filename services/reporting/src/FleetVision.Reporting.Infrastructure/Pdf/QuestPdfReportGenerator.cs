using FleetVision.Reporting.Application.Common.Dtos;
using FleetVision.Reporting.Application.Common.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FleetVision.Reporting.Infrastructure.Pdf;

public sealed class QuestPdfReportGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateFleetReportAsync(
        string         tenantName,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        FleetKpisDto          kpis,
        ViolationsSummaryDto  violations,
        FleetStatusDto        status,
        CancellationToken     _)
    {
        // License is set once at application startup (Program.cs). Do not set it here.
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Element(ComposeHeader);
                page.Content().Element(c => ComposeContent(c, kpis, violations, status));
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generado el ");
                    x.Span(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm UTC"));
                    x.Span(" · FleetVision");
                });
            });
        }).GeneratePdf();

        return Task.FromResult(bytes);

        void ComposeHeader(IContainer c)
        {
            c.Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("FleetVision").FontSize(20).Bold().FontColor("#1E3A5F");
                    col.Item().Text($"Reporte Operacional — {tenantName}").FontSize(12).FontColor("#6B7280");
                    col.Item().Text(
                        $"Período: {periodStart:yyyy-MM-dd} → {periodEnd:yyyy-MM-dd}"
                    ).FontSize(10).FontColor("#9CA3AF");
                });
            });
            c.PaddingBottom(12).LineHorizontal(1).LineColor("#E0E4EA");
        }

        void ComposeContent(
            IContainer c,
            FleetKpisDto k,
            ViolationsSummaryDto v,
            FleetStatusDto s)
        {
            c.Column(col =>
            {
                col.Spacing(20);

                // KPIs section
                col.Item().Text("KPIs de Flota").FontSize(13).Bold().FontColor("#1E3A5F");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(); cols.RelativeColumn();
                        cols.RelativeColumn(); cols.RelativeColumn();
                    });

                    void KpiCell(string label, string value)
                    {
                        table.Cell().Border(1).BorderColor("#E0E4EA").Padding(10).Column(cell =>
                        {
                            cell.Item().Text(value).FontSize(18).Bold().FontColor("#00BFA5");
                            cell.Item().Text(label).FontSize(9).FontColor("#6B7280");
                        });
                    }

                    KpiCell("Vehículos activos",       k.ActiveVehicles.ToString());
                    KpiCell("Distancia total (km)",    k.TotalDistanceKm.ToString("F1"));
                    KpiCell("Velocidad promedio (km/h)", k.AvgSpeedKmh.ToString("F1"));
                    KpiCell("Vel. máxima (km/h)",     k.MaxSpeedKmh.ToString("F1"));
                });

                // Fleet status section
                col.Item().Text("Estado de la Flota").FontSize(13).Bold().FontColor("#1E3A5F");
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(2); cols.RelativeColumn();
                    });

                    void StatusRow(string label, int count, string color)
                    {
                        table.Cell().PaddingVertical(4).Text(label).FontColor(color);
                        table.Cell().PaddingVertical(4).AlignRight().Text(count.ToString()).Bold();
                    }

                    StatusRow("Activos",          s.Active,      "#4CAF50");
                    StatusRow("En mantenimiento", s.Maintenance, "#9C27B0");
                    StatusRow("Inactivos",        s.Inactive,    "#9E9E9E");
                    StatusRow("Total",            s.Total,       "#1E3A5F");
                });

                // Violations section
                col.Item().Text($"Alertas del Período ({v.TotalViolations} total)")
                    .FontSize(13).Bold().FontColor("#1E3A5F");

                if (v.TotalViolations == 0)
                {
                    col.Item().Text("Sin alertas registradas en este período.")
                        .FontColor("#9CA3AF").Italic();
                }
                else
                {
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3); cols.RelativeColumn(); cols.RelativeColumn();
                        });

                        table.Header(h =>
                        {
                            h.Cell().Text("Tipo").Bold();
                            h.Cell().AlignRight().Text("Cantidad").Bold();
                            h.Cell().AlignRight().Text("%").Bold();
                        });

                        foreach (var vt in v.ByType)
                        {
                            table.Cell().Text(vt.ViolationType);
                            table.Cell().AlignRight().Text(vt.Count.ToString());
                            table.Cell().AlignRight().Text($"{vt.Percentage:F1}%");
                        }
                    });
                }
            });
        }
    }
}
