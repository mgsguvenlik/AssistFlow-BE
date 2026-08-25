using Business.Interfaces.PeriodicReports;
using Core.Enums;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Business.Services.PeriodicReports
{
    public sealed class PeriodicReportScheduler : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PeriodicReportOptions _options;
        private readonly ILogger<PeriodicReportScheduler> _logger;

        public PeriodicReportScheduler(
            IServiceScopeFactory scopeFactory,
            IOptions<PeriodicReportOptions> options,
            ILogger<PeriodicReportScheduler> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await InitializeMissingSchedulesAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var dueIds = await GetDueReportIdsAsync(stoppingToken);
                    foreach (var reportId in dueIds)
                    {
                        if (stoppingToken.IsCancellationRequested)
                            break;

                        try
                        {
                            await using var scope = _scopeFactory.CreateAsyncScope();
                            var executor = scope.ServiceProvider.GetRequiredService<IReportExecutionService>();
                            await executor.ExecuteAsync(
                                reportId,
                                PeriodicReportTriggerType.Scheduled,
                                triggeredByUserId: null,
                                stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Scheduled periyodik rapor çalıştırılamadı. ReportId={ReportId}", reportId);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PeriodicReportScheduler döngü hatası.");
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Clamp(_options.SchedulerPollSeconds, 5, 300)),
                    stoppingToken);
            }
        }

        private async Task<List<long>> GetDueReportIdsAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
            var now = DateTimeOffset.UtcNow;
            return await db.PeriodicReports
                .AsNoTracking()
                .Where(x =>
                    !x.IsDeleted &&
                    x.IsActive &&
                    x.NextRunAtUtc != null &&
                    x.NextRunAtUtc <= now &&
                    (x.LeaseExpiresAtUtc == null || x.LeaseExpiresAtUtc <= now))
                .OrderBy(x => x.NextRunAtUtc)
                .Take(Math.Clamp(_options.SchedulerBatchSize, 1, 100))
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }

        private async Task InitializeMissingSchedulesAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDataContext>();
            var calculator = scope.ServiceProvider.GetRequiredService<IPeriodicReportScheduleCalculator>();
            var reports = await db.PeriodicReports
                .Where(x => !x.IsDeleted && x.IsActive && x.NextRunAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var report in reports)
            {
                try
                {
                    report.NextRunAtUtc = calculator.GetNextOccurrenceUtc(
                        report.CronExpression,
                        report.TimeZoneId,
                        DateTimeOffset.UtcNow);
                }
                catch (Exception ex)
                {
                    report.IsActive = false;
                    report.LastErrorAtUtc = DateTimeOffset.UtcNow;
                    report.LastErrorMessage = "Cron ifadesi başlatılırken doğrulanamadı.";
                    _logger.LogError(ex, "Periyodik rapor schedule initialization başarısız. ReportId={ReportId}", report.Id);
                }
            }

            if (reports.Count > 0)
                await db.SaveChangesAsync(cancellationToken);
        }
    }
}
