using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace FleetVision.PredictiveMaintenance.Infrastructure.Migrations;

[DbContext(typeof(FleetVision.PredictiveMaintenance.Infrastructure.Persistence.MaintenanceDbContext))]
partial class MaintenanceDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0");
    }
}
