using Business.Interfaces;
using Business.Interfaces.Storage;
using Core.Common;
using Core.Enums;
using Data.Concrete.EfCore.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

namespace Business.Services.Crm.Collections;

public sealed class CollectionContractAttachmentService(AppDataContext db, IFileStorage storage,
    ILogger<CollectionContractAttachmentService> logger) : ICollectionContractAttachmentService
{
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
