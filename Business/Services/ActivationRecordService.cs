using Azure.Core;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Model.Abstractions;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace Business.Services
{
    public class ActivationRecordService : IActivationRecordService
    {

        private readonly IUnitOfWork _uow;
        private readonly IAuthService _auth;
        private readonly IHttpContextAccessor _httpCtx;
        private readonly TypeAdapterConfig _config;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        public ActivationRecordService(IUnitOfWork uow, IAuthService auth, IHttpContextAccessor httpCtx, TypeAdapterConfig config)
        {
            _uow = uow;
            _auth = auth;
            _httpCtx = httpCtx;
            _config = config;
        }

        public async Task LogAsync(WorkFlowActivityRecord entry, CancellationToken ct = default)
        {
            await LogCoreAsync(entry, ct);
        }
        public Task LogAsync(
            WorkFlowActionType type,
            string? requestNo,
            long? workFlowId,
            long? customerId,
            string? fromStepCode,
            string? toStepCode,
            string? summary,
            object? payload,
            CancellationToken ct = default)
        {
            var entry = new WorkFlowActivityRecord
            {
                ActionType = type,
                RequestNo = requestNo,
                WorkFlowId = workFlowId,
                FromStepCode = fromStepCode,
                ToStepCode = toStepCode,
                Summary = summary,
                CustomerId = customerId,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts)
            };

            return LogAsync(entry, ct);
        }

        public async Task LogYkbAsync(YkbWorkFlowActivityRecord entry, CancellationToken ct = default)
        {
            await LogCoreAsync(entry, ct);
        }

        public Task LogYkbAsync(
                WorkFlowActionType type,
                string? requestNo,
                long? workFlowId,
                long? customerId,
                string? fromStepCode,
                string? toStepCode,
                string? summary,
                object? payload,
                CancellationToken ct = default)
                    {
            var entry = new YkbWorkFlowActivityRecord
            {
                ActionType = type,
                RequestNo = requestNo,
                WorkFlowId = workFlowId,
                FromStepCode = fromStepCode,
                ToStepCode = toStepCode,
                Summary = summary,
                CustomerId = customerId,
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts)
            };

            return LogYkbAsync(entry, ct);
        }
    
        public async Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestActivityRecordByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<WorkFlowActivityRecord>();
            return await GetLatestActivityByRequestNoCoreAsync(requestNo, query);
        }
        public async Task<ResponseModel<PagedResult<WorkFlowActivityGroupDto>>> GetUserActivityGroupedByRequestNo(int userId, QueryParams q, int perGroupTake = 50)
        {
            if (userId <= 0)
                return ResponseModel<PagedResult<WorkFlowActivityGroupDto>>
                    .Fail("userId boş olamaz.", StatusCode.BadRequest);

            var baseQuery = _uow.Repository
                .GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.PerformedByUserId == userId);

            return await GetUserActivityGroupedCoreAsync(userId, q, perGroupTake, baseQuery);
        }
        public async Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetUserActivity_(int userId, QueryParams q)
        {
            if (userId <= 0)
                return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>
                    .Fail("userId boş olamaz.", StatusCode.BadRequest);

            var baseQuery = _uow.Repository
                .GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.PerformedByUserId == userId);

            return await GetActivityPageCoreAsync(baseQuery, q, "Kayıt bulunamadı.");
        }
        public async Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetUserActivity(int userId, QueryParams q)
        {
            if (userId <= 0)
                return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>
                    .Fail("userId boş olamaz.", StatusCode.BadRequest);

            var baseQuery = GetCombinedUserActivityQuery(userId);

            // TEntity = ActivityRecordUnion (IActivityRecordEntity implement ediyor)
            return await GetActivityPageCoreAsync(baseQuery, q, "Kayıt bulunamadı.");
        }
        public async Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetCustomerActivity_(int customerId, QueryParams q)
        {
            if (customerId <= 0)
                return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>
                    .Fail("customerId boş olamaz.", StatusCode.BadRequest);

            var baseQuery = _uow.Repository
                .GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId);

            return await GetActivityPageCoreAsync(baseQuery, q, "Kayıt bulunamadı.");
        }
        public async Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetCustomerActivity(int customerId, QueryParams q)
        {
            if (customerId <= 0)
                return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>
                    .Fail("customerId boş olamaz.", StatusCode.BadRequest);

            var baseQuery = GetCombinedCustomerActivityQuery(customerId);

            return await GetActivityPageCoreAsync(baseQuery, q, "Kayıt bulunamadı.");
        }
        #region YKB
        public async Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestYkbActivityRecordByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<YkbWorkFlowActivityRecord>();
            return await GetLatestActivityByRequestNoCoreAsync(requestNo, query);
        }
        #endregion

        private async Task LogCoreAsync<TEntity>(TEntity entry, CancellationToken ct = default)
             where TEntity : class, IActivityRecordEntity
        {
            // Kim?
            var me = (await _auth.MeAsync())?.Data;
            entry.PerformedByUserId = me?.Id;
            entry.PerformedByUserName = me?.TechnicianName ?? me?.Email;

            // İstemci
            var http = _httpCtx.HttpContext;
            entry.ClientIp = http?.Connection?.RemoteIpAddress?.ToString();
            var ua = http?.Request?.Headers["User-Agent"].ToString();
            entry.UserAgent = string.IsNullOrEmpty(ua) ? null : (ua.Length > 200 ? ua[..200] : ua);

            // Zaman
            entry.OccurredAtUtc = DateTime.Now;

            // İzleme
            entry.CorrelationId = http?.TraceIdentifier;

            await _uow.Repository.AddAsync(entry, ct);
            // CompleteAsync dışarıda
        }
   
        private async Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestActivityByRequestNoCoreAsync<TEntity>(
          string requestNo,
          IQueryable<TEntity> query)
          where TEntity : class, IActivityRecordEntity
        {
            if (string.IsNullOrWhiteSpace(requestNo))
                return ResponseModel<List<WorkFlowActivityRecorGetDto>>.Fail(
                    "RequestNo boş olamaz.", StatusCode.BadRequest);

            var rn = requestNo.Trim();
            var rnLower = rn.ToLowerInvariant();

            var entities = await query
                .AsNoTracking()
                .Where(a => a.RequestNo != null && a.RequestNo.ToLower() == rnLower)
                .OrderByDescending(a => a.OccurredAtUtc)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            if (entities.Count == 0)
                return ResponseModel<List<WorkFlowActivityRecorGetDto>>
                    .Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var items = entities.Adapt<List<WorkFlowActivityRecorGetDto>>(_config);
            return ResponseModel<List<WorkFlowActivityRecorGetDto>>.Success(items, "", StatusCode.Ok);
        }


        private async Task<ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>> GetActivityPageCoreAsync<TEntity>(
         IQueryable<TEntity> query,
         QueryParams q,
         string emptyMessage)
         where TEntity : class, IActivityRecordEntity
        {
            var page = q.Page <= 0 ? 1 : q.Page;
            var pageSize = q.PageSize <= 0 ? 20 : Math.Min(q.PageSize, 200);

            // ---- Search
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();

                query = query.Where(a =>
                    (a.RequestNo ?? "").ToLower().Contains(s) ||
                    (a.FromStepCode ?? "").ToLower().Contains(s) ||
                    (a.ToStepCode ?? "").ToLower().Contains(s) ||
                    (a.PerformedByUserName ?? "").ToLower().Contains(s) ||
                    (a.Summary ?? "").ToLower().Contains(s) ||
                    (a.CorrelationId ?? "").ToLower().Contains(s) ||
                    (a.ClientIp ?? "").ToLower().Contains(s) ||
                    (a.UserAgent ?? "").ToLower().Contains(s)
                );
            }

            // ---- Sorting
            var sort = (q.Sort ?? "").Trim().ToLowerInvariant();
            var desc = q.Desc;

            // Burada ActionType enum ama her iki entity’de de aynı enum’u kullanıyoruz.
            query = sort switch
            {
                "id" => desc ? query.OrderByDescending(a => a.Id) : query.OrderBy(a => a.Id),
                "requestno" => desc ? query.OrderByDescending(a => a.RequestNo).ThenByDescending(a => a.Id)
                                    : query.OrderBy(a => a.RequestNo).ThenBy(a => a.Id),
                "fromstepcode" => desc ? query.OrderByDescending(a => a.FromStepCode).ThenByDescending(a => a.Id)
                                       : query.OrderBy(a => a.FromStepCode).ThenBy(a => a.Id),
                "tostepcode" => desc ? query.OrderByDescending(a => a.ToStepCode).ThenByDescending(a => a.Id)
                                     : query.OrderBy(a => a.ToStepCode).ThenBy(a => a.Id),
                "actiontype" => desc ? query.OrderByDescending(a => a.ActionType).ThenByDescending(a => a.Id)
                                     : query.OrderBy(a => a.ActionType).ThenBy(a => a.Id),
                "performedbyuserid" => desc ? query.OrderByDescending(a => a.PerformedByUserId).ThenByDescending(a => a.Id)
                                            : query.OrderBy(a => a.PerformedByUserId).ThenBy(a => a.Id),
                "performedbyusername" => desc ? query.OrderByDescending(a => a.PerformedByUserName).ThenByDescending(a => a.Id)
                                              : query.OrderBy(a => a.PerformedByUserName).ThenBy(a => a.Id),
                "occurredatutc" => desc ? query.OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)
                                        : query.OrderBy(a => a.OccurredAtUtc).ThenBy(a => a.Id),
                "correlationid" => desc ? query.OrderByDescending(a => a.CorrelationId).ThenByDescending(a => a.Id)
                                       : query.OrderBy(a => a.CorrelationId).ThenBy(a => a.Id),
                _ => query.OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)
            };

            var total = await query.CountAsync();
            if (total == 0)
            {
                return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>
                    .Fail(emptyMessage, StatusCode.NotFound);
            }

            var skip = (page - 1) * pageSize;
            var pageEntities = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            var items = pageEntities.Adapt<List<WorkFlowActivityRecorGetDto>>(_config);
            var result = new PagedResult<WorkFlowActivityRecorGetDto>(items, total, page, pageSize);

            return ResponseModel<PagedResult<WorkFlowActivityRecorGetDto>>.Success(result, "", StatusCode.Ok);
        }

        private async Task<ResponseModel<PagedResult<WorkFlowActivityGroupDto>>> GetUserActivityGroupedCoreAsync<TEntity>(
        int userId,
        QueryParams q,
        int perGroupTake,
        IQueryable<TEntity> baseQuery)
        where TEntity : class, IActivityRecordEntity
        {
            var page = q.Page <= 0 ? 1 : q.Page;
            var pageSize = q.PageSize <= 0 ? 20 : Math.Min(q.PageSize, 200);
            perGroupTake = perGroupTake <= 0 ? 50 : Math.Min(perGroupTake, 200);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                baseQuery = baseQuery.Where(a =>
                    (a.RequestNo ?? "").ToLower().Contains(s) ||
                    (a.FromStepCode ?? "").ToLower().Contains(s) ||
                    (a.ToStepCode ?? "").ToLower().Contains(s) ||
                    (a.PerformedByUserName ?? "").ToLower().Contains(s) ||
                    (a.Summary ?? "").ToLower().Contains(s) ||
                    (a.CorrelationId ?? "").ToLower().Contains(s) ||
                    (a.ClientIp ?? "").ToLower().Contains(s) ||
                    (a.UserAgent ?? "").ToLower().Contains(s)
                );
            }

            var groupSummaries = baseQuery
                .GroupBy(a => a.RequestNo)
                .Select(g => new
                {
                    RequestNo = g.Key,
                    Count = g.Count(),
                    LastOccurredAtUtc = g.Max(x => x.OccurredAtUtc)
                });

            var sort = (q.Sort ?? "").Trim().ToLowerInvariant();
            var desc = q.Desc;

            IOrderedQueryable<dynamic> orderedGroups = sort switch
            {
                "requestno" => desc
                    ? groupSummaries.OrderByDescending(x => x.RequestNo)
                    : groupSummaries.OrderBy(x => x.RequestNo),

                "count" => desc
                    ? groupSummaries.OrderByDescending(x => x.Count).ThenByDescending(x => x.LastOccurredAtUtc)
                    : groupSummaries.OrderBy(x => x.Count).ThenBy(x => x.LastOccurredAtUtc),

                "lastoccurredatutc" => desc
                    ? groupSummaries.OrderByDescending(x => x.LastOccurredAtUtc)
                    : groupSummaries.OrderBy(x => x.LastOccurredAtUtc),

                _ => groupSummaries.OrderByDescending(x => x.LastOccurredAtUtc)
            };

            var totalGroups = await groupSummaries.CountAsync();
            if (totalGroups == 0)
                return ResponseModel<PagedResult<WorkFlowActivityGroupDto>>
                    .Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var skip = (page - 1) * pageSize;
            var groupPage = await orderedGroups.Skip(skip).Take(pageSize).ToListAsync();
            var requestNosOnPage = groupPage.Select(g => (string?)g.RequestNo).ToList();

            var pageItemsQuery = baseQuery
                .Where(a => requestNosOnPage.Contains(a.RequestNo))
                .OrderByDescending(a => a.OccurredAtUtc)
                .ThenByDescending(a => a.Id);

            var pageItems = await pageItemsQuery.ToListAsync();
            var mapped = pageItems.Adapt<List<WorkFlowActivityRecorGetDto>>(_config);

            var groupedDtos = requestNosOnPage
                .Select(rn =>
                {
                    var gsum = groupPage.First(x => x.RequestNo == rn);
                    var itemsForGroup = mapped
                        .Where(m => m.RequestNo == rn)
                        .Take(perGroupTake)
                        .ToList()
                        .AsReadOnly();

                    return new WorkFlowActivityGroupDto(
                        rn,
                        gsum.Count,
                        gsum.LastOccurredAtUtc,
                        itemsForGroup
                    );
                })
                .ToList()
                .AsReadOnly();

            var result = new PagedResult<WorkFlowActivityGroupDto>(groupedDtos, totalGroups, page, pageSize);
            return ResponseModel<PagedResult<WorkFlowActivityGroupDto>>.Success(result, "", StatusCode.Ok);
        }

        private IQueryable<ActivityRecordUnion> GetCombinedUserActivityQuery(int userId)
        {
            var normal = _uow.Repository
                .GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.PerformedByUserId == userId)
                .Select(a => new ActivityRecordUnion
                {
                    Id = a.Id,
                    RequestNo = a.RequestNo,
                    ActionType = a.ActionType,
                    FromStepCode = a.FromStepCode,
                    ToStepCode = a.ToStepCode,
                    OccurredAtUtc = a.OccurredAtUtc,
                    PerformedByUserId = a.PerformedByUserId,
                    PerformedByUserName = a.PerformedByUserName,
                    ClientIp = a.ClientIp,
                    UserAgent = a.UserAgent,
                    CorrelationId = a.CorrelationId,
                    CustomerId = a.CustomerId,
                    Summary = a.Summary,
                    PayloadJson = a.PayloadJson,
                    WorkFlowId = a.WorkFlowId,
                    IsYkb = false
                });

            var ykb = _uow.Repository
                .GetQueryable<YkbWorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.PerformedByUserId == userId)
                .Select(a => new ActivityRecordUnion
                {
                    Id = a.Id,
                    RequestNo = a.RequestNo,
                    ActionType = a.ActionType,
                    FromStepCode = a.FromStepCode,
                    ToStepCode = a.ToStepCode,
                    OccurredAtUtc = a.OccurredAtUtc,
                    PerformedByUserId = a.PerformedByUserId,
                    PerformedByUserName = a.PerformedByUserName,
                    ClientIp = a.ClientIp,
                    UserAgent = a.UserAgent,
                    CorrelationId = a.CorrelationId,
                    CustomerId = a.CustomerId,
                    Summary = a.Summary,
                    PayloadJson = a.PayloadJson,
                    WorkFlowId = a.WorkFlowId,
                    IsYkb = true
                });

            // EF Core bunu UNION ALL (Concat) olarak SQL'e çevirir
            return normal.Concat(ykb);
        }

        private IQueryable<ActivityRecordUnion> GetCombinedCustomerActivityQuery(int customerId)
        {
            var normal = _uow.Repository
                .GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .Select(a => new ActivityRecordUnion
                {
                    Id = a.Id,
                    RequestNo = a.RequestNo,
                    ActionType = a.ActionType,
                    FromStepCode = a.FromStepCode,
                    ToStepCode = a.ToStepCode,
                    OccurredAtUtc = a.OccurredAtUtc,
                    PerformedByUserId = a.PerformedByUserId,
                    PerformedByUserName = a.PerformedByUserName,
                    ClientIp = a.ClientIp,
                    UserAgent = a.UserAgent,
                    CorrelationId = a.CorrelationId,
                    CustomerId = a.CustomerId,
                    Summary = a.Summary,
                    PayloadJson = a.PayloadJson,
                    WorkFlowId = a.WorkFlowId,
                    IsYkb = false
                });

            var ykb = _uow.Repository
                .GetQueryable<YkbWorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.CustomerId == customerId)
                .Select(a => new ActivityRecordUnion
                {
                    Id = a.Id,
                    RequestNo = a.RequestNo,
                    ActionType = a.ActionType,
                    FromStepCode = a.FromStepCode,
                    ToStepCode = a.ToStepCode,
                    OccurredAtUtc = a.OccurredAtUtc,
                    PerformedByUserId = a.PerformedByUserId,
                    PerformedByUserName = a.PerformedByUserName,
                    ClientIp = a.ClientIp,
                    UserAgent = a.UserAgent,
                    CorrelationId = a.CorrelationId,
                    CustomerId = a.CustomerId,
                    Summary = a.Summary,
                    PayloadJson = a.PayloadJson,
                    WorkFlowId = a.WorkFlowId,
                    IsYkb = true
                });

            return normal.Concat(ykb);
        }

        private class ActivityRecordUnion : IActivityRecordEntity
        {
            public long Id { get; set; }
            public string? RequestNo { get; set; }
            public WorkFlowActionType ActionType { get; set; }

            public string? FromStepCode { get; set; }
            public string? ToStepCode { get; set; }

            public DateTime OccurredAtUtc { get; set; }

            public long? PerformedByUserId { get; set; }
            public string? PerformedByUserName { get; set; }

            public string? ClientIp { get; set; }
            public string? UserAgent { get; set; }

            public string? CorrelationId { get; set; }

            public long? CustomerId { get; set; }

            public string? Summary { get; set; }
            public string? PayloadJson { get; set; }

            public long? WorkFlowId { get; set; }

            // İleride işine yarar diye flag de ekledim (isteğe bağlı)
            public bool IsYkb { get; set; }
        }
    }

    
}
