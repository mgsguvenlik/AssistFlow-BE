using System.Security.Cryptography;
using System.Text.Json;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;

internal static class CollectionInvoicePreview
{
    // Default is read-only; transfer must supply a hash from the same recalculated plan.
    public static async Task RunAsync(string settingsPath, string outputPath, string? expectedHash = null)
    {
        using var settings = JsonDocument.Parse(await File.ReadAllTextAsync(settingsPath),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        var connection = new SqlConnectionStringBuilder(settings.RootElement.GetProperty("AppSettings").GetProperty("MSSQLConnectionString").GetString());
        if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
            throw new InvalidOperationException("Önizleme yalnız AssistFlowTest hedefi için çalışır.");
        await using var db = new AppDataContext(new DbContextOptionsBuilder<AppDataContext>()
            .UseSqlServer(connection.ConnectionString, o => o.CommandTimeout(120)).Options);
        if (db.Database.HasPendingModelChanges() || (await db.Database.GetPendingMigrationsAsync()).Any())
            throw new InvalidOperationException("Fatura modeli ve hedef şeması hazır değil; aktarım/önizleme durduruldu.");
        // Consistent source/target snapshot through a short read-only serializable transaction.
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        var candidates = await (from m in db.Set<CollectionMigrationMap>().AsNoTracking()
                                join s in db.Set<CollectionMigrationSourceRow>().AsNoTracking()
                                    on new { BatchId = m.FirstBatchId, m.EntityCode, m.SourceId }
                                    equals new { s.BatchId, s.EntityCode, s.SourceId }
                                join stage in db.Set<CollectionMigrationContractStage>().AsNoTracking() on s.Id equals stage.SourceRowId
                                join c in db.Set<CollectionContract>().AsNoTracking() on m.TargetContractId equals (long?)c.Id
                                where m.SourceSystem == "MGS" && m.EntityCode == "Contract" && !c.IsDeleted && stage.TargetCustomerId == c.CustomerId
                                select new { stage.SourceCustomerId, c.CustomerId }).ToListAsync();
        var pairs = candidates.Where(x => long.TryParse(x.SourceCustomerId, out _))
            .Select(x => (LegacyId: long.Parse(x.SourceCustomerId!), x.CustomerId)).ToList();
        var parents = await db.Set<CollectionGroupParent>().AsNoTracking().ToListAsync();
        pairs.AddRange(parents.Select(x => (x.LegacyCustomerId, x.CustomerId)));
        var mapping = pairs.GroupBy(x => x.LegacyId).ToDictionary(g => g.Key, g => g.Select(x => x.CustomerId).Distinct().Order().ToArray());
        var eligible = (await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Select(x => x.Id).ToListAsync()).ToHashSet();
        var currencies = await db.Set<CurrencyType>().AsNoTracking().Select(x => new { x.Id, x.Code }).ToListAsync();
        var invoices = await db.Database.SqlQueryRaw<InvoiceSource>("""
            SELECT CAST(i.InvoiceFollowID AS bigint) Id, CAST(i.CustomerID AS bigint) CustomerId,
                i.No AS Number, i.Date, i.Amount, CAST(i.CurrencyID AS bigint) CurrencyId,
                c.Name AS CurrencyName, i.Type, i.ProjectCode, i.Comment,
                i.CreatedBy, i.ModifiedBy, i.CreatedOn, i.ModifiedOn
            FROM MGS.Core.InvoiceFollow i LEFT JOIN MGS.Definition.Currency c ON c.CurrencyID=i.CurrencyID
            ORDER BY i.InvoiceFollowID
            """).ToListAsync();
        var payments = await db.Database.SqlQueryRaw<PaymentSource>("""
            SELECT CAST(InvoiceFollowPaymentID AS bigint) Id, CAST(CustomerID AS bigint) CustomerId,
                CAST(InvoiceFollowID AS bigint) InvoiceId, Date, Amount, Description,
                CreatedBy, ModifiedBy, CreatedOn, ModifiedOn
            FROM MGS.Core.InvoiceFollowPayment ORDER BY InvoiceFollowPaymentID
            """).ToListAsync();
        var existingInvoices = await db.Set<CollectionInvoice>().AsNoTracking().Where(x => x.LegacyInvoiceFollowId != null)
            .ToDictionaryAsync(x => x.LegacyInvoiceFollowId!.Value);
        var existingPayments = await db.Set<CollectionInvoicePayment>().AsNoTracking().Where(x => x.LegacyInvoiceFollowPaymentId != null)
            .ToDictionaryAsync(x => x.LegacyInvoiceFollowPaymentId!.Value);
        var deletedInvoices = (await db.Set<CollectionInvoiceOperation>().AsNoTracking()
            .Where(x => x.Kind == "InvoiceDelete" && x.LegacyInvoiceId != null).Select(x => x.LegacyInvoiceId!.Value).ToListAsync()).ToHashSet();
        var deletedPayments = (await db.Set<CollectionInvoiceOperation>().AsNoTracking()
            .Where(x => x.Kind == "PaymentDelete" && x.LegacyPaymentId != null).Select(x => x.LegacyPaymentId!.Value).ToListAsync()).ToHashSet();
        var invoiceRows = invoices.Select(s =>
        {
            var targets = mapping.GetValueOrDefault(s.CustomerId) ?? [];
            long? target = targets.Length == 1 ? targets[0] : null;
            var currencyTargets = currencies.Where(c => NormalizeCurrency(c.Code) == NormalizeCurrency(s.CurrencyName) && !string.IsNullOrWhiteSpace(s.CurrencyName)).ToArray();
            long? currency = currencyTargets.Length == 1 ? currencyTargets[0].Id : null;
            var reasons = new List<string>();
            if (targets.Length == 0) reasons.Add("NoRetainedCustomerMapping");
            else if (targets.Length != 1) reasons.Add("AmbiguousCustomerMapping");
            else if (!eligible.Contains(target!.Value)) reasons.Add("OutsideCollectionScope");
            if (currency is null) reasons.Add("CurrencyMappingReview");
            var targetType = s.Type?.Trim() switch { "Bireysel" or "B" => "B", "Kurumsal" or "K" => "K", _ => null };
            if (targetType is null) reasons.Add("InvoiceTypeReview");
            if (s.Date is null) reasons.Add("MissingDate");
            if (!ValidAmount(s.Amount)) reasons.Add("AmountReview");
            if (s.Number?.Length > 50 || s.ProjectCode?.Length > 100 || s.CreatedBy?.Length > 100 || s.ModifiedBy?.Length > 100) reasons.Add("TextLengthReview");
            var hash = Hash(s);
            var status = reasons.Count == 0 ? "Ready" : "Review";
            if (deletedInvoices.Contains(s.Id)) { status = "Review"; reasons.Add("ManuallyDeletedInvoice"); }
            long? targetInvoice = null;
            if (existingInvoices.TryGetValue(s.Id, out var old))
            {
                targetInvoice = old.Id;
                status = old.CustomerId == target && old.CurrencyTypeId == currency && old.SourceHash is not null && Convert.ToHexString(old.SourceHash) == hash
                    ? "AlreadyApplied" : "ExistingSourceOrMappingChanged";
            }
            return new InvoiceDecision(s, target, currency, targetInvoice, targetType, status, reasons.ToArray(), hash);
        }).ToArray();
        var invoiceById = invoiceRows.ToDictionary(x => x.Source.Id);
        var paymentRows = payments.Select(s =>
        {
            var parent = s.InvoiceId.HasValue ? invoiceById.GetValueOrDefault(s.InvoiceId.Value) : null;
            var reasons = new List<string>();
            if (parent is null) reasons.Add("MissingInvoice");
            else
            {
                if (parent.Status is not ("Ready" or "AlreadyApplied")) reasons.Add("InvoiceNotReady");
                if (s.CustomerId != parent.Source.CustomerId) reasons.Add("InvoiceCustomerMismatch");
            }
            if (s.Date is null) reasons.Add("MissingDate");
            if (!ValidAmount(s.Amount)) reasons.Add("AmountReview");
            if (s.Description?.Length > 1000 || s.CreatedBy?.Length > 100 || s.ModifiedBy?.Length > 100) reasons.Add("TextLengthReview");
            var hash = Hash(s);
            var status = reasons.Count == 0 ? "Ready" : "Review";
            if (deletedPayments.Contains(s.Id)) { status = "Review"; reasons.Add("ManuallyDeletedPayment"); }
            if (existingPayments.TryGetValue(s.Id, out var old))
                status = old.InvoiceId == parent?.TargetInvoiceId && old.SourceHash is not null && Convert.ToHexString(old.SourceHash) == hash
                    ? "AlreadyApplied" : "ExistingSourceOrMappingChanged";
            return new PaymentDecision(s, parent?.TargetCustomerId, parent?.TargetCurrencyTypeId, status, reasons.ToArray(), hash);
        }).ToArray();
        // A partial invoice payment history would show an incorrect remaining balance.
        var blocked = paymentRows.Where(x => x.Status is not ("Ready" or "AlreadyApplied") && x.Source.InvoiceId.HasValue)
            .Select(x => x.Source.InvoiceId!.Value).ToHashSet();
        invoiceRows = invoiceRows.Select(x => x.Status == "Ready" && blocked.Contains(x.Source.Id)
            ? x with { Status = "Review", Reasons = [.. x.Reasons, "PaymentHistoryReview"] } : x).ToArray();
        var blockedInvoices = invoiceRows.Where(x => x.Status is not ("Ready" or "AlreadyApplied")).Select(x => x.Source.Id).ToHashSet();
        paymentRows = paymentRows.Select(x => x.Status == "Ready" && x.Source.InvoiceId.HasValue && blockedInvoices.Contains(x.Source.InvoiceId.Value)
            ? x with { Status = "Review", Reasons = [.. x.Reasons, "InvoiceNotReady"] } : x).ToArray();
        var summary = new
        {
            InvoiceCount = invoiceRows.Length, PaymentCount = paymentRows.Length,
            InvoiceStatus = invoiceRows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count()),
            InvoiceReasons = invoiceRows.SelectMany(x => x.Reasons).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()),
            PaymentStatus = paymentRows.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Count()),
            PaymentReasons = paymentRows.SelectMany(x => x.Reasons).GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()),
            ReadyTotals = invoiceRows.Where(x => x.Status == "Ready").GroupBy(x => x.TargetCurrencyTypeId).Select(g => new
            {
                CurrencyTypeId = g.Key, Invoices = g.Count(), Amount = g.Sum(x => x.Source.Amount!.Value),
                PaymentAmount = paymentRows.Where(p => p.Status == "Ready" && p.TargetCurrencyTypeId == g.Key).Sum(p => p.Source.Amount!.Value)
            }).ToArray()
        };
        var planHash = Hash(new { invoiceRows, paymentRows });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new { PlanHash = planHash, Summary = summary, Invoices = invoiceRows, Payments = paymentRows }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { PlanHash = planHash, Summary = summary }));
        if (expectedHash is null)
        {
            await tx.RollbackAsync();
            Console.WriteLine("Salt okunur önizleme tamamlandı; veri aktarılmadı.");
            return;
        }
        if (!string.Equals(planHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Önizleme değişti; aktarım yapılmadı.");
        if (invoiceRows.Any(x => x.Status == "ExistingSourceOrMappingChanged") || paymentRows.Any(x => x.Status == "ExistingSourceOrMappingChanged"))
            throw new InvalidOperationException("Önceki aktarımın kaynağı veya eşlemesi değişti; aktarım durduruldu.");
        var importedAt = DateTimeOffset.UtcNow;
        var newInvoices = new Dictionary<long, CollectionInvoice>();
        foreach (var row in invoiceRows.Where(x => x.Status == "Ready"))
        {
            var s = row.Source;
            var entity = new CollectionInvoice
            {
                CustomerId = row.TargetCustomerId!.Value, CurrencyTypeId = row.TargetCurrencyTypeId!.Value,
                Type = row.TargetType!, Number = s.Number, Date = s.Date!.Value, Amount = s.Amount!.Value,
                ProjectCode = s.ProjectCode, Comment = s.Comment, CreatedUser = 0, CreatedDate = importedAt,
                LegacyInvoiceFollowId = s.Id, LegacyCustomerId = s.CustomerId, LegacyCreatedBy = s.CreatedBy,
                LegacyModifiedBy = s.ModifiedBy, LegacyCreatedOn = s.CreatedOn, LegacyModifiedOn = s.ModifiedOn,
                SourceHash = Convert.FromHexString(row.SourceHash)
            };
            newInvoices.Add(s.Id, entity);
            db.Add(entity);
        }
        await db.SaveChangesAsync();
        foreach (var row in paymentRows.Where(x => x.Status == "Ready"))
        {
            var s = row.Source;
            var invoiceId = newInvoices.TryGetValue(s.InvoiceId!.Value, out var added) ? added.Id : existingInvoices[s.InvoiceId.Value].Id;
            db.Add(new CollectionInvoicePayment
            {
                InvoiceId = invoiceId, Date = s.Date!.Value, Amount = s.Amount!.Value, Description = s.Description,
                CreatedUser = 0, CreatedDate = importedAt, LegacyInvoiceFollowPaymentId = s.Id,
                LegacyCustomerId = s.CustomerId, LegacyCreatedBy = s.CreatedBy, LegacyModifiedBy = s.ModifiedBy,
                LegacyCreatedOn = s.CreatedOn, LegacyModifiedOn = s.ModifiedOn, SourceHash = Convert.FromHexString(row.SourceHash)
            });
        }
        var existingAffectedIds = paymentRows.Where(x => x.Status == "Ready" && !newInvoices.ContainsKey(x.Source.InvoiceId!.Value))
            .Select(x => existingInvoices[x.Source.InvoiceId!.Value].Id).Distinct().ToArray();
        // An appended legacy payment also changes the balance concurrency token.
        foreach (var invoice in await db.Set<CollectionInvoice>().Where(x => existingAffectedIds.Contains(x.Id)).ToListAsync())
            db.Entry(invoice).Property(x => x.Comment).IsModified = true;
        await db.SaveChangesAsync();
        var ids = newInvoices.Values.Select(x => x.Id).ToArray();
        var affectedIds = ids.Concat(existingAffectedIds).Distinct().ToArray();
        var actual = await db.Set<CollectionInvoice>().AsNoTracking().Where(x => affectedIds.Contains(x.Id))
            .Select(x => new { x.LegacyInvoiceFollowId, x.Amount, PaymentCount = x.Payments.Count,
                Paid = x.Payments.Sum(p => (decimal?)p.Amount) ?? 0 }).ToListAsync();
        foreach (var item in actual)
        {
            var sourceInvoice = invoiceById[item.LegacyInvoiceFollowId!.Value].Source;
            var sourcePayments = payments.Where(x => x.InvoiceId == sourceInvoice.Id).ToArray();
            if (item.Amount != sourceInvoice.Amount || item.PaymentCount != sourcePayments.Length || item.Paid != sourcePayments.Sum(x => x.Amount ?? 0))
                throw new InvalidOperationException("Fatura bazlı mutabakat başarısız; tüm aktarım geri alınacak.");
        }
        await tx.CommitAsync();
        await File.WriteAllTextAsync(outputPath + ".applied.json", JsonSerializer.Serialize(new
        { PlanHash = planHash, ImportedAt = importedAt, Invoices = ids.Length, Payments = paymentRows.Count(x => x.Status == "Ready"), ReconciledInvoices = actual.Count, Summary = summary }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Aktarıldı: {ids.Length} fatura, {paymentRows.Count(x => x.Status == "Ready")} ödeme. Fatura bazlı mutabakat başarılı.");
    }
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static bool ValidAmount(decimal? amount) => amount.HasValue && Math.Abs(amount.Value) < 10000000000000000m && decimal.Round(amount.Value, 2) == amount.Value;
    private static string NormalizeCurrency(string? value) => value?.Trim().ToUpperInvariant() switch { "TL" or "YTL" or "TRL" => "TRY", var code => code ?? "" };
    public class AuditSource
    {
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
    }
    public sealed class InvoiceSource : AuditSource
    {
        public long Id { get; set; }
        public long CustomerId { get; set; }
        public string? Number { get; set; }
        public DateOnly? Date { get; set; }
        public decimal? Amount { get; set; }
        public long? CurrencyId { get; set; }
        public string? CurrencyName { get; set; }
        public string? Type { get; set; }
        public string? ProjectCode { get; set; }
        public string? Comment { get; set; }
    }
    public sealed class PaymentSource : AuditSource
    {
        public long Id { get; set; }
        public long? CustomerId { get; set; }
        public long? InvoiceId { get; set; }
        public DateOnly? Date { get; set; }
        public decimal? Amount { get; set; }
        public string? Description { get; set; }
    }
    private sealed record InvoiceDecision(InvoiceSource Source, long? TargetCustomerId, long? TargetCurrencyTypeId, long? TargetInvoiceId, string? TargetType, string Status, string[] Reasons, string SourceHash);
    private sealed record PaymentDecision(PaymentSource Source, long? TargetCustomerId, long? TargetCurrencyTypeId, string Status, string[] Reasons, string SourceHash);
}
