using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var runner = scope.ServiceProvider.GetRequiredService<SeedRunner>();

            // İstersen migration:
            await db.Database.MigrateAsync(ct);

            await runner.RunAsync(db, ct);
        }
    }
}
  