using Business.Services;
using Business.UnitOfWork;
using Data.Concrete;
using Data.Concrete.EfCore.Context;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using Model.Concrete;
using Core.Common;

internal sealed class ProductTestContext(DbContextOptions<AppDataContext> options) : AppDataContext(options)
{
    protected override void OnModelCreating(ModelBuilder model)
    {
        var keep = new[] { typeof(Product), typeof(Brand), typeof(Model.Concrete.Model), typeof(ProductType),
            typeof(CurrencyType), typeof(SystemType), typeof(Customer), typeof(CustomerGroup), typeof(Tenant),
            typeof(CustomerProductPrice), typeof(CustomerGroupProductPrice), typeof(TenantProductPrice) };
        foreach (var property in typeof(AppDataContext).GetProperties())
            if (property.PropertyType.IsGenericType && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            {
                var type = property.PropertyType.GetGenericArguments()[0];
                if (!keep.Contains(type)) model.Ignore(type);
            }
        foreach (var type in keep)
        {
            var entity = model.Entity(type);
            foreach (var property in type.GetProperties())
            {
                var target = property.PropertyType;
                if (target.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(target))
                    target = target.GetGenericArguments()[0];
                if (typeof(BaseEntity).IsAssignableFrom(target) && !keep.Contains(target)) entity.Ignore(property.Name);
            }
        }
        model.UseCollation("Turkish_100_CI_AS");
        model.Entity<Product>().Property(x => x.Price).HasPrecision(18, 2);
        model.Entity<Product>().Property(x => x.ServiceFeePercentage).HasPrecision(18, 2);
    }
}

internal static class ProductSearchTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    public static async Task Main()
    {
        // The connection is generated here; application configuration is never loaded.
        var database = "MgsProductSearchTests_" + Guid.NewGuid().ToString("N");
        var connection = $"Server=(localdb)\\MSSQLLocalDB;Database={database};Integrated Security=true;TrustServerCertificate=true";
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection).Options;
        await using var db = new ProductTestContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var brand = new Brand { Name = "Bosch", Desc = "Alman üretimi" };
            var model = new Model.Concrete.Model { Name = "FPA-5000", Desc = "Yangın paneli", Brand = brand };
            var type = new ProductType { Type = "Donanım", Code = "HW" };
            var currency = new CurrencyType { Code = "USD", Name = "ABD Doları" };
            var product = new Product { ProductCode = "PRD-001", OracleProductCode = "ORACLE-PROD", Description = "100% uyumlu kontrol paneli",
                SystemType = "Alarm (85887)", Brand = brand, Model = model, ProductType = type, CurrencyType = currency,
                Price = 1250.50m, PriceCurrency = "TRY", CorporateCustomerShortCode = "KURUM-01", OracleCustomerCode = "ORACLE-CUSTOMER" };
            var legacyCode = new Product { ProductCode = "LEGACY-CODE", SystemType = "85887", ProductType = type };
            var legacyName = new Product { ProductCode = "LEGACY-NAME", SystemType = "Alarm", ProductType = type };
            var renamed = new Product { ProductCode = "RENAMED", SystemType = "Eski ad (85887)", ProductType = type };
            var deleted = new Product { ProductCode = "DELETED", SystemType = "Alarm (85887)", Description = "100% uyumlu", Price = 1250.50m, IsDeleted = true };
            var unavailable = new Product { ProductCode = "OTHER-TENANT", SystemType = "Alarm (85887)" };
            var fee = new Product { ProductCode = "FEE", IsServiceFeeProduct = true, ServiceFeePercentage = 18.75m };
            var blank = new Product { ProductCode = "BLANK" };
            db.AddRange(product, legacyCode, legacyName, renamed, deleted, unavailable, fee, blank,
                new SystemType { Name = "Alarm", Code = "85887" }, new SystemType { Name = "Kamera", Code = "CAM" });
            var tenants = new[] { "YKB", "EKB", "QNB", "BIREYSEL", "EMPTY" }.Select(code => new Tenant { Name = code, Code = code }).ToArray();
            var group = new CustomerGroup { GroupName = "Özel grup", Code = "GROUP" };
            var customers = tenants.Select((tenant, index) => new Customer { SubscriberCompany = tenant.Name, Tenant = tenant,
                CustomerGroup = index == 0 ? group : null }).ToArray();
            var individual = new Customer { SubscriberCompany = "Tenant olmayan müşteri" };
            db.AddRange(customers);
            db.Add(individual);
            await db.SaveChangesAsync();
            foreach (var tenant in tenants.Take(4))
                foreach (var item in new[] { product, legacyCode, legacyName, renamed, deleted, fee, blank })
                    db.Add(new TenantProductPrice { Tenant = tenant, Product = item, Price = item == product ? 999.99m : 10m, CurrencyCode = "JPY" });
            db.Add(new CustomerProductPrice { Customer = customers[0], Product = product, Price = 888.88m, CurrencyCode = "GBP" });
            db.Add(new CustomerGroupProductPrice { CustomerGroup = group, Product = product, Price = 777.77m, CurrencyCode = "CHF" });
            db.Add(new CustomerProductPrice { Customer = individual, Product = product, Price = 666.66m, CurrencyCode = "ZAR" });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var config = new TypeAdapterConfig();
            var service = new ProductService(new UnitOfWork(new Repository(db)), new Mapper(config), config);
            foreach (var customer in customers.Take(4))
            {
                foreach (var term in new[] { "PRD-001", "ORACLE-PROD", "kontrol paneli", "Alarm", "85887", "bosch", "Alman üretimi", "FPA-5000",
                    "Yangın paneli", "Donanım", "HW", "USD", "ABD Doları", "TRY", "KURUM-01", "ORACLE-CUSTOMER", "1250,50", "1250.50", "1.250,50", "1,250.50", "%" })
                {
                    var response = await service.GetEffectivePriceByCustomerAsync(new() { Search = "  " + term + "  ", PageSize = 50 }, customer.Id);
                    Check(response.IsSuccess && response.Data!.Items.Any(x => x.ProductId == product.Id), customer.SubscriberCompany + " search: " + term);
                    Check(response.Data!.Items.All(x => x.ProductId != deleted.Id && x.ProductId != unavailable.Id), "Tenant/deleted exclusion: " + term);
                }
                foreach (var term in new[] { "Alarm", "85887" })
                {
                    var results = (await service.GetEffectivePriceByCustomerAsync(new() { Search = term }, customer.Id)).Data!.Items;
                    Check(new[] { product.Id, legacyCode.Id, legacyName.Id, renamed.Id }.All(id => results.Any(x => x.ProductId == id)), "Full/name/code legacy system matching: " + term);
                }
                var effective = customer.Id == customers[0].Id ? "777,77" : "999,99";
                var currencyTerm = customer.Id == customers[0].Id ? "CHF" : "JPY";
                foreach (var term in new[] { effective, currencyTerm })
                {
                    var result = (await service.GetEffectivePriceByCustomerAsync(new() { Search = term }, customer.Id)).Data!.Items.Single(x => x.ProductId == product.Id);
                    Check(result.EffectivePrice == (customer.Id == customers[0].Id ? 777.77m : 999.99m), "Effective-price hierarchy preserved");
                    Check(result.SystemType == product.SystemType, "System type returned to modal");
                }
                var fees = (await service.GetEffectivePriceByCustomerAsync(new() { Search = "hizmet bedeli" }, customer.Id)).Data!.Items;
                Check(fees.Count == 1 && fees[0].ProductId == fee.Id, "Service fee category");
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = "HİZMET BEDELİ" }, customer.Id)).Data!.Items.Single().ProductId == fee.Id, "Turkish case-insensitive category");
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = "normal ürün" }, customer.Id)).Data!.Items.All(x => x.ProductId != fee.Id), "Ordinary product category");
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = "18,75" }, customer.Id)).Data!.Items.Any(x => x.ProductId == fee.Id), "Service fee percentage");
                var page = (await service.GetEffectivePriceByCustomerAsync(new() { Search = "Alarm", Page = 2, PageSize = 2 }, customer.Id)).Data!;
                Check(page.TotalCount == 4 && page.Items.Count == 2, "Search before count/pagination");
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = "olmayan-bilgi" }, customer.Id)).Data!.TotalCount == 0, "No match");
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = " " }, customer.Id)).Data!.TotalCount == 6, "Empty term retains eligible catalogue");
            }
            foreach (var term in new[] { "888,88", "GBP" })
                Check(!(await service.GetEffectivePriceByCustomerAsync(new() { Search = term }, customers[0].Id)).Data!.Items.Any(x => x.ProductId == product.Id), "Lower-priority override does not match");
            Check((await service.GetEffectivePriceByCustomerAsync(new(), customers[4].Id)).Data!.TotalCount == 0, "Tenant without products");
            Check(!(await service.GetEffectivePriceByCustomerAsync(new(), long.MaxValue)).IsSuccess, "Missing customer");
            Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = "666,66" }, individual.Id)).Data!.Items.Any(x => x.ProductId == product.Id), "Customer-only price");
            Check((await service.GetPagedAsync(new() { Search = product.Id.ToString() })).Data!.Items.Any(x => x.Id == product.Id), "General catalogue numeric ID search preserved");
            var sorted = (await service.GetEffectivePriceByCustomerAsync(new() { Search = "Alarm", Sort = "ProductCode" }, customers[0].Id)).Data!.Items;
            Check(sorted.Select(x => x.ProductCode).SequenceEqual(sorted.Select(x => x.ProductCode).OrderBy(x => x)), "Catalogue sort preserved");
            foreach (var term in new[] { "Alarm", "85887", "Bosch", "HW", "ABD Doları", "1250,50" })
            {
                Check((await service.GetPagedAsync(new() { Search = term })).Data!.Items.Any(x => x.Id == product.Id), "General catalogue: " + term);
                Check((await service.GetPurchaseProductsAsync(new() { Search = term })).Data!.Items.Any(x => x.Id == product.Id), "Purchase catalogue: " + term);
                Check((await service.GetEffectivePriceByCustomerAsync(new() { Search = term }, null)).Data!.Items.Any(x => x.ProductId == product.Id), "No-customer fallback: " + term);
            }
            Console.WriteLine($"PASS: {checks} SQL product-search checks across all four tenants");
        }
        finally
        {
            if (!database.StartsWith("MgsProductSearchTests_", StringComparison.Ordinal)) throw new InvalidOperationException("Unsafe test database name");
            await db.Database.EnsureDeletedAsync();
        }
    }
}
