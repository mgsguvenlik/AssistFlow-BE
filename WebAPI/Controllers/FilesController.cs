using Business.Interfaces.Storage;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace WebAPI.Controllers;

[ApiController]
[AllowAnonymous]
public sealed class FilesController : ControllerBase
{
    private readonly IFileStorage _fileStorage;
    private readonly R2StorageOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FilesController> _logger;

    private readonly FileExtensionContentTypeProvider _contentTypeProvider = new();

    public FilesController(
        IFileStorage fileStorage,
        IOptions<R2StorageOptions> options,
        IWebHostEnvironment environment,
        ILogger<FilesController> logger)
    {
        _fileStorage = fileStorage;
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("uploads/{fileName}")]
    public async Task<IActionResult> GetFile(string fileName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("Dosya adı geçersiz.");

        // ../ gibi path traversal denemelerini engelle.
        var safeFileName = Path.GetFileName(fileName);

        if (string.IsNullOrWhiteSpace(safeFileName) ||
            !string.Equals(
                safeFileName,
                fileName,
                StringComparison.Ordinal))
        {
            return BadRequest("Dosya adı geçersiz.");
        }

        try
        {
            var existsInR2 = await _fileStorage.ExistsAsync(
                safeFileName,
                cancellationToken);

            if (existsInR2)
            {
                var publicUrl = _fileStorage.GetPublicUrl(safeFileName);

                _logger.LogDebug(
                    "Dosya Cloudflare CDN adresine yönlendiriliyor. " +
                    "FileName: {FileName}, Url: {PublicUrl}",
                    safeFileName,
                    publicUrl);

                // Geçiş döneminde 302 redirect.
                return Redirect(publicUrl);
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            return new EmptyResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cloudflare R2 dosya kontrolü başarısız. " +
                "Yerel dosya kontrol edilecek. FileName: {FileName}",
                safeFileName);
        }

        if (!_options.LegacyLocalFallbackEnabled)
            return NotFound("Dosya bulunamadı.");

        var localRoot = Path.IsPathRooted(_options.LegacyLocalRoot)
            ? _options.LegacyLocalRoot
            : Path.Combine(
                _environment.ContentRootPath,
                _options.LegacyLocalRoot);

        var localPath = Path.Combine(
            localRoot,
            safeFileName);

        if (!System.IO.File.Exists(localPath))
            return NotFound("Dosya bulunamadı.");

        if (!_contentTypeProvider.TryGetContentType(
                safeFileName,
                out var contentType))
        {
            contentType = "application/octet-stream";
        }

        // Eski yerel dosyaları stream etmek yerine PhysicalFile kullanılabilir.
        return PhysicalFile(
            localPath,
            contentType,
            enableRangeProcessing: true);
    }
}
