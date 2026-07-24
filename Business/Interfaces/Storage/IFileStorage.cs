using Microsoft.AspNetCore.Http;

namespace Business.Interfaces.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task UploadAsync(string storedFileName, Stream content, string? contentType, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
    Task DeleteManyAsync(IEnumerable<string> storedFileNames, CancellationToken cancellationToken = default);
    string GetPublicUrl(string storedFileName);
}