// Data/Seeding/Seeds/TurkeyCitiesSeed.cs
using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using System;
using System.Text.Json;

namespace Data.Seeding.Seeds
{
    public class TurkeyCitiesSeed : IDataSeed
    {
        private readonly ILogger<TurkeyCitiesSeed> _logger;
        public TurkeyCitiesSeed(ILogger<TurkeyCitiesSeed> logger) => _logger = logger;

        public string Key => CommonConstants.SeedCitiesRegions; // SeedHistory için benzersiz anahtar
        public int Order => 10; // sıralama

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            // City tablosu tamamen boşsa çalış
            return !await db.Set<City>().AnyAsync(ct);
        }

        public async Task RunAsync_(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            // 1) JSON dosya yolu (uygun gördüğünü kullan)
            var candidates = new[]
               {
                   // Çıktı köküne kopyalanmış olabilir
                   Path.Combine(AppContext.BaseDirectory,CommonConstants.TurkeyCities),
               
                   // Çıktıya 'seed/turkey-cities.json' olarak linklenmiş olabilir
                   Path.Combine(AppContext.BaseDirectory, CommonConstants.seed, CommonConstants.TurkeyCities),
               
                   // Data projesindeki klasör yapısıyla kopyalanmış olabilir
                   Path.Combine(AppContext.BaseDirectory, CommonConstants.Seeding, CommonConstants.DataFiles, CommonConstants.TurkeyCities),
                   Path.Combine(AppContext.BaseDirectory,CommonConstants.Data,CommonConstants.Seeding,CommonConstants.DataFiles, CommonConstants.TurkeyCities),
               
                   // Geliştirme sırasında CurrentDirectory üzerinden de deneyelim
                   Path.Combine(Directory.GetCurrentDirectory(), CommonConstants.Data, CommonConstants.Seeding, CommonConstants.DataFiles,  CommonConstants.TurkeyCities),
                   Path.Combine(Directory.GetCurrentDirectory(), CommonConstants.Seeding, CommonConstants.DataFiles,  CommonConstants.TurkeyCities),
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
                         .OrderBy(r => r, StringComparer.Create(new System.Globalization.CultureInfo(CommonConstants.TrTR), true))
                         .Select(r => new Region { Name = r /* veya Normalize(r) */ })
                         .ToList()
            }).ToList();

            // 4) Ekle & kaydet
            await db.Set<City>().AddRangeAsync(cities, ct);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(Messages.CityRegionSeedCompleted,
                cities.Count, cities.Sum(c => c.Regions.Count));
        }

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            var candidates = new[]
            {
                 Path.Combine(AppContext.BaseDirectory, CommonConstants.TurkeyCities),
                 Path.Combine(AppContext.BaseDirectory, CommonConstants.seed, CommonConstants.TurkeyCities),
                 Path.Combine(AppContext.BaseDirectory, CommonConstants.Seeding, CommonConstants.DataFiles, CommonConstants.TurkeyCities),
                 Path.Combine(AppContext.BaseDirectory, CommonConstants.Data, CommonConstants.Seeding, CommonConstants.DataFiles, CommonConstants.TurkeyCities),
                 Path.Combine(Directory.GetCurrentDirectory(), CommonConstants.Data, CommonConstants.Seeding, CommonConstants.DataFiles, CommonConstants.TurkeyCities),
                 Path.Combine(Directory.GetCurrentDirectory(), CommonConstants.Seeding, CommonConstants.DataFiles, CommonConstants.TurkeyCities),
                    };

            var path = candidates.FirstOrDefault(File.Exists);
            if (path is null)
                throw new FileNotFoundException(Messages.TurkeyCitiesJsonBulunamadi);

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

            var citiesFromJson = docs.Select(d => new City
            {
                Name = d.Name!.Trim(),
                Code = d.Code?.Trim(),
                Regions = (d.Regions ?? new())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => r.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(r => r, StringComparer.Create(new System.Globalization.CultureInfo(CommonConstants.TrTR), true))
                    .Select(r => new Region { Name = r })
                    .ToList()
            }).ToList();

            var citySet = db.Set<City>();
            var regionSet = db.Set<Region>();

            var existingCities = await citySet
                .Include(c => c.Regions)
                .ToListAsync(ct);

            var trComparer = StringComparer.Create(
                new System.Globalization.CultureInfo(CommonConstants.TrTR),
                ignoreCase: true
            );

            int addedCityCount = 0;
            int addedRegionCount = 0;

            foreach (var seedCity in citiesFromJson)
            {
                var existingCity = existingCities.FirstOrDefault(c =>
                    (!string.IsNullOrWhiteSpace(seedCity.Code) && c.Code == seedCity.Code) ||
                    trComparer.Equals(c.Name, seedCity.Name)  // ✅ burada comparer'ı böyle kullan
                );

                if (existingCity is null)
                {
                    await citySet.AddAsync(seedCity, ct);
                    existingCities.Add(seedCity);

                    addedCityCount++;
                    addedRegionCount += seedCity.Regions.Count;
                }
                else
                {
                    var existingRegionNames = new HashSet<string>(
                        existingCity.Regions.Select(r => r.Name),
                        trComparer
                    );

                    foreach (var region in seedCity.Regions)
                    {
                        if (!existingRegionNames.Contains(region.Name))
                        {
                            var newRegion = new Region
                            {
                                Name = region.Name,
                                City = existingCity
                            };

                            await regionSet.AddAsync(newRegion, ct);
                            existingCity.Regions.Add(newRegion);

                            existingRegionNames.Add(region.Name);
                            addedRegionCount++;
                        }
                    }
                }
            }

            if (addedCityCount > 0 || addedRegionCount > 0)
                await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                Messages.CityRegionSeedCompleted,
                addedCityCount,
                addedRegionCount
            );
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
