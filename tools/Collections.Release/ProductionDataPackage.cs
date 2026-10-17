using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Model.Concrete.Collections;

// Generates closed-by-default review scripts and an import plan; never executes write SQL.
internal static class ProductionDataPackage
{
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    private static string Id(JsonElement r, string k) => long.TryParse(Text(r, k), out var id) ? id.ToString() : Text(r, k);
    private static JsonElement Document(string file) { using var d = JsonDocument.Parse(File.ReadAllText(file)); return d.RootElement.Clone(); }
    private static IEnumerable<JsonElement> Rows(string file) => File.ReadLines(file).Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); });
    private static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    private static string Quote(string text) => "N'" + text.Replace("'", "''") + "'";
    private static string Number(string value) => long.TryParse(value, out var id) && id > 0 ? id.ToString() : "NULL";
    private static string NullableNumber(string value) => long.TryParse(value, out var id) && id >= 0 ? id.ToString() : "NULL";
    private static string Header => "-- İNCELEME TASLAĞI: restore/prova, yedek, bakım ve ortak tablo değişikliği onayı olmadan çalıştırmayın.\n"
        + "DECLARE @Apply bit=0;\nIF DB_NAME()<>N'AssistFlow' THROW 51000,N'Yanlış hedef DB.',1;\n"
        + "IF @Apply<>1 THROW 51000,N'Uygulama kapalı; yayın/onay kapıları tamamlanmalıdır.',1;\nSET XACT_ABORT ON;\nSET LOCK_TIMEOUT 15000;\n";
    private static string Begin => "BEGIN TRY\n BEGIN TRANSACTION;\n DECLARE @lock int;\n EXEC @lock=sys.sp_getapplock @Resource=N'CollectionProductionDataPreparation',@LockMode=N'Exclusive',@LockOwner=N'Transaction',@LockTimeout=15000;\n IF @lock<0 THROW 51000,N'Yayın kilidi alınamadı.',1;\n";
    private static string End => " COMMIT;\nEND TRY\nBEGIN CATCH\n IF @@TRANCOUNT>0 ROLLBACK;\n THROW;\nEND CATCH;\n";
    private static Task Save(string root, string name, object value) => File.WriteAllTextAsync(Path.Combine(root, name), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));

    public static async Task RunAsync(string capturePath, string reviewPath, string configPath, string outputPath, string? paymentReconciliationPath = null, string? relationReconciliationPath = null)
    {
        var capture = Path.GetFullPath(capturePath); var review = Path.GetFullPath(reviewPath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        if (new[] { capture, review, output }.Any(p => !p.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase))
            || Directory.Exists(output) || File.Exists(output)) throw new InvalidOperationException("Paket yalnız yeni .tools klasörüne yazılır.");
        CustomerDecisionReplay.VerifyPackage(capture); CustomerDecisionReplay.VerifyPackage(review);
        var captureManifest = Document(Path.Combine(capture, "capture-manifest.json"));
        if (captureManifest.TryGetProperty("TargetDatabase", out var captureTarget) && captureTarget.GetString() != "AssistFlow")
            throw new InvalidOperationException("Prova hedefi referanslarıyla production veri paketi üretilemez.");
        var summary = Document(Path.Combine(review, "summary.json"));
        if (Path.GetFullPath(Text(summary, "SourceCapturePath")) != capture || Text(summary, "CustomerDecisionPackageHash").Length != 64)
            throw new InvalidOperationException("Güncel okumaya bağlı karar paketiyle incelenmiş veri gereklidir.");
        string? reconciliation = null; JsonElement? reconciliationSummary = null;
        if (paymentReconciliationPath is not null)
        {
            reconciliation = Path.GetFullPath(paymentReconciliationPath);
            if (!reconciliation.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Ödeme mutabakatı .tools altında olmalıdır.");
            CustomerDecisionReplay.VerifyPackage(reconciliation);
            reconciliationSummary = Document(Path.Combine(reconciliation, "summary.json"));
            if (Text(reconciliationSummary.Value, "CapturePackageHash") != HashFile(Path.Combine(capture, "package-hashes.json"))
                || Text(reconciliationSummary.Value, "ReviewPackageHash") != HashFile(Path.Combine(review, "package-hashes.json"))
                || Text(reconciliationSummary.Value, "CustomerDecisionPackageHash") != Text(summary, "CustomerDecisionPackageHash")
                || Text(reconciliationSummary.Value, "Status") != "PREPARATION_NOT_APPROVED_FOR_EXECUTION"
                || reconciliationSummary.Value.GetProperty("ReadyToApply").GetBoolean()
                || reconciliationSummary.Value.GetProperty("BankIssues").GetInt64() != 0)
                throw new InvalidOperationException("Ödeme mutabakatı kaynak/karar/inceleme veya banka kapısıyla uyuşmuyor.");
        }
        string? relations = null; JsonElement? relationSummary = null;
        if (relationReconciliationPath is not null)
        {
            relations = Path.GetFullPath(relationReconciliationPath);
            if (!relations.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("İlişki mutabakatı .tools altında olmalıdır.");
            CustomerDecisionReplay.VerifyPackage(relations);
            relationSummary = Document(Path.Combine(relations, "summary.json"));
            if (Text(relationSummary.Value, "CapturePackageHash") != HashFile(Path.Combine(capture, "package-hashes.json"))
                || Text(relationSummary.Value, "ReviewPackageHash") != HashFile(Path.Combine(review, "package-hashes.json"))
                || Text(relationSummary.Value, "CustomerDecisionPackageHash") != Text(summary, "CustomerDecisionPackageHash")
                || Text(relationSummary.Value, "Status") != "RELATION_RECONCILIATION_PREPARATION_NOT_APPLY"
                || relationSummary.Value.GetProperty("ReadyToApply").GetBoolean())
                throw new InvalidOperationException("İlişki mutabakatı kaynak/karar/inceleme kapısıyla uyuşmuyor.");
        }
        var types = Rows(Path.Combine(capture, "live-types.ndjson")).ToArray();
        var individual = types.Single(x => CollectionCustomerClassification.IndividualTypeCodes.Contains(Text(x, "Code")));
        var group = types.Single(x => CollectionCustomerClassification.GroupTypeCodes.Contains(Text(x, "Code")));
        var bank = types.Single(x => Text(x, "Code") == "BNK01");
        var customers = Rows(Path.Combine(capture, "live-customers.ndjson")).ToDictionary(x => Id(x, "Id"));
        var proposals = Document(Path.Combine(review, "classification-proposals.json")).EnumerateArray().ToArray();
        if (proposals.Any(x => Text(x, "Status") != "PREVIEW_ONLY_NO_UPDATE")) throw new InvalidOperationException("Müşteri tipi önerisinde çözülmemiş hedef var.");
        Directory.CreateDirectory(output);

        var refs = Document(Path.Combine(review, "reference-proposals.json"));
        var missingServices = refs.GetProperty("Services").EnumerateArray().Where(x => Text(x, "Status") == "INSERT_PROPOSAL_REQUIRES_APPROVAL").ToArray();
        var serviceSql = new StringBuilder(Header + Begin);
        serviceSql.AppendLine(" DECLARE @Plan TABLE(Name nvarchar(150) NOT NULL);");
        if (missingServices.Length > 0) serviceSql.AppendLine(" INSERT @Plan VALUES " + string.Join(",", missingServices.Select(x => "(" + Quote(Text(x, "Name")) + ")")) + ";");
        serviceSql.AppendLine(" IF EXISTS(SELECT p.Name FROM @Plan p JOIN dbo.ServiceType s ON UPPER(LTRIM(RTRIM(s.Name))) COLLATE Turkish_CI_AS=UPPER(p.Name) COLLATE Turkish_CI_AS AND s.IsDeleted=0 GROUP BY p.Name HAVING COUNT(*)>1) THROW 51000,N'Mükerrer servis tipi var.',1;");
        serviceSql.AppendLine(" IF EXISTS(SELECT 1 FROM @Plan p JOIN dbo.ServiceType s ON UPPER(LTRIM(RTRIM(s.Name))) COLLATE Turkish_CI_AS=UPPER(p.Name) COLLATE Turkish_CI_AS WHERE s.IsDeleted=1 AND NOT EXISTS(SELECT 1 FROM dbo.ServiceType a WHERE a.IsDeleted=0 AND UPPER(LTRIM(RTRIM(a.Name))) COLLATE Turkish_CI_AS=UPPER(p.Name) COLLATE Turkish_CI_AS)) THROW 51000,N'Pasif aynı adlı servis tipi var; ayrı inceleme gerekir.',1;");
        serviceSql.AppendLine(" INSERT dbo.ServiceType(Name,ContractNumber,IsDeleted) SELECT p.Name,NULL,0 FROM @Plan p WHERE NOT EXISTS(SELECT 1 FROM dbo.ServiceType s WHERE s.IsDeleted=0 AND UPPER(LTRIM(RTRIM(s.Name))) COLLATE Turkish_CI_AS=UPPER(p.Name) COLLATE Turkish_CI_AS);");
        serviceSql.Append(End);
        await File.WriteAllTextAsync(Path.Combine(output, "10-service-inserts-review.sql"), serviceSql.ToString(), new UTF8Encoding(false));

        var classification = new StringBuilder(Header + Begin);
        classification.AppendLine(" DECLARE @Plan TABLE(Id bigint PRIMARY KEY,OldType bigint,NewType bigint,GroupId bigint,TenantId bigint,Subscriber nvarchar(250),GroupCode nvarchar(200));");
        foreach (var chunk in proposals.Chunk(500))
        {
            var values = chunk.Select(p =>
            {
                var live = customers[Text(p, "TargetCustomerId")];
                return "(" + string.Join(",", Number(Text(p, "TargetCustomerId")), Number(Text(p, "ExpectedCustomerTypeId")), Number(Text(p, "ProposedLiveTypeId")),
                    Number(Text(p, "ExpectedGroupId")), NullableNumber(Text(p, "ExpectedTenantId")), Quote(Text(p, "ExpectedSubscriber")), Quote(Text(live, "GroupCode"))) + ")";
            });
            classification.AppendLine(" INSERT @Plan VALUES " + string.Join(",", values) + ";");
        }
        classification.AppendLine(" IF EXISTS(SELECT 1 FROM @Plan p LEFT JOIN dbo.Customers c ON c.Id=p.Id LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId WHERE c.Id IS NULL OR c.IsDeleted=1 OR p.NewType IS NULL OR c.CustomerTypeId NOT IN(p.OldType,p.NewType) OR ISNULL(c.CustomerGroupId,-1)<>ISNULL(p.GroupId,-1) OR ISNULL(c.TenantId,-1)<>ISNULL(p.TenantId,-1) OR ISNULL(c.SubscriberCode,N'')<>p.Subscriber OR ISNULL(g.Code,N'')<>p.GroupCode OR t.Code=N'BNK01') THROW 51000,N'Müşteri önkoşulları değişmiş; uygulama durduruldu.',1;");
        classification.AppendLine(" DECLARE @Changes TABLE(Id bigint,OldTypeId bigint,NewTypeId bigint);");
        classification.AppendLine(" UPDATE c SET CustomerTypeId=p.NewType OUTPUT deleted.Id,deleted.CustomerTypeId,inserted.CustomerTypeId INTO @Changes FROM dbo.Customers c JOIN @Plan p ON p.Id=c.Id WHERE c.CustomerTypeId=p.OldType AND p.OldType<>p.NewType;");
        classification.Append(End).AppendLine("SELECT * FROM @Changes ORDER BY Id; -- İşlem makbuzu ayrıca güvenli dosyaya kaydedilmelidir.");
        await File.WriteAllTextAsync(Path.Combine(output, "20-customer-classification-review.sql"), classification.ToString(), new UTF8Encoding(false));

        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(configPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connectionText = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var builder = new SqlConnectionStringBuilder(connectionText) { ApplicationIntent = ApplicationIntent.ReadOnly, ConnectTimeout = 10 };
        if (builder.InitialCatalog != "AssistFlow") throw new InvalidOperationException("Prosedür incelemesi yalnız AssistFlow üzerinde yapılır.");
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        using var command = connection.CreateCommand(); command.CommandTimeout = 30;
        command.CommandText = "SELECT OBJECT_DEFINITION(OBJECT_ID(N'stg.sp_SyncCustomers'))";
        var oldDefinition = await command.ExecuteScalarAsync() as string ?? throw new InvalidOperationException("Canlı senkronizasyon tanımı okunamadı.");
        var pattern = @"(?is)ISNULL\(\s*TRY_CAST\(c\.\[CustomerTypeId\] AS BIGINT\),\s*0\s*\) AS CustomerTypeId";
        if (Regex.Matches(oldDefinition, pattern).Count != 1) throw new InvalidOperationException("Prosedür eşleme deseni beklenenden farklı; otomatik fark oluşturulmadı.");
        var groupSql = string.Join(",", CollectionCustomerClassification.GroupCodes.Select(Quote));
        var individualSql = string.Join(",", CollectionCustomerClassification.IndividualCodes.Select(Quote));
        var newDefinition = Regex.Replace(oldDefinition, pattern,
            "-- CollectionCustomerClassification: production-reviewed-2026-10-15\nCASE WHEN TRY_CAST(c.[CustomerTypeId] AS BIGINT)=" + Number(Id(bank, "Id")) + " THEN " + Number(Id(bank, "Id"))
            + " WHEN UPPER(LTRIM(RTRIM(cgTarget.Code))) COLLATE Turkish_CI_AS IN (" + individualSql + ") THEN " + Number(Id(individual, "Id"))
            + " WHEN UPPER(LTRIM(RTRIM(cgTarget.Code))) COLLATE Turkish_CI_AS IN (" + groupSql + ") THEN " + Number(Id(group, "Id"))
            + " ELSE ISNULL(TRY_CAST(c.[CustomerTypeId] AS BIGINT),0) END AS CustomerTypeId");
        var assignment = @"Target\.\[CustomerTypeId\]\s*=\s*Source\.\[CustomerTypeId\]";
        if (Regex.Matches(newDefinition, assignment).Count != 1) throw new InvalidOperationException("Prosedür hedef tipi ataması tekil değil.");
        newDefinition = Regex.Replace(newDefinition, assignment, "Target.[CustomerTypeId] = CASE WHEN Target.[CustomerTypeId]=" + Number(Id(bank, "Id")) + " THEN Target.[CustomerTypeId] ELSE Source.[CustomerTypeId] END");
        if (!Regex.IsMatch(newDefinition, @"(?i)^\s*CREATE\s+(OR\s+ALTER\s+)?PROCEDURE")) throw new InvalidOperationException("Prosedür başlığı beklenenden farklı.");
        newDefinition = Regex.Replace(newDefinition, @"(?i)^\s*CREATE\s+(OR\s+ALTER\s+)?PROCEDURE", "ALTER PROCEDURE");
        var oldHash = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(oldDefinition)));
        var newHash = Convert.ToHexString(SHA256.HashData(Encoding.Unicode.GetBytes(newDefinition)));
        var procedureSql = Header + Begin + " DECLARE @hash varchar(64)=CONVERT(varchar(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'stg.sp_SyncCustomers')))),2);\n"
            + " IF @hash=" + Quote(newHash) + " BEGIN COMMIT; RETURN; END;\n IF @hash IS NULL OR @hash<>" + Quote(oldHash) + " THROW 51000,N'Canlı prosedür beklenen sürüm değil.',1;\n"
            + " DECLARE @definition nvarchar(max)=" + Quote(newDefinition) + ";\n EXEC sys.sp_executesql @definition;\n" + End;
        await File.WriteAllTextAsync(Path.Combine(output, "30-sync-procedure-review.sql"), procedureSql, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(output, "sync-procedure-before.txt"), oldDefinition, new UTF8Encoding(false));
        await File.WriteAllTextAsync(Path.Combine(output, "sync-procedure-after.txt"), newDefinition, new UTF8Encoding(false));

        var eligible = Document(Path.Combine(review, "contract-review.json")).EnumerateArray()
            .Where(x => Text(x, "Status") is "CANDIDATE_AFTER_REFERENCE_APPROVAL" or "TECHNICAL_CANDIDATE_NOT_APPROVED").ToDictionary(x => Text(x, "Id"));
        await Save(output, "contract-import-plan.json", eligible.Values);
        long payments = 0;
        if (reconciliation is not null)
        {
            var ids = new HashSet<long>(); var requests = new HashSet<Guid>();
            foreach (var payment in Rows(Path.Combine(reconciliation, "payment-import-plan.ndjson")))
            {
                var id = long.Parse(Text(payment, "SourcePaymentId")); var request = payment.GetProperty("RequestId").GetGuid();
                if (!eligible.ContainsKey(Text(payment, "SourceContractId")) || !ids.Add(id) || !requests.Add(request)
                    || request != CollectionPaymentImportIdentity.For(id) || payment.GetProperty("ReadyToApply").GetBoolean())
                    throw new InvalidOperationException("Mutabakat ödeme kimliği/sözleşmesi veya uygulama kapısı geçersiz.");
                payments++;
            }
            if (payments != reconciliationSummary!.Value.GetProperty("Candidates").GetInt64()) throw new InvalidOperationException("Mutabakat ödeme sayısı farklı.");
            foreach (var name in new[] { "payment-import-plan.ndjson", "bank-baseline-plan.ndjson", "legacy-definition-evidence.json" })
                File.Copy(Path.Combine(reconciliation, name), Path.Combine(output, name));
        }
        else
        {
            await using (var writer = new StreamWriter(Path.Combine(output, "payment-import-plan.ndjson"), false, new UTF8Encoding(false)))
                foreach (var payment in Rows(Path.Combine(review, "payment-review.ndjson")).Where(x => Text(x, "Status") == "PRELIMINARY_CANDIDATE_REQUIRES_LEGACY_EVIDENCE"))
                {
                    if (!eligible.ContainsKey(Text(payment, "SourceContractId"))) throw new InvalidOperationException("Ödeme adayı korunacak sözleşme listesinde değil.");
                    var id = long.Parse(Text(payment, "SourcePaymentId"));
                    await writer.WriteLineAsync(JsonSerializer.Serialize(new { SourcePaymentId = id, RequestId = CollectionPaymentImportIdentity.For(id), Review = payment }));
                    payments++;
                }
        }
        if (relations is not null)
        {
            foreach (var name in new[] { "group-parent-plan.json", "note-plan.ndjson", "group-history-plan.ndjson", "invoice-plan.ndjson", "invoice-payment-plan.ndjson",
                "invoice-balances.json", "account-plan.json", "physical-file-verification.json", "contract-file-plan.ndjson", "activity-file-plan.json", "file-summary.json",
                "customers-enriched.ndjson", "groups-enriched.ndjson", "live-groups.ndjson", "live-customers-verified.ndjson", "live-types-verified.ndjson", "live-currencies-verified.ndjson" })
                File.Copy(Path.Combine(relations, name), Path.Combine(output, name));
            File.Copy(Path.Combine(relations, "summary.json"), Path.Combine(output, "relation-summary.json"));
        }
        await Save(output, "manifest.json", new
        {
            Status = "BLOCKED_REVIEW_PACKAGE_NOT_APPROVED_FOR_EXECUTION",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CapturePackageHash = HashFile(Path.Combine(capture, "package-hashes.json")),
            ReviewPackageHash = HashFile(Path.Combine(review, "package-hashes.json")),
            CustomerDecisionPackageHash = Text(summary, "CustomerDecisionPackageHash"),
            PaymentReconciliationPackageHash = reconciliation is null ? null : HashFile(Path.Combine(reconciliation, "package-hashes.json")),
            PaymentPlanFormat = reconciliation is null ? "preliminary-review-v1" : "legacy-reconciled-source-plan-v1",
            RelationReconciliationPackageHash = relations is null ? null : HashFile(Path.Combine(relations, "package-hashes.json")),
            RelationSummary = relationSummary,
            CollectionCustomerClassification.PolicyHash,
            ServiceInsertProposals = missingServices.Length,
            CustomerTypeUpdateProposals = proposals.Length,
            OldProcedureSqlHash = oldHash,
            ProposedProcedureSqlHash = newHash,
            Contracts = eligible.Count,
            Payments = payments,
            Gates = new[] { "Explicit shared dbo/procedure approval", "Fresh maintenance snapshot and target fingerprint verification", reconciliation is null ? "Legacy payment currency/evidence reconciliation" : "Final maintenance payment evidence recheck and bank baseline apply", "Actor mapping and audit receipts", "Group/cari/invoice/file exception completion", "Isolated restore/idempotency/rollback rehearsal", "No production apply command in preparation tool" }
        });
        await Save(output, "package-hashes.json", Directory.GetFiles(output).Order().Select(x => new { File = Path.GetFileName(x), Sha256 = HashFile(x) }).ToArray());
        Console.WriteLine($"İnceleme paketi: {missingServices.Length} servis inserti, {proposals.Length} tip önerisi, dar prosedür farkı, {eligible.Count} sözleşme/{payments} ödeme adayı. Hiçbir SQL değişikliği uygulanmadı.");
    }
}
