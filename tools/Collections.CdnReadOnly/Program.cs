using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Business.Services.Storage;
using Core.Settings.Concrete;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

try
{
    if (args.Length != 2 || args[0] != "inspect") throw new Gate("Kullanım: inspect <yeni .tools çıktı dizini>. Yalnız SELECT, HEAD ve GET.");
    await Inspection.RunAsync(args[1]);
}
catch (Exception ex)
{
    Console.Error.WriteLine("Salt-okunur CDN kontrolü durdu. Hata sınıfı: " + ex.GetType().Name);
    if (ex is Gate) Console.Error.WriteLine(ex.Message);
    if (ex is SqlException sql) Console.Error.WriteLine("SQL hata kodu: " + sql.Number);
    Environment.ExitCode = 1;
}

internal sealed class Gate(string message) : Exception(message);
internal sealed record Sample(string Table, long Id, string StoredName, string ContentType, long Size);
internal static class Inspection
{
    private static void Need(bool ok, string message) { if (!ok) throw new Gate(message); }
    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static async Task Save(string root, string name, object data) => await File.WriteAllTextAsync(Path.Combine(root, name), JsonSerializer.Serialize(data, Json));
    internal static async Task RunAsync(string output)
    {
        var root = Path.GetFullPath(output); var privateRoot = Path.GetFullPath(".tools") + Path.DirectorySeparatorChar;
        Need(root.StartsWith(privateRoot, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(root) && !File.Exists(root), "Yeni özel .tools çıktısı gerekli.");
        for (var p = Path.GetDirectoryName(root); p is not null; p = Path.GetDirectoryName(p))
            if (Directory.Exists(p)) Need((File.GetAttributes(p) & FileAttributes.ReparsePoint) == 0, "Link/reparse çıktı yolu reddedildi.");
        // Mirrors only this process's JSON/environment configuration. Not a claim about IIS/Jenkins runtime settings.
        var config = new ConfigurationBuilder().SetBasePath(Path.GetFullPath("WebAPI"))
            .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Production.json").AddEnvironmentVariables().Build();
        var o = config.GetSection("R2Storage").Get<R2StorageOptions>()!;
        Need(Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var endpoint) && endpoint.Scheme == "https" && endpoint.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase) && endpoint.UserInfo.Length == 0 && endpoint.Query.Length == 0, "HTTPS R2 endpoint doğrulanamadı.");
        Need(Uri.TryCreate(o.PublicBaseUrl, UriKind.Absolute, out var publicBase) && publicBase.Scheme == "https" && publicBase.Host == "cdn.flowassist.mgs.com.tr" && publicBase.UserInfo.Length == 0 && publicBase.Query.Length == 0, "Onaylı public CDN hostu gerekli.");
        Need(o.KeyPrefix == "uploads" && !string.IsNullOrWhiteSpace(o.BucketName) && !string.IsNullOrWhiteSpace(o.AccessKeyId) && !string.IsNullOrWhiteSpace(o.SecretAccessKey), "Canlı önek veya credential eksik/farklı.");
        Directory.CreateDirectory(root);
        await Save(root, "settings.json", new { ObservedAtUtc = DateTimeOffset.UtcNow, ConfigurationSource = "LOCAL_JSON_PLUS_CURRENT_PROCESS_ENV_NOT_DEPLOYED_HOST_PROOF", EndpointHostHash = Hash(endpoint!.Host), BucketNameHash = Hash(o.BucketName), o.KeyPrefix, o.PublicBaseUrl,
            NamespaceHash = Hash(JsonSerializer.Serialize(new { o.Endpoint, o.BucketName, o.KeyPrefix, o.PublicBaseUrl })), AccessKeyPresent = true, SecretPresent = true, o.LegacyLocalFallbackEnabled,
            RelevantEnvironmentVariableNames = Environment.GetEnvironmentVariables().Keys.Cast<string>().Where(x => x.StartsWith("R2Storage__", StringComparison.OrdinalIgnoreCase) || x == "ASPNETCORE_ENVIRONMENT").Order().ToArray() });
        var text = Environment.GetEnvironmentVariable("COLLECTION_RELEASE_CONNECTIONSTRING") ?? config["AppSettings:MSSQLConnectionString"];
        var builder = new SqlConnectionStringBuilder(text);
        Need(builder.InitialCatalog == "AssistFlow" && builder.DataSource is "192.168.1.8" or "sql2022", "Yanlış canlı salt-okuma hedefi.");
        builder.ApplicationIntent = ApplicationIntent.ReadOnly; builder.ApplicationName = "CollectionCdnReadOnlySamples"; builder.ConnectTimeout = 10;
        var samples = new List<Sample>();
        await using (var db = new SqlConnection(builder.ConnectionString))
        {
            await db.OpenAsync();
            using var guard = db.CreateCommand(); guard.CommandTimeout = 20;
            guard.CommandText = "SET LOCK_TIMEOUT 5000; IF DB_NAME()<>N'AssistFlow' OR UPPER(CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')))<>N'SQL2022' THROW 51000,N'Yanlış salt-okuma hedefi.',1;";
            await guard.ExecuteNonQueryAsync();
            // Fixed, previously inspected shared infrastructure tables. No discovery SQL, writes or full scans.
            foreach (var type in new[] { "application/pdf", "image/png", "image/jpeg" })
            {
                foreach (var table in new[] { "dbo.WorkflowAttachment", "ykb.YkbWorkflowAttachment", "crm.PurchaseAttachment" })
                {
                    using var cmd = db.CreateCommand(); cmd.CommandTimeout = 20;
                    cmd.CommandText = $"SELECT TOP(1) Id,StoredFileName,ContentType,SizeBytes FROM {table} WHERE ContentType=@type AND SizeBytes BETWEEN 1 AND 5242880 AND StoredFileName NOT LIKE N'%/%' AND StoredFileName NOT LIKE N'%\\%' AND StoredFileName NOT LIKE N'%:%' ORDER BY Id DESC";
                    cmd.Parameters.AddWithValue("@type", type);
                    await using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync()) { samples.Add(new(table, Convert.ToInt64(reader[0]), reader.GetString(1), reader.GetString(2), Convert.ToInt64(reader[3]))); break; }
                }
            }
        }
        await Save(root, "samples.private.json", samples);
        var factory = new ReadOnlyHttpFactory(endpoint.Host, samples.Select(x => "/" + o.BucketName + "/" + o.KeyPrefix + "/" + x.StoredName).ToHashSet(StringComparer.Ordinal));
        using var client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKeyId, o.SecretAccessKey), new AmazonS3Config { ServiceURL = o.Endpoint, ForcePathStyle = true, HttpClientFactory = factory, MaxErrorRetry = 0, Timeout = TimeSpan.FromSeconds(30) });
        var storage = new R2FileStorage(client, Options.Create(o), NullLogger<R2FileStorage>.Instance);
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        var results = new List<object>();
        foreach (var sample in samples)
        {
            var key = o.KeyPrefix + "/" + sample.StoredName;
            Need(Path.GetFileName(sample.StoredName) == sample.StoredName && !sample.StoredName.Contains(':') && !sample.StoredName.Contains('\\'), "Örnek nesne anahtarı güvenli değil.");
            int? originStatus = null, publicStatus = null; string? originHash = null, publicHash = null, originType = null, publicType = null, failure = null; long? originSize = null, publicSize = null;
            string? cacheControl = null, cacheStatus = null, noSniff = null, disposition = null;
            try
            {
                var meta = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = o.BucketName, Key = key }); originStatus = (int)meta.HttpStatusCode;
                using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = o.BucketName, Key = key });
                originType = response.Headers.ContentType;
                (originHash, originSize) = await ReadHashAsync(response.ResponseStream);
            }
            catch (AmazonS3Exception ex) { originStatus = (int)ex.StatusCode; failure = "ORIGIN_" + ex.GetType().Name; }
            catch (Exception ex) { failure = "ORIGIN_" + ex.GetType().Name; }
            try
            {
                var uri = new Uri(storage.GetPublicUrl(sample.StoredName));
                Need(uri.Scheme == "https" && uri.Host == publicBase!.Host && uri.AbsolutePath.StartsWith(publicBase.AbsolutePath.TrimEnd('/') + "/uploads/", StringComparison.Ordinal), "Public örnek URL kapsamı farklı.");
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead); publicStatus = (int)response.StatusCode;
                publicType = response.Content.Headers.ContentType?.MediaType;
                cacheControl = response.Headers.CacheControl?.ToString();
                cacheStatus = response.Headers.TryGetValues("CF-Cache-Status", out var cs) ? string.Join(",", cs) : null;
                noSniff = response.Headers.TryGetValues("X-Content-Type-Options", out var ns) ? string.Join(",", ns) : null;
                disposition = response.Content.Headers.ContentDisposition is null ? null : response.Content.Headers.ContentDisposition.DispositionType;
                if (response.StatusCode == HttpStatusCode.OK)
                { await using var stream = await response.Content.ReadAsStreamAsync(); (publicHash, publicSize) = await ReadHashAsync(stream); }
            }
            catch (Exception ex) { failure = (failure is null ? "" : failure + ";") + "PUBLIC_" + ex.GetType().Name; }
            var matched = originStatus == 200 && publicStatus == 200 && originHash == publicHash && originHash is not null && originSize == sample.Size && publicSize == sample.Size && originType == sample.ContentType && publicType == sample.ContentType;
            results.Add(new { SourceTable = sample.Table, sample.Id, KeyHash = Hash(key), ExpectedSize = sample.Size, ExpectedType = sample.ContentType, OriginStatus = originStatus, OriginType = originType, OriginSize = originSize, OriginSha256 = originHash,
                PublicStatus = publicStatus, PublicType = publicType, PublicSize = publicSize, PublicSha256 = publicHash, AnonymousPublicRead = publicStatus == 200, DefaultHttpsValidationEnabled = true,
                Matched = matched, CacheControl = cacheControl, CloudflareCacheStatus = cacheStatus, XContentTypeOptions = noSniff, ContentDispositionType = disposition, FailureClass = failure });
        }
        await Save(root, "result.json", new { ObservedAtUtc = DateTimeOffset.UtcNow, Scope = "EXISTING_SHARED_CDN_SAMPLES_NOT_COLLECTION_MIGRATION", SampleCount = samples.Count, Results = results,
            AllowedSignedHttpMethods = new[] { "GET", "HEAD" }, ProviderSignedRequests = factory.RequestCount, DatabaseWrites = 0, Uploads = 0, Deletes = 0, BucketListings = 0,
            WritePermissionVerified = false, ActualDeployedConfigurationVerified = false, CollectionFilesMigrated = false, ProductionApplyApproved = false });
        Console.WriteLine($"Salt-okunur CDN kontrolü tamam: {samples.Count} kayıtlı örnek. Yükleme/silme/DB yazması yok; ayrıntılar özel raporda.");
    }
    private static async Task<(string Hash, long Size)> ReadHashAsync(Stream input)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; long size = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); int n;
        while ((n = await input.ReadAsync(buffer, timeout.Token)) > 0) { size += n; Need(size <= 5242880, "Örnek okuma 5 MB sınırını aştı."); hash.AppendData(buffer, 0, n); }
        return (Convert.ToHexString(hash.GetHashAndReset()), size);
    }
}
internal sealed class ReadOnlyHttpFactory(string host, HashSet<string> paths) : Amazon.Runtime.HttpClientFactory
{
    internal int RequestCount;
    public override HttpClient CreateHttpClient(IClientConfig clientConfig) => new(new GuardHandler(host, paths, () => Interlocked.Increment(ref RequestCount)));
    private sealed class GuardHandler : DelegatingHandler
    {
        private readonly string host; private readonly HashSet<string> paths; private readonly Action observed;
        internal GuardHandler(string host, HashSet<string> paths, Action observed) : base(new HttpClientHandler { AllowAutoRedirect = false }) { this.host = host; this.paths = paths; this.observed = observed; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Get && request.Method != HttpMethod.Head || request.RequestUri?.Scheme != "https" || request.RequestUri.Host != host || !paths.Contains(Uri.UnescapeDataString(request.RequestUri.AbsolutePath)))
                throw new Gate("Salt-okunur HTTP sınırı: yalnız onaylı HTTPS hostuna GET/HEAD.");
            observed(); return base.SendAsync(request, ct);
        }
    }
}
