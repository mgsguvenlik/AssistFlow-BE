using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionGroupParentTransfer
{
    private const string MigrationId = "20260927160000_AddCollectionGroupParent";
    public static async Task RunAsync(string settingsPath, string outputPath, string? expectedHash, bool install)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Üst kart uygulaması yalnız AssistFlowTest üzerinde çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, o => o.CommandTimeout(120)).Options);
        if (install)
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
            if (pending.Length > 0 && (pending.Length != 1 || pending[0] != MigrationId))
                throw new InvalidOperationException("Yalnız üst kart migrationı uygulanabilir; diğer bekleyen migrationlar önce incelenmelidir.");
            if (pending.Length == 1) await db.Database.MigrateAsync();
            Console.WriteLine("collection.GroupParent şeması hazır.");
            return;
        }
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        if (expectedHash is not null && expectedHash != "--verify")
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @r int;
                EXEC @r=sys.sp_getapplock @Resource=N'CollectionGroupParentTransfer',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;
                IF @r<0 THROW 51000,N'Üst kart aktarım kilidi alınamadı.',1;
                """);
        var source = await db.Database.SqlQueryRaw<Source>("""
            SELECT p.CustomerID AS LegacyCustomerId,p.Name,p.SubscriberNo,p.AccountNo,p.GroupCardNo AS GroupCode,
            CAST(CASE WHEN EXISTS(SELECT 1 FROM MGS.Definition.[Group] g WHERE g.Code=p.GroupCardNo AND g.GroupType=N'Kurumsal') THEN 1 ELSE 0 END AS bit) AS IsCorporate
            FROM MGS.Core.Customer p WHERE p.Type='G'
            """).ToListAsync();
        var scoped = source.Where(x => CollectionCustomerClassification.Classify(x.GroupCode) == CollectionCustomerClass.Group).OrderBy(x => x.LegacyCustomerId).ToArray();
        var customers = await db.Customers.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.SubscriberCode, x.SubscriberCompany, x.CustomerGroupId, x.CustomerTypeId, x.TenantId, x.IsDeleted }).ToListAsync();
        var groups = await db.CustomerGroups.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.Code }).ToListAsync();
        var types = await db.Set<CustomerType>().OrderBy(x => x.Id).ToListAsync();
        var maps = await db.Set<CollectionGroupParent>().AsNoTracking().OrderBy(x => x.CustomerId).ToListAsync();
        var actor = await db.Set<CollectionMigrationBatch>().AsNoTracking().OrderBy(x => x.Id).Select(x => x.CreatedUser).FirstAsync();
        // Legacy aktarım batch'inde 0 otomatik aktarım aktörüdür; gerçek kullanıcı kimliği uydurulmaz.
        if (actor < 0) throw new InvalidOperationException("Aktarım denetim kullanıcısı geçersiz.");
        var parentTypes = types.Where(x => N(x.Code) == "G").ToArray();
        if (parentTypes.Length > 1) throw new InvalidOperationException("Üst kart müşteri tipi tekil değil.");
        var decisions = new List<Decision>();
        foreach (var s in scoped)
        {
            var code = N(s.GroupCode);
            var name = NameKey(s.Name);
            var subscriber = N(s.SubscriberNo);
            var group = groups.Where(x => N(x.Code) == code).ToArray();
            var old = maps.SingleOrDefault(x => x.LegacyCustomerId == s.LegacyCustomerId);
            var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(s));
            if (old is not null)
            {
                var c = customers.SingleOrDefault(x => x.Id == old.CustomerId);
                var valid = group.Length == 1 && old.CustomerGroupId == group[0].Id && old.SourceHash.SequenceEqual(hash)
                    && c is not null && !c.IsDeleted && c.CustomerGroupId == old.CustomerGroupId
                    && parentTypes.Length == 1 && c.CustomerTypeId == parentTypes[0].Id
                    && c.SubscriberCompany == s.Name?.Trim()
                    && c.SubscriberCode == (string.IsNullOrWhiteSpace(s.SubscriberNo) ? null : s.SubscriberNo.Trim())
                    && old.IsCorporate == s.IsCorporate
                    && old.AccountNo == (string.IsNullOrWhiteSpace(s.AccountNo) ? null : s.AccountNo.Trim());
                decisions.Add(new(s.LegacyCustomerId, valid ? "AlreadyApplied" : "ExistingMappingChanged", old.CustomerGroupId, []));
                continue;
            }
            var candidates = customers.Where(c =>
                (subscriber.Length > 0 && N(c.SubscriberCode) == subscriber) || N(c.SubscriberCode) == code
                || (name.Length > 0 && NameKey(c.SubscriberCompany) == name)
                || (name.Length >= 8 && NameKey(c.SubscriberCompany).StartsWith(name[..8], StringComparison.Ordinal))).Select(c => c.Id).ToArray();
            var duplicate = scoped.Count(x => N(x.GroupCode) == code) != 1
                || (name.Length > 0 && scoped.Count(x => NameKey(x.Name) == name) > 1)
                || (subscriber.Length > 0 && scoped.Count(x => N(x.SubscriberNo) == subscriber) > 1);
            var status = group.Length != 1 ? "GroupNotUnique" : duplicate ? "SourceNotUnique"
                : name.Length == 0 ? "NameEmpty" : candidates.Length > 0 ? "CustomerCandidates"
                : s.AccountNo?.Length > 200 ? "AccountTooLong" : "Create";
            decisions.Add(new(s.LegacyCustomerId, status, group.Length == 1 ? group[0].Id : null, candidates));
        }
        var material = new { Version = 1, Policy = CollectionCustomerClassification.PolicyHash, Source = scoped, Customers = customers, Groups = groups, Types = types.Select(x => new { x.Id, x.Code, x.Name }), Maps = maps, Actor = actor, Decisions = decisions };
        var planHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(material)));
        // Önizleme ve uygulama kanıtı hassas veri içerir; yalnız yerel/Git dışı dosyada tutulur.
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { PlanHash = planHash, Applied = false, Plan = material }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Üst kart planı: {decisions.Count(x => x.Status == "Create")} yeni; {decisions.Count(x => x.Status == "AlreadyApplied")} önceden uygulanmış; {decisions.Count(x => x.Status is not ("Create" or "AlreadyApplied"))} inceleme. SHA-256: {planHash}");
        if (expectedHash is null) return;
        if (expectedHash == "--verify")
        {
            if (decisions.Any(x => x.Status == "ExistingMappingChanged")) throw new InvalidOperationException("Üst kart alan mutabakatı başarısız.");
            var service = new Business.Services.Crm.Collections.CollectionGroupContextService(db);
            foreach (var map in maps.OrderByDescending(x => x.IsCorporate).Take(3))
            {
                var result = await service.GetAsync(map.CustomerGroupId);
                if (result.Data?.ParentCustomerId != map.CustomerId || result.Data.IsCorporate != map.IsCorporate || result.Data.AccountNo != map.AccountNo)
                    throw new InvalidOperationException("Grup bağlamı servis mutabakatı başarısız.");
            }
            var invalid = await service.GetAsync(0);
            if (invalid.IsSuccess) throw new InvalidOperationException("Geçersiz grup kabul edildi.");
            Console.WriteLine($"{maps.Count} üst kart kimlik/alan mutabakatı, 3 servis örneği ve kapsam reddi doğrulandı. Bekleyen model değişikliği: {db.Database.HasPendingModelChanges()}. Yalnız okuma.");
            return;
        }
        if (!planHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Plan değişti; yeniden önizleme gereklidir.");
        if (decisions.Any(x => x.Status == "ExistingMappingChanged")) throw new InvalidOperationException("Mevcut üst kart ilişkisi değişmiş; uygulama durduruldu.");
        var create = decisions.Where(x => x.Status == "Create").ToArray();
        if (create.Length == 0) { Console.WriteLine("Yeni kayıt yok; veriler değiştirilmedi."); return; }
        var type = parentTypes.SingleOrDefault();
        if (type is null)
        {
            type = new CustomerType { Code = "G", Name = "Grup Üst Müşteri" };
            db.Set<CustomerType>().Add(type);
            await db.SaveChangesAsync();
        }
        var created = new List<object>();
        foreach (var d in create)
        {
            var s = scoped.Single(x => x.LegacyCustomerId == d.LegacyCustomerId);
            var now = DateTimeOffset.UtcNow;
            var customer = new Customer
            {
                SubscriberCompany = s.Name?.Trim(), SubscriberCode = string.IsNullOrWhiteSpace(s.SubscriberNo) ? null : s.SubscriberNo.Trim(),
                CustomerGroupId = d.GroupId, CustomerTypeId = type.Id, TenantId = null, CreatedDate = now, CreatedUser = actor
            };
            db.Customers.Add(customer);
            await db.SaveChangesAsync();
            db.Set<CollectionGroupParent>().Add(new()
            {
                CustomerId = customer.Id, CustomerGroupId = d.GroupId!.Value, LegacyCustomerId = s.LegacyCustomerId,
                AccountNo = string.IsNullOrWhiteSpace(s.AccountNo) ? null : s.AccountNo.Trim(), IsCorporate = s.IsCorporate,
                SourceHash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(s)), CreatedDate = now, CreatedUser = actor
            });
            created.Add(new { customer.Id, s.LegacyCustomerId, d.GroupId });
        }
        await db.SaveChangesAsync();
        if (await db.Set<CollectionGroupParent>().CountAsync() != maps.Count + create.Length)
            throw new InvalidOperationException("Üst kart sayım mutabakatı başarısız.");
        await tx.CommitAsync();
        await File.WriteAllTextAsync(outputPath + ".applied.json", JsonSerializer.Serialize(new { PlanHash = planHash, Created = created }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{create.Length} üst müşteri ve ilişki tek transaction içinde oluşturuldu. Sözleşme/ödeme taşınmadı.");
    }
    private static string N(string? s) => CollectionCustomerClassification.Normalize(s);
    private static string NameKey(string? s) => Regex.Replace(N(s), @"[^\p{L}\p{Nd}]", "");
    private sealed record Decision(int LegacyCustomerId, string Status, long? GroupId, long[] Candidates);
    public sealed class Source
    {
        public int LegacyCustomerId { get; set; }
        public string? Name { get; set; }
        public string? SubscriberNo { get; set; }
        public string? AccountNo { get; set; }
        public string? GroupCode { get; set; }
        public bool IsCorporate { get; set; }
    }
}
