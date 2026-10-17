using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;

namespace Data.Seeding.Seeds;

/// <summary>Adds only the collection SMS parameter, never overwrites an edited template.</summary>
public sealed class CollectionSmsConfigurationSeed : IDataSeed
{
    public string Key => "collection.sms.configuration.v1";
    public int Order => 41;
    public Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct) => Task.FromResult(true);
    public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("SMS konfigürasyon seed işlemi transaction içinde çalışmalıdır.");
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'CollectionSmsConfigurationSeed',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, N'SMS konfigürasyon kilidi alınamadı.', 1;
            """, ct);
        if (await db.Set<Configuration>().AnyAsync(x => x.Name == CollectionSmsConstants.RateChangeTemplate, ct)) return;
        db.Add(new Configuration
        {
            Name = CollectionSmsConstants.RateChangeTemplate,
            Value = CollectionSmsConstants.DefaultRateChangeTemplate,
            Description = "Tahsilat zam SMS şablonu. Parametreler: {MusteriAdi}, {AboneNo}, {HizmetAdi}, {EskiTutar}, {YeniTutar}, {ZamOrani}, {ParaBirimi}, {OdemeDonemi}, {GecerlilikTarihi}."
        });
        await db.SaveChangesAsync(ct);
    }
}
