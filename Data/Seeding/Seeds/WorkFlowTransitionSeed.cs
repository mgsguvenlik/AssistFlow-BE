using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete.WorkFlows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Seeding.Seeds
{
    // İş akışı adımları arasındaki geçiş kurallarını (Transitions) tanımlar.
    public class WorkFlowTransitionSeed : IDataSeed
    {
        private readonly ILogger<WorkFlowTransitionSeed> _logger;

        public WorkFlowTransitionSeed(ILogger<WorkFlowTransitionSeed> logger)
        {
            _logger = logger;
        }

        public string Key => "WorkFlowTransitions"; // SeedHistory için benzersiz anahtar
        public int Order => 12; // WorkFlowStepSeed'den (11) sonra çalışması için

        public async Task RunAsync_(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            // 1. WorkFlowStep ID'lerini Code'lara göre önceden belleğe al
            var steps = await db.Set<WorkFlowStep>()
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Code!, s => s.Id, ct);

            // Gerekli tüm adımların varlığını kontrol et
            if (!steps.ContainsKey("SR") || !steps.ContainsKey("WH") || !steps.ContainsKey("TS") ||
                !steps.ContainsKey("PRC") || !steps.ContainsKey("APR") || !steps.ContainsKey("CNC"))
            {
                _logger.LogWarning("WorkFlowTransition Seed: Tüm WorkFlowStep kodları (SR, WH, TS, PRC, APR, CNC) bulunamadı. Geçişler eklenemedi.");
                return;
            }

            var transitions = new List<WorkFlowTransition>();
            // --- TEMEL İLERİ AKIŞ GEÇİŞLERİ ---
            // Adım 1: Servis Talebi (SR) -> Depo (WH) [Ürün Var]
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["WH"],
                TransitionName = "Depoya Sevkiyat Gerekiyor",
                Condition = "Request.IsProductRequirement = True"
            });

            // Adım 1: Servis Talebi (SR) -> Teknik Servis (TS) [Ürün Yok]
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["TS"],
                TransitionName = "Doğrudan Teknik Servise Yönlendir",
                Condition = "Request.IsProductRequirement = False"
            });

            // Adım 2: Depo (WH) -> Teknik Servis (TS)
            transitions.Add(new()
            {
                FromStepId = steps["WH"],
                ToStepId = steps["TS"],
                TransitionName = "Depo Sevkiyatı Tamamlandı"
            });

            // Adım 3: Teknik Servis (TS) -> Fiyatlandırma (PRC)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["PRC"],
                TransitionName = "Servis Tamamlandı, Fiyatlandırmaya Gönder"
            });

            // Adım 4: Fiyatlandırma (PRC) -> Onaylama (APR)
            transitions.Add(new()
            {
                FromStepId = steps["PRC"],
                ToStepId = steps["APR"],
                TransitionName = "Fiyat Onayına Sun"
            });

            // --- GERİ AKIŞ (REVİZYON) GEÇİŞLERİ ---

            // TS'den SR'a Geri Dönüş (En başa dönüp revize etme)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["SR"],
                TransitionName = "Revize Et: Servis Talebine Geri Dön",
                Condition = "User.Role = 'Manager' || User.Role = 'Admin'" // Yönetici yetkisi
            });

            // TS'den WH'e Geri Dönüş (Eksik/Yanlış Ürün)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["WH"],
                TransitionName = "Revize Et: Depoya Geri Gönder (Ürün Revizyonu)"
            });

            // WH'den SR'a Geri Dönüş (Depo, talepte sorun gördü)
            transitions.Add(new()
            {
                FromStepId = steps["WH"],
                ToStepId = steps["SR"],
                TransitionName = "Revize Et: Servis Talebine Geri Dön (Talep Düzeltme)"
            });

            // PRC'den TS'ye Geri Dönüş (Fiyatlandırma, Teknik Serviste eksik gördü)
            transitions.Add(new()
            {
                FromStepId = steps["PRC"],
                ToStepId = steps["TS"],
                TransitionName = "Revize Et: Teknik Servis İşlemine Geri Dön"
            });

            // APR'den PRC'ye Geri Dönüş (Onaylama Adımından Fiyatlandırmaya Revizyon)
            transitions.Add(new()
            {
                FromStepId = steps["APR"],
                ToStepId = steps["PRC"],
                TransitionName = "Revize Et: Fiyatlandırmaya Geri Dön"
            });

            // --- İPTAL GEÇİŞLERİ ---

            // SR'dan İptal
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["CNC"],
                TransitionName = "Talebi İptal Et"
            });

            // TS'den İptal
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["CNC"],
                TransitionName = "Teknik Servis İptal"
            });

            // ... Diğer adımlardan da iptal geçişleri eklenebilir.

            // 2. Geçişleri Kaydet
            foreach (var transition in transitions)
            {
                // Aynı From ve To Step ID'sine sahip bir kuralın zaten var olup olmadığını kontrol et.
                var exists = await db.Set<WorkFlowTransition>()
                    .AnyAsync(t => t.FromStepId == transition.FromStepId && t.ToStepId == transition.ToStepId, ct);

                if (!exists)
                {
                    await db.Set<WorkFlowTransition>().AddAsync(transition, ct);
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation("WorkFlowTransition Seed Completed. Added/Ensured {Count} transitions.", transitions.Count);
        }

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            // 1. WorkFlowStep ID'lerini Code'lara göre önceden belleğe al
            var steps = await db.Set<WorkFlowStep>()
                .AsNoTracking()
                .ToDictionaryAsync(s => s.Code!, s => s.Id, ct);

            // Gerekli tüm adımların varlığını kontrol et
            if (!steps.ContainsKey("SR") || !steps.ContainsKey("WH") || !steps.ContainsKey("TS") ||
                !steps.ContainsKey("PRC") || !steps.ContainsKey("APR") || !steps.ContainsKey("CNC"))
            {
                _logger.LogWarning("WorkFlowTransition Seed: Tüm WorkFlowStep kodları (SR, WH, TS, PRC, APR, CNC) bulunamadı. Geçişler eklenemedi.");
                return;
            }

            var transitions = new List<WorkFlowTransition>();

            // --- TEMEL İLERİ AKIŞ GEÇİŞLERİ ---
            // Adım 1: Servis Talebi (SR) -> Depo (WH) [Ürün Var]
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["WH"],
                TransitionName = "Depoya Sevkiyat Gerekiyor",
                Condition = "Request.IsProductRequirement = True"
            });

            // Adım 1: Servis Talebi (SR) -> Teknik Servis (TS) [Ürün Yok]
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["TS"],
                TransitionName = "Doğrudan Teknik Servise Yönlendir",
                Condition = "Request.IsProductRequirement = False"
            });

            // Adım 2: Depo (WH) -> Teknik Servis (TS)
            transitions.Add(new()
            {
                FromStepId = steps["WH"],
                ToStepId = steps["TS"],
                TransitionName = "Depo Sevkiyatı Tamamlandı"
            });

            // Adım 3: Teknik Servis (TS) -> Fiyatlandırma (PRC)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["PRC"],
                TransitionName = "Servis Tamamlandı, Fiyatlandırmaya Gönder"
            });

            // Adım 4: Fiyatlandırma (PRC) -> Onaylama (APR)
            transitions.Add(new()
            {
                FromStepId = steps["PRC"],
                ToStepId = steps["APR"],
                TransitionName = "Fiyat Onayına Sun"
            });

            // --- GERİ AKIŞ (REVİZYON) GEÇİŞLERİ ---

            // TS'den SR'a Geri Dönüş (En başa dönüp revize etme)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["SR"],
                TransitionName = "Revize Et: Servis Talebine Geri Dön",
                Condition = "User.Role = 'Manager' || User.Role = 'Admin'"
            });

            // TS'den WH'e Geri Dönüş (Eksik/Yanlış Ürün)
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["WH"],
                TransitionName = "Revize Et: Depoya Geri Gönder (Ürün Revizyonu)"
            });

            // WH'den SR'a Geri Dönüş (Depo, talepte sorun gördü)
            transitions.Add(new()
            {
                FromStepId = steps["WH"],
                ToStepId = steps["SR"],
                TransitionName = "Revize Et: Servis Talebine Geri Dön (Talep Düzeltme)"
            });

            // PRC'den TS'ye Geri Dönüş (Fiyatlandırma, Teknik Serviste eksik gördü)
            transitions.Add(new()
            {
                FromStepId = steps["PRC"],
                ToStepId = steps["TS"],
                TransitionName = "Revize Et: Teknik Servis İşlemine Geri Dön"
            });

            // APR'den PRC'ye Geri Dönüş (Onaylama Adımından Fiyatlandırmaya Revizyon)
            transitions.Add(new()
            {
                FromStepId = steps["APR"],
                ToStepId = steps["PRC"],
                TransitionName = "Revize Et: Fiyatlandırmaya Geri Dön"
            });

            // --- İPTAL GEÇİŞLERİ ---

            // SR'dan İptal
            transitions.Add(new()
            {
                FromStepId = steps["SR"],
                ToStepId = steps["CNC"],
                TransitionName = "Talebi İptal Et"
            });

            // TS'den İptal
            transitions.Add(new()
            {
                FromStepId = steps["TS"],
                ToStepId = steps["CNC"],
                TransitionName = "Teknik Servis İptal"
            });

            // ... Diğer adımlardan da iptal geçişleri eklenebilir.

            // 2. Mevcut geçişleri DB'den bir kere çek
            var existing = await db.Set<WorkFlowTransition>()
                .AsNoTracking()
                .Select(t => new
                {
                    t.FromStepId,
                    t.ToStepId,
                    t.TransitionName
                })
                .ToListAsync(ct);

            // From + To + TransitionName kombinasyonunu uniq key olarak kabul edelim
            var existingKeys = new HashSet<string>(
                existing.Select(e => $"{e.FromStepId}-{e.ToStepId}-{e.TransitionName}")
            );

            var addedCount = 0;

            foreach (var transition in transitions)
            {
                var key = $"{transition.FromStepId}-{transition.ToStepId}-{transition.TransitionName}";

                if (!existingKeys.Contains(key))
                {
                    await db.Set<WorkFlowTransition>().AddAsync(transition, ct);
                    existingKeys.Add(key);
                    addedCount++;
                }
            }

            await db.SaveChangesAsync(ct);
            _logger.LogInformation(
                "WorkFlowTransition Seed Completed. Defined {DefinedCount} transitions, added {AddedCount} new transitions.",
                transitions.Count,
                addedCount
            );
        }

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            // Sadece tabloda hiç geçiş kuralı yoksa çalıştır.
            return !await db.Set<WorkFlowTransition>().AnyAsync(ct);
        }
    }
}
