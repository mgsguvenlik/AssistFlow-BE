using Core.Utilities.Constants;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;

namespace WebAPI.Middleware
{
    public static class MigrationApplier
    {
        public static void ApplyMigrations(WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDataContext>>();

                // Check and apply pending migrations  
                var pendingMigrations = dbContext.Database.GetPendingMigrations().ToList(); // Ensure materialization
                if (pendingMigrations.Any())
                {
                    logger.LogInformation(Messages.ApplyingPendingMigrations);
                    dbContext.Database.Migrate();
                    logger.LogInformation(Messages.MigrationsAppliedSuccessfully);
                }
                else
                {
                    logger.LogInformation(Messages.NoPendingMigrationsFound);
                }
            }
        }
    }
}
