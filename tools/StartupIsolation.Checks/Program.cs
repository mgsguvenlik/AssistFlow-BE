using Business.DependencyResolvers.Autofac;
using Business.Services;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Data.Seeding.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using WebAPI.Middleware;

// Never starts WebAPI/other workers or supplies a real connection/credential.
var passed = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException(name);
    passed++;
    Console.WriteLine("PASS: " + name);
}
ServiceProvider PolicyProvider(Dictionary<string, string?> values)
{
    var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    var services = new ServiceCollection().AddLogging();
    services.AddSingleton<IConfiguration>(config);
    services.AddSingleton<IHostEnvironment>(new Microsoft.Extensions.Hosting.Internal.HostingEnvironment
        { EnvironmentName = "OfflineStartupCheck" });
    // Exercise the actual registration, without resolving/starting hosted services.
    new AutofacBusinessModule().Load(services);
    return services.BuildServiceProvider();
}
using (var defaults = PolicyProvider(new()))
{
    var policy = defaults.GetRequiredService<IOptions<StartupExecutionOptions>>().Value;
    Check("Eksik ayarlar mevcut migration/seed/Manitou davranışını korur",
        policy.MigrationsEnabled && policy.SeedsEnabled && policy.ManitouSyncEnabled);
}
foreach (var invalid in new[] { ("MigrationsEnabled", "yanlis"), ("MigrationEnabled", "false") })
{
    using var provider = PolicyProvider(new() { [$"StartupExecution:{invalid.Item1}"] = invalid.Item2 });
    var rejected = false;
    try { _ = provider.GetRequiredService<IOptions<StartupExecutionOptions>>().Value; }
    catch (InvalidOperationException) { rejected = true; }
    Check("Geçersiz/değişik yazılmış ayar reddedilir: " + invalid.Item1, rejected);
}
foreach (var (migrate, seed) in new[] { (false, false), (false, true), (true, false), (true, true) })
{
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = Array.Empty<string>(), EnvironmentName = "OfflineStartupCheck"
    });
    builder.Configuration.Sources.Clear();
    builder.Logging.ClearProviders();
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["StartupExecution:MigrationsEnabled"] = migrate.ToString(),
        ["StartupExecution:SeedsEnabled"] = seed.ToString(),
        ["StartupExecution:ManitouSyncEnabled"] = "false"
    });
    new AutofacBusinessModule().Load(builder.Services);
    var contexts = 0;
    builder.Services.AddScoped<AppDataContext>(_ =>
    {
        contexts++;
        throw new OfflineBoundaryException(); // Cannot construct/open any actual DB context.
    });
    await using var app = builder.Build(); // No Start/Run, HTTP listener or hosted worker execution.
    var blocked = false;
    try { await app.UseDataSeedingAsync<AppDataContext>(); }
    catch (OfflineBoundaryException) { blocked = true; }
    Check($"Seed yolu izin denetimi migration={migrate}, seed={seed}",
        blocked == (migrate || seed) && contexts == (migrate || seed ? 1 : 0));
    contexts = 0;
    blocked = false;
    try { MigrationApplier.ApplyMigrations(app); }
    catch (OfflineBoundaryException) { blocked = true; }
    Check($"İkinci migration yolu izin denetimi migration={migrate}, seed={seed}",
        blocked == migrate && contexts == (migrate ? 1 : 0));
}

// Only the targeted worker is exercised; its scope is a throwing sentinel,
// so even the enabled-path check cannot reach a real API/DB dependency.
foreach (var enabled in new[] { false, true })
{
    var scopeFactory = new OfflineScopeFactory();
    var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["MSSQLConnectionString"] = "DO_NOT_CONNECT", ["ManitouRunEveryMinutes"] = "0"
    }).Build().Get<AppSettings>()!;
    using var worker = new ManitouStagingSyncBackgroundService(scopeFactory,
        NullLogger<ManitouStagingSyncBackgroundService>.Instance, Options.Create(settings),
        Options.Create(new StartupExecutionOptions { ManitouSyncEnabled = enabled }));
    await worker.StartAsync(CancellationToken.None);
    await worker.StopAsync(CancellationToken.None);
    Check("Manitou ilk tur kontrolü enabled=" + enabled,
        scopeFactory.Attempts == (enabled ? 1 : 0));
}
var rehearsalOptions = new StartupExecutionOptions
    { MigrationsEnabled = false, SeedsEnabled = false, ManitouSyncEnabled = false, IsolatedRehearsalMode = true };
var rehearsalConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AppSettings:MSSQLConnectionString"] = "Server=unused;Database=AssistFlow_Performance_Restore;Application Name=CollectionHostingIsolationRehearsal;Integrated Security=true",
    ["AppSettings:SmsSimulationEnabled"] = "true"
}).Build();
var rehearsalEnvironment = new Microsoft.Extensions.Hosting.Internal.HostingEnvironment { EnvironmentName = "CollectionRehearsal" };
Check("İzole host yalnız prova ortamı/DB/kapalı işlemlerle kabul edilir",
    RehearsalStartupGuard.IsAllowed(rehearsalOptions, rehearsalEnvironment, rehearsalConfig));
rehearsalEnvironment.EnvironmentName = "Production";
Check("İzole host Production ortamında reddedilir", !RehearsalStartupGuard.IsAllowed(rehearsalOptions, rehearsalEnvironment, rehearsalConfig));
rehearsalEnvironment.EnvironmentName = "CollectionRehearsal";
rehearsalConfig["AppSettings:MSSQLConnectionString"] = "Server=unused;Database=AssistFlow;Application Name=CollectionHostingIsolationRehearsal;Integrated Security=true";
Check("İzole host canlı katalogda reddedilir", !RehearsalStartupGuard.IsAllowed(rehearsalOptions, rehearsalEnvironment, rehearsalConfig));
foreach (var isolated in new[] { false, true })
{
    ProbeWorker.Created = 0;
    using var services = new ServiceCollection().BuildServiceProvider();
    using var managed = new RehearsalManagedWorker<ProbeWorker>(services,
        Options.Create(new StartupExecutionOptions { IsolatedRehearsalMode = isolated }),
        NullLogger<RehearsalManagedWorker<ProbeWorker>>.Instance);
    await managed.StartAsync(CancellationToken.None);
    await managed.StopAsync(CancellationToken.None);
    Check("Diğer worker constructor izolasyonu isolated=" + isolated, ProbeWorker.Created == (isolated ? 0 : 1));
}
Console.WriteLine($"{passed} açılış izolasyon kontrolü geçti. Gerçek DB/API/HTTP bağlantısı yok.");

sealed class OfflineBoundaryException : Exception { }
sealed class OfflineScopeFactory : IServiceScopeFactory
{
    public int Attempts { get; private set; }
    public IServiceScope CreateScope()
    {
        Attempts++;
        throw new OfflineBoundaryException();
    }
}
sealed class ProbeWorker : IHostedService
{
    public static int Created;
    public ProbeWorker() => Created++;
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
