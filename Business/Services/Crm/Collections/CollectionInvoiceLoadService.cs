using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Interfaces;
using Business.Interfaces.Storage;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Collections;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionInvoiceLoadService(AppDataContext db, IFileStorage storage, ILogger<CollectionInvoiceLoadService> logger) : ICollectionInvoiceLoadService
{
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    public static string InvoiceKey(string type, long customer, string number) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new { type, customer, number = Normalize(number) })));
    private Task<bool> Actor(long actor, CancellationToken ct) => db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && !x.IsDeleted, ct);
    private static bool Page(int page, int size) => page is >= 1 and <= 1000000 && size is >= 1 and <= 100;
    private static IQueryable<CollectionInvoiceLoadItem> Items(IQueryable<CollectionInvoiceLoad> source) => source.Select(x =>
        new CollectionInvoiceLoadItem(x.Id, x.Type, x.FileName, x.CreatedDate, x.Rows.Count,
            x.Rows.Count(r => r.Status == "Ready"), x.Rows.Count(r => r.Status == "Error"),
            x.Rows.Count(r => r.Status == "Duplicate"), x.Rows.Count(r => r.Status == "Imported"), x.RowVersion, null));

    public async Task<ResponseModel<PagedResult<CollectionInvoiceLoadItem>>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        if (!Page(page, pageSize)) return ResponseModel<PagedResult<CollectionInvoiceLoadItem>>.Fail("Geçersiz sayfa bilgisi.");
        var source = db.Set<CollectionInvoiceLoad>().AsNoTracking();
        var count = await source.CountAsync(ct);
        var rows = await Items(source.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return ResponseModel<PagedResult<CollectionInvoiceLoadItem>>.Success(new(rows, count, page, pageSize), "Fatura yüklemeleri getirildi.");
    }
    public async Task<ResponseModel<CollectionInvoiceLoadItem>> GetAsync(long id, CancellationToken ct)
    {
        var item = await Items(db.Set<CollectionInvoiceLoad>().AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
        if (item is null) return ResponseModel<CollectionInvoiceLoadItem>.Fail("Yükleme bulunamadı.", StatusCode.NotFound);
        var key = await db.Set<CollectionInvoiceLoad>().Where(x => x.Id == id).Select(x => x.StoredFileName).SingleAsync(ct);
        return ResponseModel<CollectionInvoiceLoadItem>.Success(item with { FileUrl = storage.GetPublicUrl(key) }, "Yükleme detayı getirildi.");
    }
    public async Task<ResponseModel<PagedResult<CollectionInvoiceLoadRowItem>>> RowsAsync(long id, int page, int pageSize, string? status, CancellationToken ct)
    {
        if (!Page(page, pageSize) || status is not (null or "Ready" or "Error" or "Duplicate" or "Imported"))
            return ResponseModel<PagedResult<CollectionInvoiceLoadRowItem>>.Fail("Geçersiz satır filtresi.");
        if (!await db.Set<CollectionInvoiceLoad>().AnyAsync(x => x.Id == id, ct))
            return ResponseModel<PagedResult<CollectionInvoiceLoadRowItem>>.Fail("Yükleme bulunamadı.", StatusCode.NotFound);
        var source = db.Set<CollectionInvoiceLoadRow>().AsNoTracking().Where(x => x.LoadId == id && (status == null || x.Status == status));
        var count = await source.CountAsync(ct);
        var rows = await source.OrderBy(x => x.RowNumber).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { Row = x, CustomerName = db.Customers.Where(c => c.Id == x.CustomerId).Select(c => c.SubscriberCompany).FirstOrDefault() }).ToListAsync(ct);
        var items = rows.Select(x => new CollectionInvoiceLoadRowItem(x.Row.Id, x.Row.RowNumber,
            JsonSerializer.Deserialize<CollectionInvoiceFileRow>(x.Row.SourceJson)!, x.Row.CustomerId, x.CustomerName,
            x.Row.CurrencyTypeId, x.Row.Status, x.Row.Issue, x.Row.InvoiceId)).ToArray();
        return ResponseModel<PagedResult<CollectionInvoiceLoadRowItem>>.Success(new(items, count, page, pageSize), "Yükleme satırları getirildi.");
    }
    public async Task<ResponseModel<long>> UploadAsync(IFormFile file, string type, long actor, CancellationToken ct)
    {
        if (file is null || file.Length is < 1 or > CollectionInvoiceFileParser.MaximumBytes) return Fail("En fazla 10 MB Excel dosyası seçin.");
        if (!await Actor(actor, ct)) return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null) return Fail("Bağımsız yükleme işlem kapsamı gereklidir.", StatusCode.Conflict);
        var name = Path.GetFileName(file.FileName);
        if (name.Length is < 1 or > 260) return Fail("Dosya adı geçersiz.");
        CollectionInvoiceFilePreview parsed;
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        try { parsed = CollectionInvoiceFileParser.Parse(buffer.ToArray(), name, type); }
        catch (InvalidDataException ex) { return Fail(ex.Message); }
        var previous = await db.Set<CollectionInvoiceLoad>().AsNoTracking().SingleOrDefaultAsync(x => x.Type == type && x.FileHash == parsed.FileHash, ct);
        if (previous is not null) return ResponseModel<long>.Success(previous.Id, "Bu dosya daha önce yüklendi. Mevcut önizleme açıldı.");
        // Immutable deterministic content key permits retry after an uncertain CDN/DB response.
        var stored = $"collection-invoice-{type}-{parsed.FileHash.ToLowerInvariant()}.xlsx";
        try
        {
            buffer.Position = 0;
            if (!await storage.ExistsAsync(stored, ct)) await storage.UploadAsync(stored, buffer, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ct);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            previous = await db.Set<CollectionInvoiceLoad>().AsNoTracking().SingleOrDefaultAsync(x => x.Type == type && x.FileHash == parsed.FileHash, ct);
            if (previous is not null) return ResponseModel<long>.Success(previous.Id, "Dosya zaten kayıtlı; mevcut önizleme açıldı.");
            var batch = new CollectionInvoiceLoad { Type = type, FileHash = parsed.FileHash, FileName = name, StoredFileName = stored, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow };
            foreach (var row in parsed.Rows) batch.Rows.Add(new CollectionInvoiceLoadRow { RowNumber = row.RowNumber, SourceJson = JsonSerializer.Serialize(row) });
            db.Add(batch);
            await Validate(batch, ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ResponseModel<long>.Success(batch.Id, "Dosya yüklendi. Fatura oluşturmadan önce önizlemeyi kontrol edin.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fatura yükleme doğrulanamadı: {FileHash}", parsed.FileHash);
            return Fail("Yükleme sonucu doğrulanamadı. Aynı dosyayla tekrar deneyin; mevcut yükleme korunur.", (StatusCode)503);
        }
        finally { db.ChangeTracker.Clear(); }
    }
    public async Task<ResponseModel<long>> ProcessAsync(long id, byte[] version, bool apply, long actor, CancellationToken ct)
    {
        if (version?.Length != 8) return Fail("Güncel yükleme sürümü gereklidir.");
        if (!await Actor(actor, ct)) return Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null) return Fail("Bağımsız yükleme işlem kapsamı gereklidir.", StatusCode.Conflict);
        var created = new List<(CollectionInvoiceLoadRow Row, CollectionInvoice Invoice)>();
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var batch = await db.Set<CollectionInvoiceLoad>().Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (batch is null) return Fail("Yükleme bulunamadı.", StatusCode.NotFound);
            if (!batch.RowVersion.SequenceEqual(version)) return Fail("Yükleme değişmiş. Güncel önizlemeyi açın; tamamlanan satırlar tekrar aktarılmaz.", StatusCode.Conflict);
            var before = Snapshot(batch);
            await Validate(batch, ct);
            var changed = before != Snapshot(batch);
            db.Entry(batch).Property(x => x.FileName).IsModified = true;
            if (apply && !changed)
            {
                foreach (var row in batch.Rows.Where(x => x.Status == "Ready"))
                {
                    var s = JsonSerializer.Deserialize<CollectionInvoiceFileRow>(row.SourceJson)!;
                    var invoice = new CollectionInvoice { CustomerId = row.CustomerId!.Value, Type = batch.Type, Number = s.Number,
                        Date = s.Date!.Value, Amount = s.Amount!.Value, CurrencyTypeId = row.CurrencyTypeId!.Value,
                        Comment = s.Comment, ProjectCode = s.ProjectCode, CreatedUser = actor, CreatedDate = DateTimeOffset.UtcNow };
                    db.Add(invoice);
                    // IDs assigned on SaveChanges below; no detached intermediate commits.
                    created.Add((row, invoice));
                    row.Status = "Imported"; row.ImportedKey = InvoiceKey(batch.Type, invoice.CustomerId, s.Number);
                    row.AppliedUser = actor; row.AppliedAt = DateTimeOffset.UtcNow;
                }
                batch.AppliedAt = DateTimeOffset.UtcNow; batch.AppliedUser = actor;
            }
            await db.SaveChangesAsync(ct);
            foreach (var pair in created) pair.Row.InvoiceId = pair.Invoice.Id;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            if (apply && changed) return Fail("Eşleme veya mükerrer kayıt durumu değişti. Önizleme yenilendi; kontrol edip yeniden onaylayın.", StatusCode.Conflict);
            return ResponseModel<long>.Success(id, apply ? $"{created.Count} fatura oluşturuldu. Hatalı/mükerrer satırlar aktarılmadı." : "Önizleme güncellendi.");
        }
        catch (DbUpdateConcurrencyException) { return Fail("Yükleme başka işlem tarafından değiştirildi. Önizlemeyi yenileyin.", StatusCode.Conflict); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fatura yükleme işlemi doğrulanamadı: {LoadId}", id);
            return Fail("İşlem sonucu doğrulanamadı. Önizlemeyi yenileyin; aktarılan satırlar tekrar oluşturulmaz.", (StatusCode)503);
        }
        finally { db.ChangeTracker.Clear(); }
    }
    private static string Snapshot(CollectionInvoiceLoad b) => JsonSerializer.Serialize(b.Rows.OrderBy(x => x.RowNumber).Select(x => new { x.RowNumber, x.Status, x.CustomerId, x.CurrencyTypeId, x.Issue }));
    private async Task Validate(CollectionInvoiceLoad batch, CancellationToken ct)
    {
        var pending = batch.Rows.Where(x => x.Status != "Imported").ToArray();
        var sources = pending.ToDictionary(x => x, x => JsonSerializer.Deserialize<CollectionInvoiceFileRow>(x.SourceJson)!);
        var codes = sources.Values.Select(x => Normalize(x.AccountCode)).Distinct().ToArray();
        var accounts = await db.Set<CollectionInvoiceAccount>().AsNoTracking().Where(x => codes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, ct);
        var customerIds = accounts.Values.Where(x => x.CustomerId.HasValue).Select(x => x.CustomerId!.Value).Distinct().ToArray();
        var allowed = (await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).Where(x => customerIds.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var currencies = await db.Set<CurrencyType>().AsNoTracking().ToListAsync(ct);
        var existing = await db.Set<CollectionInvoice>().AsNoTracking().Where(x => customerIds.Contains(x.CustomerId))
            .Select(x => new { x.Type, x.CustomerId, x.Number }).ToListAsync(ct);
        var keys = existing.Where(x => !string.IsNullOrWhiteSpace(x.Number)).Select(x => InvoiceKey(x.Type, x.CustomerId, x.Number!)).ToHashSet();
        // Financial deletions remain duplicates, including invoices originally migrated by K06.
        var deleted = await db.Database.SqlQueryRaw<string>("""
            SELECT BeforeJson AS [Value] FROM collection.InvoiceOperation
            WHERE Kind = 'InvoiceDelete' AND BeforeJson IS NOT NULL
                AND TRY_CONVERT(bigint, JSON_VALUE(BeforeJson, '$.CustomerId'))
                IN (SELECT TRY_CONVERT(bigint, value) FROM OPENJSON(@customers))
            """, new SqlParameter("@customers", JsonSerializer.Serialize(customerIds))).ToListAsync(ct);
        foreach (var json in deleted.Where(x => x != null))
        {
            using var d = JsonDocument.Parse(json!); var r = d.RootElement;
            var number = r.GetProperty("Number").GetString();
            if (!string.IsNullOrWhiteSpace(number)) keys.Add(InvoiceKey(r.GetProperty("Type").GetString()!, r.GetProperty("CustomerId").GetInt64(), number));
        }
        var possibleKeys = sources.Values.Where(x => accounts.TryGetValue(Normalize(x.AccountCode), out var account) && account.CustomerId.HasValue)
            .Select(x => InvoiceKey(batch.Type, accounts[Normalize(x.AccountCode)].CustomerId!.Value, x.Number)).Distinct().ToArray();
        var imported = await db.Set<CollectionInvoiceLoadRow>().AsNoTracking().Where(x => x.ImportedKey != null && possibleKeys.Contains(x.ImportedKey)).Select(x => x.ImportedKey!).ToListAsync(ct);
        keys.UnionWith(imported);
        foreach (var row in pending)
        {
            var s = sources[row]; var errors = s.Errors.ToList(); row.CustomerId = null; row.CurrencyTypeId = null;
            if (!accounts.TryGetValue(Normalize(s.AccountCode), out var a)) errors.Add("Cari kod için onaylı müşteri eşlemesi bulunamadı.");
            else if (a.Issue != null || !a.CustomerId.HasValue) errors.Add(a.Issue ?? "Cari kod eşlemesi belirsiz.");
            else if (!allowed.Contains(a.CustomerId.Value)) errors.Add("Müşteri tahsilat kapsamı dışında.");
            else row.CustomerId = a.CustomerId;
            var currency = currencies.Where(x => Currency(x.Code) == Currency(s.CurrencyCode)).ToArray();
            if (currency.Length != 1) errors.Add("Para birimi tekil olarak eşleştirilemedi."); else row.CurrencyTypeId = currency[0].Id;
            row.Status = errors.Count == 0 ? "Ready" : "Error";
            if (errors.Count == 0 && keys.Contains(InvoiceKey(batch.Type, row.CustomerId!.Value, s.Number)))
            { row.Status = "Duplicate"; errors.Add("Bu müşterinin aynı tür ve numaradaki faturası zaten kayıtlı veya daha önce silinmiş."); }
            row.Issue = errors.Count == 0 ? null : string.Join(" ", errors);
        }
        foreach (var group in pending.Where(x => x.Status == "Ready").GroupBy(x => InvoiceKey(batch.Type, x.CustomerId!.Value, sources[x].Number)).Where(x => x.Count() > 1))
            foreach (var row in group) { row.Status = "Duplicate"; row.Issue = "Dosyada aynı müşteriye ait fatura birden fazla kez bulunuyor."; }
    }
    private static string Currency(string value) => Normalize(value) switch { "TL" or "YTL" or "TRL" => "TRY", var s => s };
    private static ResponseModel<long> Fail(string message, StatusCode status = StatusCode.BadRequest) => ResponseModel<long>.Fail(message, status);
}
