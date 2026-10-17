using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Business.Services.Storage;
using Core.Settings.Concrete;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal sealed class FileTransport : IDisposable
{
    internal readonly IAmazonS3 Client;
    internal readonly R2StorageOptions Options;
    internal readonly R2FileStorage Storage;
    internal readonly bool Isolated;
    internal FileTransport(IAmazonS3 client, R2StorageOptions options, bool isolated)
    { Client = client; Options = options; Isolated = isolated; Storage = new(client, Microsoft.Extensions.Options.Options.Create(options), NullLogger<R2FileStorage>.Instance); }
    internal static R2StorageOptions IsolatedOptions() => new() { Endpoint = "https://isolated.invalid", BucketName = "isolated", KeyPrefix = "rehearsal-files", PublicBaseUrl = "https://isolated.invalid", AccessKeyId = "unused", SecretAccessKey = "unused" };
    internal string Key(string name) => Options.KeyPrefix.Trim('/') + "/" + name;
    internal async Task<string> VerifyAsync(FileCandidate file, bool publicRead)
    {
        if (!publicRead || Isolated)
        {
            using var response = await Client.GetObjectAsync(new GetObjectRequest { BucketName = Options.BucketName, Key = Key(file.StoredName) });
            FileAdapter.Need(response.ContentLength == file.Size && response.Headers.ContentType == file.ContentType, "Uzak dosya boyutu/türü farklı; nesneye dokunulmadı.");
            return await VerifyBytesAsync(response.ResponseStream, file);
        }
        var url = new Uri(Storage.GetPublicUrl(file.StoredName)); var baseUrl = new Uri(Options.PublicBaseUrl);
        FileAdapter.Need(url.Scheme == "https" && url.Host == baseUrl.Host && url.AbsolutePath.StartsWith(baseUrl.AbsolutePath.TrimEnd('/') + "/" + Options.KeyPrefix.Trim('/') + "/", StringComparison.Ordinal), "Public URL kapsam dışı.");
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        using var responseHttp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        FileAdapter.Need(responseHttp.StatusCode == HttpStatusCode.OK && responseHttp.Content.Headers.ContentType?.MediaType == file.ContentType, "Public CDN yanıtı doğrulanmadı; redirect/HTML kabul edilmez.");
        await using var stream = await responseHttp.Content.ReadAsStreamAsync();
        return await VerifyBytesAsync(stream, file);
    }
    private static async Task<string> VerifyBytesAsync(Stream input, FileCandidate file)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long size = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        int n; while ((n = await input.ReadAsync(buffer, timeout.Token)) > 0)
        { size += n; FileAdapter.Need(size <= file.Size, "Uzak içerik boyut sınırını aştı."); hash.AppendData(buffer, 0, n); }
        var actual = Convert.ToHexString(hash.GetHashAndReset());
        FileAdapter.Need(size == file.Size && actual == file.ContentHash, "Uzak içerik hash/boyut farklı; otomatik silme yapılmadı."); return actual;
    }
    public void Dispose() => Client.Dispose();
}

// Only the three operations used by the unchanged R2FileStorage are implemented.
// Any accidental SDK fallback is denied before HTTP; no real provider credentials exist.
internal sealed class IsolatedS3(string root) : AmazonS3Client(new AnonymousAWSCredentials(), new AmazonS3Config
{ ServiceURL = "http://127.0.0.1:1", ForcePathStyle = true, HttpClientFactory = new DenyHttpFactory() })
{
    internal int Puts { get; private set; }
    internal bool CorruptReads { get; set; }
    internal bool DenyMetadata { get; set; }
    private string PathFor(string bucket, string key) => Path.Combine(root, FileAdapter.HashText(bucket + "|" + key));
    public override async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
    {
        var path = PathFor(request.BucketName, request.Key);
        FileAdapter.Need(!File.Exists(path), "İzole depo mevcut nesnenin üzerine yazmayı reddetti.");
        await using (var output = new FileStream(path, FileMode.CreateNew)) await request.InputStream.CopyToAsync(output, cancellationToken);
        await File.WriteAllTextAsync(path + ".type", request.ContentType, cancellationToken); Puts++;
        return new PutObjectResponse { HttpStatusCode = HttpStatusCode.OK };
    }
    public override Task<GetObjectMetadataResponse> GetObjectMetadataAsync(GetObjectMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (DenyMetadata) throw new AmazonS3Exception("İzole 403 kontrolü.") { StatusCode = HttpStatusCode.Forbidden };
        var path = PathFor(request.BucketName, request.Key);
        if (!File.Exists(path)) throw new AmazonS3Exception("İzole nesne yok.") { StatusCode = HttpStatusCode.NotFound, ErrorCode = "NoSuchKey" };
        return Task.FromResult(new GetObjectMetadataResponse { ContentLength = new FileInfo(path).Length, HttpStatusCode = HttpStatusCode.OK });
    }
    public override Task<GetObjectResponse> GetObjectAsync(GetObjectRequest request, CancellationToken cancellationToken = default)
    {
        var path = PathFor(request.BucketName, request.Key);
        var response = new GetObjectResponse { ContentLength = new FileInfo(path).Length, ResponseStream = CorruptReads ? new MemoryStream("corrupt"u8.ToArray()) : File.OpenRead(path), HttpStatusCode = HttpStatusCode.OK };
        response.Headers.ContentType = File.ReadAllText(path + ".type"); return Task.FromResult(response);
    }
}
internal sealed class DenyHttpFactory : Amazon.Runtime.HttpClientFactory
{
    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new DenyHandler());
    private sealed class DenyHandler : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new FileGateException("İzole provada HTTP kesin olarak kapalıdır."); }
}
