using Amazon.S3;
using Amazon.S3.Model;
using Business.Interfaces.Storage;
using Core.Settings.Concrete;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace Business.Services.Storage;

public sealed class R2FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly R2StorageOptions _options;
    private readonly ILogger<R2FileStorage> _logger;

    public R2FileStorage(IAmazonS3 s3Client, IOptions<R2StorageOptions> options, ILogger<R2FileStorage> logger)
    {
        _s3Client = s3Client;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (file.Length <= 0)
            throw new InvalidDataException("Boş dosya yüklenemez.");

        var originalFileName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalFileName)
            .ToLowerInvariant();

        var storedFileName = $"{Guid.NewGuid():N}{extension}";

        await using var inputStream = file.OpenReadStream();

        await UploadAsync(
            storedFileName,
            inputStream,
            file.ContentType,
            cancellationToken);

        // Veritabanına yine yalnızca dosya adı yazılacak.
        return storedFileName;
    }

    public async Task UploadAsync(string storedFileName, Stream content, string? contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var objectKey = BuildObjectKey(storedFileName);

        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,

            AutoCloseStream = false,

            // Cloudflare R2 için gerekli.
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };

        await _s3Client.PutObjectAsync(
            request,
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        try
        {
            await _s3Client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest
                {
                    BucketName = _options.BucketName,
                    Key = BuildObjectKey(storedFileName)
                },
                cancellationToken);

            return true;
        }
        catch (AmazonS3Exception ex) when (
            ex.StatusCode == HttpStatusCode.NotFound ||
            string.Equals(
                ex.ErrorCode,
                "NoSuchKey",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
            return;

        await _s3Client.DeleteObjectAsync(
            new DeleteObjectRequest
            {
                BucketName = _options.BucketName,
                Key = BuildObjectKey(storedFileName)
            },
            cancellationToken);
    }

    public async Task DeleteManyAsync(IEnumerable<string> storedFileNames, CancellationToken cancellationToken = default)
    {
        var fileNames = storedFileNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var storedFileName in fileNames)
        {
            try
            {
                await DeleteAsync(
                    storedFileName,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Temizlik hatası ana işlemin hatasını ezmemeli.
                _logger.LogWarning(
                    ex,
                    "R2 nesnesi silinemedi. FileName: {FileName}",
                    storedFileName);
            }
        }
    }

    public string GetPublicUrl(string storedFileName)
    {
        var objectKey = BuildObjectKey(storedFileName);

        var escapedKey = string.Join(
            "/",
            objectKey
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));

        return $"{_options.PublicBaseUrl.TrimEnd('/')}/{escapedKey}";
    }

    private string BuildObjectKey(string storedFileName)
    {
        var normalized = storedFileName
            .Replace('\\', '/')
            .Trim();

        // DB’ye tam URL veya /uploads/file yazılmış eski bir kayıt
        // gelirse yalnızca gerçek dosya adını al.
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
            normalized = uri.AbsolutePath;

        var safeFileName = Path.GetFileName(
            normalized.TrimStart('/'));

        if (string.IsNullOrWhiteSpace(safeFileName))
            throw new InvalidDataException("Dosya adı geçersiz.");

        var prefix = _options.KeyPrefix
            .Trim()
            .Trim('/');

        return string.IsNullOrWhiteSpace(prefix)
            ? safeFileName
            : $"{prefix}/{safeFileName}";
    }
}