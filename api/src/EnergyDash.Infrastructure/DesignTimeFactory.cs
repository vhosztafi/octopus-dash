using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnergyDash.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<EnergyDbContext>
{
    public EnergyDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<EnergyDbContext>().UseSqlServer("Server=localhost;Database=EnergyDash;Integrated Security=true;TrustServerCertificate=true").Options);
}
