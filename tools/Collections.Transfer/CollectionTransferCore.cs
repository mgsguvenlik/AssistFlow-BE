using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

// Shared financial transfer operations. Adapters own target/approval/transaction policy.
internal static class CollectionTransferCore
{
    internal static async Task ResilienceAsync(AppDataContext db, Input p, string output, string snapshot)
    {
        var batch = await db.Set<CollectionMigrationBatch>().AsNoTracking().SingleAsync(x => x.SnapshotKey == snapshot && x.Status == CollectionMigrationBatchStatus.Reconciled);
        var stateBefore = await StateAsync(db); var checks = new List<object>();
        var first = Rows(p.Payments, "payment-import-plan.ndjson")[0]; var request = CollectionPaymentImportIdentity.For(N(first, "SourcePaymentId"));
        var receipt = await db.Set<CollectionPaymentOperation>().AsNoTracking().SingleAsync(x => x.RequestId == request);
        foreach (var kind in new[] { "DELETE_PAYMENT", "EDIT_PAYMENT", "CORRUPT_SOURCE", "REMOVE_RECEIPT" })
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            try
            {
                if (kind == "DELETE_PAYMENT") await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM collection.Payment WHERE Id={receipt.PaymentId}");
                if (kind == "EDIT_PAYMENT") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE collection.Payment SET Amount=Amount+0.01 WHERE Id={receipt.PaymentId}");
                if (kind == "CORRUPT_SOURCE") await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE collection.MigrationSourceRow SET PayloadHash={new byte[32]} WHERE BatchId={batch.Id} AND EntityCode=N'Payment' AND SourceId={N(first, "SourcePaymentId").ToString()}");
                if (kind == "REMOVE_RECEIPT") await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM collection.PaymentOperation WHERE RequestId={request}");
                var rejected = false;
                try { await VerifyAsync(db, p, batch.Id, true); }
                catch (InvalidOperationException) when (kind is "CORRUPT_SOURCE" or "REMOVE_RECEIPT") { rejected = true; }
                if (kind is "CORRUPT_SOURCE" or "REMOVE_RECEIPT") Require(rejected, "Bozuk kaynak/makbuz reddedilmedi.");
                if (kind == "DELETE_PAYMENT") Require(!await db.Set<CollectionPayment>().AnyAsync(x => x.Id == receipt.PaymentId), "Silinen ödeme yeniden yaratıldı.");
                if (kind == "EDIT_PAYMENT") Require((await db.Set<CollectionPayment>().AsNoTracking().SingleAsync(x => x.Id == receipt.PaymentId)).Amount == first.GetProperty("Amount").GetDecimal() + 0.01m, "Düzenlenen ödeme ezildi.");
                checks.Add(new { Check = kind, Passed = true, RolledBack = true });
            }
            finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
            Console.WriteLine("Atılabilir transaction kontrolü geçti/geri alındı: " + kind);
        }
        var stateAfter = await StateAsync(db); Require(JsonSerializer.Serialize(stateBefore) == JsonSerializer.Serialize(stateAfter), "Prova dayanıklılık kontrolleri veri durumunu değiştirdi.");
        await Save(output, "result.json", new { Checks = checks, StateUnchanged = true, Before = stateBefore, After = stateAfter, ProductionWrites = false, Scope = "Same committed-batch replay verification path; no source/live writes and no external calls" });
    }
    internal static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    internal static string T(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    internal static string? Raw(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : null;
    internal static long N(JsonElement r, string k) => long.TryParse(T(r, k), out var n) ? n : 0;
    internal static JsonElement Doc(string p) { using var d = JsonDocument.Parse(File.ReadAllText(p)); return d.RootElement.Clone(); }
    internal static JsonElement[] Rows(string p, string name) => File.ReadLines(Path.Combine(p, name)).Select(x => { using var d = JsonDocument.Parse(x); return d.RootElement.Clone(); }).ToArray();
    internal static string HashFile(string p) { using var s = File.OpenRead(p); return Convert.ToHexString(SHA256.HashData(s)); }
    internal static byte[] Hash(string s) => SHA256.HashData(Encoding.UTF8.GetBytes(s));
    internal static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    internal static string Private(string p) { var full = Path.GetFullPath(p); Require(full.StartsWith(Path.GetFullPath(".tools") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Yalnız özel .tools girdisi/çıktısı kullanılabilir."); return full; }
    internal static string NewOutput(string p) { var full = Private(p); Require(!Directory.Exists(full) && !File.Exists(full), "Mevcut kanıt üzerine yazılmaz."); Directory.CreateDirectory(full); return full; }
    internal static Task Save(string root, string name, object value) => File.WriteAllTextAsync(Path.Combine(root, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    internal static void VerifyPackage(string root)
    {
        foreach (var f in Doc(Path.Combine(root, "package-hashes.json")).EnumerateArray())
        { var name = T(f, "File"); Require(Path.GetFileName(name) == name && HashFile(Path.Combine(root, name)) == T(f, "Sha256"), "Kaynak paket hash/yol uyuşmazlığı."); }
    }
    internal static DateTimeOffset AuditDate(JsonElement r, string key)
    {
        Require(DateTime.TryParse(Raw(r, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) && d.Kind == DateTimeKind.Unspecified
            && !Zone.IsAmbiguousTime(d) && !Zone.IsInvalidTime(d), "Audit tarihi eksik/belirsiz; tahmin yapılmadı.");
        return new DateTimeOffset(d, Zone.GetUtcOffset(d)).ToUniversalTime();
    }
    internal static DateTime? LegacyDate(JsonElement r, string k) => DateTime.TryParse(Raw(r, k), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d) ? d : null;
    internal static DateOnly Day(JsonElement r, string k) => DateOnly.FromDateTime(DateTime.Parse(T(r, k), CultureInfo.InvariantCulture));
    internal static DateOnly? MaybeDay(JsonElement r, string k) => T(r, k).Length == 0 ? null : Day(r, k);
    internal static DateOnly Period(JsonElement r) => new((int)N(r, "PeriodYear"), (int)N(r, "PeriodMonth"), 1);

    internal static async Task<string> Fingerprint(AppDataContext db, string sql)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand(); cmd.CommandText = sql; cmd.CommandTimeout = 60;
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        await using var reader = await cmd.ExecuteReaderAsync(); using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        while (await reader.ReadAsync()) { var row = new Dictionary<string, object?>(); for (var i = 0; i < reader.FieldCount; i++) row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i); hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row) + "\n")); }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    internal static readonly Dictionary<string, string> ReferenceQueries = new()
    {
        ["live-customers.ndjson"] = "SELECT c.Id,c.SubscriberCode,c.SubscriberCompany,c.CustomerTypeId,c.CustomerGroupId,c.TenantId,c.IsDeleted,t.Code AS TypeCode,g.Code AS GroupCode FROM dbo.Customers c LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId ORDER BY c.Id",
        ["live-types.ndjson"] = "SELECT Id,Code,Name FROM dbo.CustomerType ORDER BY Id",
        ["live-services.ndjson"] = "SELECT Id,Name,IsDeleted FROM dbo.ServiceType ORDER BY Id",
        ["live-currencies.ndjson"] = "SELECT Id,Code,Name FROM dbo.CurrencyType ORDER BY Id"
    };
    internal sealed class Input
    {
        public required string Capture { get; init; }
        public required string Review { get; init; }
        public required string Payments { get; init; }
        public required string Relations { get; init; }
        public JsonElement[] Contracts => Doc(Path.Combine(Review, "contract-review.json")).EnumerateArray().Where(x => T(x, "Status") is "CANDIDATE_AFTER_REFERENCE_APPROVAL" or "TECHNICAL_CANDIDATE_NOT_APPROVED").ToArray();
        public string CaptureHash => HashFile(Path.Combine(Capture, "package-hashes.json"));
    }
    internal sealed class SimulatedInterruption : Exception { }
    internal static async Task<DataTable> BulkTable(string destination, DataTable table, AppDataContext db)
    {
        Require(destination is "collection.MigrationSourceRow" or "collection.PaymentOperation" or "collection.BankTransaction" or "#PaymentRows", "Bulk hedefi izinli değil.");
        using var bulk = new SqlBulkCopy((SqlConnection)db.Database.GetDbConnection(), SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.KeepNulls, (SqlTransaction)db.Database.CurrentTransaction!.GetDbTransaction()) { DestinationTableName = destination, BulkCopyTimeout = 300 };
        foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
        await bulk.WriteToServerAsync(table); return table;
    }
    internal static DataTable SourceTable()
    {
        var t = new DataTable(); foreach (var (name, type) in new[] { ("BatchId", typeof(long)), ("EntityCode", typeof(string)), ("SourceId", typeof(string)), ("SourceParentId", typeof(string)), ("Payload", typeof(string)), ("PayloadHash", typeof(byte[])), ("StagedDate", typeof(DateTimeOffset)) }) t.Columns.Add(name, type); return t;
    }
    internal static void AddSource(DataTable t, long batch, string entity, string id, string? parent, JsonElement raw) => t.Rows.Add(batch, entity, id, (object?)parent ?? DBNull.Value, raw.GetRawText(), Hash(raw.GetRawText()), DateTimeOffset.UtcNow);
    internal static async Task AuditAsync(AppDataContext db, long batch, string entity, IEnumerable<(string Id, string? Parent, JsonElement Raw)> rows)
    {
        using var table = SourceTable(); foreach (var chunk in rows.Chunk(1000)) { table.Clear(); foreach (var row in chunk) AddSource(table, batch, entity, row.Id, row.Parent, row.Raw); await BulkTable("collection.MigrationSourceRow", table, db); }
    }
    internal static string Normalize(string s) => CollectionCustomerClassification.Normalize(s);

    internal static async Task ApplyReferencesAsync(AppDataContext db, Input p, long batch)
    {
        var services = await db.Set<ServiceType>().ToListAsync();
        foreach (var source in Rows(p.Capture, "services.ndjson"))
        {
            var name = T(source, "Name"); var matches = services.Where(x => Normalize(x.Name) == Normalize(name)).ToArray();
            Require(matches.Count(x => !x.IsDeleted) <= 1 && (matches.Length == 0 || matches.Any(x => !x.IsDeleted)), "Servis tipi çakışması.");
            if (matches.Length == 0) { var item = new ServiceType { Name = name, IsDeleted = false }; services.Add(item); db.Add(item); }
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await AuditAsync(db, batch, "ServiceDefinition", Rows(p.Capture, "services.ndjson").Select(x => (N(x, "ServiceTypeID").ToString(), (string?)null, x)));
        var proposals = Doc(Path.Combine(p.Review, "classification-proposals.json")).EnumerateArray().ToArray();
        var customers = await db.Customers.ToDictionaryAsync(x => x.Id); var changes = new List<(string, string?, JsonElement)>();
        var types = await db.Set<CustomerType>().AsNoTracking().ToDictionaryAsync(x => x.Id);
        foreach (var row in proposals)
        {
            var c = customers[N(row, "TargetCustomerId")];
            Require(c.CustomerTypeId == N(row, "ExpectedCustomerTypeId") && c.CustomerGroupId == N(row, "ExpectedGroupId") && c.TenantId == (T(row, "ExpectedTenantId").Length == 0 ? (long?)null : N(row, "ExpectedTenantId")) && c.SubscriberCode == T(row, "ExpectedSubscriber"), "Müşteri tip güncellemesi eski değerle uyuşmuyor.");
            Require(c.CustomerTypeId is not null && types[c.CustomerTypeId.Value].Code != "BNK01", "BANKA müşteri tipi değiştirilemez.");
            var old = c.CustomerTypeId; var type = types[N(row, "ProposedLiveTypeId")];
            Require(CollectionCustomerClassification.GroupTypeCodes.Contains(Normalize(type.Code)), "Yalnız onaylı grup müşteri tipi atanır.");
            c.CustomerTypeId = type.Id;
            changes.Add((c.Id.ToString(), null, JsonSerializer.SerializeToElement(new { CustomerId = c.Id, OldType = old, NewType = c.CustomerTypeId, c.CustomerGroupId, c.TenantId, SourceProposal = row })));
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); await AuditAsync(db, batch, "CustomerTypeChange", changes);
    }
    internal static async Task ApplyContractsAsync(AppDataContext db, Input p, long batch)
    {
        var rawContracts = Rows(p.Capture, "contracts.ndjson").ToDictionary(x => N(x, "ContractID")); var histories = Rows(p.Capture, "histories.ndjson").ToDictionary(x => N(x, "ContractHistoryID"));
        var serviceNames = Rows(p.Capture, "services.ndjson").ToDictionary(x => N(x, "ServiceTypeID"), x => T(x, "Name"));
        var services = (await db.Set<ServiceType>().Where(x => !x.IsDeleted).ToListAsync()).ToDictionary(x => Normalize(x.Name), x => x.Id);
        var statuses = await db.Set<CollectionContractStatus>().ToDictionaryAsync(x => x.Code, x => x.Id);
        var subscriptions = await db.Set<CollectionSubscriptionStatus>().ToDictionaryAsync(x => x.Code, x => x.Id);
        var methods = await db.Set<CollectionPaymentMethod>().ToDictionaryAsync(x => x.Code, x => x.Id);
        var methodCodes = new Dictionary<long, string> { [48] = "FINANSBANK_POS", [49] = "GTS", [50] = "IVR", [51] = "WEB", [52] = "MAIL_ORDER", [53] = "OFFSET", [54] = "CASH_MANUAL", [55] = "LEGACY_FREE", [56] = "ISBANK_POS", [57] = "BANK_TRANSFER" };
        var contracts = new List<(JsonElement Plan, JsonElement Raw, CollectionContract Entity)>();
        foreach (var c in p.Contracts)
        {
            var raw = rawContracts[N(c, "Id")]; var subscription = N(raw, "SubscriptionStatusID") switch { 12 => "ACTIVE", 13 => "FROZEN", _ => throw new InvalidOperationException("Abonelik durumu bilinmiyor.") };
            var status = N(raw, "ContractStatusID") switch { 13 => "EXISTS", 14 or 0 => "UNKNOWN", _ => throw new InvalidOperationException("Aday sözleşme durumu beklenenden farklı.") };
            var method = N(raw, "PaymentMethodID");
            Require(method == 0 || methodCodes.ContainsKey(method), "Ödeme yöntemi bilinmiyor.");
            Require((Raw(raw, "GtsNo")?.Length ?? 0) <= 50 && (Raw(raw, "IvrNo")?.Length ?? 0) <= 50, "GTS/IVR uzunluğu uygun değil.");
            var entity = new CollectionContract { CustomerId = N(c, "TargetCustomerId"), ServiceTypeId = services[Normalize(serviceNames[N(raw, "ServiceTypeID")])],
                StartDate = new DateOnly((int)N(raw, "StartingDateYear"), (int)N(raw, "StartingDateMonth"), 1), EndDate = MaybeDay(raw, "EndDate"), GtsNo = Raw(raw, "GtsNo"), IvrNo = Raw(raw, "IvrNo"),
                ContractStatusId = statuses[status], SubscriptionStatusId = subscriptions[subscription], PaymentMethodId = method == 0 ? null : methods[methodCodes[method]],
                CreatedDate = AuditDate(raw, "CreatedOn"), UpdatedDate = AuditDate(raw, "ModifiedOn"), CreatedUser = 0, IsDeleted = false };
            contracts.Add((c, raw, entity));
        }
        db.AddRange(contracts.Select(x => x.Entity)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var frequencies = await db.Set<CollectionPaymentFrequency>().ToDictionaryAsync(x => x.IntervalMonths, x => x.Id);
        var rates = new List<(JsonElement Raw, CollectionContractRatePeriod Entity)>();
        foreach (var (plan, raw, contract) in contracts)
        {
            db.Add(Map("Contract", N(raw, "ContractID"), contract.Id, raw, batch));
            foreach (var rate in plan.GetProperty("Rates").EnumerateArray())
            {
                Require(rate.GetProperty("Issues").GetArrayLength() == 0, "Hatalı tarife adayı."); var original = histories[N(rate, "Id")];
                var from = Day(rate, "From"); var reason = Raw(original, "Description") ?? Raw(original, "ProcessType") ?? "Legacy aktarım";
                // ChangeReason is a 100-character operational label, not the legacy description store.
                // Preserve the full original description in ContractHistory source audit, never truncate it.
                if (reason.Length > 100) reason = "Legacy aktarım; açıklamanın tamamı kaynak audit kaydındadır.";
                rates.Add((original, new CollectionContractRatePeriod { ContractId = contract.Id, EffectiveFrom = from, EffectiveToExclusive = MaybeDay(rate, "To"), BillingAnchor = from,
                    PaymentFrequencyId = frequencies[(short)N(rate, "Months")], Amount = rate.GetProperty("Amount").GetDecimal(), CurrencyTypeId = N(rate, "TargetCurrencyId") > 0 ? N(rate, "TargetCurrencyId") : null,
                    BillingBehavior = Enum.Parse<CollectionBillingBehavior>(T(rate, "Behavior")), ChangeReason = reason, CreatedDate = AuditDate(original, "CreatedOn"), UpdatedDate = AuditDate(original, "ModifiedOn"), CreatedUser = 0, IsDeleted = false }));
            }
        }
        db.AddRange(rates.Select(x => x.Entity)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var (raw, rate) in rates) db.Add(Map("ContractHistory", N(raw, "ContractHistoryID"), rate.Id, raw, batch));
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await AuditAsync(db, batch, "Contract", contracts.Select(x => (N(x.Raw, "ContractID").ToString(), N(x.Raw, "CustomerID").ToString() as string, x.Raw)));
        await AuditAsync(db, batch, "ContractHistory", rates.Select(x => (N(x.Raw, "ContractHistoryID").ToString(), N(x.Raw, "ContractID").ToString() as string, x.Raw)));
        Console.WriteLine($"Transaction içinde: {contracts.Count} sözleşme / {rates.Count} tarife ve kaynak/map audit'i.");
    }
    internal static CollectionMigrationMap Map(string entity, long source, long target, JsonElement raw, long batch) => new()
    {
        SourceSystem = "MGS", EntityCode = entity, SourceId = source.ToString(), TargetContractId = entity == "Contract" ? target : null, TargetRatePeriodId = entity == "ContractHistory" ? target : null,
        AppliedPayloadHash = Hash(raw.GetRawText()), FirstBatchId = batch, LastBatchId = batch, CreatedDate = DateTimeOffset.UtcNow, LastSeenDate = DateTimeOffset.UtcNow
    };
    internal static CollectionPayment Payment(JsonElement row, long contract) { var r = row.GetProperty("Source"); return new() { ContractId = contract, CurrencyTypeId = N(row, "CurrencyTypeId"), Period = Period(r), PaymentDate = Day(r, "Date"),
        Amount = row.GetProperty("Amount").GetDecimal(), IsFree = row.GetProperty("IsFree").GetBoolean(), Description = Raw(r, "Description"), CreatedDate = AuditDate(r, "CreatedOn"), UpdatedDate = AuditDate(r, "ModifiedOn"), CreatedUser = 0 }; }
    internal static byte[] CommandHash(CollectionPayment p) => CollectionPaymentCommandRules.ComputeHash(new(CollectionPaymentOperationKind.Create, null, null, p.ContractId, p.Period, p.PaymentDate, p.Amount, p.CurrencyTypeId, p.Description, p.IsFree));
    internal static async Task ApplyPaymentsAsync(AppDataContext db, Input p, long batch, bool interrupt)
    {
        var maps = await db.Set<CollectionMigrationMap>().Where(x => x.EntityCode == "Contract").ToDictionaryAsync(x => x.SourceId, x => x.TargetContractId!.Value); db.ChangeTracker.Clear();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE #PaymentRows(SourceId bigint PRIMARY KEY,ContractId bigint,CurrencyTypeId bigint,Period date,PaymentDate date,Amount decimal(18,2),IsFree bit,Description nvarchar(1000),CreatedDate datetimeoffset,UpdatedDate datetimeoffset,CreatedUser bigint,UpdatedUser bigint NULL);");
        long total = 0;
        foreach (var chunk in Rows(p.Payments, "payment-import-plan.ndjson").Chunk(2000))
        {
            using var table = new DataTable();
            foreach (var (name, type) in new[] { ("SourceId", typeof(long)), ("ContractId", typeof(long)), ("CurrencyTypeId", typeof(long)), ("Period", typeof(DateTime)), ("PaymentDate", typeof(DateTime)), ("Amount", typeof(decimal)), ("IsFree", typeof(bool)), ("Description", typeof(string)), ("CreatedDate", typeof(DateTimeOffset)), ("UpdatedDate", typeof(DateTimeOffset)), ("CreatedUser", typeof(long)), ("UpdatedUser", typeof(long)) }) table.Columns.Add(name, type);
            var inputs = new Dictionary<long, (JsonElement Raw, CollectionPayment Payment)>();
            foreach (var row in chunk)
            {
                var raw = row.GetProperty("Source"); var id = N(row, "SourcePaymentId"); Require(T(row, "SourceHash") == Convert.ToHexString(Hash(raw.GetRawText())) && row.GetProperty("RequestId").GetGuid() == CollectionPaymentImportIdentity.For(id), "Ödeme kaynak/hash/kimliği farklı.");
                var payment = Payment(row, maps[N(row, "SourceContractId").ToString()]); inputs.Add(id, (raw, payment));
                table.Rows.Add(id, payment.ContractId, payment.CurrencyTypeId, payment.Period.ToDateTime(TimeOnly.MinValue), payment.PaymentDate.ToDateTime(TimeOnly.MinValue), payment.Amount, payment.IsFree,
                    (object?)payment.Description ?? DBNull.Value, payment.CreatedDate, payment.UpdatedDate, 0L, DBNull.Value);
            }
            await BulkTable("#PaymentRows", table, db);
            using (var cmd = db.Database.GetDbConnection().CreateCommand())
            {
                cmd.Transaction = db.Database.CurrentTransaction!.GetDbTransaction(); cmd.CommandTimeout = 300;
                cmd.CommandText = "MERGE collection.Payment AS t USING #PaymentRows AS s ON 1=0 WHEN NOT MATCHED THEN INSERT(ContractId,CurrencyTypeId,Period,PaymentDate,Amount,IsFree,Description,CreatedDate,UpdatedDate,CreatedUser,UpdatedUser) VALUES(s.ContractId,s.CurrencyTypeId,s.Period,s.PaymentDate,s.Amount,s.IsFree,s.Description,s.CreatedDate,s.UpdatedDate,s.CreatedUser,s.UpdatedUser) OUTPUT s.SourceId,inserted.Id,inserted.RowVersion;";
                await using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) { var item = inputs[reader.GetInt64(0)]; item.Payment.Id = reader.GetInt64(1); item.Payment.RowVersion = (byte[])reader.GetValue(2); }
            }
            using var sources = SourceTable(); using var receipts = new DataTable();
            foreach (var (name, type) in new[] { ("RequestId", typeof(Guid)), ("ActorUserId", typeof(long)), ("Kind", typeof(byte)), ("PayloadHash", typeof(byte[])), ("PaymentId", typeof(long)), ("BeforeJson", typeof(string)), ("AfterJson", typeof(string)), ("CompletedDate", typeof(DateTimeOffset)) }) receipts.Columns.Add(name, type);
            foreach (var (id, input) in inputs)
            {
                Require(input.Payment.Id > 0, "Ödeme hedef kimliği alınamadı."); AddSource(sources, batch, "Payment", id.ToString(), N(input.Raw, "ContractID").ToString(), input.Raw);
                receipts.Rows.Add(CollectionPaymentImportIdentity.For(id), 0L, (byte)0, CommandHash(input.Payment), input.Payment.Id, DBNull.Value, CollectionPaymentSnapshotRules.Serialize(input.Payment), DateTimeOffset.UtcNow);
            }
            await BulkTable("collection.MigrationSourceRow", sources, db); await BulkTable("collection.PaymentOperation", receipts, db);
            await db.Database.ExecuteSqlRawAsync("DELETE FROM #PaymentRows;"); total += inputs.Count;
            if (interrupt) throw new SimulatedInterruption();
            if (total % 20000 == 0) Console.WriteLine($"Transaction içinde ödeme + kaynak + makbuz: {total}");
        }
        await db.Database.ExecuteSqlRawAsync("DROP TABLE #PaymentRows;");
    }
    internal static bool IsCandidate(JsonElement row) => row.GetProperty("Issues").GetArrayLength() == 0;
    internal static async Task ApplyRelationsAsync(AppDataContext db, Input p, long batch)
    {
        var parents = Doc(Path.Combine(p.Relations, "group-parent-plan.json")).EnumerateArray().Where(IsCandidate).ToArray();
        var parentTargets = new Dictionary<long, long>();
        if (parents.Length > 0)
        {
            var parentType = await db.Set<CustomerType>().SingleOrDefaultAsync(x => x.Code == "G");
            if (parentType is null) { parentType = new CustomerType { Code = "G", Name = "Grup Üst Kartı" }; db.Add(parentType); await db.SaveChangesAsync(); }
            var parentTypeId = parentType.Id;
            foreach (var row in parents)
            {
                Require(row.GetProperty("CandidateCustomerIds").GetArrayLength() == 0, "Üst kart aday çakışması.");
                var raw = row.GetProperty("Source"); var group = await db.CustomerGroups.SingleAsync(x => x.Id == N(row, "TargetGroupId"));
                Require(Normalize(group.Code) == T(row, "GroupCode") && CollectionCustomerClassification.Classify(group.Code) == CollectionCustomerClass.Group, "Üst kart grubu farklı.");
                var customer = new Customer { SubscriberCompany = Raw(raw, "Name")?.Trim(), SubscriberCode = Raw(raw, "SubscriberNo")?.Trim(),
                    CustomerGroupId = group.Id, CustomerTypeId = parentTypeId, TenantId = null, CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow, IsDeleted = false };
                db.Add(customer); await db.SaveChangesAsync();
                var id = N(row, "SourceId"); parentTargets.Add(id, customer.Id);
                db.Add(new CollectionGroupParent { CustomerId = customer.Id, CustomerGroupId = group.Id, LegacyCustomerId = id, AccountNo = Raw(raw, "AccountNo")?.Trim(), IsCorporate = row.GetProperty("IsCorporate").GetBoolean(), SourceHash = Hash(raw.GetRawText()), CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow });
            }
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            await AuditAsync(db, batch, "GroupParent", parents.Select(x => (N(x, "SourceId").ToString(), (string?)null, x.GetProperty("Source"))));
        }
        long Owner(JsonElement row)
        {
            var owner = row.GetProperty("Owner"); if (N(owner, "TargetCustomerId") > 0) return N(owner, "TargetCustomerId");
            Require(parentTargets.TryGetValue(N(owner, "ParentLegacyId"), out var id), "Koşullu üst kart eşlemesi kurulamadı."); return id;
        }
        var noteRows = Rows(p.Relations, "note-plan.ndjson").Where(IsCandidate).ToArray();
        foreach (var chunk in noteRows.Chunk(1000))
        {
            db.AddRange(chunk.Select(row => { var s = row.GetProperty("Source"); return new CollectionCustomerNote { CustomerId = Owner(row), Text = Raw(s, "Comment")!, CreatedUser = 0,
                CreatedDate = AuditDate(s, "CreatedOn"), UpdatedDate = AuditDate(s, "ModifiedOn"), LegacyCommentId = N(s, "CommentID"), LegacyCustomerId = N(s, "CustomerID"), LegacyCreatedBy = Raw(s, "CreatedBy"), LegacyModifiedBy = Raw(s, "ModifiedBy"),
                LegacyCreatedOn = LegacyDate(s, "CreatedOn"), LegacyModifiedOn = LegacyDate(s, "ModifiedOn"), SourceHash = Hash(s.GetRawText()) }; }));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        await AuditAsync(db, batch, "CustomerNote", noteRows.Select(x => (N(x, "SourceId").ToString(), N(x.GetProperty("Source"), "CustomerID").ToString() as string, x.GetProperty("Source"))));
        var contractMap = await db.Set<CollectionMigrationMap>().Where(x => x.EntityCode == "Contract").ToDictionaryAsync(x => long.Parse(x.SourceId), x => x.TargetContractId!.Value); db.ChangeTracker.Clear();
        var statusNames = Rows(p.Capture, "group-statuses.ndjson").ToDictionary(x => N(x, "GroupStatusID"), x => Normalize(T(x, "Name")));
        var statusMap = (await db.Set<CollectionGroupStatus>().AsNoTracking().ToListAsync()).ToDictionary(x => Normalize(x.Name), x => x.Id);
        var groupRows = Rows(p.Relations, "group-history-plan.ndjson").Where(IsCandidate).ToArray();
        foreach (var chunk in groupRows.Where(x => T(x, "Status") != "IDENTICAL_DUPLICATE_REPRESENTED_ONCE").Chunk(1000))
        {
            db.AddRange(chunk.Select(row => { var s = row.GetProperty("Source"); return new CollectionContractPeriodFollowUp { ContractId = contractMap[N(s, "ContractID")], Period = Period(s),
                GroupStatusId = N(s, "GroupStatusID") == 0 ? null : statusMap[statusNames[N(s, "GroupStatusID")]], Description = Raw(s, "StatusDescription"), LegacyFollowGroupStatusId = N(s, "FollowGroupStatusID"),
                SourceHash = Hash(s.GetRawText()), CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow }; }));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        await AuditAsync(db, batch, "GroupHistory", groupRows.Select(x => (N(x, "SourceId").ToString(), N(x.GetProperty("Source"), "ContractID").ToString() as string, x.GetProperty("Source"))));
        var currencies = Doc(Path.Combine(p.Review, "reference-proposals.json")).GetProperty("Currencies").EnumerateArray().Where(x => T(x, "Status") == "EXISTING_MATCH").ToDictionary(x => N(x, "LegacyId"), x => N(x, "TargetId"));
        var invoiceRows = Rows(p.Relations, "invoice-plan.ndjson").Where(IsCandidate).ToArray(); var invoices = new Dictionary<long, CollectionInvoice>();
        foreach (var row in invoiceRows)
        {
            var s = row.GetProperty("Source"); var invoice = new CollectionInvoice { CustomerId = Owner(row), Type = T(s, "Type") is "B" or "Bireysel" ? "B" : "K", Number = Raw(s, "No"), Date = Day(s, "Date"),
                Amount = s.GetProperty("Amount").GetDecimal(), CurrencyTypeId = currencies[N(s, "CurrencyID")], ProjectCode = Raw(s, "ProjectCode"), Comment = Raw(s, "Comment"), CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow,
                LegacyInvoiceFollowId = N(s, "InvoiceFollowID"), LegacyCustomerId = N(s, "CustomerID"), LegacyCreatedBy = Raw(s, "CreatedBy"), LegacyModifiedBy = Raw(s, "ModifiedBy"), LegacyCreatedOn = LegacyDate(s, "CreatedOn"), LegacyModifiedOn = LegacyDate(s, "ModifiedOn"), SourceHash = Hash(s.GetRawText()) };
            invoices.Add(N(s, "InvoiceFollowID"), invoice); db.Add(invoice);
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); await AuditAsync(db, batch, "Invoice", invoiceRows.Select(x => (N(x, "SourceId").ToString(), N(x.GetProperty("Source"), "CustomerID").ToString() as string, x.GetProperty("Source"))));
        var invoicePaymentRows = Rows(p.Relations, "invoice-payment-plan.ndjson").Where(IsCandidate).ToArray();
        var invoicePayments = new List<(JsonElement Raw, CollectionInvoicePayment Payment)>();
        foreach (var row in invoicePaymentRows)
        {
            var s = row.GetProperty("Source"); invoicePayments.Add((s, new CollectionInvoicePayment { InvoiceId = invoices[N(s, "InvoiceFollowID")].Id, Date = Day(s, "Date"), Amount = s.GetProperty("Amount").GetDecimal(), Description = Raw(s, "Description"),
                CreatedUser = 0, CreatedDate = DateTimeOffset.UtcNow, LegacyInvoiceFollowPaymentId = N(s, "InvoiceFollowPaymentID"), LegacyCustomerId = N(s, "CustomerID"), LegacyCreatedBy = Raw(s, "CreatedBy"), LegacyModifiedBy = Raw(s, "ModifiedBy"),
                LegacyCreatedOn = LegacyDate(s, "CreatedOn"), LegacyModifiedOn = LegacyDate(s, "ModifiedOn"), SourceHash = Hash(s.GetRawText()) }));
        }
        db.AddRange(invoicePayments.Select(x => x.Payment)); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        foreach (var (raw, payment) in invoicePayments) db.Add(new CollectionInvoiceOperation { RequestId = new Guid(Hash("AssistFlow:collection:rehearsal-invoice-payment:v1:MGS:" + N(raw, "InvoiceFollowPaymentID"))[..16]),
            ActorUserId = 0, Kind = "PaymentCreate", InvoiceId = payment.InvoiceId, PaymentId = payment.Id, LegacyInvoiceId = N(raw, "InvoiceFollowID"), LegacyPaymentId = N(raw, "InvoiceFollowPaymentID"),
            PayloadHash = Hash(raw.GetRawText()), AfterJson = JsonSerializer.Serialize(new { payment.Id, payment.InvoiceId, payment.Date, payment.Amount, payment.Description }), CompletedDate = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); await AuditAsync(db, batch, "InvoicePayment", invoicePaymentRows.Select(x => (N(x, "SourceId").ToString(), N(x.GetProperty("Source"), "InvoiceFollowID").ToString() as string, x.GetProperty("Source"))));
        var accounts = Doc(Path.Combine(p.Relations, "account-plan.json")).EnumerateArray().ToArray();
        foreach (var row in accounts)
        {
            var code = T(row, "Code"); if (code.Length > 200) continue;
            var accepted = IsCandidate(row); db.Add(new CollectionInvoiceAccount { Code = code, CustomerId = accepted ? Owner(row) : null,
                Issue = accepted ? null : "Kaynak cari eşlemesi incelemede; otomatik sahiplik atanmadı.", SourceIdsJson = row.GetProperty("SourceCustomerIds").GetRawText(), SourceHash = Convert.ToHexString(Hash(row.GetRawText())), VerifiedAt = DateTimeOffset.UtcNow });
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await AuditAsync(db, batch, "InvoiceAccount", accounts.Where(x => T(x, "Code").Length <= 200).Select(x => (Convert.ToHexString(Hash(T(x, "Code"))), (string?)null, x)));
        var bankRows = Rows(p.Capture, "bank-identities.ndjson");
        foreach (var chunk in bankRows.Chunk(2000))
        {
            using var table = new DataTable(); table.Columns.Add("Key", typeof(string)); table.Columns.Add("Source", typeof(string)); table.Columns.Add("RecordedAt", typeof(DateTimeOffset));
            foreach (var r in chunk) table.Rows.Add(CollectionBankLoadService.Key(T(r, "FileType"), T(r, "FileRowID")), "MGS", DateTimeOffset.UtcNow);
            await BulkTable("collection.BankTransaction", table, db);
        }
        db.Add(new CollectionBankBaseline { Id = 1, SourceHash = HashFile(Path.Combine(p.Capture, "bank-identities.ndjson")), SourceCount = bankRows.Length, VerifiedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); await AuditAsync(db, batch, "BulkPayment", bankRows.Select(x => (N(x, "BulkPaymentID").ToString(), (string?)null, x)));
        var filePlans = Rows(p.Relations, "contract-file-plan.ndjson").Where(x => T(x, "Status") == "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN").ToArray();
        await AuditAsync(db, batch, "ContractFileEvidence", filePlans.Select(x => (N(x, "SourceContractId").ToString(), (string?)null,
            JsonSerializer.SerializeToElement(new { Plan = x, TargetContractId = contractMap[N(x, "SourceContractId")], CdnUploaded = false, FakeAttachmentCreated = false }))));
        var activityPlans = Doc(Path.Combine(p.Relations, "activity-file-plan.json")).EnumerateArray().Where(x => T(x, "Status") == "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN").ToArray();
        await AuditAsync(db, batch, "CustomerFileEvidence", activityPlans.Select(x => (Convert.ToHexString(Hash(T(x, "Path"))), (string?)null, x)));
        Console.WriteLine($"Transaction içinde ilişkiler: {parents.Length} üst kart, {noteRows.Length} not, {groupRows.Length} grup kaynak satırı, {invoiceRows.Length}/{invoicePaymentRows.Length} fatura/ödeme; banka {bankRows.Length}. Dosyalar yalnız hash audit'i; CDN yok.");
    }
    internal static readonly (string Table, string Key)[] StateTables = [ ("collection.MigrationBatch", "Id"), ("collection.MigrationSourceRow", "Id"), ("collection.MigrationMap", "Id"),
        ("collection.Contract", "Id"), ("collection.ContractRatePeriod", "Id"), ("collection.Payment", "Id"), ("collection.PaymentOperation", "Id"), ("collection.CustomerNote", "Id"),
        ("collection.GroupParent", "CustomerId"), ("collection.ContractPeriodFollowUp", "Id"), ("collection.Invoice", "Id"), ("collection.InvoicePayment", "Id"), ("collection.InvoiceOperation", "Id"),
        ("collection.InvoiceAccount", "Code"), ("collection.BankTransaction", "[Key]"), ("collection.BankBaseline", "Id"), ("dbo.ServiceType", "Id"), ("dbo.CustomerType", "Id") ];
    internal static async Task<Dictionary<string, string>> StateAsync(AppDataContext db)
    {
        var state = new Dictionary<string, string>(); foreach (var (table, key) in StateTables) state[table] = await Fingerprint(db, $"SELECT * FROM {table} ORDER BY {key}");
        state["dbo.CustomerReferences"] = await Fingerprint(db, ReferenceQueries["live-customers.ndjson"]); return state;
    }
    internal static async Task<object> VerifyAsync(AppDataContext db, Input p, long batch, bool preserveEdits)
    {
        var sources = (await db.Set<CollectionMigrationSourceRow>().AsNoTracking().Where(x => x.BatchId == batch).ToListAsync()).ToDictionary(x => (x.EntityCode, x.SourceId));
        foreach (var r in sources.Values) Require(r.PayloadHash.SequenceEqual(Hash(r.Payload)), "Ham kaynak audit hash'i farklı.");
        void VerifySource(string entity, long id, JsonElement raw) => Require(sources.TryGetValue((entity, id.ToString()), out var s) && s.PayloadHash.SequenceEqual(Hash(raw.GetRawText())), "Kaynak audit eksik/değişmiş: " + entity);
        var maps = (await db.Set<CollectionMigrationMap>().AsNoTracking().ToListAsync()).ToDictionary(x => (x.EntityCode, x.SourceId));
        var contracts = await db.Set<CollectionContract>().AsNoTracking().ToDictionaryAsync(x => x.Id); var rates = await db.Set<CollectionContractRatePeriod>().AsNoTracking().ToDictionaryAsync(x => x.Id);
        var frequencies = await db.Set<CollectionPaymentFrequency>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.IntervalMonths);
        var serviceNames = (await db.Set<ServiceType>().AsNoTracking().ToListAsync()).ToDictionary(x => x.Id, x => Normalize(x.Name));
        var originalServices = Rows(p.Capture, "services.ndjson").ToDictionary(x => N(x, "ServiceTypeID"), x => Normalize(T(x, "Name")));
        var originalContracts = Rows(p.Capture, "contracts.ndjson").ToDictionary(x => N(x, "ContractID")); var originalRates = Rows(p.Capture, "histories.ndjson").ToDictionary(x => N(x, "ContractHistoryID"));
        var statuses = await db.Set<CollectionContractStatus>().ToDictionaryAsync(x => x.Id, x => x.Code); var subscriptions = await db.Set<CollectionSubscriptionStatus>().ToDictionaryAsync(x => x.Id, x => x.Code);
        Require(contracts.Count == p.Contracts.Length && rates.Count == p.Contracts.Sum(x => x.GetProperty("Rates").GetArrayLength()), "Sözleşme/tarife sayısı uyuşmuyor.");
        foreach (var c in p.Contracts)
        {
            var id = N(c, "Id"); var source = originalContracts[id]; VerifySource("Contract", id, source); var map = maps[("Contract", id.ToString())]; var actual = contracts[map.TargetContractId!.Value];
            Require(map.AppliedPayloadHash.SequenceEqual(Hash(source.GetRawText())) && actual.CustomerId == N(c, "TargetCustomerId") && actual.CreatedUser == 0
                && actual.StartDate == new DateOnly((int)N(source, "StartingDateYear"), (int)N(source, "StartingDateMonth"), 1) && actual.EndDate == MaybeDay(source, "EndDate")
                && actual.GtsNo == Raw(source, "GtsNo") && actual.IvrNo == Raw(source, "IvrNo") && serviceNames[actual.ServiceTypeId] == originalServices[N(source, "ServiceTypeID")]
                && statuses[actual.ContractStatusId!.Value] == (N(source, "ContractStatusID") == 13 ? "EXISTS" : "UNKNOWN") && subscriptions[actual.SubscriptionStatusId!.Value] == (N(source, "SubscriptionStatusID") == 13 ? "FROZEN" : "ACTIVE"), "Sözleşme alan mutabakatı farklı.");
            foreach (var rate in c.GetProperty("Rates").EnumerateArray())
            {
                var original = originalRates[N(rate, "Id")]; VerifySource("ContractHistory", N(rate, "Id"), original); var rm = maps[("ContractHistory", N(rate, "Id").ToString())]; var r = rates[rm.TargetRatePeriodId!.Value];
                Require(rm.AppliedPayloadHash.SequenceEqual(Hash(original.GetRawText())) && r.ContractId == actual.Id && r.EffectiveFrom == Day(rate, "From") && r.EffectiveToExclusive == MaybeDay(rate, "To")
                    && r.BillingAnchor == Day(rate, "From") && frequencies[r.PaymentFrequencyId] == N(rate, "Months") && r.Amount == rate.GetProperty("Amount").GetDecimal()
                    && r.CurrencyTypeId == N(rate, "TargetCurrencyId") && r.BillingBehavior.ToString() == T(rate, "Behavior") && r.CreatedUser == 0, "Tarife mutabakatı farklı.");
            }
        }
        var payments = await db.Set<CollectionPayment>().AsNoTracking().ToDictionaryAsync(x => x.Id); var receipts = await db.Set<CollectionPaymentOperation>().AsNoTracking().ToDictionaryAsync(x => x.RequestId);
        long count = 0, deleted = 0, edited = 0; var expectedTotals = new Dictionary<long, decimal>();
        foreach (var row in Rows(p.Payments, "payment-import-plan.ndjson"))
        {
            var raw = row.GetProperty("Source"); var id = N(row, "SourcePaymentId"); VerifySource("Payment", id, raw);
            Require(receipts.TryGetValue(CollectionPaymentImportIdentity.For(id), out var receipt), "Kalıcı ödeme makbuzu eksik; yeniden yaratma yapılmaz.");
            var expected = Payment(row, maps[("Contract", N(row, "SourceContractId").ToString())].TargetContractId!.Value);
            Require(receipt!.ActorUserId == 0 && receipt.Kind == CollectionPaymentOperationKind.Create && receipt.PayloadHash.SequenceEqual(CommandHash(expected)) && receipt.AfterJson is not null, "Makbuz kaynağı/komut hash'i farklı.");
            using var after = JsonDocument.Parse(receipt.AfterJson!); expected.Id = receipt.PaymentId; expected.RowVersion = Convert.FromBase64String(T(after.RootElement, "RowVersion"));
            Require(CollectionPaymentSnapshotRules.Serialize(expected) == receipt.AfterJson, "Makbuz audit snapshot'ı kaynakla uyuşmuyor.");
            if (!payments.TryGetValue(receipt.PaymentId, out var actual)) deleted++;
            else if (CollectionPaymentSnapshotRules.Serialize(actual) != receipt.AfterJson) edited++;
            expectedTotals[expected.CurrencyTypeId] = expectedTotals.GetValueOrDefault(expected.CurrencyTypeId) + expected.Amount; count++;
        }
        Require(receipts.Count == count && payments.Count == count - deleted, "Ödeme/makbuz adedi farklı veya plan dışı ödeme var.");
        if (!preserveEdits) Require(deleted == 0 && edited == 0, "İlk import ödeme alanları farklı.");
        var actualTotals = payments.Values.GroupBy(x => x.CurrencyTypeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        if (!preserveEdits) Require(expectedTotals.Count == actualTotals.Count && expectedTotals.All(x => actualTotals.GetValueOrDefault(x.Key) == x.Value), "Para birimi toplamı farklı.");
        var noteRows = Rows(p.Relations, "note-plan.ndjson").Where(IsCandidate).ToArray(); var notes = await db.Set<CollectionCustomerNote>().AsNoTracking().ToDictionaryAsync(x => x.LegacyCommentId!.Value);
        var parentPlans = Doc(Path.Combine(p.Relations, "group-parent-plan.json")).EnumerateArray().Where(IsCandidate).ToArray();
        var parents = await db.Set<CollectionGroupParent>().AsNoTracking().ToDictionaryAsync(x => x.LegacyCustomerId);
        var customers = await db.Customers.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Require(parents.Count == parentPlans.Length, "Üst kart sayısı farklı.");
        foreach (var row in parentPlans)
        {
            var raw = row.GetProperty("Source"); var parent = parents[N(row, "SourceId")]; VerifySource("GroupParent", N(row, "SourceId"), raw);
            Require(parent.CustomerGroupId == N(row, "TargetGroupId") && parent.SourceHash.SequenceEqual(Hash(raw.GetRawText())) && parent.IsCorporate == row.GetProperty("IsCorporate").GetBoolean()
                && parent.AccountNo == Raw(raw, "AccountNo")?.Trim() && customers[parent.CustomerId].TenantId is null, "Üst kart sahiplik/audit farklı.");
        }
        long Owner(JsonElement row) { var owner = row.GetProperty("Owner"); return N(owner, "TargetCustomerId") > 0 ? N(owner, "TargetCustomerId") : parents[N(owner, "ParentLegacyId")].CustomerId; }
        foreach (var row in Doc(Path.Combine(p.Review, "classification-proposals.json")).EnumerateArray())
            Require(customers[N(row, "TargetCustomerId")].CustomerTypeId == N(row, "ProposedLiveTypeId"), "Müşteri sınıflandırması farklı.");
        Require(notes.Count == noteRows.Length, "Not sayısı farklı."); foreach (var row in noteRows) { var s = row.GetProperty("Source"); VerifySource("CustomerNote", N(s, "CommentID"), s); var note = notes[N(s, "CommentID")]; Require(note.CustomerId == Owner(row) && note.Text == Raw(s, "Comment") && note.CreatedUser == 0 && note.CreatedDate == AuditDate(s, "CreatedOn") && note.LegacyCreatedBy == Raw(s, "CreatedBy") && note.SourceHash!.SequenceEqual(Hash(s.GetRawText())), "Not sahiplik/içerik/hash farklı."); }
        var groupRows = Rows(p.Relations, "group-history-plan.ndjson").Where(IsCandidate).ToArray(); var groupExpected = groupRows.Count(x => T(x, "Status") != "IDENTICAL_DUPLICATE_REPRESENTED_ONCE");
        Require(await db.Set<CollectionContractPeriodFollowUp>().CountAsync() == groupExpected, "Grup geçmişi sayısı farklı.");
        foreach (var row in groupRows) VerifySource("GroupHistory", N(row, "SourceId"), row.GetProperty("Source"));
        var follows = await db.Set<CollectionContractPeriodFollowUp>().AsNoTracking().ToDictionaryAsync(x => x.LegacyFollowGroupStatusId!.Value);
        var originalGroupStatuses = Rows(p.Capture, "group-statuses.ndjson").ToDictionary(x => N(x, "GroupStatusID"), x => Normalize(T(x, "Name")));
        var groupStatuses = await db.Set<CollectionGroupStatus>().AsNoTracking().ToDictionaryAsync(x => x.Id, x => Normalize(x.Name));
        foreach (var row in groupRows.Where(x => T(x, "Status") != "IDENTICAL_DUPLICATE_REPRESENTED_ONCE"))
        {
            var s = row.GetProperty("Source"); var actual = follows[N(row, "SourceId")];
            Require(actual.ContractId == maps[("Contract", N(s, "ContractID").ToString())].TargetContractId && actual.Period == Period(s) && actual.Description == Raw(s, "StatusDescription")
                && actual.SourceHash!.SequenceEqual(Hash(s.GetRawText())) && actual.CreatedUser == 0
                && (N(s, "GroupStatusID") == 0 ? actual.GroupStatusId is null : groupStatuses[actual.GroupStatusId!.Value] == originalGroupStatuses[N(s, "GroupStatusID")]), "Grup dönem/sözleşme/durum mutabakatı farklı.");
        }
        var invoiceRows = Rows(p.Relations, "invoice-plan.ndjson").Where(IsCandidate).ToArray(); var invoices = await db.Set<CollectionInvoice>().AsNoTracking().ToDictionaryAsync(x => x.LegacyInvoiceFollowId!.Value);
        var invoicePaymentRows = Rows(p.Relations, "invoice-payment-plan.ndjson").Where(IsCandidate).ToArray(); var invoicePayments = await db.Set<CollectionInvoicePayment>().AsNoTracking().ToDictionaryAsync(x => x.LegacyInvoiceFollowPaymentId!.Value);
        Require(invoices.Count == invoiceRows.Length && invoicePayments.Count == invoicePaymentRows.Length && await db.Set<CollectionInvoiceOperation>().CountAsync() == invoicePaymentRows.Length, "Fatura/ödeme/makbuz sayısı farklı.");
        var currencyMap = Doc(Path.Combine(p.Review, "reference-proposals.json")).GetProperty("Currencies").EnumerateArray().Where(x => T(x, "Status") == "EXISTING_MATCH").ToDictionary(x => N(x, "LegacyId"), x => N(x, "TargetId"));
        var invoiceReceipts = await db.Set<CollectionInvoiceOperation>().AsNoTracking().ToDictionaryAsync(x => x.LegacyPaymentId!.Value);
        foreach (var row in invoiceRows) { var s = row.GetProperty("Source"); VerifySource("Invoice", N(row, "SourceId"), s); var actual = invoices[N(row, "SourceId")]; Require(actual.CustomerId == Owner(row) && actual.CurrencyTypeId == currencyMap[N(s, "CurrencyID")] && actual.Number == Raw(s, "No") && actual.CreatedUser == 0 && actual.Amount == s.GetProperty("Amount").GetDecimal() && actual.Date == Day(s, "Date") && actual.SourceHash!.SequenceEqual(Hash(s.GetRawText())), "Fatura sahiplik/para birimi/alan/hash farklı."); }
        foreach (var row in invoicePaymentRows)
        {
            var s = row.GetProperty("Source"); VerifySource("InvoicePayment", N(row, "SourceId"), s); var actual = invoicePayments[N(row, "SourceId")]; var receipt = invoiceReceipts[N(row, "SourceId")];
            Require(actual.InvoiceId == invoices[N(s, "InvoiceFollowID")].Id && actual.Amount == s.GetProperty("Amount").GetDecimal() && actual.Date == Day(s, "Date") && actual.Description == Raw(s, "Description") && actual.SourceHash!.SequenceEqual(Hash(s.GetRawText()))
                && receipt.ActorUserId == 0 && receipt.Kind == "PaymentCreate" && receipt.PaymentId == actual.Id && receipt.InvoiceId == actual.InvoiceId && receipt.PayloadHash.SequenceEqual(Hash(s.GetRawText()))
                && receipt.AfterJson == JsonSerializer.Serialize(new { actual.Id, actual.InvoiceId, actual.Date, actual.Amount, actual.Description }), "Fatura ödeme/makbuz zinciri farklı.");
        }
        foreach (var balance in Doc(Path.Combine(p.Relations, "invoice-balances.json")).EnumerateArray()) { var inv = invoices[N(balance, "SourceInvoiceId")]; var paid = invoicePayments.Values.Where(x => x.InvoiceId == inv.Id).Sum(x => x.Amount); Require(paid == balance.GetProperty("Paid").GetDecimal() && inv.Amount - paid == balance.GetProperty("Remaining").GetDecimal(), "Fatura bakiyesi farklı."); }
        var bankRows = Rows(p.Capture, "bank-identities.ndjson"); var bankKeys = (await db.Set<CollectionBankTransaction>().AsNoTracking().Select(x => x.Key).ToListAsync()).ToHashSet();
        Require(bankKeys.Count == bankRows.Length && bankRows.All(x => bankKeys.Contains(CollectionBankLoadService.Key(T(x, "FileType"), T(x, "FileRowID")))) && await db.Set<CollectionBankBaseline>().AnyAsync(x => x.Id == 1 && x.SourceCount == bankRows.Length), "Banka tekrar koruması eksik/farklı.");
        foreach (var row in bankRows) VerifySource("BulkPayment", N(row, "BulkPaymentID"), row);
        var accounts = await db.Set<CollectionInvoiceAccount>().AsNoTracking().ToDictionaryAsync(x => x.Code);
        var accountPlans = Doc(Path.Combine(p.Relations, "account-plan.json")).EnumerateArray().Where(x => T(x, "Code").Length <= 200).ToArray();
        Require(accounts.Count == accountPlans.Length, "Cari sayısı farklı.");
        foreach (var row in accountPlans)
        {
            var actual = accounts[T(row, "Code")];
            Require(actual.CustomerId == (IsCandidate(row) ? Owner(row) : (long?)null) && actual.SourceHash == Convert.ToHexString(Hash(row.GetRawText())) && actual.SourceIdsJson == row.GetProperty("SourceCustomerIds").GetRawText()
                && sources.ContainsKey(("InvoiceAccount", Convert.ToHexString(Hash(T(row, "Code"))))), "Cari sahiplik/audit farklı.");
        }
        Require(!await db.Set<CollectionSmsNotification>().AnyAsync() && !await db.Set<CollectionSmsAttempt>().AnyAsync() && !await db.Set<CollectionContractAttachment>().AnyAsync() && !await db.Set<CollectionCustomerAttachment>().AnyAsync(), "Prova bildirim veya sahte CDN ilişkisi oluşturmamalı.");
        return new { Contracts = contracts.Count, Rates = rates.Count, Payments = count, PaymentReceipts = receipts.Count, PreservedDeletedPayments = deleted, PreservedEditedPayments = edited, ExpectedTotalsByCurrency = expectedTotals, ActualTotalsByCurrency = actualTotals,
            Notes = notes.Count, GroupHistory = groupExpected, GroupAliasAudit = groupRows.Length - groupExpected, Invoices = invoices.Count, InvoicePayments = invoicePayments.Count, BankIdentities = bankKeys.Count, RawSourceAuditRows = sources.Count,
            GroupParents = await db.Set<CollectionGroupParent>().CountAsync(), Accounts = await db.Set<CollectionInvoiceAccount>().CountAsync(), Files = "LOCAL_HASH_EVIDENCE_ONLY", OutboundCalls = 0 };
    }
}

