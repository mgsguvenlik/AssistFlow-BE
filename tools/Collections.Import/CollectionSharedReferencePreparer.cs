using System.Globalization;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionSharedReferencePreparer
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly (string SourceId, string Name)[] ServiceTypes =
    [
        ("26", "Gözlem"), ("27", "GPRS"), ("28", "Kiralık"),
        ("29", "Kiralık + Açma Kapama Rapor"), ("30", "Gözlem + Servis Bakım"),
        ("31", "Gözlem + GPRS"), ("32", "Gözlem + Açma Kapama Rapor"),
        ("33", "Gözlem + Aylık Rapor"), ("34", "Gözlem + Anlık Rapor"),
        ("1032", "Servis"), ("2032", "Hizmet Yok"), ("2033", "Bakım"),
        ("2034", "GPRS Modülü"), ("2035", "SMS Hizmeti"),
        ("2036", "GPRS Vodafone"), ("2037", "8 GB GPRS"),
        ("2038", "Sağlık Takip Bilekliği"), ("2039", "GPRS TTKom"),
        ("2040", "STB GSM + Gözlem")
    ];

    public static async Task RunAsync(string sourceSystem, string snapshotKey, string settingsPath)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings")
            .GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Ortak referans hazırlığı yalnız AssistFlowTest üzerinde çalışır.");
        var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString, sql =>
        {
            sql.CommandTimeout(180);
        }).Options;
        await using var db = new AppDataContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'CollectionSharedReferencePreparer',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, N'Tahsilat referans hazırlık kilidi alınamadı.', 1;
            """);

        var batch = await db.Set<CollectionMigrationBatch>().SingleOrDefaultAsync(x =>
            x.SourceSystem == sourceSystem && x.SnapshotKey == snapshotKey)
            ?? throw new InvalidOperationException("Kesit staging kaydı bulunamadı.");
        if (batch.Status is not (CollectionMigrationBatchStatus.Staged or CollectionMigrationBatchStatus.NeedsReview
            or CollectionMigrationBatchStatus.Validating))
            throw new InvalidOperationException("Bu durumdaki kesite referans hazırlığı uygulanamaz.");

        var activeServices = await db.Set<ServiceType>().Where(x => !x.IsDeleted).ToListAsync();
        foreach (var (_, name) in ServiceTypes)
        {
            var matches = activeServices.Where(x => Normalize(x.Name) == Normalize(name)).ToList();
            if (matches.Count > 1)
                throw new InvalidOperationException($"'{name}' için birden fazla aktif servis tipi bulundu.");
            if (matches.Count == 1) continue;
            var service = new ServiceType { Name = name, ContractNumber = null, IsDeleted = false };
            db.Add(service);
            activeServices.Add(service);
        }

        var individual = await db.Set<CustomerType>().SingleAsync(x => x.Id == 4);
        var group = await db.Set<CustomerType>().SingleAsync(x => x.Id == 5);
        var bank = await db.Set<CustomerType>().SingleAsync(x => x.Id == 6);
        if (!string.Equals(bank.Code, "BNK01", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("BANKA müşteri tipi beklenen BNK01 kodunda değil; otomatik değiştirilmedi.");
        individual.Code = "N";
        group.Code = "G";
        group.Name = "Grup/Kurumsal";
        var member = await GetOrCreateCustomerTypeAsync(db, "GM", "Grup Üyesi");
        var account = await GetOrCreateCustomerTypeAsync(db, "A", "Cari/Fatura");
        await db.SaveChangesAsync();

        var maps = await db.Set<CollectionMigrationReferenceMap>()
            .Where(x => x.BatchId == batch.Id && x.Status == CollectionMigrationDecisionStatus.Accepted).ToListAsync();
        foreach (var (sourceId, name) in ServiceTypes)
        {
            var target = activeServices.Single(x => Normalize(x.Name) == Normalize(name));
            AddMap("ServiceType", sourceId, target.Id, map => map.TargetServiceTypeId = target.Id,
                $"Onaylı ad eşlemesi: legacy ServiceType {sourceId} → {name}.");
        }
        AddCustomerMap("N", individual.Id, "Legacy normal/bireysel müşteri.");
        AddCustomerMap("G", group.Id, "Legacy grup üst kartı.");
        AddCustomerMap("GM", member.Id, "Legacy grup üyesi.");
        AddCustomerMap("A", account.Id, "Legacy cari/fatura müşterisi.");
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        Console.WriteLine($"Referans hazırlığı tamamlandı: {ServiceTypes.Length} servis tipi ve 4 müşteri tipi eşlemesi hazır.");

        void AddCustomerMap(string sourceId, long targetId, string evidence) =>
            AddMap("CustomerType", sourceId, targetId, map => map.TargetCustomerTypeId = targetId, evidence);

        void AddMap(string kind, string sourceId, long targetId,
            Action<CollectionMigrationReferenceMap> setTarget, string evidence)
        {
            var existing = maps.SingleOrDefault(x => x.ReferenceKind == kind && x.SourceId == sourceId);
            if (existing is not null)
            {
                var existingTarget = kind == "ServiceType" ? existing.TargetServiceTypeId : existing.TargetCustomerTypeId;
                if (existingTarget != targetId)
                    throw new InvalidOperationException($"{kind} {sourceId} daha önce farklı hedefe eşlenmiş.");
                return;
            }
            var map = new CollectionMigrationReferenceMap
            {
                BatchId = batch.Id, ReferenceKind = kind, SourceId = sourceId,
                MatchMethod = "ExplicitApproved", Evidence = "Kullanıcı onaylı Faz 0 referans matrisi | " + evidence,
                Status = CollectionMigrationDecisionStatus.Accepted,
                DecidedDate = DateTimeOffset.UtcNow
            };
            setTarget(map);
            db.Add(map);
            maps.Add(map);
        }
    }

    private static async Task<CustomerType> GetOrCreateCustomerTypeAsync(AppDataContext db, string code, string name)
    {
        var byCode = await db.Set<CustomerType>().SingleOrDefaultAsync(x => x.Code == code);
        if (byCode is not null)
        {
            if (Normalize(byCode.Name) != Normalize(name))
                throw new InvalidOperationException($"{code} müşteri tipi farklı adla mevcut; otomatik değiştirilmedi.");
            return byCode;
        }
        if (await db.Set<CustomerType>().AnyAsync(x => x.Name == name))
            throw new InvalidOperationException($"{name} müşteri tipi farklı kodla mevcut; otomatik birleştirilmedi.");
        var created = new CustomerType { Code = code, Name = name };
        db.Add(created);
        return created;
    }

    private static string Normalize(string value) => string.Join(' ', value.Trim()
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpper(Turkish);
}
