using Business.Interfaces;
using Business.Interfaces.Storage;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Data.Concrete.EfCore.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionContractAttachmentService(AppDataContext db, IFileStorage storage,
    ILogger<CollectionContractAttachmentService> logger) : ICollectionContractAttachmentService
{
    public async Task<ResponseModel<PagedResult<CollectionContractAttachmentItem>>> GetCustomerPageAsync(long customerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 1000000 || pageSize is < 1 or > 100) return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Fail("Geçersiz sayfa bilgisi.");
        if (!await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).AnyAsync(x => x.Id == customerId, cancellationToken))
            return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Fail("Tahsilat kapsamında müşteri bulunamadı.", StatusCode.NotFound);
        var query = db.Set<CollectionCustomerAttachment>().AsNoTracking().Where(x => x.CustomerId == customerId && !x.IsDeleted);
        var count = await query.CountAsync(cancellationToken);
        var files = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Success(new(files.Select(x => new CollectionContractAttachmentItem(x.Id,
            x.OriginalFileName, storage.GetPublicUrl(x.StoredFileName), x.ContentType, x.SizeBytes, x.CreatedDate)).ToList(), count, page, pageSize));
    }
    private async Task<bool> CustomerActorAsync(long id, long actor, CancellationToken ct) => actor > 0 &&
        await db.Users.AsNoTracking().AnyAsync(x => x.Id == actor && !x.IsDeleted, ct) &&
        await CollectionCustomerScopeQuery.Customers(db.Customers.AsNoTracking()).AnyAsync(x => x.Id == id, ct);

    public async Task<ResponseModel> RemoveCustomerAsync(long customerId, long attachmentId, long actorId, CancellationToken ct = default)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await CustomerActorAsync(customerId, actorId, ct)) return ResponseModel.Fail("Geçerli kullanıcı veya tahsilat müşterisi bulunamadı.");
            var file = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == attachmentId && x.CustomerId == customerId, ct);
            if (file is null) return ResponseModel.Fail("Dosya bu müşteriye ait değil.", StatusCode.NotFound);
            await db.Set<CollectionCustomerAttachment>().Where(x => x.Id == attachmentId && x.CustomerId == customerId && !x.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.RemovedUser, actorId).SetProperty(x => x.RemovedDate, DateTimeOffset.UtcNow), ct);
            await transaction.CommitAsync(ct);
            return ResponseModel.Success("Dosya listeden kaldırıldı; CDN arşivi ve aktarım geçmişi korunuyor.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Müşteri dosyası kaldırma sonucu doğrulanamadı. CustomerId: {CustomerId}, AttachmentId: {AttachmentId}", customerId, attachmentId);
            return ResponseModel.Fail("Dosya kaldırma sonucu doğrulanamadı. Listeyi yenileyip kontrol edin.", (StatusCode)503);
        }
    }

    public async Task<ResponseModel> UploadCustomerAsync(long customerId, IFormFile file, Guid requestId, long actorId, CancellationToken ct = default)
    {
        if (requestId == Guid.Empty || file is null || file.Length is < 1 or > 20971520) return ResponseModel.Fail("Geçerli işlem anahtarı ve en fazla 20 MB dosya gereklidir.");
        var name = Path.GetFileName(file.FileName); var ext = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 260 || !AllowedTypes.TryGetValue(ext, out var type) || !await HasExpectedSignatureAsync(file, ext, ct))
            return ResponseModel.Fail("İçeriği geçerli PDF, PNG veya JPG dosyası seçin.");
        if (db.ChangeTracker.Entries().Any() || db.Database.CurrentTransaction is not null) return ResponseModel.Fail("Bağımsız işlem kapsamı gereklidir.", StatusCode.Conflict);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            if (!await CustomerActorAsync(customerId, actorId, ct)) return ResponseModel.Fail("Geçerli kullanıcı veya tahsilat müşterisi bulunamadı.");
            using var data = new MemoryStream(); await file.CopyToAsync(data, ct);
            var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data.ToArray()));
            var path = $"manual/{customerId}/{requestId:N}";
            var old = await db.Set<CollectionCustomerAttachment>().AsNoTracking().SingleOrDefaultAsync(x => x.SourcePath == path, ct);
            if (old != null)
                return old.CreatedUser == actorId && old.ContentHash == hash && old.OriginalFileName == name
                    ? ResponseModel.Success(old.IsDeleted ? "Dosya daha önce yüklenip listeden kaldırılmış; yeniden etkinleştirilmedi." : "Dosya daha önce yüklendi.")
                    : ResponseModel.Fail("İşlem anahtarı farklı kullanıcı veya dosyaya ait.", StatusCode.Conflict);
            var stored = $"collection-customer-{customerId}-{requestId:N}-{hash.ToLowerInvariant()}{ext.ToLowerInvariant()}";
            data.Position = 0; await storage.UploadAsync(stored, data, type, ct);
            db.Add(new CollectionCustomerAttachment { CustomerId = customerId, SourcePath = path, ContentHash = hash, OriginalFileName = name,
                StoredFileName = stored, ContentType = type, SizeBytes = file.Length, CreatedDate = DateTimeOffset.UtcNow, CreatedUser = actorId, Decision = "Kullanıcı tarafından yüklendi." });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return ResponseModel.Success("Müşteri dosyası yüklendi.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Müşteri dosyası yükleme sonucu doğrulanamadı. CustomerId: {CustomerId}", customerId);
            return ResponseModel.Fail("Sonuç doğrulanamadı. Aynı dosyayla tekrar deneyin; çift kayıt oluşturulmaz.", (StatusCode)503);
        }
        finally { db.ChangeTracker.Clear(); }
    }
    public async Task<ResponseModel> RemoveAsync(long contractId, long attachmentId, long actorId,
        CancellationToken cancellationToken = default)
    {
        if (contractId <= 0 || attachmentId <= 0 || actorId <= 0)
            return ResponseModel.Fail("Geçerli sözleşme, dosya ve kullanıcı gereklidir.");
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
            if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
                return ResponseModel.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
            if (!await CollectionCustomerScopeQuery.Contracts(db.Set<CollectionContract>(), db.Customers)
                .AnyAsync(x => x.Id == contractId && !x.IsDeleted && !x.Customer.IsDeleted, cancellationToken))
                return ResponseModel.Fail("Tahsilat kapsamında sözleşme bulunamadı.", StatusCode.NotFound);
            var file = await db.Set<CollectionContractAttachment>().AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == attachmentId && x.ContractId == contractId, cancellationToken);
            if (file is null) return ResponseModel.Fail("Dosya bu sözleşmede bulunamadı.", StatusCode.NotFound);
            if (file.IsDeleted) return ResponseModel.Success("Dosya zaten sözleşme listesinden kaldırılmış.");
            // Metadata is immutable; concurrent/repeated removals must preserve the original removal actor/time.
            await db.Set<CollectionContractAttachment>().Where(x => x.Id == attachmentId && x.ContractId == contractId && !x.IsDeleted)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDeleted, true).SetProperty(x => x.UpdatedUser, actorId)
                    .SetProperty(x => x.UpdatedDate, DateTimeOffset.UtcNow), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            // The CDN object and migration evidence remain intact. This is not physical file erasure.
            return ResponseModel.Success("Dosya sözleşme listesinden kaldırıldı. Arşiv kopyası korunuyor.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Sözleşme dosyası kaldırılamadı. ContractId: {ContractId}, AttachmentId: {AttachmentId}", contractId, attachmentId);
            return ResponseModel.Fail("Dosya kaldırma sonucu doğrulanamadı. Listeyi yenileyip kontrol edin.", StatusCode.Error);
        }
    }

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf", [".png"] = "image/png",
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg"
    };

    public async Task<ResponseModel<PagedResult<CollectionContractAttachmentItem>>> GetPageAsync(long contractId,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (contractId <= 0 || page is < 1 or > 1000000 || pageSize is < 1 or > 100)
            return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Fail("Geçerli sözleşme ve sayfa bilgisi gereklidir.");
        if (!await ContractExistsAsync(contractId, cancellationToken))
            return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        var query = db.Set<CollectionContractAttachment>().AsNoTracking()
            .Where(x => x.ContractId == contractId && !x.IsDeleted);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.OriginalFileName, x.StoredFileName, x.ContentType, x.SizeBytes, x.CreatedDate })
            .ToListAsync(cancellationToken);
        var items = rows.Select(x => new CollectionContractAttachmentItem(x.Id, x.OriginalFileName,
            storage.GetPublicUrl(x.StoredFileName), x.ContentType, x.SizeBytes, x.CreatedDate)).ToList();
        return ResponseModel<PagedResult<CollectionContractAttachmentItem>>.Success(
            new(items, count, page, pageSize), "Sözleşme dosyaları getirildi.");
    }

    public async Task<ResponseModel<CollectionContractAttachmentItem>> UploadAsync(long contractId, IFormFile file,
        long actorId, CancellationToken cancellationToken = default)
    {
        if (contractId <= 0 || actorId <= 0 || file is null || file.Length is < 1 or > 20971520)
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Geçerli sözleşme ve en fazla 20 MB dosya gereklidir.");
        var name = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(name);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 260 || !AllowedTypes.TryGetValue(extension, out var contentType)
            || file.ContentType is not ("application/octet-stream" or "")
                && !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Yalnız PDF, PNG veya JPG dosyası yüklenebilir.");
        if (!await ContractExistsAsync(contractId, cancellationToken))
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Sözleşme bulunamadı.", StatusCode.NotFound);
        if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && !x.IsDeleted, cancellationToken))
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Geçerli kullanıcı bulunamadı.", StatusCode.Unauthorized);
        if (!await HasExpectedSignatureAsync(file, extension, cancellationToken))
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Dosyanın içeriği uzantısıyla uyuşmuyor.");
        string? stored = null;
        try
        {
            stored = await storage.SaveAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(stored) || stored.Length > 260)
                throw new InvalidDataException("CDN dosya anahtarı geçersiz.");
            var entity = new CollectionContractAttachment
            {
                ContractId = contractId, OriginalFileName = name, StoredFileName = stored,
                ContentType = contentType, SizeBytes = file.Length,
                CreatedUser = actorId, CreatedDate = DateTimeOffset.UtcNow
            };
            db.Set<CollectionContractAttachment>().Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            return ResponseModel<CollectionContractAttachmentItem>.Success(
                new(entity.Id, name, storage.GetPublicUrl(stored), contentType, file.Length, entity.CreatedDate),
                "Sözleşme dosyası yüklendi.", StatusCode.Created);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // DB commit sonucu belirsizse CDN nesnesini silmek geçerli metadata'yı bozabilir.
            logger.LogWarning(ex, "Sözleşme dosyası yüklenemedi. ContractId: {ContractId}, StoredFileName: {StoredFileName}", contractId, stored);
            return ResponseModel<CollectionContractAttachmentItem>.Fail("Dosya yükleme sonucu doğrulanamadı. Tekrar yüklemeden önce dosya listesini yenileyin.", StatusCode.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
    }

    private Task<bool> ContractExistsAsync(long id, CancellationToken token) => db.Set<CollectionContract>()
        .AsNoTracking().AnyAsync(x => x.Id == id && !x.IsDeleted && !x.Customer.IsDeleted, token);

    private static async Task<bool> HasExpectedSignatureAsync(IFormFile file, string extension, CancellationToken token)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        var length = 0;
        while (length < header.Length)
        {
            var read = await stream.ReadAsync(header.AsMemory(length), token);
            if (read == 0) break;
            length += read;
        }
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => length >= 5 && header.AsSpan(0, 5).SequenceEqual("%PDF-"u8),
            ".png" => length >= 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => length >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            _ => false
        };
    }

}
