using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Core.Settings.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Data.Seeding.Infrastructure
{
    public static class SeedingExtensions
    {
        public static IServiceCollection AddDataSeeding(this IServiceCollection services, params Type[] seedTypes)
        {
            // SeedRunner
            services.AddScoped<SeedRunner>();

            // Seed implementasyonlarını kaydet
            foreach (var t in seedTypes)
            {
                if (!typeof(IDataSeed).IsAssignableFrom(t))
                    throw new InvalidOperationException($"{t.Name} {Messages.NotIDataSeed}");
                services.AddScoped(typeof(IDataSeed), t);
            }
            return services;
        }

        public static async Task UseDataSeedingAsync<TDbContext>(this IHost app, CancellationToken ct = default)
            where TDbContext : DbContext
        {
            var execution = app.Services.GetService<IOptions<StartupExecutionOptions>>()?.Value
                ?? new StartupExecutionOptions();
            var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("StartupExecution");
            logger.LogInformation("Açılış politikası: migration={MigrationsEnabled}, seed={SeedsEnabled}, Manitou={ManitouSyncEnabled}. Ayar değişiklikleri yeniden başlatma gerektirir.",
                execution.MigrationsEnabled, execution.SeedsEnabled, execution.ManitouSyncEnabled);
            if (!execution.MigrationsEnabled && !execution.SeedsEnabled)
            {
                logger.LogWarning("Otomatik açılış migration ve seed işlemleri kapalı; DbContext oluşturulmadı.");
                return;
            }

            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();

            if (execution.MigrationsEnabled)
                await db.Database.MigrateAsync(ct);
            else if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
                throw new InvalidOperationException("Açılış migration işlemi kapalı ancak bekleyen migration var. Seed çalıştırılmadı; önce onaylı şema geçişini tamamlayın.");

            if (execution.SeedsEnabled)
            {
                var runner = scope.ServiceProvider.GetRequiredService<SeedRunner>();
                await runner.RunAsync(db, ct);
            }
            else
                logger.LogWarning("Otomatik açılış seed işlemleri kapalı; seed çalıştırılmadı.");
        }
    }
}
