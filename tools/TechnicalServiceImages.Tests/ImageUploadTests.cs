using Business.Interfaces.Storage;
using Business.Utilities;
using Core.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;

internal static class ImageUploadTests
{
    private static int checks;

    public static async Task Main()
    {
        Check(TechnicalServiceImageUploader.Validate(Request(1, "photo.JPG", "image/jpeg")) is null, "JPEG accepted");
        Check(TechnicalServiceImageUploader.Validate(Request(1, "photo.png", "image/png", 5 * 1024 * 1024)) is null, "5 MB accepted");
        Check(TechnicalServiceImageUploader.Validate(Request(1, "photo.png", "image/png", 5 * 1024 * 1024 + 1)) is not null, "over 5 MB rejected");
        Check(TechnicalServiceImageUploader.Validate(Request(1, "file.pdf", "application/pdf")) is not null, "PDF rejected");
        Check(TechnicalServiceImageUploader.Validate(Request(1, "photo.webp", "image/webp")) is not null, "WebP rejected");
        Check(TechnicalServiceImageUploader.Validate(Request(1, "photo.png", "application/octet-stream")) is not null, "invalid MIME rejected");
        Check(TechnicalServiceImageUploader.Validate(Request(1, length: 0)) is not null, "empty file rejected");
        Check(TechnicalServiceImageUploader.Validate(Request(3)) is not null, "invalid category rejected");
        var tooManyForms = Request(2);
        tooManyForms.Files.Add(tooManyForms.Files[0]);
        Check(TechnicalServiceImageUploader.Validate(tooManyForms) is not null, "multiple forms rejected");
        Check(TechnicalServiceImageUploader.Validate(new() { RequestNo = "TEST" }) is not null, "no files rejected");
        Check(TechnicalServiceImageUploader.Validate(new() { Type = TechnicalServiceImageType.Service, Files = Request(1).Files }) is not null, "no request number rejected");

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new ImageContext(new DbContextOptionsBuilder<ImageContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var storage = new FakeStorage();

        async Task<Core.Common.ResponseModel<List<TechnicalServiceImageGetDto>>> Upload(TechnicalServiceImageUploadDto dto, long? parent = 42) =>
            await TechnicalServiceImageUploader.UploadAsync(
                context, storage, dto, _ => Task.FromResult(parent),
                id => (TestImage image) => image.ParentId == id,
                (id, url) => new TestImage { ParentId = id, Url = url },
                image => new TechnicalServiceImageGetDto { Id = image.Id, Url = image.Url },
                NullLogger.Instance);

        var success = await Upload(Request(1));
        Check(success.IsSuccess && success.Data is { Count: 1 } && success.Data[0].Id > 0, "upload returns saved ID");
        Check(success.Data![0].Url.StartsWith("https://test.invalid/"), "upload returns public URL");
        Check(await context.Set<TestImage>().CountAsync() == 1, "image persisted");
        Check((await context.Set<TestImage>().SingleAsync()).Url == storage.Saved[0], "database stores file key");
        var initialSaved = storage.Saved.Count;
        Check(!(await Upload(Request(2))).IsSuccess, "existing form prevents second form");
        Check(storage.Saved.Count == initialSaved, "count rejection does not write storage");
        Check(!(await Upload(Request(1), null)).IsSuccess, "missing request rejected");
        Check(storage.Saved.Count == initialSaved, "missing request does not write storage");

        context.AddRange(Enumerable.Range(0, 9).Select(i => new TestImage { ParentId = 42, Url = $"old-{i}" }));
        await context.SaveChangesAsync();
        Check(!(await Upload(Request(1))).IsSuccess, "10 existing photos reject upload");
        Check(storage.Saved.Count == initialSaved, "photo limit does not write storage");
        Check((await Upload(Request(1), 99)).IsSuccess, "counts are scoped to this request");

        var batch = Request(1);
        batch.Files.Add(batch.Files[0]);
        storage.FailOnSave = storage.Saved.Count + 2;
        var beforeBatch = await context.Set<TestImage>().CountAsync();
        var deletedBefore = storage.Deleted.Count;
        Check(!(await Upload(batch, 100)).IsSuccess, "partial storage failure reported");
        Check(await context.Set<TestImage>().CountAsync() == beforeBatch, "partial failure adds no database rows");
        Check(storage.Deleted.Count == deletedBefore + 1 && storage.Deleted.Last() == storage.Saved.Last(), "only newly uploaded file cleaned up");

        storage.FailOnSave = null;
        context.FailSave = true;
        deletedBefore = storage.Deleted.Count;
        Check(!(await Upload(Request(1), 101)).IsSuccess, "database failure reported");
        Check(storage.Deleted.Count == deletedBefore + 1, "database failure cleans new storage file");
        Check(await context.Set<TestImage>().CountAsync() == beforeBatch, "database failure preserves existing rows");
        Check(!context.ChangeTracker.Entries<TestImage>().Any(e => e.State == EntityState.Added), "failed new entities detached");
        context.FailSave = false;
        Check((await Upload(Request(2), 102)).IsSuccess, "first form uploads after earlier failure");
        Check(!(await Upload(Request(2), 102)).IsSuccess, "second form rejected after first commit");

        Console.WriteLine($"PASS: {checks} upload checks (isolated SQLite and fake storage; no external services).");
    }

    private static TechnicalServiceImageUploadDto Request(int type, string name = "photo.jpg", string contentType = "image/jpeg", int length = 8) => new()
    {
        RequestNo = "TEST-REQUEST",
        Type = (TechnicalServiceImageType)type,
        Files = new() { new FormFile(new MemoryStream(new byte[length]), 0, length, "Files", name) { Headers = new HeaderDictionary(), ContentType = contentType } }
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception($"FAIL: {message}");
        checks++;
    }
}

internal sealed class TestImage
{
    public long Id { get; set; }
    public long ParentId { get; set; }
    public string Url { get; set; } = "";
}

internal sealed class ImageContext(DbContextOptions<ImageContext> options) : DbContext(options)
{
    public bool FailSave { get; set; }
    protected override void OnModelCreating(ModelBuilder builder) => builder.Entity<TestImage>().HasKey(x => x.Id);
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        FailSave ? throw new InvalidOperationException("Simulated database failure") : base.SaveChangesAsync(cancellationToken);
}

internal sealed class FakeStorage : IFileStorage
{
    public List<string> Saved { get; } = new();
    public List<string> Deleted { get; } = new();
    public int? FailOnSave { get; set; }
    public Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (Saved.Count + 1 == FailOnSave) throw new IOException("Simulated storage failure");
        var key = $"test-{Saved.Count + 1}.jpg";
        Saved.Add(key);
        return Task.FromResult(key);
    }
    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default) { Deleted.Add(storedFileName); return Task.CompletedTask; }
    public string GetPublicUrl(string storedFileName) => $"https://test.invalid/{storedFileName}";
    public Task UploadAsync(string storedFileName, Stream content, string? contentType, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> ExistsAsync(string storedFileName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task DeleteManyAsync(IEnumerable<string> storedFileNames, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
