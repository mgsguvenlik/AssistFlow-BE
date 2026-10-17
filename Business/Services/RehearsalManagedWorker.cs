using Core.Settings.Concrete;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Business.Services;

// Prevent even constructor side effects of unrelated workers during a bounded host rehearsal.
public sealed class RehearsalManagedWorker<T>(IServiceProvider services,
    IOptions<StartupExecutionOptions> options, ILogger<RehearsalManagedWorker<T>> logger)
    : IHostedService, IDisposable where T : class, IHostedService
{
    private T? _worker;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Value.IsolatedRehearsalMode)
        {
            logger.LogWarning("İzole prova: {Worker} oluşturulmadı/başlatılmadı.", typeof(T).Name);
            return;
        }
        _worker = ActivatorUtilities.CreateInstance<T>(services);
        await _worker.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        _worker?.StopAsync(cancellationToken) ?? Task.CompletedTask;

    public void Dispose() => (_worker as IDisposable)?.Dispose();
}

public static class RehearsalStartupGuard
{
    public static bool IsAllowed(StartupExecutionOptions options, IHostEnvironment environment,
        IConfiguration configuration)
    {
        if (!options.IsolatedRehearsalMode) return true;
        if (environment.EnvironmentName != "CollectionRehearsal" || options.MigrationsEnabled ||
            options.SeedsEnabled || options.ManitouSyncEnabled ||
            configuration.GetValue<bool>("CollectionRead:Enabled") ||
            configuration.GetValue<bool>("CollectionRead:ContractCreateEnabled") ||
            configuration.GetValue<bool>("AppSettings:CollectionSmsEnabled") ||
            !configuration.GetValue<bool>("AppSettings:SmsSimulationEnabled")) return false;
        try
        {
            var connection = new SqlConnectionStringBuilder(configuration["AppSettings:MSSQLConnectionString"]);
            return connection.InitialCatalog == "AssistFlow_Performance_Restore" &&
                connection.ApplicationName == "CollectionHostingIsolationRehearsal";
        }
        catch (ArgumentException) { return false; }
    }
}
