using Core.Utilities.Constants;
using Data.Concrete.EfCore.Context;
using Core.Settings.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace WebAPI.Middleware
{
    public static class MigrationApplier
    {
        public static void ApplyMigrations(WebApplication app)
        {
            var execution = app.Services.GetService<IOptions<StartupExecutionOptions>>()?.Value
                ?? new StartupExecutionOptions();
            if (!execution.MigrationsEnabled)
            {
                app.Logger.LogWarning("İkinci açılış migration yolu kapalı; DbContext oluşturulmadı.");
                return;
            }

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
