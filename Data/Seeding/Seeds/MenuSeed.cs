using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;

namespace Data.Seeding.Seeds
{
    public class MenuSeed : IDataSeed
    {
        private readonly ILogger<MenuSeed> _logger;
        public MenuSeed(ILogger<MenuSeed> logger) => _logger = logger;

        public string Key => "SeedMenus";
        public int Order => 20;

        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            // Code -> Türkçe açıklama
            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Dashboard"] = "Kontrol Paneli",
                ["UserList"] = "Kullanıcı Listesi",
                ["UserDetail"] = "Kullanıcı Detayı",
                ["RoleList"] = "Rol Listesi",
                ["RoleListPermission"] = "Rol Yetkileri",
                ["CustomerList"] = "Müşteri Listesi",
                ["CustomerGroupList"] = "Müşteri Grup Listesi",
                ["CustomerTypeList"] = "Müşteri Tipi Listesi",
                ["ServiceRequestCreate"] = "Servis Talebi Oluştur",
                ["ServiceRequestWarehouse"] = "Servis Talebi - Depo",
                ["ServiceRequestTechnicalService"] = "Servis Talebi - Teknik Servis",
                ["ServiceRequestPricing"] = "Servis Talebi - Fiyatlandırma",
                ["ServiceRequestFinalApproval"] = "Servis Talebi - Son Onay",
                ["ServiceRequestList"] = "Servis Talep Listesi",
                ["ProductList"] = "Ürün Listesi",
                ["ProductTypeList"] = "Ürün Tipi Listesi",
                ["SystemTypeList"] = "Sistem Tipi Listesi",
                ["CurrencyTypeList"] = "Para Birimi Tipi Listesi",
                ["BrandList"] = "Marka Listesi",
                ["ModelList"] = "Model Listesi",
                ["ServiceReportsList"] = "Servis Rapor Listesi",
                ["ConfigurationList"] = "Konfigürasyon Listesi",
                ["ServiceTypeList"] = "Servis Tipi Listesi",
                ["MenuList"] = "Menü Listesi",
                ["FlowStatusList"] = "Akış Durum Listesi",
                ["Mailbox"] = "Posta Kutusu"
            };

            var set = db.Set<Menu>();
            var existingNames = await set.AsNoTracking()
                                         .Select(m => m.Name)
                                         .ToListAsync(ct);

            var toInsert = items
                .Where(kv => !existingNames.Contains(kv.Key, StringComparer.OrdinalIgnoreCase))
                .Select(kv => new Menu
                {
                    // Name = Code
                    Name = kv.Key,
                    // Description = Türkçe karşılığı
                    Description = kv.Value
                })
                .ToList();

            if (toInsert.Count == 0)
            {
                _logger.LogInformation("Menu seed: eklenecek kayıt yok (tüm menüler mevcut).");
                return;
            }

            await set.AddRangeAsync(toInsert, ct);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("Menu seed tamamlandı. Eklenen kayıt sayısı: {Count}", toInsert.Count);
        }

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            // Tüm menüler mevcutsa false döner; biri bile eksikse seed çalışsın
            var required = new[]
            {
                "Dashboard","UserList","UserDetail","RoleList","RoleListPermission",
                "CustomerList","CustomerGroupList","CustomerTypeList","ServiceRequestCreate",
                "ServiceRequestWarehouse","ServiceRequestTechnicalService","ServiceRequestPricing",
                "ServiceRequestFinalApproval","ServiceRequestList","ProductList","ProductTypeList",
                "SystemTypeList","CurrencyTypeList","BrandList","ModelList","ServiceReportsList",
                "ConfigurationList","ServiceTypeList","MenuList","FlowStatusList","Mailbox"
            };

            var existing = await db.Set<Menu>()
                                   .AsNoTracking()
                                   .Select(m => m.Name)
                                   .ToListAsync(ct);

            var missing = required.Except(existing, StringComparer.OrdinalIgnoreCase);
            return missing.Any();
        }
    }
}
