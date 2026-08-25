using Business.Interfaces;
using Business.Interfaces.PeriodicReports;
using Business.Models;
using Core.Enums;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete.PeriodicReports;
using System.Net;

namespace Business.Services.PeriodicReports
{
    public sealed class ReportExecutionService : IReportExecutionService
    {
        private readonly AppDataContext _db;
        private readonly IReportQueryExecutor _queryExecutor;
        private readonly IReadOnlyDictionary<PeriodicReportOutputFormat, IReportExporter> _exporters;
        private readonly IPeriodicReportScheduleCalculator _scheduleCalculator;
        private readonly IMailService _mailService;
        private readonly PeriodicReportOptions _options;
        private readonly ILogger<ReportExecutionService> _logger;

        public ReportExecutionService(
            AppDataContext db,
            IReportQueryExecutor queryExecutor,
            IEnumerable<IReportExporter> exporters,
            IPeriodicReportScheduleCalculator scheduleCalculator,
            IMailService mailService,
            IOptions<PeriodicReportOptions> options,
            ILogger<ReportExecutionService> logger)
        {
            _db = db;
            _queryExecutor = queryExecutor;
            _exporters = exporters.ToDictionary(x => x.Format);
            _scheduleCalculator = scheduleCalculator;
            _mailService = mailService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ReportExecutionOutcome> ExecuteAsync(
            long reportId,
            PeriodicReportTriggerType triggerType,
            long? triggeredByUserId,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.UtcNow;
            var leaseUntil = now.AddMinutes(Math.Max(5, _options.ExecutionLeaseMinutes));
            var claimQuery = _db.PeriodicReports.Where(x =>
                x.Id == reportId &&
                !x.IsDeleted &&
                (x.LeaseExpiresAtUtc == null || x.LeaseExpiresAtUtc <= now));

            if (triggerType == PeriodicReportTriggerType.Scheduled)
            {
                claimQuery = claimQuery.Where(x =>
                    x.IsActive &&
                    x.NextRunAtUtc != null &&
                    x.NextRunAtUtc <= now);
            }

            var claimed = await claimQuery.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.LeaseExpiresAtUtc, leaseUntil)
                .SetProperty(x => x.LastRunAtUtc, now), cancellationToken);

            if (claimed == 0)
            {
                return new ReportExecutionOutcome(
                    Acquired: false,
                    ExecutionId: null,
                    Status: null,
                    Message: "Rapor bulunamadı, zamanı gelmedi veya başka bir işlem tarafından çalıştırılıyor.");
            }

            PeriodicReportExecution? execution = null;
            PeriodicReport? report = null;

            try
            {
                report = await _db.PeriodicReports
                    .AsNoTracking()
                    .Include(x => x.Recipients)
                    .FirstAsync(x => x.Id == reportId, cancellationToken);

                execution = new PeriodicReportExecution
                {
                    PeriodicReportId = report.Id,
                    StartedAtUtc = now,
                    Status = PeriodicReportExecutionStatus.Running,
                    OutputFormat = report.OutputFormat,
                    MailRecipientCount = report.Recipients.Count,
                    TriggerType = triggerType,
                    TriggeredByUserId = triggeredByUserId,
                    CreatedDate = now
                };
                _db.PeriodicReportExecutions.Add(execution);
                await _db.SaveChangesAsync(cancellationToken);

                var data = await _queryExecutor.ExecuteAsync(
                    report.SqlQuery,
                    Math.Max(1, _options.MaxRows),
                    allowTruncation: false,
                    cancellationToken);

                if (!_exporters.TryGetValue(report.OutputFormat, out var exporter))
                    throw new InvalidOperationException("Seçilen çıktı formatı için exporter bulunamadı.");

                var file = await exporter.ExportAsync(report.Name, data, cancellationToken);
                var maxAttachmentBytes = Math.Max(1, _options.MaxAttachmentSizeMb) * 1024L * 1024L;
                if (file.Content.LongLength > maxAttachmentBytes)
                {
                    throw new InvalidOperationException(
                        $"Üretilen dosya {Math.Max(1, _options.MaxAttachmentSizeMb)} MB attachment sınırını aşıyor.");
                }

                var recipients = report.Recipients
                    .Select(x => x.EmailAddress)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var localTime = TimeZoneInfo.ConvertTime(
                    now,
                    ResolveTimeZone(report.TimeZoneId));
                var subject = $"[{report.Name}] - {localTime:dd.MM.yyyy}";
                var body = $"<p><strong>{WebUtility.HtmlEncode(report.Name)}</strong> ekte yer almaktadır.</p>" +
                           $"<p>Rapor Tarihi: {localTime:dd.MM.yyyy HH:mm}<br/>" +
                           $"Kayıt Sayısı: {data.Rows.Count:N0}</p>";

                await _mailService.SendWithAttachmentAsync(
                    recipients,
                    subject,
                    body,
                    new MailAttachmentData(file.FileName, file.ContentType, file.Content),
                    cancellationToken);

                var completedAt = DateTimeOffset.UtcNow;
                execution.Status = PeriodicReportExecutionStatus.Success;
                execution.CompletedAtUtc = completedAt;
                execution.RowCount = data.Rows.Count;
                execution.FileName = file.FileName;
                execution.FileSize = file.Content.LongLength;
                await _db.SaveChangesAsync(cancellationToken);

                await UpdateReportAfterExecutionAsync(
                    report,
                    triggerType,
                    succeeded: true,
                    completedAt,
                    errorMessage: null,
                    cancellationToken);

                return new ReportExecutionOutcome(
                    Acquired: true,
                    ExecutionId: execution.Id,
                    Status: execution.Status,
                    Message: "Rapor oluşturuldu ve e-posta ile gönderildi.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await TryRecordFailureAsync(report, execution, triggerType, "Rapor çalışması iptal edildi.", CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                var safeError = SanitizeError(ex.Message);
                _logger.LogError(ex, "Periyodik rapor çalışması başarısız. ReportId={ReportId}", reportId);
                await TryRecordFailureAsync(report, execution, triggerType, safeError, CancellationToken.None);

                return new ReportExecutionOutcome(
                    Acquired: true,
                    ExecutionId: execution?.Id,
                    Status: PeriodicReportExecutionStatus.Failed,
                    Message: safeError);
            }
            finally
            {
                await _db.PeriodicReports
                    .Where(x => x.Id == reportId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(x => x.LeaseExpiresAtUtc, (DateTimeOffset?)null),
                        CancellationToken.None);
            }
        }

        private async Task TryRecordFailureAsync(
            PeriodicReport? report,
            PeriodicReportExecution? execution,
            PeriodicReportTriggerType triggerType,
            string errorMessage,
            CancellationToken cancellationToken)
        {
            var failedAt = DateTimeOffset.UtcNow;
            if (execution != null)
            {
                execution.Status = PeriodicReportExecutionStatus.Failed;
                execution.CompletedAtUtc = failedAt;
                execution.ErrorMessage = errorMessage;
                await _db.SaveChangesAsync(cancellationToken);
            }

            if (report != null)
            {
                await UpdateReportAfterExecutionAsync(
                    report,
                    triggerType,
                    succeeded: false,
                    failedAt,
                    errorMessage,
                    cancellationToken);
            }
        }

        private async Task UpdateReportAfterExecutionAsync(
            PeriodicReport report,
            PeriodicReportTriggerType triggerType,
            bool succeeded,
            DateTimeOffset completedAt,
            string? errorMessage,
            CancellationToken cancellationToken)
        {
            DateTimeOffset? nextRun = report.NextRunAtUtc;
            if (report.IsActive &&
                (triggerType == PeriodicReportTriggerType.Scheduled || !nextRun.HasValue))
            {
                nextRun = _scheduleCalculator.GetNextOccurrenceUtc(
                    report.CronExpression,
                    report.TimeZoneId,
                    completedAt);
            }

            if (succeeded)
            {
                await _db.PeriodicReports
                    .Where(x => x.Id == report.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.LastSuccessAtUtc, completedAt)
                        .SetProperty(x => x.LastErrorMessage, (string?)null)
                        .SetProperty(x => x.NextRunAtUtc, nextRun), cancellationToken);
            }
            else
            {
                await _db.PeriodicReports
                    .Where(x => x.Id == report.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.LastErrorAtUtc, completedAt)
                        .SetProperty(x => x.LastErrorMessage, errorMessage)
                        .SetProperty(x => x.NextRunAtUtc, nextRun), cancellationToken);
            }
        }

        private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException) when (
                timeZoneId.Equals("Europe/Istanbul", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
        }

        private static string SanitizeError(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "Bilinmeyen rapor çalıştırma hatası.";

            var singleLine = message.ReplaceLineEndings(" ").Trim();
            return singleLine.Length <= 4000 ? singleLine : singleLine[..4000];
        }
    }
}
