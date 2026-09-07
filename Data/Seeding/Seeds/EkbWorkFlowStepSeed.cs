using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ekb;

namespace Data.Seeding.Seeds
{
    // Bu, WorkFlow adımlarını (Steps) veritabanına ekleyen Seed sınıfıdır.
    public class EkbWorkFlowStepSeed : IDataSeed
    {
        private readonly ILogger<EkbWorkFlowStepSeed> _logger;

        public EkbWorkFlowStepSeed(ILogger<EkbWorkFlowStepSeed> logger)
        {
            _logger = logger;
        } 
        public string Key => "EkbWorkFlowSteps"; // SeedHistory için benzersiz anahtar
        public int Order => 11; // ConfigSeed'den sonra çalışması için 10'dan büyük bir değer
        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            var workFlowSteps = new List<EkbWorkFlowStep>
            {
                new() {
                     Name = "Müşteri Formu Oluşturma",
                     Code = "CF", // Services Request
                     Order = 1,
                 },
                new() {
                     Name = "Servis Talebi Oluşturma",
                     Code = "SR", // Services Request
                     Order = 2,
                 },
                new() {
                     Name = "Depo Sevkiyatı",
                     Code = "WH", // Warehouse
                     Order = 3,
                 },
                new() {
                     Name = "Teknik Servis İşlemleri",
                     Code = "TS", // Technical Service
                     Order = 4,
                 },
                new() {
                     Name = "Fiyatlandırma",
                     Code = "PRC", // Pricing
                     Order = 5,
                 },
                new() {
                     Name = "Onaylama",
                     Code = "APR", // Approval
                     Order = 6 
                },

                 new() {
                     Name = "Müşteri Onayında",
                     Code = "CAPR", //Customer Approval
                     Order = 7,
                 },
                new() {
                     Name = "İptal Edildi",
                     Code = "CNC", // Cancelled
                     Order = 99,
                 },
                new() {
                     Name = "Tamamlandı",
                     Code = "CMP", // Completed
                     Order = 100,
                 }
            };

            var existingNames = await db.Set<EkbWorkFlowStep>()
                .Select(w => w.Code)
                .ToListAsync(ct);

            var existingNameSet = new HashSet<string>(existingNames);

            foreach (var step in workFlowSteps)
            {
                // Code'e göre kontrol
                if (!existingNameSet.Contains(step.Code))
                {
                    await db.Set<EkbWorkFlowStep>().AddAsync(step, ct);
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WorkFlowStep Seed Completed. Ensured {Count} steps (only missing names were added).",
                workFlowSteps.Count
            );
        }

        // Sadece tabloda hiç veri yoksa çalıştır
        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            // Eğer tabloda hiç WorkFlowStep yoksa çalıştır.
            return !await db.Set<EkbWorkFlowStep>().AnyAsync(ct);
        }
    }
}
