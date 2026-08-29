using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data.Concrete.EfCore.Context;

public sealed class AppDataContextDesignFactory : IDesignTimeDbContextFactory<AppDataContext>
{
    public AppDataContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AssistFlowDesignTime;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly("Data"))
            .Options;
        return new AppDataContext(options);
    }
}
