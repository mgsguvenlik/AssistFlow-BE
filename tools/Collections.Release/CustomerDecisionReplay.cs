using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// File-only reconciliation of recorded customer decisions. Does not apply database changes.
internal static class CustomerDecisionReplay
{
    internal static readonly IReadOnlyDictionary<string, string> ApprovedHistoryIds = new Dictionary<string, string>
    { ["34133"] = "27590", ["82291"] = "48715", ["84457"] = "49221" };
    private const string WorkbookHash = "2CA674418FE29BEDCC4895A1172883D9D733646FC4709E8BA8142B9A13CC5315";
    private const string ArchivedDecisionHash = "D3EE3D15D7AEE5301804161987694913C07AE83616C04D1A78B737955AB97D1A";
    private const string ArchivedManifestHash = "950FD53132F1E8467B42BB02640563EF92EF8C3A46B1A69075450642FE49FDC7";
    private static readonly string[] ContractFields = ["ContractID", "CustomerID", "ServiceTypeID", "SubscriptionStatusID", "ContractStatusID", "PaymentMethodID", "Amount", "CurrencyID", "StartingDateMonth", "StartingDateYear", "EndDate", "PaymentTypeID", "GtsNo", "IvrNo", "InheritContractID"];
    private static readonly string[] HistoryFields = ["ContractHistoryID", "ContractID", "CustomerID", "StartingDateMonth", "StartingDateYear", "EndDate", "PaymentTypeID", "Amount", "ProcessType", "CurrencyID"];
    private static readonly string[] CustomerFields = ["CustomerID", "SubscriberNo", "Name", "Type", "GroupID", "GroupCardNo", "CreatedOn"];
    private static readonly string[] PaymentFields = ["PaymentID", "CustomerID", "ContractID", "PeriodMonth", "PeriodYear", "Date", "Amount", "Free", "Description"];
    private static string Text(JsonElement r, string k) => r.TryGetProperty(k, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString().Trim() : "";
    private static string Id(JsonElement r, string k) => long.TryParse(Text(r, k), out var id) ? id.ToString(CultureInfo.InvariantCulture) : Text(r, k);
    private static JsonElement Document(string file) { using var d = JsonDocument.Parse(File.ReadAllText(file)); return d.RootElement.Clone(); }
    private static Dictionary<string, JsonElement> Rows(string file, string key) => File.ReadLines(file).Select(line =>
    { using var d = JsonDocument.Parse(line); return d.RootElement.Clone(); }).ToDictionary(x => Id(x, key));
    private static string HashFile(string file) { using var s = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(s)); }
    private static Task Save(string root, string file, object value) => File.WriteAllTextAsync(Path.Combine(root, file), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
    private static string? Canonical(JsonElement r, string field)
    {
        if (!r.TryGetProperty(field, out var v)) return "(MISSING_FIELD)";
        if (v.ValueKind == JsonValueKind.Null) return null;
        var text = v.ToString().Trim();
        if (field.EndsWith("ID") || field.EndsWith("Month") || field.EndsWith("Year"))
            return long.TryParse(text, out var n) ? n.ToString(CultureInfo.InvariantCulture) : text;
        if (field == "Amount") return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount.ToString("G29", CultureInfo.InvariantCulture) : text;
        if (field is "EndDate" or "Date") return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date) ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : text;
        if (field == "CreatedOn") return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var created)
            ? created.ToString("O", CultureInfo.InvariantCulture) : text;
        return text;
    }
    private static string[] Differences(JsonElement old, JsonElement current, string[] fields) => fields.Where(f => Canonical(old, f) != Canonical(current, f)).ToArray();
    private static string FieldHash(JsonElement row, string[] fields) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(fields.Select(f => new { Field = f, Value = Canonical(row, f) })))));

    public static async Task RunAsync(string capturePath, string archivePath, string outputPath)
    {
        var capture = Path.GetFullPath(capturePath); var archive = Path.GetFullPath(archivePath); var output = Path.GetFullPath(outputPath);
        var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        if (!capture.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase) || !output.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output) || File.Exists(output)) throw new InvalidOperationException("Karar karşılaştırması yalnız yeni .tools klasörüne yazılır.");
        VerifyPackage(capture);
        var snapshot = Path.Combine(archive, "tools/Collections.Snapshot/snapshots/payment-review-20260926-225543248");
        var manifestPath = Path.Combine(snapshot, "manifest.json");
        if (HashFile(manifestPath) != ArchivedManifestHash) throw new InvalidOperationException("Eski MGS kesit manifesti beklenen kanıt değil.");
        foreach (var f in Document(manifestPath).GetProperty("files").EnumerateArray())
        {
            var file = Text(f, "FileName");
            if (Path.GetFileName(file) != file || HashFile(Path.Combine(snapshot, file)) != Text(f, "Sha256"))
                throw new InvalidOperationException("Eski MGS kaynak dosyası hash'i uyuşmuyor.");
        }
        var decisionFile = Path.Combine(archive, ".tools/items567-decisions.json");
        if (HashFile(decisionFile) != ArchivedDecisionHash) throw new InvalidOperationException("Arşiv karar dosyası değişmiş.");
        var decisions = Document(decisionFile);
        if (!string.Equals(Text(decisions.GetProperty("summary"), "hash"), WorkbookHash, StringComparison.OrdinalIgnoreCase)
            || decisions.GetProperty("decisions").GetArrayLength() != 794 || decisions.GetProperty("rows").GetArrayLength() != 807)
            throw new InvalidOperationException("Müşteri karar kaynağı/sayısı uyuşmuyor.");
        var appliedPlan = Document(Path.Combine(archive, "tools/Collections.Import/snapshots/item-five-transfer-20260926-171434442/plan.json"));
        if (Text(appliedPlan, "Hash") != "BE3417A3EB7C1CD6B1FA3A8002A3EDEE37361093E7E2E63A4EBB32590C738173"
            || appliedPlan.GetProperty("Contracts").GetArrayLength() != 3 || appliedPlan.GetProperty("Rates").GetArrayLength() != 13)
            throw new InvalidOperationException("Önceki üç kayıt onayının plan kanıtı uyuşmuyor.");

        Directory.CreateDirectory(output);
        var oldCustomers = Rows(Path.Combine(snapshot, "customers.ndjson"), "CustomerID");
        var oldContracts = Rows(Path.Combine(snapshot, "contracts.ndjson"), "ContractID");
        var oldHistories = Rows(Path.Combine(snapshot, "contract-history.ndjson"), "ContractHistoryID");
        var newCustomers = Rows(Path.Combine(capture, "customers.ndjson"), "CustomerID");
        var newContracts = Rows(Path.Combine(capture, "contracts.ndjson"), "ContractID");
        var newHistories = Rows(Path.Combine(capture, "histories.ndjson"), "ContractHistoryID");
        var oldSets = oldHistories.Values.ToLookup(x => Id(x, "ContractID"));
        var newSets = newHistories.Values.ToLookup(x => Id(x, "ContractID"));
        var overrides = new List<object>(); var replay = new List<object>();
        var gates = new Dictionary<string, HashSet<string>>();
        void Gate(string contract, string code)
        {
            if (!gates.TryGetValue(contract, out var list)) gates[contract] = list = [];
            list.Add(code);
        }
        var approvalResults = new Dictionary<string, string>();
        foreach (var (historyId, contractId) in ApprovedHistoryIds)
        {
            var reasons = new List<string>();
            if (!oldContracts.TryGetValue(contractId, out var oldContract) || !newContracts.TryGetValue(contractId, out var currentContract)) reasons.Add("APPROVED_CONTRACT_MISSING");
            else
            {
                reasons.AddRange(Differences(oldContract, currentContract, ContractFields).Select(x => "CONTRACT_CHANGED:" + x));
                var customer = Id(currentContract, "CustomerID");
                if (!oldCustomers.TryGetValue(customer, out var oc) || !newCustomers.TryGetValue(customer, out var nc)) reasons.Add("APPROVED_CUSTOMER_MISSING");
                else reasons.AddRange(Differences(oc, nc, CustomerFields).Select(x => "CUSTOMER_CHANGED:" + x));
            }
            var before = oldSets[contractId].ToDictionary(x => Id(x, "ContractHistoryID"));
            var after = newSets[contractId].ToDictionary(x => Id(x, "ContractHistoryID"));
            if (!before.Keys.ToHashSet().SetEquals(after.Keys)) reasons.Add("APPROVED_HISTORY_SET_CHANGED");
            foreach (var (key, old) in before)
                if (after.TryGetValue(key, out var current)) reasons.AddRange(Differences(old, current, HistoryFields).Select(f => $"HISTORY_CHANGED:{key}:{f}"));
            if (!newHistories.TryGetValue(historyId, out var rate) || rate.GetProperty("ProcessType").ValueKind != JsonValueKind.String
                || Text(rate, "ProcessType") != "null" || Id(rate, "ContractID") != contractId) reasons.Add("APPROVED_LITERAL_NULL_CHANGED");
            approvalResults[historyId] = reasons.Count == 0 ? "APPROVED_UNCHANGED" : "HOLD_SOURCE_CHANGED";
            if (reasons.Count == 0)
                overrides.Add(new
                {
                    HistoryId = historyId,
                    ContractId = contractId,
                    BillingBehavior = "Billable",
                    WorkbookHash,
                    OldFinancialHash = FieldHash(oldHistories[historyId], HistoryFields),
                    CurrentFinancialHash = FieldHash(newHistories[historyId], HistoryFields),
                    Status = "APPROVED_UNCHANGED",
                    ContractHistoryCount = after.Count
                });
            else Gate(contractId, "PREVIOUS_APPROVAL_SOURCE_CHANGED");
            replay.Add(new { EntityKey = "ApprovedHistory:" + historyId, ContractId = contractId, Status = approvalResults[historyId], Reasons = reasons });
        }
        var rowNotes = decisions.GetProperty("rows").EnumerateArray().ToLookup(x => Text(x, "entityKey"));
        var decisionCounts = new Dictionary<string, int>();
        foreach (var decision in decisions.GetProperty("decisions").EnumerateArray())
        {
            var entity = Text(decision, "entity"); var id = Text(decision, "id"); var contract = Text(decision, "contractId");
            var key = Text(decision, "entityKey"); var previous = Text(decision, "decision");
            var isHistory = entity == "ContractHistory";
            var baseline = isHistory ? oldHistories : oldContracts; var current = isHistory ? newHistories : newContracts;
            var changes = baseline.TryGetValue(id, out var old) && current.TryGetValue(id, out var now)
                ? Differences(old, now, isHistory ? HistoryFields : ContractFields) : ["SOURCE_OR_BASELINE_MISSING"];
            var historicalDrift = rowNotes[key].Any(r => r.GetProperty("liveMismatch").GetArrayLength() > 0 || r.GetProperty("workbookMismatch").GetArrayLength() > 0);
            var status = previous switch
            {
                "PRESERVE_ITEM_FOUR_EXCLUSION" or "PRESERVE_PREVIOUS_EXCLUSION" => "PRESERVE_PREVIOUS_EXCLUSION",
                "HOLD_CONFLICT" => "PRESERVE_CONFLICT_HOLD",
                "HOLD_UNANSWERED" => "PRESERVE_UNANSWERED_HOLD",
                "EXCLUDE_CANDIDATE" => "PRESERVE_UNRESOLVED_EXCLUSION_REVIEW",
                "KEEP_WITH_VALIDATION" when ApprovedHistoryIds.ContainsKey(id) && isHistory => approvalResults[id],
                "KEEP_WITH_VALIDATION" when changes.Length > 0 || historicalDrift => "HOLD_DECISION_SOURCE_DRIFT",
                "KEEP_WITH_VALIDATION" => "KEEP_SUBJECT_TO_TECHNICAL_VALIDATION",
                _ => "HOLD_UNRECOGNIZED_DECISION"
            };
            if (status is not ("APPROVED_UNCHANGED" or "KEEP_SUBJECT_TO_TECHNICAL_VALIDATION")) Gate(contract, status);
            decisionCounts[status] = decisionCounts.GetValueOrDefault(status) + 1;
            replay.Add(new
            {
                EntityKey = key,
                ContractId = contract,
                PreviousDecision = previous,
                Status = status,
                ChangedFields = changes,
                HistoricalWorkbookOrSourceDrift = historicalDrift,
                Notes = rowNotes[key].Select(r => new { Sheet = Text(r, "sheet"), Row = Text(r, "excelRow"), Action = Text(r, "action"), Note = Text(r, "note") })
            });
        }
        await Save(output, "decision-replay.json", replay);
        await Save(output, "billing-overrides.json", overrides);
        await Save(output, "contract-decision-gates.json", gates.OrderBy(x => x.Key).Select(x => new { SourceContractId = x.Key, Issues = x.Value.Order().ToArray() }));

        var deltas = new List<object>();
        async Task Delta(string entity, Dictionary<string, JsonElement> old, Dictionary<string, JsonElement> current, string[] fields)
        {
            var added = current.Keys.Except(old.Keys).Order().ToArray(); var missing = old.Keys.Except(current.Keys).Order().ToArray();
            var changed = old.Keys.Intersect(current.Keys).Select(id => new { SourceId = id, Fields = Differences(old[id], current[id], fields) }).Where(x => x.Fields.Length > 0).ToArray();
            await Save(output, entity + "-delta.json", new { Added = added, Missing = missing, Changed = changed });
            deltas.Add(new { Entity = entity, Baseline = old.Count, Current = current.Count, Added = added.Length, Missing = missing.Length, Changed = changed.Length });
        }
        await Delta("customer", oldCustomers, newCustomers, CustomerFields);
        await Delta("contract", oldContracts, newContracts, ContractFields);
        await Delta("history", oldHistories, newHistories, HistoryFields);
        await Delta("payment", Rows(Path.Combine(snapshot, "payments.ndjson"), "PaymentID"), Rows(Path.Combine(capture, "payments.ndjson"), "PaymentID"), PaymentFields);
        await Save(output, "summary.json", new
        {
            Status = "DECISION_REPLAY_PREPARATION_NOT_APPLY",
            WorkbookHash,
            CapturePackageHash = HashFile(Path.Combine(capture, "package-hashes.json")),
            ArchivedDecisionHash,
            ArchivedManifestHash,
            ApprovedOverrides = overrides.Count,
            ApprovalResults = approvalResults,
            DecisionResults = decisionCounts,
            GatedContracts = gates.Count,
            Deltas = deltas,
            Note = "Eski müşteri kararları korunur; cevaplı fakat çözümlenmemiş teknik/koşullu dışlama otomatik uygulanmaz. Üç net kayıtta yalnız aynı müşteri+sözleşme+tam tarihçe kümesi değişmemişse literal-null davranış onayı yeniden bağlanır."
        });
        await Save(output, "package-hashes.json", Directory.GetFiles(output).Order().Select(file => new { File = Path.GetFileName(file), Sha256 = HashFile(file) }).ToArray());
        Console.WriteLine($"794 karar karşılaştırıldı; {overrides.Count} değişmemiş açık onay yeniden bağlandı. DB bağlantısı/yazması yok.");
    }

    internal static void VerifyPackage(string root)
    {
        foreach (var item in Document(Path.Combine(root, "package-hashes.json")).EnumerateArray())
        {
            var file = Text(item, "File");
            if (Path.GetFileName(file) != file || HashFile(Path.Combine(root, file)) != Text(item, "Sha256"))
                throw new InvalidOperationException("Paket kanıtı hash'i doğrulanamadı.");
        }
    }
}
