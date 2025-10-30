using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Model.Concrete.WorkFlows;
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
            entry.OccurredAtUtc = DateTime.UtcNow;

            // İzleme
            entry.CorrelationId = http?.TraceIdentifier;

            await _uow.Repository.AddAsync(entry, ct);
            // CompleteAsync çağrısını dışarıya bırakıyoruz (aynı transaction'da kalsın)
        }

        public Task LogAsync(
            WorkFlowActionType type,
            string? requestNo,
            long? workFlowId,
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
                PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload, JsonOpts)
            };

            return LogAsync(entry, ct);
        }

        public async Task<ResponseModel<List<WorkFlowActivityRecorGetDto>>> GetLatestActivityRecordByRequestNoAsync(string requestNo)
        {
            if (string.IsNullOrWhiteSpace(requestNo))
                return ResponseModel<List<WorkFlowActivityRecorGetDto>>.Fail("RequestNo boş olamaz.", StatusCode.BadRequest);

            var rn = requestNo.Trim();
            var rnLower = rn.ToLowerInvariant();

            // Not: ToLowerInvariant() SQL'e çevrilebilir (SQL Server / PostgreSQL). 
            // İndeks kullanımı etkilenmesin istiyorsan, DB kolasyonunu case-insensitive seçebilirsin.
            var entities = await _uow.Repository.GetQueryable<WorkFlowActivityRecord>()
                .AsNoTracking()
                .Where(a => a.RequestNo != null && a.RequestNo.ToLower() == rnLower)
                .OrderByDescending(a => a.OccurredAtUtc)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            if (entities.Count == 0)
                return ResponseModel<List<WorkFlowActivityRecorGetDto>>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // EF translation riskini kaldırmak için bellekte map’le
            var items = entities.Adapt<List<WorkFlowActivityRecorGetDto>>(_config);

            return ResponseModel<List<WorkFlowActivityRecorGetDto>>.Success(items, "", StatusCode.Ok);
        }
    }
}
