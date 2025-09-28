using Microsoft.EntityFrameworkCore;

namespace Data.Seeding.Abstractions
{
    public interface IDataSeed
    {
        /// Benzersiz anahtar (örn: "seed.cities.regions.v1")
        string Key { get; }

        /// Çalışma sırası (küçükten büyüğe)
        int Order { get; }

        /// Çalıştırmadan önce koşul (örn: tablo boş mu?)
        Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct);

        /// Seed’i çalıştır (işlem burada)
        Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct);
    }
}
