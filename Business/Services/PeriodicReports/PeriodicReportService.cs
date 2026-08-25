using Business.Interfaces;
using Business.Interfaces.PeriodicReports;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Data.Concrete.EfCore.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Model.Concrete.PeriodicReports;
using Model.Dtos.PeriodicReports;
using System.Net.Mail;

namespace Business.Services.PeriodicReports
{
    public sealed class PeriodicReportService : IPeriodicReportService
    {
        private readonly AppDataContext _db;
        private readonly ICurrentUser _currentUser;
        private readonly IReportSqlValidator _sqlValidator;
        private readonly IReportQueryExecutor _queryExecutor;
        private readonly IPeriodicReportScheduleCalculator _scheduleCalculator;
        private readonly PeriodicReportOptions _options;

        public PeriodicReportService(
            AppDataContext db,
            ICurrentUser currentUser,
            IReportSqlValidator sqlValidator,
            IReportQueryExecutor queryExecutor,
            IPeriodicReportScheduleCalculator scheduleCalculator,
            IOptions<PeriodicReportOptions> options)
        {
            _db = db;
            _currentUser = currentUser;
            _sqlValidator = sqlValidator;
            _queryExecutor = queryExecutor;
            _scheduleCalculator = scheduleCalculator;
            _options = options.Value;
        }

        public async Task<ResponseModel<PagedResult<PeriodicReportListItemDto>>> GetPagedAsync(
            QueryParams query,
            CancellationToken cancellationToken)
        {
            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var reportsQuery = _db.PeriodicReports
                .AsNoTracking()
                .Include(x => x.Recipients)
                .Include(x => x.Executions)
                .Where(x => !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                reportsQuery = reportsQuery.Where(x =>
                    x.Name.Contains(search) ||
                    (x.Description != null && x.Description.Contains(search)));
            }

            var total = await reportsQuery.CountAsync(cancellationToken);
            var entities = await reportsQuery
                .OrderByDescending(x => x.UpdatedDate ?? x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            return ResponseModel<PagedResult<PeriodicReportListItemDto>>.Success(
                new PagedResult<PeriodicReportListItemDto>(
                    entities.Select(MapListItem).ToList(),
                    total,
                    page,
                    pageSize));
        }

        public async Task<ResponseModel<PeriodicReportDetailDto>> GetByIdAsync(
            long id,
            CancellationToken cancellationToken)
        {
            var entity = await GetEntityAsync(id, asTracking: false, cancellationToken);
            return entity == null
                ? ResponseModel<PeriodicReportDetailDto>.Fail("Periyodik rapor bulunamadı.", StatusCode.NotFound)
                : ResponseModel<PeriodicReportDetailDto>.Success(MapDetail(entity));
        }

        public async Task<ResponseModel<PeriodicReportDetailDto>> CreateAsync(
            PeriodicReportUpsertDto dto,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(dto, out var emails, out var timeZoneId);
            if (validationError != null)
                return ResponseModel<PeriodicReportDetailDto>.Fail(validationError, StatusCode.BadRequest);

            var normalizedName = dto.Name.Trim();
            if (await _db.PeriodicReports.AnyAsync(
                x => !x.IsDeleted && x.Name == normalizedName,
                cancellationToken))
            {
                return ResponseModel<PeriodicReportDetailDto>.Fail(
                    "Aynı adla aktif bir periyodik rapor zaten var.",
                    StatusCode.Conflict);
            }

            var me = await _currentUser.GetAsync(cancellationToken);
            if (me == null || me.Id <= 0)
                return ResponseModel<PeriodicReportDetailDto>.Fail("Kullanıcı bilgisi bulunamadı.", StatusCode.Unauthorized);

            var now = DateTimeOffset.UtcNow;
            var entity = new PeriodicReport
            {
                Name = normalizedName,
                Description = NormalizeNullable(dto.Description),
                SqlQuery = dto.SqlQuery.Trim(),
                OutputFormat = dto.OutputFormat!.Value,
                CronExpression = dto.CronExpression.Trim(),
                TimeZoneId = timeZoneId,
                IsActive = dto.IsActive,
                NextRunAtUtc = dto.IsActive
                    ? _scheduleCalculator.GetNextOccurrenceUtc(dto.CronExpression, timeZoneId, now)
                    : null,
                CreatedDate = now,
                CreatedUser = me.Id,
                IsDeleted = false,
                Recipients = emails.Select(email => new PeriodicReportRecipient
                {
                    EmailAddress = email
                }).ToList()
            };

            _db.PeriodicReports.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return ResponseModel<PeriodicReportDetailDto>.Success(
                MapDetail(entity),
                "Periyodik rapor oluşturuldu.",
                StatusCode.Created);
        }

        public async Task<ResponseModel<PeriodicReportDetailDto>> UpdateAsync(
            long id,
            PeriodicReportUpsertDto dto,
            CancellationToken cancellationToken)
        {
            var validationError = Validate(dto, out var emails, out var timeZoneId);
            if (validationError != null)
                return ResponseModel<PeriodicReportDetailDto>.Fail(validationError, StatusCode.BadRequest);

            var entity = await GetEntityAsync(id, asTracking: true, cancellationToken);
            if (entity == null)
                return ResponseModel<PeriodicReportDetailDto>.Fail("Periyodik rapor bulunamadı.", StatusCode.NotFound);

            if (entity.LeaseExpiresAtUtc > DateTimeOffset.UtcNow)
                return ResponseModel<PeriodicReportDetailDto>.Fail("Çalışan rapor tamamlanmadan tanım güncellenemez.", StatusCode.Conflict);

            var normalizedName = dto.Name.Trim();
            if (await _db.PeriodicReports.AnyAsync(
                x => x.Id != id && !x.IsDeleted && x.Name == normalizedName,
                cancellationToken))
            {
                return ResponseModel<PeriodicReportDetailDto>.Fail(
                    "Aynı adla aktif bir periyodik rapor zaten var.",
                    StatusCode.Conflict);
            }

            var me = await _currentUser.GetAsync(cancellationToken);
            if (me == null || me.Id <= 0)
                return ResponseModel<PeriodicReportDetailDto>.Fail("Kullanıcı bilgisi bulunamadı.", StatusCode.Unauthorized);

            entity.Name = normalizedName;
            entity.Description = NormalizeNullable(dto.Description);
            entity.SqlQuery = dto.SqlQuery.Trim();
            entity.OutputFormat = dto.OutputFormat!.Value;
            entity.CronExpression = dto.CronExpression.Trim();
            entity.TimeZoneId = timeZoneId;
            entity.IsActive = dto.IsActive;
            entity.NextRunAtUtc = dto.IsActive
                ? _scheduleCalculator.GetNextOccurrenceUtc(dto.CronExpression, timeZoneId, DateTimeOffset.UtcNow)
                : null;
            entity.UpdatedDate = DateTimeOffset.UtcNow;
            entity.UpdatedUser = me.Id;

            _db.PeriodicReportRecipients.RemoveRange(entity.Recipients);
            entity.Recipients = emails.Select(email => new PeriodicReportRecipient
            {
                PeriodicReportId = entity.Id,
                EmailAddress = email
            }).ToList();

            await _db.SaveChangesAsync(cancellationToken);
            return ResponseModel<PeriodicReportDetailDto>.Success(MapDetail(entity), "Periyodik rapor güncellendi.");
        }

        public async Task<ResponseModel<bool>> DeleteAsync(long id, CancellationToken cancellationToken)
        {
            var entity = await _db.PeriodicReports
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
            if (entity == null)
                return ResponseModel<bool>.Fail("Periyodik rapor bulunamadı.", StatusCode.NotFound, false);

            if (entity.LeaseExpiresAtUtc > DateTimeOffset.UtcNow)
                return ResponseModel<bool>.Fail("Çalışan rapor tamamlanmadan silinemez.", StatusCode.Conflict, false);

            var me = await _currentUser.GetAsync(cancellationToken);
            if (me == null || me.Id <= 0)
                return ResponseModel<bool>.Fail("Kullanıcı bilgisi bulunamadı.", StatusCode.Unauthorized, false);

            entity.IsDeleted = true;
            entity.IsActive = false;
            entity.NextRunAtUtc = null;
            entity.UpdatedDate = DateTimeOffset.UtcNow;
            entity.UpdatedUser = me.Id;
            await _db.SaveChangesAsync(cancellationToken);

            return ResponseModel<bool>.Success(true, "Periyodik rapor silindi.", StatusCode.Ok);
        }

        public async Task<ResponseModel<DynamicReportDataDto>> PreviewAsync(
            PeriodicReportPreviewRequestDto dto,
            CancellationToken cancellationToken)
        {
            var validation = _sqlValidator.Validate(dto.SqlQuery);
            if (!validation.IsValid)
            {
                return ResponseModel<DynamicReportDataDto>.Fail(
                    string.Join(" ", validation.Errors),
                    StatusCode.BadRequest);
            }

            try
            {
                var result = await _queryExecutor.ExecuteAsync(
                    dto.SqlQuery,
                    Math.Clamp(_options.PreviewMaxRows, 1, 500),
                    allowTruncation: true,
                    cancellationToken);

                return ResponseModel<DynamicReportDataDto>.Success(new DynamicReportDataDto
                {
                    Columns = result.Columns,
                    Rows = result.Rows,
                    IsTruncated = result.IsTruncated
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return ResponseModel<DynamicReportDataDto>.Fail(
                    $"Sorgu önizlemesi başarısız: {SanitizeError(ex.Message)}",
                    StatusCode.BadRequest);
            }
        }

        public async Task<ResponseModel<PagedResult<PeriodicReportExecutionDto>>> GetExecutionsAsync(
            long id,
            QueryParams query,
            CancellationToken cancellationToken)
        {
            if (!await _db.PeriodicReports.AnyAsync(x => x.Id == id && !x.IsDeleted, cancellationToken))
                return ResponseModel<PagedResult<PeriodicReportExecutionDto>>.Fail("Periyodik rapor bulunamadı.", StatusCode.NotFound);

            var page = Math.Max(1, query.Page);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var executions = _db.PeriodicReportExecutions
                .AsNoTracking()
                .Where(x => x.PeriodicReportId == id);
            var total = await executions.CountAsync(cancellationToken);
            var items = await executions
                .OrderByDescending(x => x.StartedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new PeriodicReportExecutionDto
                {
                    Id = x.Id,
                    PeriodicReportId = x.PeriodicReportId,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    Status = x.Status,
                    RowCount = x.RowCount,
                    OutputFormat = x.OutputFormat,
                    FileName = x.FileName,
                    FileSize = x.FileSize,
                    MailRecipientCount = x.MailRecipientCount,
                    ErrorMessage = x.ErrorMessage,
                    TriggerType = x.TriggerType,
                    TriggeredByUserId = x.TriggeredByUserId
                })
                .ToListAsync(cancellationToken);

            return ResponseModel<PagedResult<PeriodicReportExecutionDto>>.Success(
                new PagedResult<PeriodicReportExecutionDto>(items, total, page, pageSize));
        }

        private string? Validate(
            PeriodicReportUpsertDto dto,
            out List<string> emails,
            out string timeZoneId)
        {
            emails = dto.RecipientEmails
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .ToList();
            timeZoneId = string.IsNullOrWhiteSpace(dto.TimeZoneId)
                ? _options.TimeZoneId
                : dto.TimeZoneId.Trim();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return "Rapor adı zorunludur.";
            if (string.IsNullOrWhiteSpace(dto.SqlQuery))
                return "SQL sorgusu zorunludur.";
            if (!dto.OutputFormat.HasValue || !Enum.IsDefined(dto.OutputFormat.Value))
                return "Çıktı formatı geçersiz.";
            if (emails.Count == 0)
                return "En az bir alıcı e-posta adresi zorunludur.";
            if (emails.Count > 100)
                return "Bir rapor için en fazla 100 alıcı tanımlanabilir.";
            if (emails.Distinct(StringComparer.OrdinalIgnoreCase).Count() != emails.Count)
                return "Aynı e-posta adresi birden fazla eklenemez.";
            if (emails.Any(email => !MailAddress.TryCreate(email, out _)))
                return "Geçersiz e-posta adresi var.";

            var sqlValidation = _sqlValidator.Validate(dto.SqlQuery);
            if (!sqlValidation.IsValid)
                return string.Join(" ", sqlValidation.Errors);

            if (!_scheduleCalculator.IsValid(dto.CronExpression, timeZoneId, out var scheduleError))
                return scheduleError;

            return null;
        }

        private async Task<PeriodicReport?> GetEntityAsync(
            long id,
            bool asTracking,
            CancellationToken cancellationToken)
        {
            var query = _db.PeriodicReports
                .Include(x => x.Recipients)
                .Include(x => x.Executions)
                .Where(x => x.Id == id && !x.IsDeleted);

            if (!asTracking)
                query = query.AsNoTracking();

            return await query.AsSplitQuery().FirstOrDefaultAsync(cancellationToken);
        }

        private static PeriodicReportListItemDto MapListItem(PeriodicReport entity)
        {
            var lastExecution = entity.Executions.OrderByDescending(x => x.StartedAtUtc).FirstOrDefault();
            return new PeriodicReportListItemDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Description = entity.Description,
                OutputFormat = entity.OutputFormat,
                CronExpression = entity.CronExpression,
                TimeZoneId = entity.TimeZoneId,
                IsActive = entity.IsActive,
                NextRunAtUtc = entity.NextRunAtUtc,
                LastRunAtUtc = entity.LastRunAtUtc,
                LastSuccessAtUtc = entity.LastSuccessAtUtc,
                LastErrorAtUtc = entity.LastErrorAtUtc,
                LastErrorMessage = entity.LastErrorMessage,
                LastExecutionStatus = lastExecution?.Status,
                RecipientCount = entity.Recipients.Count
            };
        }

        private static PeriodicReportDetailDto MapDetail(PeriodicReport entity)
        {
            var list = MapListItem(entity);
            return new PeriodicReportDetailDto
            {
                Id = list.Id,
                Name = list.Name,
                Description = list.Description,
                OutputFormat = list.OutputFormat,
                CronExpression = list.CronExpression,
                TimeZoneId = list.TimeZoneId,
                IsActive = list.IsActive,
                NextRunAtUtc = list.NextRunAtUtc,
                LastRunAtUtc = list.LastRunAtUtc,
                LastSuccessAtUtc = list.LastSuccessAtUtc,
                LastErrorAtUtc = list.LastErrorAtUtc,
                LastErrorMessage = list.LastErrorMessage,
                LastExecutionStatus = list.LastExecutionStatus,
                RecipientCount = list.RecipientCount,
                SqlQuery = entity.SqlQuery,
                RecipientEmails = entity.Recipients.Select(x => x.EmailAddress).OrderBy(x => x).ToList(),
                CreatedDate = entity.CreatedDate,
                CreatedUser = entity.CreatedUser,
                UpdatedDate = entity.UpdatedDate,
                UpdatedUser = entity.UpdatedUser
            };
        }

        private static string? NormalizeNullable(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string SanitizeError(string message) =>
            string.IsNullOrWhiteSpace(message)
                ? "Bilinmeyen hata."
                : message.Length <= 1000 ? message : message[..1000];
    }
}
