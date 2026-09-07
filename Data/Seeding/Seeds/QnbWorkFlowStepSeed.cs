using Data.Seeding.Abstractions;
using Data.Seeding.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete.Qnb;

namespace Data.Seeding.Seeds
{
    public class QnbWorkFlowStepSeed : IDataSeed
    {
        private readonly ILogger<QnbWorkFlowStepSeed> _logger;

        public QnbWorkFlowStepSeed(ILogger<QnbWorkFlowStepSeed> logger)
        {
            _logger = logger;
        }

        public string Key => "QnbWorkFlowSteps";
        public int Order => 12; // YKB (11) seed'inden sonra çalışsın

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            var workFlowSteps = new List<QnbWorkFlowStep>
            {
                new() { Name = "Müşteri Formu Oluşturma", Code = "CF",   Order = 1   },
                new() { Name = "Servis Talebi Oluşturma", Code = "SR",   Order = 2   },
                new() { Name = "Depo Sevkiyatı",          Code = "WH",   Order = 3   },
                new() { Name = "Teknik Servis İşlemleri", Code = "TS",   Order = 4   },
                new() { Name = "Fiyatlandırma",           Code = "PRC",  Order = 5   },
                new() { Name = "Onaylama",                Code = "APR",  Order = 6   },
                new() { Name = "Müşteri Onayında",        Code = "CAPR", Order = 7   },
                new() { Name = "İptal Edildi",            Code = "CNC",  Order = 99  },
                new() { Name = "Tamamlandı",              Code = "CMP",  Order = 100 },
            };

            var existingCodes = await db.Set<QnbWorkFlowStep>()
                .Select(w => w.Code)
                .ToListAsync(ct);

            var existingSet = new HashSet<string>(existingCodes!);

            foreach (var step in workFlowSteps)
            {
                if (!existingSet.Contains(step.Code!))
                    await db.Set<QnbWorkFlowStep>().AddAsync(step, ct);
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "QnbWorkFlowStep Seed Completed. Ensured {Count} steps (only missing codes were added).",
                workFlowSteps.Count
            );
        }

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
            => !await db.Set<QnbWorkFlowStep>().AnyAsync(ct);
    }
}