using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Produces audit/proof inputs only. No restore, DDL, seed, import, application or external sender is invoked.
internal static class RehearsalPreparation
{
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";
    private static string Id(JsonElement r, string k) => Text(r, k).Trim();
    private static JsonElement Document(string file) { using var d = JsonDocument.Parse(File.ReadAllText(file)); return d.RootElement.Clone(); }
    private static IEnumerable<JsonElement> Rows(string root, string name) => File.ReadLines(Path.Combine(root, name)).Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); });
    private static string Hash(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)); }
    private static Task Save(string root, string name, object value) => File.WriteAllTextAsync(Path.Combine(root, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }

    public static async Task RunAsync(string capturePath, string packagePath, string configPath, string outputPath)
    {
        var capture = Path.GetFullPath(capturePath); var package = Path.GetFullPath(packagePath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        Require(new[] { capture, package, output }.All(x => x.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)) && !Directory.Exists(output) && !File.Exists(output), "Prova hazırlığı yalnız yeni .tools klasöründe oluşturulabilir.");
        CustomerDecisionReplay.VerifyPackage(capture); CustomerDecisionReplay.VerifyPackage(package);
        var manifest = Document(Path.Combine(package, "manifest.json"));
        Require(Text(manifest, "CapturePackageHash") == Hash(Path.Combine(capture, "package-hashes.json"))
            && Text(manifest, "Status") == "BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION"
            && Text(manifest, "RelationReconciliationPackageHash").Length == 64 && Text(manifest, "PaymentReconciliationPackageHash").Length == 64, "Kaynak ve birleşik paket zinciri eksik/farklı.");
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(configPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var builder = new SqlConnectionStringBuilder(cs) { ApplicationIntent = ApplicationIntent.ReadOnly, ConnectTimeout = 10, ApplicationName = "CollectionRehearsalPreparationReadOnly" };
        Require(builder.InitialCatalog == "AssistFlow", "Metadata kontrolü yalnız AssistFlow üzerinde yapılabilir.");
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        async Task<JsonElement[]> Read(string sql)
        {
            using var cmd = connection.CreateCommand(); cmd.CommandText = "SET LOCK_TIMEOUT 5000; " + sql; cmd.CommandTimeout = 30;
            await using var reader = await cmd.ExecuteReaderAsync(); var rows = new List<JsonElement>();
            while (await reader.ReadAsync()) { var fields = new Dictionary<string, object?>(); for (var i = 0; i < reader.FieldCount; i++) fields[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i); rows.Add(JsonSerializer.SerializeToElement(fields)); }
            return rows.ToArray();
        }
        var environment = await Read("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) ServerName,DB_NAME() DatabaseName,CONVERT(nvarchar(128),SERVERPROPERTY('Edition')) Edition,CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) ProductVersion,SYSUTCDATETIME() CheckedAtUtc");
        var users = await Read("SELECT COUNT_BIG(*) UserCount,SUM(CASE WHEN Id=0 THEN 1 ELSE 0 END) ZeroUserRows FROM dbo.Users");
        var auditFks = await Read("SELECT s.name SchemaName,t.name TableName,c.name ColumnName,ps.name PrincipalSchema,pt.name PrincipalTable,pc.name PrincipalColumn FROM sys.foreign_key_columns f JOIN sys.tables t ON t.object_id=f.parent_object_id JOIN sys.schemas s ON s.schema_id=t.schema_id JOIN sys.columns c ON c.object_id=t.object_id AND c.column_id=f.parent_column_id JOIN sys.tables pt ON pt.object_id=f.referenced_object_id JOIN sys.schemas ps ON ps.schema_id=pt.schema_id JOIN sys.columns pc ON pc.object_id=pt.object_id AND pc.column_id=f.referenced_column_id WHERE c.name IN ('CreatedUser','UpdatedUser','ActorUserId','ApprovedByUserId') ORDER BY s.name,t.name,c.name");
        JsonElement[] backups = []; string? backupMetadataFailure = null;
        try
        {
            backups = await Read("SELECT b.database_name,b.backup_start_date,b.backup_finish_date,b.type,b.is_copy_only,b.has_backup_checksums,b.is_damaged,b.first_lsn,b.last_lsn,b.database_backup_lsn,b.backup_set_id,m.family_sequence_number,m.physical_device_name FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON m.media_set_id=b.media_set_id WHERE b.backup_set_id IN (SELECT MAX(backup_set_id) FROM msdb.dbo.backupset WHERE database_name IN (N'AssistFlow',N'MGS') AND type='D' AND backup_finish_date IS NOT NULL GROUP BY database_name) ORDER BY b.database_name,m.family_sequence_number");
        }
        catch (SqlException ex) { backupMetadataFailure = "SQL_METADATA_ACCESS_" + ex.Number.ToString(CultureInfo.InvariantCulture); }
        // A reservation or a missing backup is a gate, never a reason to modify a live user or request a live backup automatically.
        var zeroUserRows = users[0].GetProperty("ZeroUserRows").ValueKind == JsonValueKind.Null ? 0 : users[0].GetProperty("ZeroUserRows").GetInt32();
        await using var modelDb = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>().UseSqlServer("Server=localhost;Database=DesignOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var modelFks = modelDb.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys().Where(f => f.Properties.Any(p => p.Name is "CreatedUser" or "UpdatedUser" or "ActorUserId" or "ApprovedByUserId"))
            .Select(f => new { Schema = e.GetSchema() ?? "dbo", Table = e.GetTableName(), Properties = f.Properties.Select(p => p.Name).ToArray(), Principal = f.PrincipalEntityType.GetTableName() })).ToArray();
        var actorGate = zeroUserRows == 0 && !modelFks.Any(x => x.Schema == "collection" || x.Table == "Customers")
            && !auditFks.Any(x => Text(x, "SchemaName") == "collection" || Text(x, "TableName") == "Customers");
        Directory.CreateDirectory(output);
        await Save(output, "live-metadata-read-only.json", new { Environment = environment, Users = users, AuditForeignKeys = auditFks, ModelAuditForeignKeys = modelFks, Backups = backups, BackupMetadataFailure = backupMetadataFailure });
        var timezone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var actors = new Dictionary<string, (string Field, string Value, long Count)>();
        var totals = new Dictionary<string, long>(); var dateIssues = new Dictionary<string, long>();
        var contractIds = Document(Path.Combine(package, "contract-import-plan.json")).EnumerateArray().Select(x => Id(x, "Id")).ToHashSet();
        var paymentIds = Rows(package, "payment-import-plan.ndjson").Select(x => Id(x, "SourcePaymentId")).ToHashSet();
        var noteIds = Rows(package, "note-plan.ndjson").Where(x => x.GetProperty("Issues").GetArrayLength() == 0).Select(x => Id(x, "SourceId")).ToHashSet();
        var invoiceIds = Rows(package, "invoice-plan.ndjson").Where(x => x.GetProperty("Issues").GetArrayLength() == 0).Select(x => Id(x, "SourceId")).ToHashSet();
        var invoicePaymentIds = Rows(package, "invoice-payment-plan.ndjson").Where(x => x.GetProperty("Issues").GetArrayLength() == 0).Select(x => Id(x, "SourceId")).ToHashSet();
        (string File, string Key, string Entity, HashSet<string>? Ids)[] selected = [ ("contracts.ndjson", "ContractID", "Contract", contractIds),
            ("histories.ndjson", "ContractHistoryID", "RatePeriod", (HashSet<string>?)null),
            ("payments.ndjson", "PaymentID", "Payment", paymentIds), ("notes.ndjson", "CommentID", "CustomerNote", noteIds),
            ("invoice-follow.ndjson", "InvoiceFollowID", "Invoice", invoiceIds), ("invoice-payments.ndjson", "InvoiceFollowPaymentID", "InvoicePayment", invoicePaymentIds) ];
        var rowIds = new HashSet<string>(); long auditRows = 0;
        await using (var writer = new StreamWriter(Path.Combine(output, "source-audit-plan.ndjson"), false, new UTF8Encoding(false)))
            foreach (var (file, key, entity, ids) in selected)
                foreach (var row in Rows(capture, file))
                {
                    if (ids is null ? !contractIds.Contains(Id(row, "ContractID")) : !ids.Contains(Id(row, key))) continue;
                    var id = Id(row, key); Require(rowIds.Add(entity + ":" + id), "Mükerrer audit kaynak kimliği.");
                    foreach (var field in new[] { "CreatedBy", "ModifiedBy" })
                    {
                        var value = Text(row, field); if (value.Length == 0) continue;
                        var actorKey = JsonSerializer.Serialize(new[] { field, value }); var prior = actors.GetValueOrDefault(actorKey);
                        actors[actorKey] = (field, value, prior.Count + 1);
                    }
                    object NormalizeDate(string field)
                    {
                        var raw = Text(row, field);
                        if (raw.Length == 0) { var reason = field + "_MISSING"; dateIssues[reason] = dateIssues.GetValueOrDefault(reason) + 1; return new { Raw = (string?)null, Utc = (DateTimeOffset?)null, Status = "MISSING_PRESERVE_NULL_OR_IMPORT_TIMESTAMP_WITH_REASON" }; }
                        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) || date.Kind != DateTimeKind.Unspecified
                            || timezone.IsInvalidTime(date) || timezone.IsAmbiguousTime(date))
                        { var reason = field + "_REVIEW"; dateIssues[reason] = dateIssues.GetValueOrDefault(reason) + 1; return new { Raw = raw, Utc = (DateTimeOffset?)null, Status = "REVIEW_DO_NOT_GUESS" }; }
                        return new { Raw = raw, Utc = (DateTimeOffset?)new DateTimeOffset(date, timezone.GetUtcOffset(date)).ToUniversalTime(), Status = "LEGACY_ISTANBUL_TO_UTC" };
                    }
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new
                    {
                        Entity = entity,
                        SourceId = id,
                        TechnicalCreatedUser = 0,
                        TechnicalUpdatedUser = (long?)null,
                        LegacyCreatedBy = Text(row, "CreatedBy"),
                        LegacyModifiedBy = Text(row, "ModifiedBy"),
                        Created = NormalizeDate("CreatedOn"),
                        Modified = NormalizeDate("ModifiedOn"),
                        SourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText()))),
                        Retention = "MigrationSourceRow raw payload + entity Legacy fields where available; never impersonate a current user",
                        ReadyToApply = false
                    }));
                    totals[entity] = totals.GetValueOrDefault(entity) + 1; auditRows++;
                }
        await Save(output, "legacy-actor-inventory.json", actors.Values.OrderBy(x => x.Field).ThenBy(x => x.Value, StringComparer.Ordinal)
            .Select(x => new { x.Field, RawLegacyValue = x.Value, Occurrences = x.Count, TechnicalActor = 0, HumanUserMapping = "NOT_INFERRED_FROM_NAME", ReadyToApply = false }));
        await Save(output, "audit-policy.json", new
        {
            Version = "collection-production-audit-v1",
            TechnicalActor = 0,
            TechnicalActorMeaning = "Legacy migration system; not dbo.Users.Id=0 and not a human approver",
            TechnicalActorGatePassed = actorGate,
            ZeroUserRows = zeroUserRows,
            ApprovalActor = (long?)null,
            ApprovalActorStatus = "REQUIRES_EXPLICIT_LIVE_USER_CODE_AND_VERIFICATION",
            TimeZoneId = timezone.Id,
            TimestampPolicy = "Raw legacy dates/names/hash retained. Explicit historical Istanbul offset; ambiguous/invalid dates held. Never DateTimeStyles.AssumeLocal in new apply path.",
            MissingDates = "Do not invent history; preserve null or use recorded import timestamp only where target requires it, with fallback reason.",
            ReceiptPolicy = "Payment + operation receipt + raw source + map in one transaction; stable legacy RequestId reused. Deleted payment receipt retained, never recreate on retry.",
            OtherReceipts = "Contract/rate source maps, note/group/fatura legacy IDs and raw hashes; file journal before/after CDN readback; baseline all bank identities",
            SharedChanges = "Record operator approval, before/after values, procedure SQL hash and package hash separately; technical actor is not approval.",
            RequiredImporterWork = "Production-specific apply must consume this audit plan and retain raw sources for every entity, including group/fatura. Test-only importers are not unlocked.",
            ReadyToApply = false
        });
        await Save(output, "other-entity-audit-policy.json", new
        {
            TechnicalActor = 0,
            UpdatedUser = (long?)null,
            Entities = new[] { "GroupParent", "GroupPeriodFollowUp", "InvoiceAccount", "BankTransaction/Baseline", "ContractAttachment", "CustomerAttachment" },
            Policy = "Each retained row keeps its source identity/hash or approved file hash/path and deterministic mapping. Target creation time is recorded import time, not guessed historical creation time. Current shared customer modifications keep before/after values and separate operator approval.",
            SourceEvidence = "Hash-bound relation plans and bank baseline in data package. No legacy author or creation date inferred for sources without that field.",
            SystemActorNeverAuthorizesHumanActions = true,
            ReadyToApply = false
        });
        var schemaRoot = Path.GetFullPath("docs/releases/collections-2026-10-15/schema");
        var schema = Document(Path.Combine(schemaRoot, "manifest.json"));
        foreach (var f in schema.GetProperty("files").EnumerateArray())
        { var file = Text(f, "file"); Require(Path.GetFileName(file) == file && Hash(Path.Combine(schemaRoot, file)) == Text(f, "sha256"), "Şema dosyası hash'i uyuşmuyor."); }
        await Save(output, "rehearsal-environment.example.json", new
        {
            Status = "NOT_CONFIGURED_DO_NOT_EXECUTE",
            ExpectedIsolatedServerName = "",
            TargetDatabase = "AssistFlowCollectionRehearsal",
            SourceDatabase = "MGSCollectionRehearsal",
            ForbiddenServerName = Text(environment[0], "ServerName"),
            BackupFiles = Array.Empty<string>(),
            DataLogMoves = Array.Empty<object>(),
            OperatorCode = "",
            OutboundNetworkBlocked = false,
            SqlAgentDisabledOrNotInstalled = false,
            ApplicationStartupDisabled = true,
            RealSmsMailCdnDisabled = true,
            RestoreApproved = false
        });
        await Save(output, "rehearsal-plan.json", new
        {
            Status = "PREPARATION_ONLY_ENVIRONMENT_AND_APPLY_PATH_REQUIRED",
            CapturePackageHash = Hash(Path.Combine(capture, "package-hashes.json")),
            DataPackageHash = Hash(Path.Combine(package, "package-hashes.json")),
            SchemaManifestHash = Hash(Path.Combine(schemaRoot, "manifest.json")),
            SchemaFiles = schema.GetProperty("files"),
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            TechnicalActorGatePassed = actorGate,
            AuditRows = auditRows,
            AuditCounts = totals,
            DateIssues = dateIssues,
            DistinctLegacyActorLabels = actors.Count,
            BackupMetadataRows = backups.Length,
            BackupMetadataFailure = backupMetadataFailure,
            BackupReadabilityOrRestoreVerified = false,
            Steps = new[] { "Approve dedicated isolated instance and backup files; never reuse live/test application database", "Network deny live SQL/R2/SMS/SMTP/Manitou; no cloned SQL Agent jobs or WebAPI startup",
                "RESTORE HEADERONLY/FILELISTONLY/VERIFYONLY on isolated instance and explicit MOVE list; fresh target only, no REPLACE", "Restore AssistFlow + MGS independently, record recovery/collation/LSN and fingerprint; no claim of cross-DB atomicity",
                "Baseline DB backup/fingerprint before schema, 21 migrations in manifest order with first-error stop; seed and schema rerun must no-op", "Retarget guarded SQL to approved rehearsal DB/server; never run production-guarded scripts unmodified",
                "Implement and review isolated-only full apply path; parent/reference approvals then contract/rates then payments/receipts and related entities", "Reconcile all entity counts/hashes, per-currency amounts, frozen/free/YOK and invoice balance; excluded rows never leak",
                "Second apply: zero new IDs/receipts/bank identities/files. Changed payload, wrong server and interrupted transaction must fail safely", "Delete/edit retry receipt survival checked only on disposable rehearsal data; restore pre-import baseline to a separate fresh name and compare",
                "No live CDN upload; use no-op/stub transport isolated from provider, then separate explicitly approved production CDN verification", "Export evidence/remaining exceptions; operator signoff. Preparation never authorizes production deployment" },
            RequiredInputs = new[] { "Isolated SQL instance/connection via secure environment", "SQL-server-accessible verified AssistFlow and MGS backup files + file MOVE destinations", "Live approver user Code; do not choose Id=1 or map legacy names by similarity" },
            RemainingGates = new[] { "Isolation environment/backup access", "Named operator approval", "Production/isolation-specific full apply implementation", "Timestamp review rows", "Parent/shared reference and unresolved customer decisions", "Actual restore/idempotency/rollback proof" },
            ReadyToApply = false
        });
        await Save(output, "package-hashes.json", Directory.GetFiles(output).Order().Select(x => new { File = Path.GetFileName(x), Sha256 = Hash(x) }).ToArray());
        Console.WriteLine($"Audit hazırlığı: {auditRows} kaynak kayıt / {actors.Count} legacy kişi etiketi; sistem aktörü önkoşulu {actorGate}. Prova planı üretildi; restore/DB/CDN değişikliği yapılmadı.");
    }
}
