using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Logging;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.WorkFlowSlaSetting;
using System.Linq.Expressions;

namespace Business.Services
{
    public class WorkFlowSlaSettingService
        : CrudServiceBase<WorkFlowSlaSetting, long, WorkFlowSlaSettingCreateDto, WorkFlowSlaSettingUpdateDto, WorkFlowSlaSettingGetDto>,
          IWorkFlowSlaSettingService
    {
        private readonly ILogger<WorkFlowSlaSettingService> _logger;

        public WorkFlowSlaSettingService(
            IUnitOfWork uow,
            IMapper mapper,
            TypeAdapterConfig config,
            ILogger<WorkFlowSlaSettingService> logger)
            : base(uow, mapper, config)
        {
            _logger = logger;
        }

        protected override long ReadKey(WorkFlowSlaSetting e) => e.Id;

        protected override Expression<Func<WorkFlowSlaSetting, bool>> KeyPredicate(long id)
            => x => x.Id == id;

        protected override async Task<WorkFlowSlaSetting?> ResolveEntityForUpdateAsync(WorkFlowSlaSettingUpdateDto dto)
        {
            if (dto.Id <= 0) return null;

            var entity = await _unitOfWork.Repository.GetByIdAsync<WorkFlowSlaSetting>(
                asNoTracking: false,
                id: dto.Id);

            if (entity != null) return entity;
            else return null;
        }

        // ===== CREATE Override (Validation Ekleme) =====
        public override async Task<ResponseModel<WorkFlowSlaSettingGetDto>> CreateAsync(WorkFlowSlaSettingCreateDto dto)
        {
            // 🔹 Validation: Aynı CustomerType + Priority kombinasyonu var mı?
            var exists = await _unitOfWork.Repository.AnyAsync<WorkFlowSlaSetting>(
                x => x.CustomerType == dto.CustomerType && x.Priority == dto.Priority);

            if (exists)
            {
                return ResponseModel<WorkFlowSlaSettingGetDto>.Fail(
                    $"{dto.CustomerType} müşteri tipi ve {dto.Priority} önceliği için zaten bir SLA ayarı mevcut",
                    Core.Enums.StatusCode.Conflict);
            }

            // 🔹 Validation: NotificationBeforeDays < SlaDurationDays
            if (dto.NotificationBeforeDays >= dto.SlaDurationDays)
            {
                return ResponseModel<WorkFlowSlaSettingGetDto>.Fail(
                    "Bildirim süresi, SLA süresinden küçük olmalıdır",
                    Core.Enums.StatusCode.BadRequest);
            }

            // 🔹 Base create'i çağır
            var result = await base.CreateAsync(dto);

            // 🔹 Başarılıysa log at
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "SLA ayarı oluşturuldu. CustomerType: {CustomerType}, Priority: {Priority}",
                    dto.CustomerType, dto.Priority);
            }

            return result;
        }

        // ===== UPDATE Override (Validation Ekleme) =====
        public override async Task<ResponseModel<WorkFlowSlaSettingGetDto>> UpdateAsync(WorkFlowSlaSettingUpdateDto dto)
        {
            // 🔹 Entity'yi bul
            var entity = await ResolveEntityForUpdateAsync(dto);
            if (entity == null)
            {
                return ResponseModel<WorkFlowSlaSettingGetDto>.Fail(
                    "SLA ayarı bulunamadı",
                    Core.Enums.StatusCode.NotFound);
            }

            // 🔹 Validation: CustomerType veya Priority değiştiriliyorsa tekil kontrol
            if (entity.CustomerType != dto.CustomerType || entity.Priority != dto.Priority)
            {
                var exists = await _unitOfWork.Repository.AnyAsync<WorkFlowSlaSetting>(
                    x => x.CustomerType == dto.CustomerType
                      && x.Priority == dto.Priority
                      && x.Id != dto.Id);

                if (exists)
                {
                    return ResponseModel<WorkFlowSlaSettingGetDto>.Fail(
                        $"{dto.CustomerType} müşteri tipi ve {dto.Priority} önceliği için zaten bir SLA ayarı mevcut",
                        Core.Enums.StatusCode.Conflict);
                }
            }

            // 🔹 Validation: NotificationBeforeDays < SlaDurationDays
            if (dto.NotificationBeforeDays >= dto.SlaDurationDays)
            {
                return ResponseModel<WorkFlowSlaSettingGetDto>.Fail(
                    "Bildirim süresi, SLA süresinden küçük olmalıdır",
                    Core.Enums.StatusCode.BadRequest);
            }

            // 🔹 Base update'i çağır
            var result = await base.UpdateAsync(dto);

            // 🔹 Başarılıysa log at
            if (result.IsSuccess)
            {
                _logger.LogInformation("SLA ayarı güncellendi. Id: {Id}", dto.Id);
            }

            return result;
        }

        // ===== GetPagedAsync Override (Enum String Dönüşümü) =====
        public override async Task<ResponseModel<PagedResult<WorkFlowSlaSettingGetDto>>> GetPagedAsync(QueryParams q)
        {
            var result = await base.GetPagedAsync(q);

            // 🔹 Enum değerlerini string'e çevir
            if (result.IsSuccess && result.Data?.Items != null)
            {
                foreach (var item in result.Data.Items)
                {
                    item.CustomerTypeName = item.CustomerType.ToString();
                    item.PriorityName = item.Priority.ToString();
                }
            }

            return result;
        }

        // ===== GetByIdAsync Override (Enum String Dönüşümü) =====
        public override async Task<ResponseModel<WorkFlowSlaSettingGetDto>> GetByIdAsync(long id)
        {
            var result = await base.GetByIdAsync(id);

            // 🔹 Enum değerlerini string'e çevir
            if (result.IsSuccess && result.Data != null)
            {
                result.Data.CustomerTypeName = result.Data.CustomerType.ToString();
                result.Data.PriorityName = result.Data.Priority.ToString();
            }

            return result;
        }

        // ===== IWorkFlowSlaSettingService Custom Method =====
        public async Task<ResponseModel<WorkFlowSlaSetting?>> GetSlaSettingAsync(WorkFlowCustomerType customerType, WorkFlowPriority priority)
        {
            try
            {
                var result = await _unitOfWork.Repository.GetSingleAsync<WorkFlowSlaSetting>(
                    asNoTracking: false,
                    whereExpression: x => x.CustomerType == customerType
                                       && x.Priority == priority
                                       && x.IsActive);

                if (result == null)
                {
                    return ResponseModel<WorkFlowSlaSetting?>.Fail(
                        $"{customerType} müşteri tipi ve {priority} önceliği için aktif SLA ayarı bulunamadı",
                        Core.Enums.StatusCode.NotFound);
                }

                return ResponseModel<WorkFlowSlaSetting?>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SLA ayarı alınırken hata oluştu. CustomerType: {CustomerType}, Priority: {Priority}",
                    customerType, priority);

                return ResponseModel<WorkFlowSlaSetting?>.Fail(
                    "SLA ayarı alınırken bir hata oluştu",
                    Core.Enums.StatusCode.Error);
            }
        }
    }
}