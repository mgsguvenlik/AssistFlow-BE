using Core.Utilities.Constants;
using Data.Seeding.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;

namespace Data.Seeding.Seeds
{
    public class ConfigSeed : IDataSeed
    {
        private readonly ILogger<ConfigSeed> _logger;
        public ConfigSeed(ILogger<ConfigSeed> logger)
        {
            _logger = logger;
        }
        public string Key => CommonConstants.SeedConfiguration; // SeedHistory için benzersiz anahtar
        public int Order => 10; // sıralama
        public async Task RunAsync(DbContext db, IServiceProvider sp, CancellationToken ct)
        {
            var configs = new List<Configuration>
             {
                 new() { Name = "TestName",                        Value = "TestValue",           Description = "Test Desc" },
                 new() { Name = "MailIsHtml",                      Value = "1",                   Description = "E-posta gövdesi HTML mi? (1: Evet, 0: Hayır)" },
                 new() { Name = "CallUs",                          Value = "02123555000",         Description = "Mgs şirket telefon numarası" },
                 new() { Name = "MailDomain",                      Value = "domain",              Description = "SMTP domain (gerekirse)" },
                 new() { Name = "MailAttachment",                  Value = "Attachmetn",          Description = "Mail Gönderim.  Ek dosya yolu (varsa)" },
                 new() { Name = "Office",                          Value = "Papirus Plaza, Ayazma Cd. No:37 K:13,Kağıthane/İstanbul", Description = "Mgs şirket açık adresi" },
                 new() { Name = "EmailUs",                         Value = "info@medalarm.com.tr", Description = "Mgs şirket mail adresi" },
                 new() { Name = "X",                               Value = "#",                   Description = "Mgs şirket x adresi" },
                 new() { Name = "MailServerPort",                  Value = "588",                 Description = "SMTP sunucu portu" },
                 new() { Name = "MailFrom",                        Value = "mgs@mgs.com.tr",      Description = "Gönderen e-posta adresi" },
                 new() { Name = "MailCcs",                         Value = "MailCcs",             Description = "CC e-posta adresleri (noktalı virgül/virgül ile ayrılmış)" },
                 new() { Name = "MailUseCredential",               Value = "1",                   Description = "SMTP için kimlik doğrulama kullanılsın mı? (1: Evet, 0: Hayır)" },
                 new() { Name = "MailFromName",                    Value = "MGS Müşteri",         Description = "Gönderen görünen adı" },
                 new() { Name = "MailUser",                        Value = "rapor@mgs.com.tr",    Description = "SMTP kullanıcı adı" },
                 new() { Name = "MailUseSSL",                      Value = "1",                   Description = "SMTP için SSL kullanılsın mı? (1: Evet, 0: Hayır)" },
                 new() { Name = "MailTos",                         Value = "burkmez@mgs.com.tr",  Description = "Alıcı e-posta adresleri (noktalı virgül/virgül ile ayrılmış)" },
                 new() { Name = "MailServer",                      Value = "mail.mgs.com.tr",     Description = "SMTP sunucu adresi" },
                 new() { Name = "MailPassword",                    Value = "Admin@2018",          Description = "SMTP şifresi" },
                 new() { Name = "TechnicianCustomerMinDistanceKm", Value = "3",                   Description = "Teknisyen müşteri arası minimum mesafe (km)" },
                 new() { Name = "TechnicalServiceManagerEmails",   Value = "karamehmetzeki506@gmail.com",  Description = "Teknisyen lokasyon bildirimi maili" },
             };

            var configSet = db.Set<Configuration>();

            // Veritabanında zaten olan Name'leri çek
            var names = configs.Select(c => c.Name).ToList();

            var existingNames = await configSet
                .Where(c => names.Contains(c.Name))
                .Select(c => c.Name)
                .ToListAsync(ct);

            // Sadece veritabanında olmayanları ekle
            var toAdd = configs
                .Where(c => !existingNames.Contains(c.Name))
                .ToList();

            if (toAdd.Count > 0)
            {
                await configSet.AddRangeAsync(toAdd, ct);
                await db.SaveChangesAsync(ct);
            }

            _logger.LogInformation(Messages.ConfigurationSeedCompleted, toAdd.Count);
        }

        public async Task<bool> ShouldRunAsync(DbContext db, CancellationToken ct)
        {
            return !await db.Set<Configuration>().AnyAsync(ct);
        }
    }
}
