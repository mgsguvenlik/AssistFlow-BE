using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Seeding.Infrastructure
{
    public class SeedRunner
    {
        private readonly IEnumerable<IDataSeed> _seeds;
        private readonly IServiceProvider _sp;
        private readonly ILogger<SeedRunner> _logger;

        public SeedRunner(IEnumerable<IDataSeed> seeds, IServiceProvider sp, ILogger<SeedRunner> logger)
        {
            _seeds = seeds.OrderBy(s => s.Order).ToList();
            _sp = sp;
            _logger = logger;
        }

        public async Task RunAsync(DbContext db, CancellationToken ct = default)
        {
            foreach (var seed in _seeds)
            {
                var already = await db.Set<SeedHistory>().AnyAsync(x => x.Key == seed.Key, ct);
                if (already)
                {
                    _logger.LogInformation("Seed {Key} zaten uygulanmış, atlandı.", seed.Key);
                    continue;
                }

                var should = await seed.ShouldRunAsync(db, ct);
                if (!should)
                {
                    _logger.LogInformation("Seed {Key} koşul sağlamadı, atlandı.", seed.Key);
                    continue;
                }

                _logger.LogInformation("Seed {Key} başlıyor...", seed.Key);
                using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    await seed.RunAsync(db, _sp, ct);

                    db.Set<SeedHistory>().Add(new SeedHistory { Key = seed.Key });
                    await db.SaveChangesAsync(ct);

                    await tx.CommitAsync(ct);
                    _logger.LogInformation("Seed {Key} tamamlandı.", seed.Key);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex, "Seed {Key} hata verdi.", seed.Key);
                    throw;
                }
            }
        }
    }
}
