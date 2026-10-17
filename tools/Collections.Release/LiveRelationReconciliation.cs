using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Model.Concrete.Collections;

// SELECT-only enrichment + offline relationship review. Never runs application startup, uploads or SQL writes.
internal static class LiveRelationReconciliation
{
    private sealed record Owner(string? TargetCustomerId, string? ParentLegacyId, string Status);
    private sealed record Decision(string SourceId, string Status, string[] Issues, Owner? Owner, string SourceHash, JsonElement Source)
    { public bool ReadyToApply => false; }
    private static int Length(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Length : 0;
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    private static string Id(JsonElement r, string k) => long.TryParse(Text(r, k), out var n) ? n.ToString(CultureInfo.InvariantCulture) : Text(r, k);
    private static string Hash(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)); }
    private static string RowHash(JsonElement row) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.GetRawText())));
    private static JsonElement Document(string path) { using var d = JsonDocument.Parse(File.ReadAllText(path)); return d.RootElement.Clone(); }
    private static JsonElement[] Rows(string root, string file) => File.ReadLines(Path.Combine(root, file)).Select(line => { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); }).ToArray();
    private static Task Save(string root, string file, object value) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    private static async Task SaveRows(string root, string file, IEnumerable<object> rows)
    { await using var writer = new StreamWriter(Path.Combine(root, file), false, new UTF8Encoding(false)); foreach (var row in rows) await writer.WriteLineAsync(JsonSerializer.Serialize(row)); }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    private static string Norm(string s) => CollectionCustomerClassification.Normalize(s);
    private static string Name(string s) => Regex.Replace(Norm(s), @"[^\p{L}\p{Nd}]", "");
    private static string NormalizePath(string s) => string.Join('/', s.Trim().Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries));
    private static bool Amount(JsonElement r) => decimal.TryParse(Text(r, "Amount"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n)
        && Math.Abs(n) < 10000000000000000m && decimal.Round(n, 2) == n;
    private static bool Date(JsonElement r, string k) => DateTime.TryParse(Text(r, k), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _);
    private static string Period(JsonElement r) => int.TryParse(Text(r, "PeriodYear"), out var y) && y is >= 1 and <= 9998
        && int.TryParse(Text(r, "PeriodMonth"), out var m) && m is >= 1 and <= 12 ? $"{y:D4}-{m:D2}-01" : "";
    private static bool Candidate(Decision d) => d.Issues.Length == 0;
    private static string Status(List<string> issues, Owner? owner = null) => issues.Count > 0 ? "HOLD_OR_EXCLUDE"
        : owner?.ParentLegacyId is not null ? "CANDIDATE_AFTER_PARENT_CREATION" : "TECHNICAL_CANDIDATE_NOT_APPROVED";

    public static async Task RunAsync(string capturePath, string reviewPath, string configPath, string fileRootPath, string fileManifestPath, string activityArchive, string outputPath)
    {
        var capture = Path.GetFullPath(capturePath); var review = Path.GetFullPath(reviewPath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        Require(new[] { capture, review, output }.All(p => p.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)) && !Directory.Exists(output) && !File.Exists(output), "Mutabakat yalnız yeni .tools klasöründe oluşturulabilir.");
        CustomerDecisionReplay.VerifyPackage(capture); CustomerDecisionReplay.VerifyPackage(review);
        var reviewSummary = Document(Path.Combine(review, "summary.json"));
        Require(Path.GetFullPath(Text(reviewSummary, "SourceCapturePath")) == capture && Text(reviewSummary, "CustomerDecisionPackageHash").Length == 64, "Kaynak/karar incelemesi uyuşmuyor.");
        Require(Hash(fileManifestPath) == "4104B6A26F4AEF6E97C33976DA1ED444CD660F97D583D3CB87045D01DCA59ED7", "Arşiv dosya manifesti beklenen kanıt değil.");
        Require(Hash(activityArchive) == "398B4120D946D7969A3569771DE8E03F2E4FDE59A9CCA045CA7A5D611F96C480", "Aktivite arşivi değişmiş.");
        using var cfg = JsonDocument.Parse(await File.ReadAllTextAsync(configPath), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var cs = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? cfg.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString();
        var builder = new SqlConnectionStringBuilder(cs) { ApplicationIntent = ApplicationIntent.ReadOnly, ConnectTimeout = 10, ApplicationName = "CollectionRelationsReadOnly" };
        Require(builder.InitialCatalog == "AssistFlow", "Yalnız AssistFlow referansları okunabilir.");
        var captureManifest = Document(Path.Combine(capture, "capture-manifest.json"));
        var targetDatabase = captureManifest.TryGetProperty("TargetDatabase", out var capturedTarget) ? capturedTarget.GetString() : "AssistFlow";
        Require(targetDatabase is "AssistFlow" or "AssistFlow_Performance_Restore", "Desteklenmeyen ilişki hedefi.");
        builder.InitialCatalog = targetDatabase;
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync();
        var queries = new Dictionary<string, string>
        {
            ["customers-enriched"] = "SELECT CustomerID,SubscriberNo,Name,Type,GroupID,GroupCardNo,CreatedOn,ModifiedOn,CreatedBy,ModifiedBy,AccountNo FROM MGS.Core.Customer ORDER BY CustomerID",
            ["groups-enriched"] = "SELECT GroupID,Code,Name,GroupType FROM MGS.Definition.[Group] ORDER BY GroupID",
            ["live-groups"] = "SELECT Id,Code,GroupName,ParentGroupId FROM dbo.CustomerGroups ORDER BY Id",
            ["live-customers-verified"] = "SELECT c.Id,c.SubscriberCode,c.SubscriberCompany,c.CustomerTypeId,c.CustomerGroupId,c.TenantId,c.IsDeleted,t.Code AS TypeCode,g.Code AS GroupCode FROM dbo.Customers c LEFT JOIN dbo.CustomerType t ON t.Id=c.CustomerTypeId LEFT JOIN dbo.CustomerGroups g ON g.Id=c.CustomerGroupId ORDER BY c.Id",
            ["live-types-verified"] = "SELECT Id,Code,Name FROM dbo.CustomerType ORDER BY Id",
            ["live-currencies-verified"] = "SELECT Id,Code,Name FROM dbo.CurrencyType ORDER BY Id"
        };
        async Task<Dictionary<string, JsonElement[]>> Read()
        {
            var result = new Dictionary<string, JsonElement[]>();
            foreach (var (name, sql) in queries)
            {
                using var cmd = connection.CreateCommand(); cmd.CommandText = "SET LOCK_TIMEOUT 5000; " + sql; cmd.CommandTimeout = 120;
                await using var reader = await cmd.ExecuteReaderAsync(); var list = new List<JsonElement>();
                while (await reader.ReadAsync()) { var fields = new Dictionary<string, object?>(); for (var i = 0; i < reader.FieldCount; i++) fields[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i); list.Add(JsonSerializer.SerializeToElement(fields)); }
                result[name] = list.ToArray();
            }
            return result;
        }
        var enriched = await Read(); var check = await Read();
        Require(JsonSerializer.Serialize(enriched) == JsonSerializer.Serialize(check), "İlişki kaynakları iki okumada değişti; yeni kesit gerekir.");
        foreach (var (oldFile, newName, key) in new[] { ("customers.ndjson", "customers-enriched", "CustomerID"), ("groups.ndjson", "groups-enriched", "GroupID"),
            ("live-customers.ndjson", "live-customers-verified", "Id"), ("live-types.ndjson", "live-types-verified", "Id"), ("live-currencies.ndjson", "live-currencies-verified", "Id") })
        {
            var old = Rows(capture, oldFile); var current = enriched[newName].ToDictionary(x => Id(x, key));
            var changed = old.Where(r => !current.TryGetValue(Id(r, key), out var c) || r.EnumerateObject().Any(p => c.GetProperty(p.Name).ToString() != p.Value.ToString())).ToArray();
            Require(old.Length == current.Count && changed.Length == 0, $"{newName}: önceki {old.Length}, güncel {current.Count}, değişmiş/eski eksik {changed.Length}. İlk kesitten sonra fark var; yeniden okuma gerekir.");
        }
        Directory.CreateDirectory(output);
        foreach (var (name, rows) in enriched) await SaveRows(output, name + ".ndjson", rows.Cast<object>());
        var customers = enriched["customers-enriched"].ToDictionary(x => Id(x, "CustomerID"));
        var targets = Rows(capture, "live-customers.ndjson").Where(x => !x.GetProperty("IsDeleted").GetBoolean()).ToArray();
        var targetById = targets.ToDictionary(x => Id(x, "Id"));
        var contracts = Document(Path.Combine(review, "contract-review.json")).EnumerateArray().ToArray();
        var retained = contracts.Where(x => Text(x, "Status") is "CANDIDATE_AFTER_REFERENCE_APPROVAL" or "TECHNICAL_CANDIDATE_NOT_APPROVED").ToDictionary(x => Id(x, "Id"));
        var contractOwners = retained.Values.GroupBy(x => Id(x, "CustomerId")).ToDictionary(g => g.Key, g => g.Select(x => Text(x, "TargetCustomerId")).Distinct().ToArray());
        var parentPlans = new List<object>(); var parents = new Dictionary<string, Owner>();
        var scopedParents = customers.Values.Where(x => Text(x, "Type") == "G" && CollectionCustomerClassification.Classify(Text(x, "GroupCardNo")) == CollectionCustomerClass.Group).ToArray();
        foreach (var p in scopedParents)
        {
            var id = Id(p, "CustomerID"); var code = Norm(Text(p, "GroupCardNo")); var name = Name(Text(p, "Name")); var sub = Norm(Text(p, "SubscriberNo"));
            var groups = enriched["live-groups"].Where(x => Norm(Text(x, "Code")) == code).ToArray(); var issues = new List<string>();
            var candidates = targets.Where(x => sub.Length > 0 && Norm(Text(x, "SubscriberCode")) == sub || Norm(Text(x, "SubscriberCode")) == code
                || name.Length > 0 && Name(Text(x, "SubscriberCompany")) == name || name.Length >= 8 && Name(Text(x, "SubscriberCompany")).StartsWith(name[..8], StringComparison.Ordinal)).ToArray();
            if (groups.Length != 1) issues.Add("GROUP_NOT_UNIQUE");
            if (scopedParents.Count(x => Norm(Text(x, "GroupCardNo")) == code) != 1 || name.Length > 0 && scopedParents.Count(x => Name(Text(x, "Name")) == name) > 1
                || sub.Length > 0 && scopedParents.Count(x => Norm(Text(x, "SubscriberNo")) == sub) > 1) issues.Add("SOURCE_PARENT_NOT_UNIQUE");
            if (name.Length == 0) issues.Add("NAME_EMPTY");
            if (Length(p, "AccountNo") > 200) issues.Add("ACCOUNT_TOO_LONG");
            // A name/prefix hit is only a collision warning, never an automatic ownership match.
            if (candidates.Length > 0) issues.Add("LIVE_CUSTOMER_CANDIDATES_REQUIRE_VERIFIED_PARENT_MAPPING");
            var owner = new Owner(null, id, issues.Count == 0 ? "PROPOSE_PARENT_CREATE_REQUIRES_APPROVAL" : "PARENT_REVIEW");
            if (issues.Count == 0) parents[id] = owner;
            parentPlans.Add(new
            {
                SourceId = id,
                Owner = owner,
                GroupCode = code,
                TargetGroupId = groups.Length == 1 ? Id(groups[0], "Id") : null,
                IsCorporate = enriched["groups-enriched"].Any(x => Norm(Text(x, "Code")) == code && Text(x, "GroupType") == "Kurumsal"),
                CandidateCustomerIds = candidates.Select(x => Id(x, "Id")),
                Issues = issues,
                Source = p,
                SourceHash = RowHash(p),
                ReadyToApply = false
            });
        }
        await Save(output, "group-parent-plan.json", parentPlans);
        Owner? ResolveOwner(string source)
        {
            if (contractOwners.TryGetValue(source, out var ids) && ids.Length == 1 && ids[0].Length > 0) return new(ids[0], null, "RETAINED_CONTRACT_OWNER");
            return parents.GetValueOrDefault(source);
        }
        var notePlans = Rows(capture, "notes.ndjson").Select(r =>
        {
            var owner = ResolveOwner(Id(r, "CustomerID")); var issues = new List<string>();
            if (owner is null) issues.Add("NO_VERIFIED_RETAINED_OWNER");
            if (Text(r, "Comment").Length == 0) issues.Add("EMPTY_NOTE");
            if (Length(r, "Comment") > 10000 || Length(r, "CreatedBy") > 200 || Length(r, "ModifiedBy") > 200) issues.Add("TEXT_LENGTH_REVIEW");
            return new Decision(Id(r, "CommentID"), Status(issues, owner), issues.ToArray(), owner, RowHash(r), r);
        }).ToArray();
        await SaveRows(output, "note-plan.ndjson", notePlans);
        var groupRows = Rows(capture, "group-history.ndjson");
        var duplicateGroups = groupRows.GroupBy(x => (Id(x, "ContractID"), Period(x))).Where(g => g.Key.Item2.Length > 0 && g.Count() > 1).ToArray();
        string State(JsonElement r) => JsonSerializer.Serialize(new[] { Id(r, "GroupStatusID"), r.TryGetProperty("StatusDescription", out var v) ? v.GetRawText() : "MISSING" });
        var conflicts = duplicateGroups.Where(g => g.Select(State).Distinct().Count() > 1).Select(g => g.Key).ToHashSet();
        var aliases = duplicateGroups.Where(g => g.Select(State).Distinct().Count() == 1).SelectMany(g => g.OrderBy(x => long.Parse(Id(x, "FollowGroupStatusID"))).Skip(1)).Select(x => Id(x, "FollowGroupStatusID")).ToHashSet();
        var statuses = Rows(capture, "group-statuses.ndjson").ToDictionary(x => Id(x, "GroupStatusID"));
        var groupPlans = groupRows.Select(r =>
        {
            var issues = new List<string>(); var contract = retained.GetValueOrDefault(Id(r, "ContractID")); Owner? owner = null;
            if (contract.ValueKind == JsonValueKind.Undefined) issues.Add("NO_RETAINED_CONTRACT");
            else { owner = ResolveOwner(Id(contract, "CustomerId")); if (!targetById.TryGetValue(Text(contract, "TargetCustomerId"), out var target) || CollectionCustomerClassification.Classify(Text(target, "GroupCode")) != CollectionCustomerClass.Group) issues.Add("OUTSIDE_GROUP_SCOPE"); }
            if (Period(r).Length == 0) issues.Add("INVALID_PERIOD");
            if (conflicts.Contains((Id(r, "ContractID"), Period(r)))) issues.Add("CONFLICTING_DUPLICATE_PERIOD");
            if (Text(r, "GroupStatusID").Length > 0 && !statuses.ContainsKey(Id(r, "GroupStatusID"))) issues.Add("STATUS_REFERENCE_MISSING");
            if (Length(r, "StatusDescription") > 500) issues.Add("DESCRIPTION_TOO_LONG");
            return new Decision(Id(r, "FollowGroupStatusID"), issues.Count == 0 && aliases.Contains(Id(r, "FollowGroupStatusID")) ? "IDENTICAL_DUPLICATE_REPRESENTED_ONCE" : Status(issues, owner), issues.ToArray(), owner, RowHash(r), r);
        }).ToArray();
        await SaveRows(output, "group-history-plan.ndjson", groupPlans);
        var currencyRefs = Document(Path.Combine(review, "reference-proposals.json")).GetProperty("Currencies").EnumerateArray().Where(x => Text(x, "Status") == "EXISTING_MATCH").ToDictionary(x => Id(x, "LegacyId"));
        var invoices = Rows(capture, "invoice-follow.ndjson").Select(r =>
        {
            var owner = ResolveOwner(Id(r, "CustomerID")); var issues = new List<string>();
            if (owner is null) issues.Add("NO_VERIFIED_RETAINED_OWNER");
            if (!currencyRefs.ContainsKey(Id(r, "CurrencyID"))) issues.Add("CURRENCY_REVIEW");
            if (Text(r, "Type") is not ("Bireysel" or "B" or "Kurumsal" or "K")) issues.Add("INVOICE_TYPE_REVIEW");
            if (!Date(r, "Date")) issues.Add("DATE_REVIEW"); if (!Amount(r)) issues.Add("AMOUNT_REVIEW");
            if (Length(r, "No") > 50 || Length(r, "ProjectCode") > 100 || Length(r, "CreatedBy") > 100 || Length(r, "ModifiedBy") > 100) issues.Add("TEXT_LENGTH_REVIEW");
            return new Decision(Id(r, "InvoiceFollowID"), Status(issues, owner), issues.ToArray(), owner, RowHash(r), r);
        }).ToDictionary(x => x.SourceId);
        var invoicePayments = Rows(capture, "invoice-payments.ndjson").Select(r =>
        {
            var parent = invoices.GetValueOrDefault(Id(r, "InvoiceFollowID")); var issues = new List<string>();
            if (parent is null) issues.Add("INVOICE_MISSING");
            else { if (!Candidate(parent)) issues.Add("INVOICE_HELD"); if (Id(r, "CustomerID") != Id(parent.Source, "CustomerID")) issues.Add("INVOICE_CUSTOMER_MISMATCH"); }
            if (!Date(r, "Date")) issues.Add("DATE_REVIEW"); if (!Amount(r)) issues.Add("AMOUNT_REVIEW");
            if (Length(r, "Description") > 1000 || Length(r, "CreatedBy") > 100 || Length(r, "ModifiedBy") > 100) issues.Add("TEXT_LENGTH_REVIEW");
            return new Decision(Id(r, "InvoiceFollowPaymentID"), Status(issues, parent?.Owner), issues.ToArray(), parent?.Owner, RowHash(r), r);
        }).ToArray();
        var blockedInvoices = invoicePayments.Where(x => !Candidate(x)).Select(x => Id(x.Source, "InvoiceFollowID")).ToHashSet();
        foreach (var key in blockedInvoices.Where(invoices.ContainsKey)) { var p = invoices[key]; invoices[key] = p with { Status = "HOLD_OR_EXCLUDE", Issues = [.. p.Issues, "PAYMENT_HISTORY_REVIEW"] }; }
        invoicePayments = invoicePayments.Select(p => Candidate(p) && invoices.TryGetValue(Id(p.Source, "InvoiceFollowID"), out var parent) && !Candidate(parent)
            ? p with { Status = "HOLD_OR_EXCLUDE", Issues = [.. p.Issues, "INVOICE_HELD"] } : p).ToArray();
        await SaveRows(output, "invoice-plan.ndjson", invoices.Values); await SaveRows(output, "invoice-payment-plan.ndjson", invoicePayments);
        await Save(output, "invoice-balances.json", invoices.Values.Where(Candidate).Select(i => new
        {
            SourceInvoiceId = i.SourceId,
            i.Owner,
            Currency = Text(currencyRefs[Id(i.Source, "CurrencyID")], "Code"),
            CurrencyTypeId = Id(currencyRefs[Id(i.Source, "CurrencyID")], "TargetId"),
            Amount = i.Source.GetProperty("Amount").GetDecimal(),
            Paid = invoicePayments.Where(p => Candidate(p) && Id(p.Source, "InvoiceFollowID") == i.SourceId).Sum(p => p.Source.GetProperty("Amount").GetDecimal()),
            Remaining = i.Source.GetProperty("Amount").GetDecimal() - invoicePayments.Where(p => Candidate(p) && Id(p.Source, "InvoiceFollowID") == i.SourceId).Sum(p => p.Source.GetProperty("Amount").GetDecimal()),
            ReadyToApply = false
        }));
        var accounts = customers.Values.Where(x => Text(x, "AccountNo").Length > 0).GroupBy(x => Text(x, "AccountNo").ToUpperInvariant()).Select(g =>
        {
            var sources = g.ToArray(); var owner = sources.Length == 1 ? ResolveOwner(Id(sources[0], "CustomerID")) : null;
            var issues = new List<string>(); if (g.Key.Length > 200) issues.Add("ACCOUNT_TOO_LONG"); if (sources.Length != 1) issues.Add("DUPLICATE_SOURCE_ACCOUNT"); if (owner is null) issues.Add("NO_VERIFIED_RETAINED_OWNER");
            return new { Code = g.Key, SourceCustomerIds = sources.Select(x => Id(x, "CustomerID")), Owner = owner, Status = Status(issues, owner), Issues = issues, ReadyToApply = false };
        }).ToArray();
        await Save(output, "account-plan.json", accounts);
        await ReconcileFiles(output, capture, retained, ResolveOwner, customers, fileRootPath, fileManifestPath, activityArchive);
        var invoiceTotals = invoices.Values.Where(Candidate).GroupBy(x => Text(currencyRefs[Id(x.Source, "CurrencyID")], "Code")).Select(g => new
        {
            Currency = g.Key,
            Count = g.Count(),
            Amount = g.Sum(x => x.Source.GetProperty("Amount").GetDecimal()),
            Paid = invoicePayments.Where(p => Candidate(p) && g.Any(i => i.SourceId == Id(p.Source, "InvoiceFollowID"))).Sum(p => p.Source.GetProperty("Amount").GetDecimal())
        });
        object Summary(IEnumerable<Decision> rows) => new { Count = rows.Count(), Statuses = rows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count()), Issues = rows.SelectMany(x => x.Issues).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()) };
        await Save(output, "summary.json", new
        {
            Status = "RELATION_RECONCILIATION_PREPARATION_NOT_APPLY",
            CapturePackageHash = Hash(Path.Combine(capture, "package-hashes.json")),
            ReviewPackageHash = Hash(Path.Combine(review, "package-hashes.json")),
            CustomerDecisionPackageHash = Text(reviewSummary, "CustomerDecisionPackageHash"),
            ObservedAtUtc = DateTimeOffset.UtcNow,
            ParentCount = scopedParents.Length,
            ParentCreateProposals = parents.Count,
            ParentReview = scopedParents.Length - parents.Count,
            Notes = Summary(notePlans),
            GroupHistory = Summary(groupPlans),
            Invoices = Summary(invoices.Values),
            InvoicePayments = Summary(invoicePayments),
            InvoiceTotals = invoiceTotals,
            AccountCodes = accounts.Length,
            AccountCandidates = accounts.Count(x => x.Issues.Count == 0),
            AccountIssues = accounts.SelectMany(x => x.Issues).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()),
            CoreInvoices = Rows(capture, "invoices.ndjson").Length,
            FileSummary = Document(Path.Combine(output, "file-summary.json")),
            Gates = new[] { "Shared parent/customer type approval and live identity allocation", "Current source and live target recheck in maintenance window", "Actor/audit mapping", "Production CDN ownership/readback and security verification", "Isolated restore/idempotency/rollback rehearsal" },
            ReadyToApply = false
        });
        await Save(output, "package-hashes.json", Directory.GetFiles(output).Order().Select(x => new { File = Path.GetFileName(x), Sha256 = Hash(x) }).ToArray());
        Console.WriteLine($"İlişki incelemesi: {notePlans.Length} not, {groupPlans.Length} grup geçmişi, {invoices.Count} fatura, {invoicePayments.Length} fatura ödemesi, {accounts.Length} cari kod. Üst kart {parents.Count} oluşturma önerisi / {scopedParents.Length - parents.Count} inceleme. DB/CDN yazması yok.");
    }

    private static async Task ReconcileFiles(string output, string capture, Dictionary<string, JsonElement> retained, Func<string, Owner?> owner,
        Dictionary<string, JsonElement> customers, string rootPath, string manifestPath, string activityArchive)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require((File.GetAttributes(root) & FileAttributes.ReparsePoint) == 0, "Dosya kökü link olamaz.");
        var files = Document(manifestPath).GetProperty("Files").EnumerateArray().ToDictionary(x => NormalizePath(Text(x, "RelativePath")), StringComparer.OrdinalIgnoreCase);
        var physical = new Dictionary<string, object>();
        foreach (var (path, f) in files)
        {
            Require(!path.Split('/').Any(x => x is ".." or "." || x.Contains(':')), "Güvensiz manifest yolu.");
            var full = Path.GetFullPath(Path.Combine(root, path)); Require(full.StartsWith(root, StringComparison.OrdinalIgnoreCase), "Dosya kök dışına çıkıyor.");
            for (var p = Path.GetDirectoryName(full); p is not null && p.Length >= root.TrimEnd(Path.DirectorySeparatorChar).Length; p = Path.GetDirectoryName(p))
                Require((File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0, "Dosya yolunda link var.");
            var info = new FileInfo(full); Require(info.Exists && (info.Attributes & FileAttributes.ReparsePoint) == 0 && info.Length == f.GetProperty("Size").GetInt64() && Hash(full) == Text(f, "SHA256"), "Fiziksel dosya eski kanıtla uyuşmuyor.");
            physical[path] = new { RelativePath = path, Size = info.Length, SHA256 = Text(f, "SHA256"), Status = Text(f, "Status") };
        }
        await Save(output, "physical-file-verification.json", physical.Values);
        var plans = new List<object>(); var counts = new Dictionary<string, int>();
        foreach (var c in Rows(capture, "contracts.ndjson"))
        {
            var id = Id(c, "ContractID"); var path = NormalizePath(Text(c, "FileAttachmentPath")); var name = Text(c, "FileAttachmentName");
            string? relative = path.StartsWith("~/Content/Contract/", StringComparison.OrdinalIgnoreCase) ? path[10..] : null;
            var status = !retained.ContainsKey(id) ? "CONTRACT_HELD_OR_EXCLUDED" : path.Length == 0 && name.Length == 0 ? "NO_METADATA"
                : relative is null || relative.Split('/').Any(x => x is ".." or "." || x.Contains(':')) ? "PATH_REVIEW"
                : name.Length == 0 || name.Length > 260 ? "ORIGINAL_NAME_REVIEW"
                : !files.TryGetValue(relative, out var f) ? "MISSING_IN_SUPPLIED_ARCHIVE"
                : Text(f, "Status") != "Valid" ? "FILE_" + Text(f, "Status").ToUpperInvariant()
                : MimeExtension(name) != Text(f, "ContentType") ? "ORIGINAL_NAME_TYPE_REVIEW" : "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN";
            counts[status] = counts.GetValueOrDefault(status) + 1;
            plans.Add(new
            {
                SourceContractId = id,
                SourceCustomerId = Id(c, "CustomerID"),
                Status = status,
                RelativePath = relative,
                OriginalName = name,
                SourceHash = RowHash(c),
                File = relative is not null && physical.TryGetValue(relative, out var file) ? file : null,
                ReadyToApply = false
            });
        }
        await SaveRows(output, "contract-file-plan.ndjson", plans);
        async Task<byte[]> SevenZip(int limit, params string[] args)
        {
            var start = new ProcessStartInfo("7z") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var a in args) start.ArgumentList.Add(a);
            using var process = Process.Start(start)!; var error = process.StandardError.ReadToEndAsync();
            using var data = new MemoryStream(); var buffer = new byte[65536];
            try { int read; while ((read = await process.StandardOutput.BaseStream.ReadAsync(buffer)) > 0) { Require(data.Length + read <= limit, "Arşiv çıktısı boyut sınırını aştı."); await data.WriteAsync(buffer.AsMemory(0, read)); } await process.WaitForExitAsync(); await error; Require(process.ExitCode == 0, "Arşiv okunamadı."); }
            catch { if (!process.HasExited) process.Kill(true); throw; }
            return data.ToArray();
        }
        var listing = Encoding.UTF8.GetString(await SevenZip(64 * 1024 * 1024, "l", "-slt", "-sccUTF-8", Path.GetFullPath(activityArchive)));
        var identityMatches = Document(Path.Combine(capture, "customer-matches.json")).EnumerateArray().ToDictionary(x => Id(x, "SourceId"));
        var activity = new List<object>(); var activityCounts = new Dictionary<string, int>();
        foreach (var block in Regex.Split(listing.Replace("\r", ""), "\n\n"))
        {
            if (!Regex.IsMatch(block, "^Folder = -$", RegexOptions.Multiline)) continue;
            var path = Regex.Match(block, "^Path = (.+)$", RegexOptions.Multiline).Groups[1].Value; var parts = path.Replace('\\', '/').Split('/');
            if (parts.Any(x => x is ".." or "." || x.Contains(':'))) throw new InvalidOperationException("Güvensiz aktivite yolu.");
            if (parts.Any(x => x.Equals("Thumb", StringComparison.OrdinalIgnoreCase))) continue;
            if (parts.Length != 3 || parts[0] != "Aktivite" || !long.TryParse(parts[1], out _)) throw new InvalidOperationException("Aktivite dosya yolu beklenenden farklı.");
            var source = parts[1]; var winner = source; var proof = "DIRECT_RETAINED_OWNER";
            if (source is "924" or "2206")
            {
                winner = source == "924" ? "291401" : "87400"; proof = "PREVIOUS_EXPLICIT_FILE_TRANSFER_APPROVAL";
                var old = customers.GetValueOrDefault(source); var current = customers.GetValueOrDefault(winner);
                var valid = old.ValueKind != JsonValueKind.Undefined && current.ValueKind != JsonValueKind.Undefined && Name(Text(old, "Name")).Length > 0
                    && Name(Text(old, "Name")) == Name(Text(current, "Name")) && Date(old, "CreatedOn") && Date(current, "CreatedOn")
                    && System.DateTime.Parse(Text(current, "CreatedOn"), CultureInfo.InvariantCulture) > System.DateTime.Parse(Text(old, "CreatedOn"), CultureInfo.InvariantCulture);
                if (!valid) { winner = ""; proof = "APPROVED_FILE_WINNER_SOURCE_DRIFT_REVIEW"; }
            }
            var resolved = owner(winner);
            // The separately approved file transfer needs a verified current customer, not financial contract eligibility.
            // It never transfers the discarded subscription's payments or relaxes note/invoice ownership.
            if (resolved is null && proof == "PREVIOUS_EXPLICIT_FILE_TRANSFER_APPROVAL" && identityMatches.TryGetValue(winner, out var match)
                && Text(match, "Status") is "IDENTITY_MATCH_CANDIDATE" or "MATCH_REQUIRES_CLASSIFICATION" && Text(match, "TargetId").Length > 0)
                resolved = new Owner(Text(match, "TargetId"), null, "EXPLICIT_FILE_TRANSFER_CURRENT_IDENTITY_VERIFIED");
            var bytes = await SevenZip(20971520, "e", "-so", "-spd", Path.GetFullPath(activityArchive), path);
            var contentType = Mime(parts[2], bytes); var status = resolved is null ? "NO_VERIFIED_RETAINED_OWNER" : contentType.Length == 0 ? "CONTENT_REVIEW" : "LOCAL_VERIFIED_PENDING_PRODUCTION_CDN";
            activityCounts[status] = activityCounts.GetValueOrDefault(status) + 1;
            activity.Add(new
            {
                SourceCustomerId = source,
                RetainedLegacyCustomerId = winner,
                Owner = resolved,
                Status = status,
                Proof = proof,
                Path = path,
                Size = bytes.Length,
                SHA256 = Convert.ToHexString(SHA256.HashData(bytes)),
                ContentType = contentType,
                ReadyToApply = false
            });
        }
        await Save(output, "activity-file-plan.json", activity);
        await Save(output, "file-summary.json", new
        {
            VerifiedPhysicalFiles = files.Count,
            PhysicalManifestHash = Hash(manifestPath),
            ContractResults = counts,
            ActivityFiles = activity.Count,
            ActivityResults = activityCounts,
            Note = "Local archive bytes rechecked; no production CDN upload/readback or current malware scan. Test CDN keys are not production ownership proof. Missing files do not delete contracts."
        });
    }
    private static string Mime(string name, byte[] bytes) => Path.GetExtension(name).ToLowerInvariant() switch
    {
        ".pdf" when bytes.Length >= 4 && bytes.AsSpan(0, 4).SequenceEqual("%PDF"u8) => "application/pdf",
        ".jpg" or ".jpeg" when bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 => "image/jpeg",
        ".png" when bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) => "image/png",
        _ => ""
    };
    private static string MimeExtension(string name) => Path.GetExtension(name).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => "" };
}
