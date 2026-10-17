using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Business.Interfaces;
using Business.Interfaces.Storage;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionBankLoadService(AppDataContext db, IFileStorage storage) : ICollectionBankLoadService
{
    public static string Key(string type, string number) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { type, number = number.Trim().ToUpperInvariant() })));
    private Task<bool> Actor(long actor, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && !x.IsDeleted, ct);
    private static bool Page(int page, int size) => page is >= 1 and <= 1000000 && size is >= 1 and <= 100;
    private static IQueryable<CollectionBankLoadItem> Items(IQueryable<CollectionBankLoad> source) => source.Select(x =>
        new CollectionBankLoadItem(x.Id, x.Type, x.FileName, x.Period, x.CreatedDate, x.Rows.Count,
            x.Rows.Count(r => r.Status == "Ready"), x.Rows.Count(r => r.Status == "Review"),
            x.Rows.Count(r => r.Status == "BankFailed"), x.Rows.Count(r => r.Status == "Duplicate"),
            x.Rows.Count(r => r.Status == "Imported"), x.RowVersion, null));
    public async Task<ResponseModel<PagedResult<CollectionBankLoadItem>>> ListAsync(int page, int size, CancellationToken ct)
    {
        if (!Page(page, size)) return ResponseModel<PagedResult<CollectionBankLoadItem>>.Fail("Geçersiz sayfa bilgisi.");
        var q = db.Set<CollectionBankLoad>().AsNoTracking();
        return ResponseModel<PagedResult<CollectionBankLoadItem>>.Success(new(
            await Items(q.OrderByDescending(x => x.Id).Skip((page - 1) * size).Take(size)).ToListAsync(ct), await q.CountAsync(ct), page, size));
    }
    public async Task<ResponseModel<CollectionBankLoadItem>> GetAsync(long id, CancellationToken ct)
    {
        var item = await Items(db.Set<CollectionBankLoad>().AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (item is null) return ResponseModel<CollectionBankLoadItem>.Fail("Yükleme bulunamadı.", StatusCode.NotFound);
        var key = await db.Set<CollectionBankLoad>().Where(x => x.Id == id).Select(x => x.StoredFileName).SingleAsync(ct);
        return ResponseModel<CollectionBankLoadItem>.Success(item with { FileUrl = storage.GetPublicUrl(key) });
    }
    public async Task<ResponseModel<PagedResult<CollectionBankLoadRowItem>>> RowsAsync(long id, int page, int size, string? status, CancellationToken ct)
    {
        if (!Page(page, size) || status is not (null or "Ready" or "Review" or "BankFailed" or "Duplicate" or "Imported"))
            return ResponseModel<PagedResult<CollectionBankLoadRowItem>>.Fail("Geçersiz satır filtresi.");
        var q = db.Set<CollectionBankLoadRow>().AsNoTracking().Where(x => x.LoadId == id && (status == null || x.Status == status));
        var rows = await q.OrderBy(x => x.RowNumber).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionBankLoadRowItem>>.Success(new(rows.Select(x => new CollectionBankLoadRowItem(
            x.Id, x.RowNumber, Read(x), x.ContractId, x.CurrencyTypeId, x.Status, x.Issue, x.PaymentId)).ToArray(), await q.CountAsync(ct), page, size));
    }
    private static CollectionBankFileRow Read(CollectionBankLoadRow row) => JsonSerializer.Deserialize<CollectionBankFileRow>(row.SourceJson)!;
    public async Task<ResponseModel<long>> UploadAsync(IFormFile file, string type, DateOnly period, long actor, CancellationToken ct)
    {
        if (file is null || file.Length is < 1 or > CollectionBankFileParser.MaximumBytes) return ResponseModel<long>.Fail("En fazla 10 MB dosya seçin.");
        if (!await Actor(actor, ct)) return ResponseModel<long>.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
        var name = Path.GetFileName(file.FileName);
        if (name.Length is < 1 or > 260) return ResponseModel<long>.Fail("Dosya adı geçersiz.");
        using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, ct);
        CollectionBankFilePreview parsed;
        try { parsed = CollectionBankFileParser.Parse(buffer.ToArray(), name, type, period); }
        catch (InvalidDataException ex) { return ResponseModel<long>.Fail(ex.Message); }
        type = parsed.Type;
        var existing = await db.Set<CollectionBankLoad>().AsNoTracking().SingleOrDefaultAsync(x => x.Type == type && x.FileHash == parsed.FileHash && x.Period == period, ct);
        if (existing is not null) return ResponseModel<long>.Success(existing.Id, "Dosya ve dönem zaten kayıtlı; mevcut önizleme açıldı.");
        // Never upload the original workbook. Serialize only the explicitly allowed payment fields.
        var clean = JsonSerializer.SerializeToUtf8Bytes(parsed);
        var stored = $"collection-bank-{type}-{parsed.FileHash.ToLowerInvariant()}-{period:yyyyMM}.json";
        try
        {
            if (!await storage.ExistsAsync(stored, ct))
            { using var stream = new MemoryStream(clean); await storage.UploadAsync(stored, stream, "application/json", ct); }
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            existing = await db.Set<CollectionBankLoad>().AsNoTracking().SingleOrDefaultAsync(x => x.Type == type && x.FileHash == parsed.FileHash && x.Period == period, ct);
            if (existing is not null) return ResponseModel<long>.Success(existing.Id, "Dosya zaten kayıtlı.");
            var batch = new CollectionBankLoad { Type = type, FileHash = parsed.FileHash, FileName = name, StoredFileName = stored,
                Period = period, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow, ReviewedAt = DateTimeOffset.UtcNow };
            foreach (var row in parsed.Rows) batch.Rows.Add(new() { RowNumber = row.RowNumber, SourceJson = JsonSerializer.Serialize(row), TransactionKey = Key(type, row.TransactionNumber) });
            await Validate(batch, batch.Rows.ToArray(), ct);
            db.Add(batch); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ResponseModel<long>.Success(batch.Id, "Dosya önizlemesi hazır. Henüz ödeme oluşturulmadı.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return ResponseModel<long>.Fail("Yükleme sonucu doğrulanamadı. Aynı dosya ve dönemle tekrar deneyin.", (StatusCode)503); }
        finally { db.ChangeTracker.Clear(); }
    }

    private async Task Validate(CollectionBankLoad batch, CollectionBankLoadRow[] rows, CancellationToken ct)
    {
        var pending = rows.Where(x => x.Status != "Imported").ToArray();
        var contracts = await CandidateContracts(batch.Type, pending.Select(Read).ToArray(), ct);
        var ids = contracts.Select(x => x.Id).ToArray();
        var scope = await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>(), db.Customers)
            .Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var until = batch.Period.AddMonths(1);
        var rates = await db.Set<CollectionContractRatePeriod>().AsNoTracking().Where(x => ids.Contains(x.ContractId) && !x.IsDeleted &&
            x.EffectiveFrom < until && (x.EffectiveToExclusive == null || x.EffectiveToExclusive > batch.Period) && x.BillingBehavior == CollectionBillingBehavior.Billable)
            .Select(x => new { x.ContractId, x.CurrencyTypeId }).Distinct().ToListAsync(ct);
        var currencies = await db.Set<CurrencyType>().AsNoTracking().Select(x => new { x.Id, x.Code }).ToListAsync(ct);
        var keys = pending.Select(x => x.TransactionKey).ToArray();
        var used = (await db.Set<CollectionBankTransaction>().AsNoTracking().Where(x => keys.Contains(x.Key)).Select(x => x.Key).ToListAsync(ct)).ToHashSet();
        var baseline = await db.Set<CollectionBankBaseline>().AnyAsync(x => x.Id == 1, ct);
        var duplicateKeys = batch.Rows.GroupBy(x => x.TransactionKey).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        foreach (var row in pending)
        {
            var source = Read(row); row.ContractId = null; row.CurrencyTypeId = null;
            row.Status = source.Status == "Candidate" ? "Ready" : source.Status;
            var issues = source.Issues.ToList();
            if (!baseline) issues.Add("Geçmiş banka işlem kontrol listesi kurulmadı; ödeme aktarımı kapalı.");
            if (used.Contains(row.TransactionKey) || duplicateKeys.Contains(row.TransactionKey))
            { row.Status = "Duplicate"; issues.Add("Banka işlemi geçmişte veya bu dosyada mevcut; tekrar aktarılmaz."); }
            var match = MatchingContracts(batch.Type, source, contracts).ToArray();
            var choice = source.SelectedContractId.HasValue ? match.SingleOrDefault(x => x.Id == source.SelectedContractId) :
                match.Length == 1 && (source.SubscriberSuffix is null || Name(source.CustomerName) == Name(match[0].CustomerName)) ? match[0] : null;
            if (choice is null) issues.Add(match.Length > 0 ? "Müşteri/sözleşme eşleşmesini kontrol edip sözleşme seçin." : "Müşteri bilgileriyle eşleşen sözleşme bulunamadı.");
            else if (!scope.Contains(choice.Id)) issues.Add("Sözleşme tahsilat kapsamı dışında.");
            else row.ContractId = choice.Id;
            var currency = currencies.Where(x => NormalizeCurrency(x.Code) == source.CurrencyCode).ToArray();
            if (currency.Length != 1) issues.Add("Para birimi eşlemesi tekil değil veya bulunamadı.");
            else row.CurrencyTypeId = currency[0].Id;
            if (row.ContractId != null && row.CurrencyTypeId != null && !rates.Any(x => x.ContractId == row.ContractId && x.CurrencyTypeId == row.CurrencyTypeId))
                issues.Add("Seçilen dönem ve para biriminde ücretli tarife bulunamadı.");
            if (row.Status == "Ready" && issues.Count > 0) row.Status = "Review";
            row.Issue = issues.Count == 0 ? null : string.Join(" ", issues);
        }
    }
    private static string Name(string? value) => string.Concat((value ?? "").ToUpper(CultureInfo.GetCultureInfo("tr-TR")).Where(char.IsLetterOrDigit));
    private static bool Reference(string? value, string reference) => !string.IsNullOrWhiteSpace(reference) && string.Equals(value?.Trim(), reference.Trim(), StringComparison.OrdinalIgnoreCase);
    private async Task<List<CollectionBankContractOption>> CandidateContracts(string type, CollectionBankFileRow[] rows, CancellationToken ct)
    {
        var refs = rows.Select(x => x.ContractReference).Distinct().ToArray();
        var suffixes = rows.Where(x => x.SubscriberSuffix is not null).Select(x => x.SubscriberSuffix!).Distinct().ToArray();
        return await db.Set<CollectionContract>().AsNoTracking().Where(x => !x.IsDeleted && !x.Customer.IsDeleted &&
            (type == "GTS" ? refs.Contains(x.GtsNo!) : refs.Contains(x.IvrNo!)) ||
            !x.IsDeleted && !x.Customer.IsDeleted && suffixes.Length > 0 &&
            (refs.Contains(x.IvrNo!) || refs.Contains(x.GtsNo!) || x.Customer.SubscriberCode != null && x.Customer.SubscriberCode.Length >= 4 &&
                suffixes.Contains(x.Customer.SubscriberCode.Substring(x.Customer.SubscriberCode.Length - 4))))
            .OrderBy(x => x.Id).Select(x => new CollectionBankContractOption(x.Id, x.Customer.SubscriberCode,
                x.Customer.SubscriberCompany, x.ServiceType.Name, x.StartDate, x.GtsNo, x.IvrNo)).ToListAsync(ct);
    }
    private static IEnumerable<CollectionBankContractOption> MatchingContracts(string type, CollectionBankFileRow source, IEnumerable<CollectionBankContractOption> contracts)
    {
        if (source.SubscriberSuffix is null)
            return contracts.Where(x => Reference(type == "GTS" ? x.GtsNo : x.IvrNo, source.ContractReference));
        var name = Name(source.CustomerName);
        // Prefix names are suggestions only, never automatic matches. Suffix alone never authorizes a payment.
        return contracts.Where(x => x.SubscriberCode?.Trim().EndsWith(source.SubscriberSuffix, StringComparison.Ordinal) == true &&
            name.Length > 0 && (Name(x.CustomerName) == name || name.Length >= 6 && Name(x.CustomerName).StartsWith(name, StringComparison.Ordinal)));
    }

    public async Task<ResponseModel<PagedResult<CollectionBankContractOption>>> ContractsAsync(long id, long rowId, int page, int size, CancellationToken ct)
    {
        if (!Page(page, size)) return ResponseModel<PagedResult<CollectionBankContractOption>>.Fail("Geçersiz sayfa bilgisi.");
        var row = await db.Set<CollectionBankLoadRow>().AsNoTracking().Include(x => x.Load).SingleOrDefaultAsync(x => x.Id == rowId && x.LoadId == id, ct);
        if (row is null) return ResponseModel<PagedResult<CollectionBankContractOption>>.Fail("Yükleme satırı bulunamadı.", StatusCode.NotFound);
        var source = Read(row);
        var contracts = MatchingContracts(row.Load.Type, source, await CandidateContracts(row.Load.Type, [source], ct)).ToArray();
        var ids = contracts.Select(x => x.Id).ToArray();
        var scope = await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>(), db.Customers).Where(x => ids.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        var available = contracts.Where(x => scope.Contains(x.Id)).ToArray();
        return ResponseModel<PagedResult<CollectionBankContractOption>>.Success(new(available.Skip((page - 1) * size).Take(size).ToArray(), available.Length, page, size));
    }

    public async Task<ResponseModel<int>> SelectContractAsync(long id, long rowId, CollectionBankLoadSelect command, long actor, CancellationToken ct)
    {
        if (command.ContractId <= 0 || !await Actor(actor, ct)) return ResponseModel<int>.Fail("Geçerli kullanıcı ve sözleşme seçilmelidir.");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var batch = await db.Set<CollectionBankLoad>().Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id, ct);
            var row = batch?.Rows.SingleOrDefault(x => x.Id == rowId);
            if (batch is null || row is null) return ResponseModel<int>.Fail("Yükleme satırı bulunamadı.", StatusCode.NotFound);
            if (Convert.ToBase64String(batch.RowVersion) != command.RowVersion) return ResponseModel<int>.Fail("Önizleme değişmiş; güncel bilgileri yükleyin.", StatusCode.Conflict);
            var source = Read(row);
            if (row.PaymentId.HasValue || row.Status is "Imported" or "Duplicate" or "BankFailed" || source.Status != "Candidate")
                return ResponseModel<int>.Fail("Aktarılmış, mükerrer veya geçersiz banka satırına sözleşme seçilemez.", StatusCode.Conflict);
            var candidates = await CandidateContracts(batch.Type, [source], ct);
            if (!MatchingContracts(batch.Type, source, candidates).Any(x => x.Id == command.ContractId))
                return ResponseModel<int>.Fail("Seçilen sözleşme satırın müşteri bilgileriyle eşleşmiyor.");
            var original = row.SourceJson;
            row.SourceJson = JsonSerializer.Serialize(source with { SelectedContractId = command.ContractId,
                Selections = [.. source.Selections ?? [], new(command.ContractId, actor, DateTimeOffset.UtcNow)] });
            await Validate(batch, [row], ct);
            if (row.ContractId != command.ContractId || row.Status != "Ready")
            {
                row.SourceJson = original;
                return ResponseModel<int>.Fail(row.Issue ?? "Seçilen sözleşme ödeme aktarımına uygun değil.");
            }
            batch.ReviewedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ResponseModel<int>.Success(1, "Sözleşme seçimi kaydedildi. Henüz ödeme oluşturulmadı.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return ResponseModel<int>.Fail("Sözleşme seçimi doğrulanamadı. Önizlemeyi yenileyin.", (StatusCode)503); }
        finally { db.ChangeTracker.Clear(); }
    }
    private static string NormalizeCurrency(string code) => code.Trim().ToUpperInvariant() is "TL" or "YTL" or "TRL" ? "TRY" : code.Trim().ToUpperInvariant();
    private static string Snapshot(CollectionBankLoadRow row) => JsonSerializer.Serialize(new { row.Status, row.Issue, row.ContractId, row.CurrencyTypeId });

    public async Task<ResponseModel<int>> ProcessAsync(long id, string version, bool apply, long actor, CancellationToken ct)
    {
        if (!await Actor(actor, ct)) return ResponseModel<int>.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
        var completed = 0;
        try
        {
            CollectionBankLoad batch;
            CollectionBankLoadRow[] ready;
            await using (var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct))
            {
                batch = (await db.Set<CollectionBankLoad>().Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id, ct))!;
                if (batch is null) return ResponseModel<int>.Fail("Yükleme bulunamadı.", StatusCode.NotFound);
                if (Convert.ToBase64String(batch.RowVersion) != version) return ResponseModel<int>.Fail("Önizleme değişmiş; güncel bilgileri yükleyin.", StatusCode.Conflict);
                var before = batch.Rows.ToDictionary(x => x.Id, Snapshot);
                await Validate(batch, batch.Rows.ToArray(), ct);
                var changed = batch.Rows.Any(x => before[x.Id] != Snapshot(x));
                if (!apply || changed)
                {
                    batch.ReviewedAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                    return changed && apply ? ResponseModel<int>.Fail("Eşleşmeler değişti. Güncel önizlemeyi kontrol edip yeniden onaylayın.", StatusCode.Conflict)
                        : ResponseModel<int>.Success(0, "Önizleme güncellendi.");
                }
                ready = batch.Rows.Where(x => x.Status == "Ready").OrderBy(x => x.RowNumber).Take(100).ToArray();
                await tx.CommitAsync(ct);
            }
            db.ChangeTracker.Clear();
            foreach (var row in ready)
            {
                var source = Read(row);
                var command = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null, row.ContractId,
                    batch.Period, DateOnly.FromDateTime(source.PaymentDate!.Value), source.Amount, row.CurrencyTypeId,
                    $"{batch.Type} Toplu Yükleme / {source.TransactionNumber}", false);
                // Global bank key plus durable ledger prevents a second payment across files, periods and actors.
                var request = new Guid(Convert.FromHexString(row.TransactionKey)[..16]);
                var result = await new CollectionPaymentTransaction(db).ExecuteAsync(request, actor, command, ct, async token =>
                {
                    if (!await db.Set<CollectionBankLoad>().AnyAsync(x => x.Id == id && x.RowVersion == batch.RowVersion, token))
                        return "Önizleme eşzamanlı değişti; yeniden yükleyin.";
                    var current = await db.Set<CollectionBankLoadRow>().AsNoTracking().SingleAsync(x => x.Id == row.Id, token);
                    await Validate(batch, [current], token);
                    if (current.Status != "Ready" || Snapshot(current) != Snapshot(row)) return "Satır eşleşmesi veya banka işlem durumu değişti; önizlemeyi yenileyin.";
                    return null;
                }, async (paymentId, token) =>
                {
                    // Parameterized, transaction-bound insert avoids residual tracked entities on a retry.
                    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO collection.BankTransaction ([Key], [Source], PaymentId, LoadRowId, RecordedAt) VALUES ({row.TransactionKey}, {"AssistFlow"}, {paymentId}, {row.Id}, {DateTimeOffset.UtcNow})", token);
                    var count = await db.Set<CollectionBankLoadRow>().Where(x => x.Id == row.Id && x.Status == "Ready")
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Imported").SetProperty(x => x.PaymentId, paymentId)
                            .SetProperty(x => x.AppliedUser, actor).SetProperty(x => x.AppliedAt, DateTimeOffset.UtcNow), token);
                    if (count != 1) throw new InvalidOperationException("Banka satırı eşzamanlı değişti.");
                });
                db.ChangeTracker.Clear();
                if (!result.IsSuccess) return ResponseModel<int>.Fail($"{completed} ödeme işlendi. {result.Message} Önizlemeyi yenileyin.", result.StatusCode, completed);
                completed++;
            }
            return ResponseModel<int>.Success(completed, $"{completed} ödeme işlendi. Kalan hazır satırlar için işlemi yeniden çalıştırabilirsiniz.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return ResponseModel<int>.Fail($"{completed} ödeme doğrulandı; kalan sonuç doğrulanamadı. Önizlemeyi yenileyip tekrar deneyin; banka kimlikleri korunur.", (StatusCode)503, completed); }
        finally { db.ChangeTracker.Clear(); }
    }
}
