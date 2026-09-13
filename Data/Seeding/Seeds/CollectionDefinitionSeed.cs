using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;

namespace Data.Seeding.Seeds;

/// <summary>Explicitly invoked module-only seed. Never overwrites existing definitions or shared tables.</summary>
public sealed class CollectionDefinitionSeed : IDataSeed
{
    public string Key => "collection.definitions.v1";
    public int Order => 40;
    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);

    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tahsilat tanımları transaction içinde kurulmalıdır.");
        // SeedRunner owns the transaction. Serialize concurrent executions of this seed only.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'CollectionDefinitionSeed',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, N'Tahsilat tanım kilidi alınamadı.', 1;
            """, ct);

        var frequencies = new (string Code, string Name, short Months)[]
        {
            ("MONTHLY", "Aylık", 1), ("EVERY_2_MONTHS", "2 Aylık", 2),
            ("EVERY_3_MONTHS", "3 Aylık", 3), ("EVERY_4_MONTHS", "4 Aylık", 4),
            ("EVERY_6_MONTHS", "6 Aylık", 6), ("YEARLY", "Yıllık", 12),
            ("EVERY_24_MONTHS", "2 Yıllık", 24), ("EVERY_36_MONTHS", "3 Yıllık", 36)
        };
        var existing = await db.Set<CollectionPaymentFrequency>().AsNoTracking().ToListAsync(ct);
        foreach (var (code, name, months) in frequencies)
        {
            var match = existing.SingleOrDefault(x => x.Code == code);
            if (match is not null)
            {
                if (match.IntervalMonths != months)
                    throw new InvalidOperationException("Mevcut ödeme sıklığının kod ve dönem eşlemesi uyuşmuyor.");
                continue;
            }
            if (existing.Any(x => x.IntervalMonths == months || SameName(x.Name, name) || SameName(x.Code, code)))
                throw new InvalidOperationException("Mevcut ödeme sıklığıyla çakışan tanım var; otomatik birleştirilmedi.");
            db.Add(new CollectionPaymentFrequency { Code = code, Name = name, IntervalMonths = months,
                DisplayOrder = months, IsActive = true });
        }
        await AddMissing(db, new[] { ("EXISTS", "VAR"), ("NONE", "YOK"), ("UNKNOWN", "Belirtilmemiş") },
            (code, name) => new CollectionContractStatus { Code = code, Name = name, IsActive = true }, x => x.Code, x => x.Name, ct);
        await AddMissing(db, new[] { ("ACTIVE", "Aktif"), ("FROZEN", "Donuk") },
            (code, name) => new CollectionSubscriptionStatus { Code = code, Name = name, IsActive = true }, x => x.Code, x => x.Name, ct);
        await AddMissing(db, new[] { ("OFFSET", "Mahsup"), ("CANCELLED", "İptal"), ("PENDING", "Beklemede"),
            ("FREE", "Ücretsiz"), ("INVOICED", "Fatura Kesildi"), ("PAID", "Ödendi"), ("APPROVED", "Onaylandı") },
            (code, name) => new CollectionGroupStatus { Code = code, Name = name, IsActive = true }, x => x.Code, x => x.Name, ct);
        await AddMissing(db, new[] { ("FINANSBANK_POS", "Finansbank POS"), ("GTS", "GTS"), ("IVR", "IVR"),
            ("WEB", "Web"), ("MAIL_ORDER", "Mail Order"), ("OFFSET", "Mahsup"),
            ("CASH_MANUAL", "Nakit / Manuel"), ("LEGACY_FREE", "Ücretsiz"),
            ("ISBANK_POS", "İş Bankası POS"), ("BANK_TRANSFER", "Banka Havalesi") },
            // Preserve historical identities; active new-contract methods require separate selection policy.
            (code, name) => new CollectionPaymentMethod { Code = code, Name = name, IsActive = false }, x => x.Code, x => x.Name, ct);
    }

    private static bool SameName(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static async Task AddMissing<T>(DbContext db, IEnumerable<(string Code, string Name)> definitions,
        Func<string, string, T> create, Func<T, string> codeOf, Func<T, string> nameOf, CancellationToken ct) where T : class
    {
        var existing = await db.Set<T>().AsNoTracking().ToListAsync(ct);
        foreach (var (code, name) in definitions)
        {
            if (existing.Any(x => codeOf(x) == code)) continue;
            if (existing.Any(x => SameName(codeOf(x), code) || SameName(nameOf(x), name)))
                throw new InvalidOperationException("Mevcut tahsilat tanımıyla ad/kod çakışması var; kayıt değiştirilmedi.");
            db.Add(create(code, name));
        }
    }
}
