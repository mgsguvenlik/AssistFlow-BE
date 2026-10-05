using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data.Concrete.EfCore.Context;

// Migration tooling does not start WebAPI, run seeds, or connect to a production database.
public sealed class AppDataContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDataContext>
{
    public AppDataContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer("Server=localhost;Database=AssistFlowDesignTime;Integrated Security=true;TrustServerCertificate=true")
            .Options);
}
