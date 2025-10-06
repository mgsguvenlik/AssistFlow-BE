// Data/Seeding/Seeds/TurkeyCitiesSeed.cs
using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using System.Text.Json;

namespace Data.Seeding.Seeds
{
    public class TurkeyCitiesSeed : IDataSeed
    {
        private readonly ILogger<TurkeyCitiesSeed> _logger;
        public TurkeyCitiesSeed(ILogger<TurkeyCitiesSeed> logger) => _logger = logger;

        public string Key => "seed.cities.regions.v1"; // SeedHistory için benzersiz anahtar
        public int Order => 10; // sıralama

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            // City tablosu tamamen boşsa çalış
            return !await db.Set<City>().AnyAsync(ct);
        }

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            // 1) JSON dosya yolu (uygun gördüğünü kullan)
            var candidates = new[]
               {
                   // Çıktı köküne kopyalanmış olabilir
                   Path.Combine(AppContext.BaseDirectory, "turkey-cities.json"),
               
                   // Çıktıya 'seed/turkey-cities.json' olarak linklenmiş olabilir
                   Path.Combine(AppContext.BaseDirectory, "seed", "turkey-cities.json"),
               
                   // Data projesindeki klasör yapısıyla kopyalanmış olabilir
                   Path.Combine(AppContext.BaseDirectory, "Seeding", "DataFiles", "turkey-cities.json"),
                   Path.Combine(AppContext.BaseDirectory, "Data", "Seeding", "DataFiles", "turkey-cities.json"),
               
                   // Geliştirme sırasında CurrentDirectory üzerinden de deneyelim
                   Path.Combine(Directory.GetCurrentDirectory(), "Data", "Seeding", "DataFiles", "turkey-cities.json"),
                   Path.Combine(Directory.GetCurrentDirectory(), "Seeding", "DataFiles", "turkey-cities.json"),
               };

            var path = candidates.FirstOrDefault(File.Exists);
            if (path is null)
                throw new FileNotFoundException(Messages.TurkeyCitiesJsonBulunamadi);

            // 2) JSON oku
            var json = await File.ReadAllTextAsync(path, ct);
            var docs = JsonSerializer.Deserialize<List<CityDoc>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            if (docs.Count == 0)
            {
                _logger.LogWarning(Messages.TurkeyCitiesJsonEmpty);
                return;
            }

            // 3) City/Region entity’lerine dönüştür
            // İSTERSEN büyük harfleri Title Case yapmak için Normalize fonksiyonunu kullanabilirsin (aşağıda var).
            var cities = docs.Select(d => new City
            {
                Name = d.Name!.Trim(),               // veya Normalize(d.Name!)

                Code = d.Code?.Trim(),
                Regions = (d.Regions ?? new())
                         .Where(r => !string.IsNullOrWhiteSpace(r))
                         .Select(r => r.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(r => r, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
                         .Select(r => new Region { Name = r /* veya Normalize(r) */ })
                         .ToList()
            }).ToList();

            // 4) Ekle & kaydet
            await db.Set<City>().AddRangeAsync(cities, ct);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(Messages.CityRegionSeedCompleted,
                cities.Count, cities.Sum(c => c.Regions.Count));
        }

        // JSON şeması (senin verdiğinle birebir)
        private sealed class CityDoc
        {
            public string? Name { get; set; }    // "ADANA"
            public string? Code { get; set; }    // "01"
            public List<string>? Regions { get; set; }
        }

    }
}
