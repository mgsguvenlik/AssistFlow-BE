using System.Text.Json;
using System.Data.Common;
using Business.Services.Crm.Collections;
using Core.Common;
using Data.Concrete.EfCore.Context;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Model.Concrete;
using Model.Concrete.Collections;
using Model.Dtos.Crm.Collections;

var browserFixture = args.Length == 3 && args[0] is ("--prepare-browser-contract" or "--cleanup-browser-contract");
if (!browserFixture && (args.Length != 2 || args[0] is not ("--apply-test-fixtures" or "--seed-definitions" or "--read-definitions" or "--read-tracking" or "--read-balance" or "--read-migration-staging" or "--create-contract-fixtures")))
    throw new InvalidOperationException("Yalnız AssistFlowTest için --apply-test-fixtures veya --seed-definitions ve Development JSON yolu gereklidir.");
using var config = JsonDocument.Parse(File.ReadAllText(args[1]), new JsonDocumentOptions
    { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
var connection = new SqlConnectionStringBuilder(config.RootElement.GetProperty("AppSettings")
    .GetProperty("MSSQLConnectionString").GetString());
if (connection.DataSource != "192.168.1.8" || connection.InitialCatalog != "AssistFlowTest")
    throw new InvalidOperationException("Test hedefi izin verilen AssistFlowTest değil.");
AppDataContext Context(IInterceptor? interceptor = null)
{
    var options = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString,
        sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(2), null));
    if (interceptor is not null) options.AddInterceptors(interceptor);
    return new AppDataContext(options.Options);
}
if (args[0] == "--read-migration-staging")
{
    await using var context = Context();
    var tableCount = await context.Database.SqlQueryRaw<int>("""
        SELECT COUNT(*) AS [Value] FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = 'collection' AND t.name LIKE 'Migration%'
        """).SingleAsync();
    var rowCount = await context.Set<CollectionMigrationSourceRow>().AsNoTracking().CountAsync()
        + await context.Set<CollectionMigrationBatch>().AsNoTracking().CountAsync()
        + await context.Set<CollectionMigrationIssue>().AsNoTracking().CountAsync()
        + await context.Set<CollectionMigrationMap>().AsNoTracking().CountAsync();
    if (tableCount != 7 || rowCount != 0)
        throw new InvalidOperationException($"Staging doğrulaması başarısız: tablo={tableCount}, kayıt={rowCount}.");
    Console.WriteLine("Staging doğrulandı: 7 tablo, 0 aktarım kaydı.");
    return;
}
if (browserFixture)
{
    await CollectionBrowserFixture.RunAsync(() => Context(), args[0] == "--cleanup-browser-contract", args[2]);
    return;
}
if (args[0] == "--create-contract-fixtures")
{
    await CollectionContractCreateTests.RunAsync(interceptor =>
    {
        var settings = new DbContextOptionsBuilder<AppDataContext>().UseSqlServer(connection.ConnectionString);
        if (interceptor is not null) settings.AddInterceptors(interceptor);
        return new AppDataContext(settings.Options);
    });
    return;
}
if (args[0] == "--read-definitions")
{
    await using var db = Context();
    var reader = new CollectionDefinitionReadService(new Business.UnitOfWork.UnitOfWork(new Data.Concrete.Repository(db)));
    foreach (var kind in Enum.GetValues<CollectionDefinitionKind>())
    {
        var full = await reader.GetPageAsync(new() { Kind = kind, PageSize = 100 });
        var first = await reader.GetPageAsync(new() { Kind = kind, PageSize = 1 });
        var second = await reader.GetPageAsync(new() { Kind = kind, PageSize = 1, Page = 2 });
        if (full.Data is null || first.Data is null || second.Data is null
            || !full.Data.Items.Take(1).Select(x => x.Id).SequenceEqual(first.Data.Items.Select(x => x.Id))
            || !full.Data.Items.Skip(1).Take(1).Select(x => x.Id).SequenceEqual(second.Data.Items.Select(x => x.Id))
            || full.Data.TotalCount != first.Data.TotalCount)
            throw new InvalidOperationException("Tanım sayfalaması doğrulanamadı.");
        Console.WriteLine($"PASS: {kind} SQL okuma ve sayfalama; toplam {full.Data.TotalCount}");
    }
    if ((int)(await reader.GetPageAsync(new() { Kind = (CollectionDefinitionKind)99 })).StatusCode != 400
        || (int)(await reader.GetPageAsync(new() { PageSize = 101 })).StatusCode != 400)
        throw new InvalidOperationException("Tanım doğrulaması başarısız.");
    Console.WriteLine("PASS: geçersiz tür ve sayfa boyutu reddedildi. Veritabanı değiştirilmedi.");
    return;
}
if (args[0] == "--read-tracking")
{
    await using var db = Context();
    var trackingService = new CollectionTrackingService(new Business.UnitOfWork.UnitOfWork(new Data.Concrete.Repository(db)));
    var result = await trackingService.GetPageAsync(new() { Period = new DateOnly(2026, 9, 1), PageSize = 1 });
    if (result.Data is null) throw new InvalidOperationException(result.Message);
    var grouped = await trackingService.GetPageAsync(new()
        { Period = new DateOnly(2026, 9, 1), PageSize = 1, View = CollectionFollowUpView.Group });
    if (grouped.Data is null) throw new InvalidOperationException(grouped.Message);
    Console.WriteLine($"Tahsilat takip sorguları çalıştı; bireysel {result.Data.TotalCount}, grup {grouped.Data.TotalCount} kayıt.");
    return;
}
if (args[0] == "--read-balance")
{
    await using var db = Context();
    var contractId = await db.Set<CollectionContract>().AsNoTracking().Where(x => !x.IsDeleted)
        .OrderBy(x => x.Id).Select(x => x.Id).FirstOrDefaultAsync();
    if (contractId == 0) { Console.WriteLine("Bakiye kontrolü için sözleşme yok; DB değiştirilmedi."); return; }
    var readService = new CollectionContractReadService(db,
        new Business.UnitOfWork.UnitOfWork(new Data.Concrete.Repository(db)));
    var period = new DateOnly(2026, 9, 1);
    var single = await readService.GetBalanceAsync(contractId, new() { Period = period, AsOfDate = new DateOnly(2026, 9, 15) });
    var carry = await readService.GetBalanceAsync(contractId, new()
        { Period = period, AsOfDate = new DateOnly(2026, 9, 15), IncludeCarryOver = true });
    if (single.Data is null || carry.Data is null) throw new InvalidOperationException(single.Data is null ? single.Message : carry.Message);
    Console.WriteLine($"Bakiye sorguları çalıştı; sözleşme {contractId}, dönem {single.Data.Items.Count}, devirli {carry.Data.Items.Count} para birimi.");
    return;
}
if (args[0] == "--seed-definitions")
{
    await CollectionDefinitionSeedTests.RunAsync(() => Context());
    return;
}
var passed = 0;
void Check(string name, bool success)
{
    if (!success) throw new InvalidOperationException(name);
    Console.WriteLine($"PASS: {name}");
    passed++;
}
var requests = new List<Guid>();
Guid Key() { var key = Guid.NewGuid(); requests.Add(key); return key; }
async Task<ResponseModel<CollectionPaymentCommitResult>> Execute(Guid key, CollectionPaymentCommand command,
    long actor = 1, IInterceptor? interceptor = null)
{
    await using var db = Context(interceptor);
    return await new CollectionPaymentTransaction(db).ExecuteAsync(key, actor, command);
}
await using var setup = Context();
var customer = await setup.Set<Customer>().AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
var service = await setup.Set<ServiceType>().AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
var currency = await setup.Set<CurrencyType>().AsNoTracking().OrderBy(x => x.Id).Select(x => x.Id).FirstAsync();
var marker = "SQLTEST-" + Guid.NewGuid().ToString("N");
var contract = new CollectionContract { CustomerId = customer, ServiceTypeId = service,
    StartDate = new DateOnly(2026, 9, 1), GtsNo = marker, CreatedUser = 1, CreatedDate = DateTimeOffset.UtcNow };
setup.Add(contract);
await setup.SaveChangesAsync();
Console.WriteLine($"Test sözleşmesi: {contract.Id}; işaret: {marker}");
try
{
    var command = new CollectionPaymentCommand(CollectionPaymentOperationKind.Create, null, null,
        contract.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11), 123.45m, currency, marker, false);
    var key = Key();
    var created = await Execute(key, command);
    Check("Ödeme ve makbuz oluşturulur", created.Data is { Replayed: false });
    var paymentId = created.Data!.PaymentId;
    Check("Aynı istek aynı ödeme ile tekrar yanıtlanır", (await Execute(key, command)).Data is { Replayed: true } replay
        && replay.PaymentId == paymentId);
    Check("Değişen içerik reddedilir", (int)(await Execute(key, command with { Amount = 999m })).StatusCode == 409);
    Check("Başka kullanıcı aynı anahtarı kullanamaz", (int)(await Execute(key, command, 2)).StatusCode == 409);
    await using var verify = Context();
    var payment = await verify.Set<CollectionPayment>().AsNoTracking().SingleAsync(x => x.Id == paymentId);
    var update = command with { Kind = CollectionPaymentOperationKind.Update, PaymentId = paymentId,
        ExpectedRowVersion = Convert.ToBase64String(payment.RowVersion), Amount = 234.56m };
    Check("Güncel rowversion ile güncelleme başarılı", (await Execute(Key(), update)).Data is not null);
    Check("Eski rowversion reddedilir", (int)(await Execute(Key(), update)).StatusCode == 409);
    var rollbackKey = Key();
    var interrupted = false;
    try { await Execute(rollbackKey, command, interceptor: new FailReceiptSave()); }
    catch (InjectedFailure) { interrupted = true; }
    Check("Ödeme yazıldıktan sonra makbuz hatası enjekte edildi", interrupted);
    Check("Makbuz hatasında ödeme rollback olur", await verify.Set<CollectionPayment>().CountAsync(x => x.ContractId == contract.Id) == 1);
    Check("Başarısız işlemin makbuzu kalmaz", !await verify.Set<CollectionPaymentOperation>().AnyAsync(x => x.RequestId == rollbackKey));
    Check("Rollback sonrası aynı anahtar kullanılabilir", (await Execute(rollbackKey, command)).Data is { Replayed: false });
    var commitKey = Key();
    var lostAcknowledgement = new LoseCommitAcknowledgement();
    var recovered = await Execute(commitKey, command, interceptor: lostAcknowledgement);
    Check("Commit sonrası yanıt kaybı retry ile makbuzdan çözülür", lostAcknowledgement.Injected
        && recovered.Data is { Replayed: true });
    Check("Commit retry tek makbuz üretir", await verify.Set<CollectionPaymentOperation>().CountAsync(x => x.RequestId == commitKey) == 1);
    var raceKey = Key();
    var barrier = new ConcurrentSaveBarrier();
    var race = await Task.WhenAll(Execute(raceKey, command, interceptor: barrier), Execute(raceKey, command, interceptor: barrier));
    Check("İki işlem aynı anda yazma sınırına ulaştı", barrier.Arrivals >= 2);
    Check("Eşzamanlı aynı anahtar tek ödeme üretir", await verify.Set<CollectionPaymentOperation>().CountAsync(x => x.RequestId == raceKey) == 1
        && race.Any(x => x.Data is not null));
    Check("Yarış sonrası tekrar güvenli", (await Execute(raceKey, command)).Data is { Replayed: true });
    Check("Retry ve yarışlar sahipsiz veya çift ödeme bırakmaz",
        await verify.Set<CollectionPayment>().CountAsync(x => x.ContractId == contract.Id) == 4);
    payment = await verify.Set<CollectionPayment>().AsNoTracking().SingleAsync(x => x.Id == paymentId);
    var deleteKey = Key();
    var delete = new CollectionPaymentCommand(CollectionPaymentOperationKind.Delete, paymentId,
        Convert.ToBase64String(payment.RowVersion), null, null, null, null, null, null, null);
    Check("Fiziksel silme başarılı", (await Execute(deleteKey, delete)).Data is not null);
    Check("Ödeme silinir makbuz korunur", !await verify.Set<CollectionPayment>().AnyAsync(x => x.Id == paymentId)
        && await verify.Set<CollectionPaymentOperation>().AnyAsync(x => x.RequestId == deleteKey && x.BeforeJson != null && x.AfterJson == null));
    Check("Silme tekrarı başarılı", (await Execute(deleteKey, delete)).Data is { Replayed: true });
    Check("Eski oluşturma isteği silinen ödemeyi yeniden yaratmaz", (await Execute(key, command)).Data is { Replayed: true }
        && !await verify.Set<CollectionPayment>().AnyAsync(x => x.Id == paymentId));
    Console.WriteLine($"{passed} SQL transaction kontrolü geçti.");
}
finally
{
    // Exact fixture ownership only; never clear a table or alter shared lookup rows.
    await using var cleanup = Context();
    await cleanup.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await cleanup.Database.BeginTransactionAsync();
        if (!await cleanup.Set<CollectionContract>().AnyAsync(x => x.Id == contract.Id && x.GtsNo == marker))
            throw new InvalidOperationException("Test kaydı sahipliği doğrulanamadı; temizlik durduruldu.");
        await cleanup.Set<CollectionPaymentOperation>().Where(x => requests.Contains(x.RequestId)).ExecuteDeleteAsync();
        await cleanup.Set<CollectionPayment>().Where(x => x.ContractId == contract.Id).ExecuteDeleteAsync();
        await cleanup.Set<CollectionContract>().Where(x => x.Id == contract.Id && x.GtsNo == marker).ExecuteDeleteAsync();
        await transaction.CommitAsync();
    });
    Console.WriteLine("Yalnız bu çalışmanın test sözleşmesi, ödemeleri ve makbuzları temizlendi.");
}

sealed class InjectedFailure : Exception;
sealed class LoseCommitAcknowledgement : DbTransactionInterceptor
{
    public bool Injected { get; private set; }
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!Injected)
        {
            Injected = true;
            throw new TimeoutException("Test: commit tamamlandıktan sonra yanıt kaybı.");
        }
        return Task.CompletedTask;
    }
}
sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
{
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;
    public int Arrivals => arrivals;
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<CollectionPayment>().Any(x => x.State == EntityState.Added))
        {
            if (Interlocked.Increment(ref arrivals) >= 2) ready.TrySetResult();
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
        }
        return result;
    }
}
sealed class FailReceiptSave : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<CollectionPaymentOperation>().Any(x => x.State == EntityState.Added))
            throw new InjectedFailure();
        return ValueTask.FromResult(result);
    }
}
