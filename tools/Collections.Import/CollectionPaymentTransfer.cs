using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Services.Crm.Collections.Calculation;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

/// <summary>Onaylı ödeme alt kümesi; mevcut source-row ve kalıcı PaymentOperation makbuzlarını kullanır.</summary>
internal static class CollectionPaymentTransfer
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private sealed record Entry(long SourceId, string Payload, byte[] SourceHash, CollectionPayment Payment, byte[] CommandHash);

    public static async Task RunAsync(string snapshotDirectory, string settingsPath, long contractBatchId,
        string manifestHash, string referencePath, string referenceHash, string? expectedPlanHash)
    {
        snapshotDirectory = Path.GetFullPath(snapshotDirectory);
        var manifestBytes = await File.ReadAllBytesAsync(Path.Combine(snapshotDirectory, "manifest.json"));
        Require(Hash(manifestBytes) == manifestHash, "Ödeme kesiti manifesti değişmiş.");
        using var manifestDoc = JsonDocument.Parse(manifestBytes);
        var manifest = manifestDoc.RootElement;
        var referenceBytes = await File.ReadAllBytesAsync(referencePath);
        Require(Hash(referenceBytes) == referenceHash, "Legacy kanıtı değişmiş.");
        using var referenceDoc = JsonDocument.Parse(referenceBytes);
        var reference = referenceDoc.RootElement;
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath), new JsonDocumentOptions
        { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var builder = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        Require(builder.DataSource == "192.168.1.8" && builder.InitialCatalog == "AssistFlowTest", "Aktarım yalnız AssistFlowTest içindir.");

        // Kaynak yalnız SELECT; kullanıcı tarafından kapalı olduğu doğrulanan MGS kesiti işlem boyunca sabittir.
        var sourceBuilder = new SqlConnectionStringBuilder(builder.ConnectionString)
        { InitialCatalog = "MGS", ApplicationIntent = ApplicationIntent.ReadOnly };
        await using var source = new SqlConnection(sourceBuilder.ConnectionString);
        await source.OpenAsync();
        await using var sourceTransaction = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);
        await VerifySourceAsync(source, sourceTransaction, manifest, reference);
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(builder.ConnectionString, sql => sql.CommandTimeout(300)).Options);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource=N'CollectionLegacyPaymentApply',
                @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=15000;
            IF @result < 0 THROW 51000, N'Ödeme aktarım kilidi alınamadı.', 1;
            """);
        var actualMaps = await db.Set<CollectionMigrationReferenceMap>().AsNoTracking()
            .Where(x => x.BatchId == contractBatchId && x.ReferenceKind == "CurrencyType" && x.Status == CollectionMigrationDecisionStatus.Accepted)
            .ToDictionaryAsync(x => x.SourceId, x => x.TargetCurrencyTypeId);
        var expectedMaps = reference[4].EnumerateArray().Where(x => Id(x, "Status") == 1 && Id(x, "BatchId") == contractBatchId)
            .ToDictionary(x => Text(x, "SourceId")!, x => (long?)Id(x, "TargetCurrencyTypeId"));
        Require(actualMaps.Count == expectedMaps.Count && actualMaps.All(x => expectedMaps.TryGetValue(x.Key, out var id) && id == x.Value), "Onaylı para birimi eşlemeleri değişmiş.");

        // Hedef doğrulaması ve yazma aynı transaction'da: eski yerel raporu uygulama izni sayma.
        var reviewDirectory = await CollectionPaymentReviewer.RunAsync(snapshotDirectory, settingsPath, contractBatchId, manifestHash, db);
        var comparisonDirectory = await CollectionPaymentCurrencyComparison.RunAsync(reviewDirectory, referencePath, referenceHash);
        var approved = new HashSet<long>();
        var exceptions = new List<JsonElement>();
        await foreach (var line in File.ReadLinesAsync(Path.Combine(comparisonDirectory, "comparison.ndjson")))
        {
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.GetProperty("PreliminaryCandidate").GetBoolean()) approved.Add(Id(doc.RootElement, "PaymentId"));
            else exceptions.Add(doc.RootElement.Clone());
        }
        await File.WriteAllTextAsync(Path.Combine(comparisonDirectory, "payment-exceptions.json"), JsonSerializer.Serialize(exceptions, Json));
        var reviewed = new Dictionary<long, JsonElement>();
        await foreach (var line in File.ReadLinesAsync(Path.Combine(reviewDirectory, "payments.ndjson")))
        {
            using var doc = JsonDocument.Parse(line);
            if (approved.Contains(Id(doc.RootElement, "PaymentId"))) reviewed.Add(Id(doc.RootElement, "PaymentId"), doc.RootElement.Clone());
        }
        var entries = new List<Entry>();
        var now = DateTimeOffset.UtcNow;
        await foreach (var line in File.ReadLinesAsync(Path.Combine(snapshotDirectory, "payments.ndjson")))
        {
            using var doc = JsonDocument.Parse(line);
            var raw = doc.RootElement;
            var id = Id(raw, "PaymentID");
            if (!reviewed.TryGetValue(id, out var checkedRow)) continue;
            var sourceHash = SHA256.HashData(Encoding.UTF8.GetBytes(line));
            Require(Convert.ToHexString(sourceHash) == Text(checkedRow, "SourcePayloadHash"), "Ödeme kaynak satırı incelemeden sonra değişmiş.");
            var payment = new CollectionPayment
            {
                ContractId = Id(checkedRow, "TargetContractId"),
                CurrencyTypeId = Id(checkedRow.GetProperty("Currency"), "ResolvedCurrencyTypeId"),
                Period = DateOnly.ParseExact(Text(checkedRow, "Period")!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                PaymentDate = DateOnly.ParseExact(Text(checkedRow, "PaymentDate")!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Amount = checkedRow.GetProperty("Amount").GetDecimal(), IsFree = checkedRow.GetProperty("IsFree").GetBoolean(),
                Description = Text(raw, "Description"), CreatedDate = ParseDate(Text(raw, "CreatedOn")) ?? now,
                UpdatedDate = ParseDate(Text(raw, "ModifiedOn")), CreatedUser = 0, UpdatedUser = null
            };
            var command = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null, payment.ContractId,
                payment.Period, payment.PaymentDate, payment.Amount, payment.CurrencyTypeId, payment.Description, payment.IsFree);
            entries.Add(new(id, line, sourceHash, payment, CollectionPaymentCommandRules.ComputeHash(command)));
        }
        Require(entries.Count == approved.Count && entries.Select(x => x.SourceId).Distinct().Count() == entries.Count, "Ödeme aday kapsamı uyuşmuyor.");
        var receipts = await db.Set<CollectionPaymentOperation>().AsNoTracking().ToDictionaryAsync(x => x.RequestId);
        var priorSources = await db.Set<CollectionMigrationSourceRow>().AsNoTracking()
            .Where(x => x.Batch.SourceSystem == "MGS" && x.EntityCode == "Payment")
            .Select(x => new { x.SourceId, x.PayloadHash }).ToListAsync();
        var oldSources = priorSources.ToLookup(x => x.SourceId);
        var newEntries = new List<Entry>();
        var priorPayments = await db.Set<CollectionPayment>().AsNoTracking().ToDictionaryAsync(x => x.Id);
        var already = 0;
        var deleted = 0;
        foreach (var entry in entries)
        {
            var sourceId = entry.SourceId.ToString(CultureInfo.InvariantCulture);
            if (!receipts.TryGetValue(CollectionPaymentImportIdentity.For(entry.SourceId), out var receipt))
            {
                Require(!oldSources[sourceId].Any(), "Kaynak ödeme audit'i var fakat kalıcı makbuzu yok; otomatik tekrar yapılmaz.");
                newEntries.Add(entry);
                continue;
            }
            Require(receipt.ActorUserId == 0 && receipt.Kind == CollectionPaymentOperationKind.Create
                && receipt.PayloadHash.SequenceEqual(entry.CommandHash) && oldSources[sourceId].Any()
                && oldSources[sourceId].All(x => x.PayloadHash.SequenceEqual(entry.SourceHash)), "Kalıcı ödeme kimliği farklı kaynak içerikle kullanılmış.");
            already++;
            if (!priorPayments.ContainsKey(receipt.PaymentId)) deleted++;
            // Hedef ödeme sonradan düzenlenmiş/silinmiş olabilir: asla üzerine yazma/geri yaratma.
        }
        var planPath = Path.Combine(comparisonDirectory, "transfer-plan.ndjson");
        await using (var writer = new StreamWriter(planPath, false, new UTF8Encoding(false)))
            foreach (var entry in entries.OrderBy(x => x.SourceId))
                await writer.WriteLineAsync(JsonSerializer.Serialize(new { entry.SourceId, SourceHash = Convert.ToHexString(entry.SourceHash),
                    CommandHash = Convert.ToHexString(entry.CommandHash), RequestId = CollectionPaymentImportIdentity.For(entry.SourceId) }));
        var planHash = Hash(await File.ReadAllBytesAsync(planPath));
        var totals = newEntries.GroupBy(x => x.Payment.CurrencyTypeId).Select(g => new { CurrencyTypeId = g.Key, Count = g.Count(), Amount = g.Sum(x => x.Payment.Amount) }).ToArray();
        Console.WriteLine($"Aktarım planı: yeni {newEntries.Count}; daha önce işlenmiş {already}; bunların silinmiş olanı {deleted}. Plan SHA-256: {planHash}");
        await File.WriteAllTextAsync(Path.Combine(comparisonDirectory, "transfer-preview.json"), JsonSerializer.Serialize(new
        { PlanHash = planHash, ManifestHash = manifestHash, ReferenceHash = referenceHash, NewCount = newEntries.Count,
            AlreadyImported = already, PreviouslyDeleted = deleted, Totals = totals, TargetPaymentCountBefore = priorPayments.Count }, Json));
        if (expectedPlanHash is null) return;
        Require(planHash.Equals(expectedPlanHash, StringComparison.OrdinalIgnoreCase), "Önizleme planı değişmiş; ödeme aktarımı durduruldu.");
        if (newEntries.Count == 0)
        {
            Console.WriteLine("Yeni ödeme yok; veritabanı değiştirilmedi. Silinen ödemeler geri oluşturulmadı.");
            return;
        }

        var snapshotKey = "payments-" + Text(manifest, "snapshotKey");
        var batch = await db.Set<CollectionMigrationBatch>().AsTracking().SingleOrDefaultAsync(x => x.SourceSystem == "MGS" && x.SnapshotKey == snapshotKey);
        if (batch is null)
        {
            batch = new CollectionMigrationBatch { SourceSystem = "MGS", SnapshotKey = snapshotKey, ManifestHash = Convert.FromHexString(manifestHash),
                RuleVersion = "payment-transfer-v1", NormalizationVersion = "subscriber-v1", Status = CollectionMigrationBatchStatus.Applying, CreatedDate = now, CreatedUser = 0 };
            db.Add(batch);
            await db.SaveChangesAsync();
        }
        else Require(batch.ManifestHash.SequenceEqual(Convert.FromHexString(manifestHash)) && batch.RuleVersion == "payment-transfer-v1", "Ödeme batch içeriği farklı.");
        var paymentBatchId = batch.Id;
        var sourceCountBefore = await db.Set<CollectionMigrationSourceRow>().CountAsync(x => x.BatchId == paymentBatchId && x.EntityCode == "Payment");
        db.ChangeTracker.Clear();
        var inserted = new List<(Entry Entry, long TargetId)>();
        foreach (var chunk in newEntries.Chunk(750))
        {
            db.AddRange(chunk.Select(x => x.Payment));
            await db.SaveChangesAsync();
            foreach (var entry in chunk)
            {
                db.Add(new CollectionMigrationSourceRow { BatchId = paymentBatchId, EntityCode = "Payment", SourceId = entry.SourceId.ToString(CultureInfo.InvariantCulture),
                    SourceParentId = Text(JsonSerializer.Deserialize<JsonElement>(entry.Payload), "ContractID")?.Trim(),
                    Payload = entry.Payload, PayloadHash = entry.SourceHash, StagedDate = now });
                db.Add(new CollectionPaymentOperation { RequestId = CollectionPaymentImportIdentity.For(entry.SourceId), ActorUserId = 0,
                    Kind = CollectionPaymentOperationKind.Create, PayloadHash = entry.CommandHash, PaymentId = entry.Payment.Id,
                    AfterJson = CollectionPaymentSnapshotRules.Serialize(entry.Payment), CompletedDate = now });
                inserted.Add((entry, entry.Payment.Id));
            }
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            if (inserted.Count % 15000 == 0) Console.WriteLine($"Ödeme aktarımı transaction içinde: {inserted.Count}/{newEntries.Count}");
        }
        var after = await db.Set<CollectionPayment>().AsNoTracking().ToDictionaryAsync(x => x.Id);
        Require(after.Count == priorPayments.Count + newEntries.Count, "Hedef ödeme sayısı mutabakatı başarısız.");
        foreach (var (entry, targetId) in inserted)
            Require(after.TryGetValue(targetId, out var actual) && CollectionPaymentSnapshotRules.Serialize(actual) == CollectionPaymentSnapshotRules.Serialize(entry.Payment),
                "Hedef ödeme alan mutabakatı başarısız.");
        foreach (var old in priorPayments)
            Require(after.TryGetValue(old.Key, out var actual) && CollectionPaymentSnapshotRules.Serialize(actual) == CollectionPaymentSnapshotRules.Serialize(old.Value),
                "Önceden var olan ödeme değişmiş.");
        var newReceiptCount = await db.Set<CollectionPaymentOperation>().CountAsync(x => x.ActorUserId == 0 && x.CompletedDate == now);
        var sourceCount = await db.Set<CollectionMigrationSourceRow>().CountAsync(x => x.BatchId == paymentBatchId && x.EntityCode == "Payment");
        Require(newReceiptCount == newEntries.Count && sourceCount == sourceCountBefore + newEntries.Count,
            "Ödeme audit/makbuz sayısı mutabakatı başarısız.");
        batch = await db.Set<CollectionMigrationBatch>().AsTracking().SingleAsync(x => x.Id == paymentBatchId);
        batch.Status = CollectionMigrationBatchStatus.NeedsReview; // Kalan istisnalar henüz tamamlanmış değildir.
        batch.CompletedDate = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await File.WriteAllTextAsync(Path.Combine(comparisonDirectory, "inserted-identities.json"), JsonSerializer.Serialize(
            inserted.Select(x => new { x.Entry.SourceId, x.TargetId, RequestId = CollectionPaymentImportIdentity.For(x.Entry.SourceId) }), Json));
        await transaction.CommitAsync();
        await sourceTransaction.CommitAsync();
        await File.WriteAllTextAsync(Path.Combine(comparisonDirectory, "transfer-result.json"), JsonSerializer.Serialize(new
        { Committed = true, PaymentBatchId = paymentBatchId, PlanHash = planHash, Inserted = newEntries.Count, AlreadyImported = already,
            TargetPaymentCount = after.Count, Totals = totals, CompletedDate = DateTimeOffset.UtcNow }, Json));
        Console.WriteLine($"Tamamlandı: {newEntries.Count} ödeme AssistFlowTest'e aktarıldı; alan/adet mutabakatı başarılı. Batch: {paymentBatchId}");
    }

    private static async Task VerifySourceAsync(SqlConnection connection, SqlTransaction transaction, JsonElement manifest, JsonElement reference)
    {
        foreach (var (table, key, file) in new[] { ("Customer", "CustomerID", "customers.ndjson"), ("Contract", "ContractID", "contracts.ndjson"),
            ("ContractHistory", "ContractHistoryID", "contract-history.ndjson"), ("Payment", "PaymentID", "payments.ndjson") })
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var command = new SqlCommand($"SELECT * FROM [Core].[{table}] ORDER BY [{key}]", connection, transaction) { CommandTimeout = 300 };
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            long count = 0;
            while (await reader.ReadAsync())
            {
                using var stream = new MemoryStream();
                using (var writer = new Utf8JsonWriter(stream))
                {
                    writer.WriteStartObject();
                    for (var i = 0; i < names.Length; i++)
                    {
                        writer.WritePropertyName(names[i]);
                        if (await reader.IsDBNullAsync(i)) writer.WriteNullValue();
                        else WriteValue(writer, reader.GetValue(i));
                    }
                    writer.WriteEndObject();
                }
                hash.AppendData(stream.GetBuffer(), 0, checked((int)stream.Length));
                hash.AppendData("\n"u8);
                count++;
            }
            var expected = manifest.GetProperty("files").EnumerateArray().Single(x => Text(x, "FileName") == file);
            Require(count == Id(expected, "Count") && Convert.ToHexString(hash.GetHashAndReset()) == Text(expected, "Sha256"), $"Canlı {table} kesitten farklı; aktarım durduruldu.");
            Console.WriteLine($"Canlı MGS {table}: {count} kayıt, kesit hash'i aynı.");
        }
        foreach (var (table, key, index) in new[] { ("Currency", "CurrencyID", 0), ("PaymentType", "PaymentTypeID", 1) })
        {
            var expected = reference[index].EnumerateArray().ToDictionary(x => Id(x, key), x => Text(x, "Name"));
            await using var command = new SqlCommand($"SELECT [{key}],Name FROM Definition.[{table}]", connection, transaction);
            await using var reader = await command.ExecuteReaderAsync();
            var count = 0;
            while (await reader.ReadAsync())
            {
                Require(expected.TryGetValue(Convert.ToInt64(reader[0]), out var name) && name == (reader.IsDBNull(1) ? null : reader.GetString(1)), "Legacy tanımlar değişmiş.");
                count++;
            }
            Require(count == expected.Count, "Legacy tanım sayısı değişmiş.");
        }
        foreach (var definition in reference[3].EnumerateArray())
        {
            await using var command = new SqlCommand("SELECT m.definition FROM sys.sql_modules m JOIN sys.objects o ON o.object_id=m.object_id JOIN sys.schemas s ON s.schema_id=o.schema_id WHERE s.name=@schema AND o.name=@name", connection, transaction);
            command.Parameters.AddWithValue("@schema", Text(definition, "SchemaName"));
            command.Parameters.AddWithValue("@name", Text(definition, "ObjectName"));
            Require(await command.ExecuteScalarAsync() as string == Text(definition, "Definition"), "Legacy SQL tanımı değişmiş.");
        }
        await using var yearCommand = new SqlCommand("SELECT YEAR(GETDATE()),CONVERT(nvarchar(128),DATABASEPROPERTYEX(DB_NAME(),'Collation'))", connection, transaction);
        await using var yearReader = await yearCommand.ExecuteReaderAsync();
        await yearReader.ReadAsync();
        Require(yearReader.GetInt32(0) == Id(reference[2][0], "SourceYear") && yearReader.GetString(1) == Text(reference[2][0], "DatabaseCollation"), "Legacy yıl/collation değişmiş.");
    }

    private static void WriteValue(Utf8JsonWriter writer, object value)
    {
        switch (value)
        {
            case string x: writer.WriteStringValue(x); break;
            case bool x: writer.WriteBooleanValue(x); break;
            case byte x: writer.WriteNumberValue(x); break;
            case short x: writer.WriteNumberValue(x); break;
            case int x: writer.WriteNumberValue(x); break;
            case long x: writer.WriteNumberValue(x); break;
            case decimal x: writer.WriteNumberValue(x); break;
            case double x: writer.WriteNumberValue(x); break;
            case float x: writer.WriteNumberValue(x); break;
            case DateTime x: writer.WriteStringValue(x.ToString("O", CultureInfo.InvariantCulture)); break;
            case DateTimeOffset x: writer.WriteStringValue(x.ToString("O", CultureInfo.InvariantCulture)); break;
            case Guid x: writer.WriteStringValue(x); break;
            case byte[] x: writer.WriteBase64StringValue(x); break;
            default: writer.WriteStringValue(Convert.ToString(value, CultureInfo.InvariantCulture)); break;
        }
    }
    private static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date) ? date : null;
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value));
    private static string? Text(JsonElement row, string key) => row.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null ? value.ToString() : null;
    private static long Id(JsonElement row, string key) => long.TryParse(Text(row, key)?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : 0;
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}
