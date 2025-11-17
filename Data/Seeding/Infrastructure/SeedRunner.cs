using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
                //var already = await db.Set<SeedHistory>().AnyAsync(x => x.Key == seed.Key, ct);
                //if (already)
                //{
                //    _logger.LogInformation(Messages.SeedAlreadyApplied, seed.Key);
                //    continue;
                //}

                var should = await seed.ShouldRunAsync(db, ct);
                if (!should)
                {
                    _logger.LogInformation(Messages.SeedAlreadyApplied, seed.Key);
                    continue;
                }

                _logger.LogInformation(Messages.SeedStarting, seed.Key);
                using var tx = await db.Database.BeginTransactionAsync(ct);
                try
                {
                    await seed.RunAsync(db, _sp, ct);

                    db.Set<SeedHistory>().Add(new SeedHistory { Key = seed.Key });
                    await db.SaveChangesAsync(ct);

                    await tx.CommitAsync(ct);
                    _logger.LogInformation(Messages.SeedCompleted, seed.Key);
                }
                catch (Exception ex)
                {
                    await tx.RollbackAsync(ct);
                    _logger.LogError(ex, Messages.SeedFailed, seed.Key);
                    throw;
                }
            }
        }
    }
}
