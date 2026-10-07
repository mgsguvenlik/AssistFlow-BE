using Business.Interfaces.Storage;
using Core.Common;
using Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using System.Data;
using System.Linq.Expressions;

namespace Business.Utilities;

public static class TechnicalServiceImageUploader
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    public static int GetLimit(TechnicalServiceImageType type) => type switch
    {
        TechnicalServiceImageType.Service => 10,
        TechnicalServiceImageType.Form => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    public static string? Validate(TechnicalServiceImageUploadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.RequestNo))
            return "Talep numarası gerekli.";
        if (!Enum.IsDefined(dto.Type))
            return "Görsel türü geçersiz.";
        if (dto.Files is null || dto.Files.Count == 0)
            return "En az bir görsel seçin.";
        if (dto.Files.Count > GetLimit(dto.Type))
            return $"En fazla {GetLimit(dto.Type)} görsel yükleyebilirsiniz.";
        foreach (var file in dto.Files)
        {
            if (file is null || file.Length <= 0)
                return "Boş dosya yüklenemez.";
            if (file.Length > MaxFileSize)
                return "Her görsel en fazla 5 MB olabilir.";
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not (".jpg" or ".jpeg" or ".png") ||
                file.ContentType?.ToLowerInvariant() is not ("image/jpeg" or "image/png"))
                return "Yalnızca JPG, JPEG veya PNG görselleri yükleyebilirsiniz.";
        }
        return null;
    }

    public static async Task<ResponseModel<List<TechnicalServiceImageGetDto>>> UploadAsync<TImage>(
        DbContext context,
        IFileStorage storage,
        TechnicalServiceImageUploadDto dto,
        Func<CancellationToken, Task<long?>> findTechnicalService,
        Func<long, Expression<Func<TImage, bool>>> imageFilter,
        Func<long, string, TImage> createImage,
        Func<TImage, TechnicalServiceImageGetDto> toDto,
        ILogger logger,
        CancellationToken cancellationToken = default) where TImage : class
    {
        var error = Validate(dto);
        if (error is not null)
            return ResponseModel<List<TechnicalServiceImageGetDto>>.Fail(error, StatusCode.BadRequest);

        var savedFiles = new List<string>();
        var images = new List<TImage>();
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var technicalServiceId = await findTechnicalService(cancellationToken);
            if (technicalServiceId is null)
                return ResponseModel<List<TechnicalServiceImageGetDto>>.Fail("Talebe ait teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            // Count persisted images inside the transaction so concurrent uploads cannot exceed the limit.
            var existingCount = await context.Set<TImage>().CountAsync(imageFilter(technicalServiceId.Value), cancellationToken);
            if (existingCount + dto.Files.Count > GetLimit(dto.Type))
                return ResponseModel<List<TechnicalServiceImageGetDto>>.Fail(
                    $"Mevcut görseller dahil en fazla {GetLimit(dto.Type)} görsel olabilir.", StatusCode.BadRequest);

            foreach (var file in dto.Files)
            {
                var storedFileName = await storage.SaveAsync(file, cancellationToken);
                savedFiles.Add(storedFileName);
                images.Add(createImage(technicalServiceId.Value, storedFileName));
            }
            context.Set<TImage>().AddRange(images);
            await context.SaveChangesAsync(cancellationToken);
            var result = images.Select(toDto).ToList();
            foreach (var image in result)
                image.Url = storage.GetPublicUrl(image.Url);
            await transaction.CommitAsync(cancellationToken);
            return ResponseModel<List<TechnicalServiceImageGetDto>>.Success(result, "Görseller eklendi.");
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(CancellationToken.None); }
            catch (Exception rollbackError) { logger.LogWarning(rollbackError, "Görsel yükleme işlemi geri alınamadı."); }
            foreach (var image in images)
                context.Entry(image).State = EntityState.Detached;
            foreach (var file in savedFiles)
            {
                try { await storage.DeleteAsync(file, CancellationToken.None); }
                catch (Exception cleanupError) { logger.LogWarning(cleanupError, "Yarım kalan görsel yüklemesi temizlenemedi: {File}", file); }
            }
            logger.LogError(ex, "Teknik servis görselleri eklenemedi. RequestNo: {RequestNo}", dto.RequestNo);
            return ResponseModel<List<TechnicalServiceImageGetDto>>.Fail("Görseller eklenemedi. Lütfen tekrar deneyin.", StatusCode.Error);
        }
    }
}
