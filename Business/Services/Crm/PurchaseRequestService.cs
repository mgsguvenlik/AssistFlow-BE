using Business.Interfaces;
using Business.Interfaces.Crm;
using Business.Interfaces.Storage;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Enums.Crm;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Core.Utilities.IoC;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Crm;
using Model.Dtos.Auth;
using Model.Dtos.Crm.PurchaseAttachment;
using Model.Dtos.Crm.PurchaseRequest;
using Model.Dtos.Crm.PurchaseRequestAction;
using Model.Dtos.Crm.PurchaseRequestHistory;
using Model.Dtos.Crm.PurchaseRequestItem;
using Model.Dtos.Crm.PurchaseRequestStep;
using Model.Dtos.Crm.PurchaseRequestTask;

namespace Business.Services.Crm
{
    public sealed class PurchaseRequestService : IPurchaseRequestService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IFileStorage _fileStorage;
        private readonly IMapper _mapper;
        private readonly TypeAdapterConfig _config;
        private readonly ILogger<PurchaseRequestService> _logger;
        private readonly IMailPushService _mailPushService;
        private readonly IUserService _userService;
        private readonly IOptionsSnapshot<AppSettings> _appSettings;

        /*
         * Bu kodların crm.PurchaseRequestStep seed/config kayıtlarında
         * kullanılan Code alanlarıyla aynı olması gerekir.
         */
        private static class StepCodes
        {
            public const string Draft = "DRAFT";
            public const string ManagerFirstApproval = "MANAGER_FIRST_APPROVAL";
            public const string PurchasingResearch = "PURCHASING_RESEARCH";
            public const string RequesterResearchReview = "REQUESTER_RESEARCH_REVIEW";
            public const string ManagerSecondApproval = "MANAGER_SECOND_APPROVAL";
            public const string ManagementApproval = "MANAGEMENT_APPROVAL";
            public const string Procurement = "PROCUREMENT";
            public const string WarehouseControl = "WAREHOUSE_CONTROL";
            public const string Accounting = "ACCOUNTING";
            public const string RequesterDeliveryControl = "REQUESTER_DELIVERY_CONTROL";
            public const string InvoiceControl = "INVOICE_CONTROL";
            public const string Completed = "COMPLETED";
            public const string Rejected = "REJECTED";
            public const string Cancelled = "CANCELLED";
        }

        private static class ActionCodes
        {
            public const string Submit = "SUBMIT";
            public const string Approve = "APPROVE";
            public const string Cancel = "CANCEL";
        }


        public PurchaseRequestService(
           IUnitOfWork uow,
           ICurrentUser currentUser,
           IFileStorage fileStorage,
           IMapper mapper,
           TypeAdapterConfig config,
           ILogger<PurchaseRequestService> logger,
           IMailPushService mailPushService,
           IUserService userService,
           IOptionsSnapshot<AppSettings> appSettings)
        {
            _uow = uow;
            _currentUser = currentUser;
            _fileStorage = fileStorage;
            _mapper = mapper;
            _config = config;
            _logger = logger;
            _mailPushService = mailPushService;
            _userService = userService;
            _appSettings = appSettings;
        }


        // =====================================================
        // REQUEST
        // =====================================================

        public async Task<ResponseModel<PurchaseRequestGetDto>> CreateAsync(PurchaseRequestCreateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                if (dto == null)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        "Satın alma talebi bilgisi boş olamaz.",
                        StatusCode.BadRequest);
                }

                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var currentUser = currentUserResult.Data!;

                var entity = _mapper.Map<PurchaseRequest>(dto);
                var tenantId = dto.TenantId;
                var validationResult = await ValidateRequestRelationsAsync(entity, cancellationToken);

                if (!validationResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        validationResult.Message,
                        validationResult.StatusCode);
                }

                var initialStep = await _uow.Repository
                    .GetQueryable<PurchaseRequestStep>()
                    .AsNoTracking()
                    .Where(x => x.IsInitial && x.IsActive)
                    .OrderBy(x => x.OrderNo)
                    .FirstOrDefaultAsync(cancellationToken);

                if (initialStep == null)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail("Satın alma workflow başlangıç adımı bulunamadı.", StatusCode.BadRequest);
                }


                entity.Id = 0;
                entity.RequesterUserId = currentUser.Id;
                entity.TenantId = tenantId;
                entity.Status = GetPurchaseStatus("Draft");
                entity.CurrentStepId = initialStep.Id;
                entity.ClosedDate = null;

                /*
                 * ID oluşmadan önce RequestNo üretmemek için
                 * geçici unique değer.
                 */
                entity.RequestNo = $"TMP-{Guid.NewGuid():N}";

                ApplyCreateAudit(entity, currentUser.Id);

                await _uow.Repository.AddAsync(entity, cancellationToken);

                await _uow.Repository.CompleteAsync(cancellationToken);

                /*
                 * DB-generated Id ile okunabilir ve unique
                 * SAT-2026-000001 benzeri numara.
                 */
                entity.RequestNo = $"SAT-{DateTime.Now:yyyy}-{entity.Id:D6}";

                ApplyUpdateAudit(entity, currentUser.Id);

                await _uow.Repository.CompleteAsync(cancellationToken);

                var created = await GetRequestDtoAsync(
                    entity.Id,
                    cancellationToken);

                if (created == null)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                return ResponseModel<PurchaseRequestGetDto>.Success(
                    created,
                    Messages.Created,
                    StatusCode.Created);
            }
            catch (DbUpdateException ex)
            {
                return ResponseModel<PurchaseRequestGetDto>.Fail(
                    $"{Messages.DatabaseError}: {ex.GetBaseException().Message}",
                    StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestGetDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<PurchaseRequestGetDto>> UpdateAsync(PurchaseRequestUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                if (dto == null)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        "Satın alma talebi bilgisi boş olamaz.",
                        StatusCode.BadRequest);
                }

                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var currentUser = currentUserResult.Data!;

                var mapped = _mapper.Map<PurchaseRequest>(dto);

                var entity = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .FirstOrDefaultAsync(x => x.Id == dto.Id, cancellationToken);

                if (entity == null)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                /*
                 * Talep ana bilgileri yalnızca:
                 * - Draft
                 * - RevisionRequired
                 * durumlarında talep sahibi tarafından değiştirilebilir.
                 */
                if (!IsEditableByRequester(entity, currentUser.Id))
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        "Bu satın alma talebi mevcut durumda düzenlenemez.",
                        StatusCode.BadRequest);
                }

                /*
                 * Sistem tarafından yönetilen değerleri koru.
                 */
                var id = entity.Id;
                var requestNo = entity.RequestNo;
                var requesterUserId = entity.RequesterUserId;
                var tenantId = entity.TenantId;
                var status = entity.Status;
                var currentStepId = entity.CurrentStepId;
                var closedDate = entity.ClosedDate;

                _mapper.Map(dto, entity);

                entity.Id = id;
                entity.RequestNo = requestNo;
                entity.RequesterUserId = requesterUserId;
                entity.TenantId = tenantId;
                entity.Status = status;
                entity.CurrentStepId = currentStepId;
                entity.ClosedDate = closedDate;

                var validationResult = await ValidateRequestRelationsAsync(entity, cancellationToken);

                if (!validationResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestGetDto>.Fail(
                        validationResult.Message,
                        validationResult.StatusCode);
                }

                ApplyUpdateAudit(entity, currentUser.Id);

                await _uow.Repository.CompleteAsync(cancellationToken);

              
                var updated = await GetRequestDtoAsync(entity.Id, cancellationToken);

                return updated == null
                    ? ResponseModel<PurchaseRequestGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound)
                    : ResponseModel<PurchaseRequestGetDto>.Success(
                        updated,
                        Messages.Updated);
            }
            catch (DbUpdateConcurrencyException)
            {
                return ResponseModel<PurchaseRequestGetDto>.Fail(
                    Messages.ConflictError,
                    StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestGetDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<bool>> CancelAsync(long id, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<bool>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode,
                        false);
                }

                var currentUser = currentUserResult.Data!;

                var request = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .Include(x => x.CurrentStep)
                    .FirstOrDefaultAsync(
                        x => x.Id == id,
                        cancellationToken);

                if (request == null)
                {
                    return ResponseModel<bool>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound,
                        false);
                }

                if (request.RequesterUserId != currentUser.Id)
                {
                    return ResponseModel<bool>.Fail(
                        "Yalnızca talep sahibi satın alma talebini iptal edebilir.",
                        StatusCode.BadRequest,
                        false);
                }

                if (!IsStatus(
                        request.Status,
                        "Draft",
                        "RevisionRequired"))
                {
                    return ResponseModel<bool>.Fail(
                        "Bu talep mevcut durumda iptal edilemez.",
                        StatusCode.BadRequest,
                        false);
                }

                if (request.CurrentStepId == null)
                {
                    return ResponseModel<bool>.Fail(
                        "Talebin mevcut workflow adımı bulunamadı.",
                        StatusCode.BadRequest,
                        false);
                }

                var cancelStep = await FindStepByCodeAsync(
                    StepCodes.Cancelled,
                    cancellationToken);

                if (cancelStep == null)
                {
                    return ResponseModel<bool>.Fail(
                        $"Workflow adımı bulunamadı: {StepCodes.Cancelled}",
                        StatusCode.BadRequest,
                        false);
                }

                var cancelAction = await _uow.Repository
                    .GetQueryable<PurchaseRequestAction>()
                    .FirstOrDefaultAsync(
                        x =>
                            x.PurchaseRequestStepId == request.CurrentStepId.Value &&
                            x.Code == ActionCodes.Cancel &&
                            x.IsActive,
                        cancellationToken);

                if (cancelAction == null)
                {
                    return ResponseModel<bool>.Fail(
                        "Mevcut workflow adımı için CANCEL aksiyonu tanımlanmamış.",
                        StatusCode.BadRequest,
                        false);
                }

                var previousStatus = request.Status;
                var fromStepId = request.CurrentStepId;

                await CancelPendingTasksAsync(
                    request.Id,
                    currentUser.Id,
                    cancellationToken);

                request.Status = GetPurchaseStatus("Cancelled");
                request.CurrentStepId = cancelStep.Id;
                request.ClosedDate = DateTimeOffset.Now;

                ApplyUpdateAudit(request, currentUser.Id);

                var history = new PurchaseRequestHistory
                {
                    PurchaseRequestId = request.Id,
                    FromStepId = fromStepId,
                    ToStepId = cancelStep.Id,
                    PurchaseRequestActionId = cancelAction.Id,
                    Description = "Talep sahibi tarafından iptal edildi.",
                    PreviousStatus = previousStatus,
                    NewStatus = GetPurchaseStatus("Cancelled")
                };

                ApplyCreateAudit(history, currentUser.Id);

                await _uow.Repository.AddAsync(
                    history,
                    cancellationToken);

                await _uow.Repository.CompleteAsync(
                    cancellationToken);

                return ResponseModel<bool>.Success(
                    true,
                    "Satın alma talebi iptal edildi.");
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error,
                    false);
            }
        }

        /// <summary>
        /// Teknik olarak DELETE değildir.
        ///
        /// PurchaseRequest tarihsel bir business kayıt olduğu için
        /// Cancelled durumuna geçirilir.
        ///
        /// Draft veya RevisionRequired talep, talep sahibi tarafından
        /// iptal edilebilir.
        /// </summary>
        public async Task<ResponseModel<bool>> DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult =
                    await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<bool>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode,
                        false);
                }

                var currentUser = currentUserResult.Data!;

                var request = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .Include(x => x.CurrentStep)
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (request == null)
                {
                    return ResponseModel<bool>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound,
                        false);
                }

                if (request.RequesterUserId != currentUser.Id)
                {
                    return ResponseModel<bool>.Fail(
                        "Yalnızca talep sahibi satın alma talebini iptal edebilir.",
                        StatusCode.BadRequest,
                        false);
                }

                if (!IsStatus(
                        request.Status,
                        "Draft",
                        "RevisionRequired"))
                {
                    return ResponseModel<bool>.Fail(
                        "Bu talep mevcut durumda iptal edilemez.",
                        StatusCode.BadRequest,
                        false);
                }

                if (request.CurrentStepId == null)
                {
                    return ResponseModel<bool>.Fail(
                        "Talebin mevcut workflow adımı bulunamadı.",
                        StatusCode.BadRequest,
                        false);
                }

                var cancelStep = await FindStepByCodeAsync(
                    StepCodes.Cancelled,
                    cancellationToken);

                if (cancelStep == null)
                {
                    return ResponseModel<bool>.Fail(
                        $"Workflow adımı bulunamadı: {StepCodes.Cancelled}",
                        StatusCode.BadRequest,
                        false);
                }

                /*
                 * History.PurchaseRequestActionId zorunlu olduğu için
                 * her iptal edilebilir step'te CANCEL action tanımlı olmalı.
                 */
                var cancelAction = await _uow.Repository
                    .GetQueryable<PurchaseRequestAction>()
                    .FirstOrDefaultAsync(
                        x =>
                            x.PurchaseRequestStepId ==
                            request.CurrentStepId.Value &&
                            x.Code == ActionCodes.Cancel &&
                            x.IsActive,
                        cancellationToken);

                if (cancelAction == null)
                {
                    return ResponseModel<bool>.Fail(
                        "Mevcut workflow adımı için CANCEL aksiyonu tanımlanmamış.",
                        StatusCode.BadRequest,
                        false);
                }

                var previousStatus = request.Status;
                var fromStepId = request.CurrentStepId;

                await CancelPendingTasksAsync(request.Id, currentUser.Id, cancellationToken);

                request.Status = GetPurchaseStatus("Cancelled");
                request.CurrentStepId = cancelStep.Id;
                request.ClosedDate = DateTimeOffset.Now;

                ApplyUpdateAudit(request, currentUser.Id);

                var history = new PurchaseRequestHistory
                {
                    PurchaseRequestId = request.Id,
                    FromStepId = fromStepId,
                    ToStepId = cancelStep.Id,
                    PurchaseRequestActionId = cancelAction.Id,
                    Description = "Talep sahibi tarafından iptal edildi.",
                    PreviousStatus = previousStatus,
                    NewStatus = GetPurchaseStatus("Cancelled")
                };

                ApplyCreateAudit(history, currentUser.Id);

                await _uow.Repository.AddAsync(history, cancellationToken);

                await _uow.Repository.CompleteAsync(cancellationToken);

                return ResponseModel<bool>.Success(true, "Satın alma talebi iptal edildi.");
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error,
                    false);
            }
        }


        public async Task<ResponseModel<PurchaseRequestDetailDto>> GetDetailAsync(long id, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var dto = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .ProjectToType<PurchaseRequestDetailDto>(_config)
                    .FirstOrDefaultAsync(cancellationToken);

                if (dto == null)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                return ResponseModel<PurchaseRequestDetailDto>.Success(dto);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestDetailDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        public async Task<ResponseModel<PaginatedList<PurchaseRequestGetDto>>> GetPagedAsync(QueryParams queryParams, CancellationToken cancellationToken = default)
        {
            try
            {
                queryParams ??= new QueryParams();

                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PaginatedList<PurchaseRequestGetDto>>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var page = queryParams.Page <= 0 ? 1 : queryParams.Page;

                var pageSize = queryParams.PageSize <= 0 ? 20 : Math.Min(queryParams.PageSize, 100);

                IQueryable<PurchaseRequest> query = _uow.Repository.GetQueryable<PurchaseRequest>().AsNoTracking();


                // -------------------------------------------------
                // FILTER
                // -------------------------------------------------
                query = ApplyPurchaseRequestVisibilityFilter(query, currentUserResult.Data!);

                return await GetPagedResultAsync(query, queryParams, cancellationToken);


            }
            catch (Exception ex)
            {
                return ResponseModel<
                    PaginatedList<PurchaseRequestGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // ITEM
        // =====================================================

        public async Task<ResponseModel<PurchaseRequestItemGetDto>> AddItemAsync(long purchaseRequestId, PurchaseRequestItemCreateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                var request = await GetTrackedRequestAsync(
                        purchaseRequestId,
                        cancellationToken);

                if (request == null)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                /*
                 * Yeni talep kalemi yalnızca talep sahibi
                 * Draft / Revision sırasında ekleyebilir.
                 */
                if (!IsEditableByRequester(
                        request,
                        user.Id))
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        "Bu talebe yeni ürün kalemi eklenemez.",
                        StatusCode.BadRequest);
                }

                var entity = _mapper.Map<PurchaseRequestItem>(dto);

                entity.Id = 0;
                entity.PurchaseRequestId = purchaseRequestId;

                /*
                 * LineNo client tarafından yönetilmesin.
                 */
                var maxLineNo = await _uow.Repository
                    .GetQueryable<PurchaseRequestItem>()
                    .Where(x =>
                        x.PurchaseRequestId ==
                        purchaseRequestId)
                    .Select(x => (int?)x.LineNo)
                    .MaxAsync(cancellationToken)
                    ?? 0;

                entity.LineNo = maxLineNo + 1;

                var validation = await ValidateItemAsync(entity, cancellationToken);

                if (!validation.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        validation.Message,
                        validation.StatusCode);
                }

                ApplyCreateAudit(entity, user.Id);

                await _uow.Repository.AddAsync(
                    entity,
                    cancellationToken);

                await _uow.Repository.CompleteAsync(
                    cancellationToken);

                var result = await _uow.Repository
                    .GetQueryable<PurchaseRequestItem>()
                    .AsNoTracking()
                    .Where(x => x.Id == entity.Id)
                    .ProjectToType<PurchaseRequestItemGetDto>(_config)
                    .FirstAsync(cancellationToken);

                return ResponseModel<PurchaseRequestItemGetDto>.Success(
                    result,
                    Messages.Created,
                    StatusCode.Created);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        public async Task<ResponseModel<PurchaseRequestItemGetDto>> UpdateItemAsync(PurchaseRequestItemUpdateDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                /*
                 * DTO alanlarına sıkı bağımlı olmamak için
                 * önce entity shape'ına map ediyoruz.
                 */
                var mapped = _mapper.Map<PurchaseRequestItem>(dto);

                var entity = await _uow.Repository
                    .GetQueryable<PurchaseRequestItem>()
                    .Include(x => x.PurchaseRequest)
                    .FirstOrDefaultAsync(x => x.Id == mapped.Id, cancellationToken);

                if (entity == null)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                var canEditByRequester =
                    IsEditableByRequester(
                        entity.PurchaseRequest,
                        user.Id);

                var canEditByWorkflowUser =
                    await UserCanProcessCurrentStepAsync(
                        entity.PurchaseRequest,
                        user,
                        cancellationToken);

                if (!canEditByRequester &&
                    !canEditByWorkflowUser)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        "Bu ürün kalemini güncelleme yetkiniz bulunmuyor.",
                        StatusCode.BadRequest);
                }

                var id = entity.Id;

                var requestId = entity.PurchaseRequestId;

                var lineNo = entity.LineNo;

                _mapper.Map(dto, entity);

                entity.Id = id;
                entity.PurchaseRequestId = requestId;
                entity.LineNo = lineNo;

                var validation = await ValidateItemAsync(
                        entity,
                        cancellationToken);

                if (!validation.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                        validation.Message,
                        validation.StatusCode);
                }

                ApplyUpdateAudit(entity, user.Id);

                await _uow.Repository.CompleteAsync(cancellationToken);

                var result = await _uow.Repository
                    .GetQueryable<PurchaseRequestItem>()
                    .AsNoTracking()
                    .Where(x => x.Id == entity.Id)
                    .ProjectToType<PurchaseRequestItemGetDto>(_config)
                    .FirstAsync(cancellationToken);

                return ResponseModel<PurchaseRequestItemGetDto>.Success(
                    result,
                    Messages.Updated);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestItemGetDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        public async Task<ResponseModel<bool>> DeleteItemAsync(long itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<bool>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode,
                        false);
                }

                var user = currentUserResult.Data!;

                var entity = await _uow.Repository
                    .GetQueryable<PurchaseRequestItem>()
                    .Include(x => x.PurchaseRequest)
                    .FirstOrDefaultAsync(x => x.Id == itemId, cancellationToken);

                if (entity == null)
                {
                    return ResponseModel<bool>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound,
                        false);
                }

                if (!IsEditableByRequester(
                        entity.PurchaseRequest,
                        user.Id))
                {
                    return ResponseModel<bool>.Fail(
                        "Bu ürün kalemi mevcut durumda silinemez.",
                        StatusCode.BadRequest,
                        false);
                }

                await _uow.Repository.HardDeleteAsync(
                    entity,
                    cancellationToken);

                await _uow.Repository.CompleteAsync(
                    cancellationToken);

                return ResponseModel<bool>.Success(
                    true,
                    Messages.Deleted);
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error,
                    false);
            }
        }


        // =====================================================
        // ATTACHMENT
        // =====================================================

        public async Task<ResponseModel<PurchaseAttachmentGetDto>> AddAttachmentAsync(long purchaseRequestId, IFormFile file, CancellationToken cancellationToken = default)
        {
            string? storedFileName = null;
            var isCommitted = false;

            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(currentUserResult.Message, currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                var request = await GetTrackedRequestAsync(purchaseRequestId, cancellationToken);

                if (request == null)
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                var canModify = IsEditableByRequester(request, user.Id) || await UserCanProcessCurrentStepAsync(request, user, cancellationToken);

                if (!canModify)
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        "Bu talebe dosya ekleme yetkiniz bulunmuyor.",
                        StatusCode.BadRequest);
                }

                // -------------------------------------------------
                // FILE VALIDATION
                // -------------------------------------------------

                if (file == null || file.Length <= 0)
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        "Yüklenecek dosya boş olamaz.",
                        StatusCode.BadRequest);
                }

                var originalFileName = Path.GetFileName(file.FileName);

                if (string.IsNullOrWhiteSpace(originalFileName))
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        "Dosya adı geçersiz.",
                        StatusCode.BadRequest);
                }

                var extension =
                    Path.GetExtension(originalFileName)
                        ?.Trim()
                        .ToLowerInvariant()
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(extension))
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        "Dosya uzantısı bulunamadı.",
                        StatusCode.BadRequest);
                }

                var contentType =
                    string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/octet-stream"
                        : file.ContentType.Trim();

                // -------------------------------------------------
                // STORAGE
                // -------------------------------------------------

                storedFileName = await _fileStorage.SaveAsync(file, cancellationToken);

                if (string.IsNullOrWhiteSpace(storedFileName))
                {
                    return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                        "Dosya storage üzerine yüklenemedi.",
                        StatusCode.Error);
                }

                if (storedFileName.Length > 260)
                {
                    throw new InvalidDataException(
                        "Dosya storage anahtarı 260 karakter sınırını aşmaktadır.");
                }

                // -------------------------------------------------
                // DB METADATA
                // -------------------------------------------------

                var entity = new PurchaseAttachment
                {
                    PurchaseRequestId = request.Id,
                    OriginalFileName = Path.GetFileName(file.FileName),
                    StoredFileName = storedFileName,
                    Extension = Path.GetExtension(file.FileName).Trim().ToLowerInvariant(),
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType.Trim(),
                    SizeBytes = file.Length,
                    UploadedStepId = request.CurrentStepId
                };


                ApplyCreateAudit(entity, user.Id);

                await _uow.Repository.AddAsync(entity, cancellationToken);

                await _uow.Repository.CompleteAsync(cancellationToken);
                isCommitted = true;

                // -------------------------------------------------
                // RESULT
                // -------------------------------------------------

                var result =
                    await _uow.Repository
                        .GetQueryable<PurchaseAttachment>()
                        .AsNoTracking()
                        .Where(x => x.Id == entity.Id)
                        .ProjectToType<PurchaseAttachmentGetDto>(_config)
                        .FirstAsync(cancellationToken);

                return ResponseModel<PurchaseAttachmentGetDto>.Success(
                    result,
                    Messages.Created,
                    StatusCode.Created);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                /*
                 * Storage upload tamamlandı ancak DB commit olmadıysa
                 * orphan dosya bırakmayalım.
                 */
                if (!isCommitted && !string.IsNullOrWhiteSpace(storedFileName))
                {
                    await TryDeleteUploadedAttachmentAsync(storedFileName);
                }

                throw;
            }
            catch (InvalidDataException ex)
            {
                if (!isCommitted && !string.IsNullOrWhiteSpace(storedFileName))
                {
                    await TryDeleteUploadedAttachmentAsync(storedFileName);
                }

                _logger.LogWarning(
                    ex,
                    "Purchase attachment doğrulama hatası. PurchaseRequestId: {PurchaseRequestId}",
                    purchaseRequestId);

                return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                    ex.Message,
                    StatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                /*
                 * Dosya CDN'e çıktı fakat DB işlemi başarısız olduysa
                 * CDN üzerinde orphan dosya bırakmıyoruz.
                 */
                if (!isCommitted &&
                    !string.IsNullOrWhiteSpace(storedFileName))
                {
                    await TryDeleteUploadedAttachmentAsync(storedFileName);
                }

                _logger.LogError(
                    ex,
                    "Purchase attachment eklenirken hata oluştu. PurchaseRequestId: {PurchaseRequestId}",
                    purchaseRequestId);

                return ResponseModel<PurchaseAttachmentGetDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }

        private async Task TryDeleteUploadedAttachmentAsync(string storedFileName)
        {
            try
            {
                await _fileStorage.DeleteAsync(
                    storedFileName,
                    CancellationToken.None);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(
                    cleanupEx,
                    "DB işlemi başarısız olduktan sonra Purchase attachment storage dosyası temizlenemedi. FileName: {FileName}",
                    storedFileName);
            }
        }


        public async Task<ResponseModel<bool>> DeleteAttachmentAsync(
            long attachmentId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult =
                    await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<bool>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode,
                        false);
                }

                var user = currentUserResult.Data!;

                var attachment = await _uow.Repository
                    .GetQueryable<PurchaseAttachment>()
                    .Include(x => x.PurchaseRequest)
                    .FirstOrDefaultAsync(x => x.Id == attachmentId, cancellationToken);

                if (attachment == null)
                {
                    return ResponseModel<bool>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound,
                        false);
                }

                var canModify =
                    IsEditableByRequester(
                        attachment.PurchaseRequest,
                        user.Id)

                    ||

                    await UserCanProcessCurrentStepAsync(
                        attachment.PurchaseRequest,
                        user,
                        cancellationToken);

                if (!canModify)
                {
                    return ResponseModel<bool>.Fail(
                        "Bu dosyayı silme yetkiniz bulunmuyor.",
                        StatusCode.BadRequest,
                        false);
                }

                var storedFileName =
                    attachment.StoredFileName;

                /*
                 * Önce DB kaydını kaldırıyoruz.
                 *
                 * Storage silme başarısız olursa DB'de bozuk metadata
                 * bırakmaktansa R2'de orphan dosya kalması daha güvenli.
                 */
                await _uow.Repository.HardDeleteAsync(
                    attachment,
                    cancellationToken);

                await _uow.Repository.CompleteAsync(
                    cancellationToken);

                try
                {
                    await _fileStorage.DeleteAsync(
                        storedFileName,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Purchase attachment storage dosyası silinemedi. FileName: {FileName}",
                        storedFileName);
                }

                return ResponseModel<bool>.Success(
                    true,
                    Messages.Deleted);
            }
            catch (Exception ex)
            {
                return ResponseModel<bool>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error,
                    false);
            }
        }


        public async Task<ResponseModel<List<PurchaseAttachmentGetDto>>> GetAttachmentsAsync(long purchaseRequestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var context = await GetCurrentUserAsync(cancellationToken);

                if (!context.IsSuccess)
                {
                    return ResponseModel<List<PurchaseAttachmentGetDto>>.Fail(context.Message, context.StatusCode);
                }

                var exists = await RequestExistsAsync(purchaseRequestId, cancellationToken);

                if (!exists)
                {
                    return ResponseModel<List<PurchaseAttachmentGetDto>>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                var items = await _uow.Repository
                    .GetQueryable<PurchaseAttachment>()
                    .AsNoTracking()
                    .Where(x => x.PurchaseRequestId == purchaseRequestId)
                    .OrderByDescending(x => x.CreatedDate)
                    .ProjectToType<PurchaseAttachmentGetDto>(_config)
                    .ToListAsync(cancellationToken);


                var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
                var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? "";
                string? NormalizeImageUrl(string? urlOrFileName)
                {
                    if (string.IsNullOrWhiteSpace(urlOrFileName))
                        return urlOrFileName;

                    // 1) Zaten tam URL ise (http/https) → hiç dokunma
                    if (urlOrFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        urlOrFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        return urlOrFileName;
                    }

                    // 2) /uploads/xxx.png gibi relative path ise
                    if (urlOrFileName.StartsWith("/"))
                    {
                        return string.IsNullOrEmpty(baseUrl)
                            ? urlOrFileName
                            : $"{baseUrl}{urlOrFileName}";
                    }

                    // 3) Sadece dosya adı ise (Guid.ext)
                    var relative = $"/uploads/{urlOrFileName}";
                    return string.IsNullOrEmpty(baseUrl)
                        ? relative
                        : $"{baseUrl}{relative}";
                }
                foreach (var item in items)
                {
                    item.Url = NormalizeImageUrl(item.StoredFileName);
                }

                return ResponseModel<List<PurchaseAttachmentGetDto>>.Success(items);
            }
            catch (Exception ex)
            {
                return ResponseModel<List<PurchaseAttachmentGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // PROCESS ACTION
        // =====================================================

        public async Task<ResponseModel<PurchaseRequestDetailDto>> ProcessActionAsync(PurchaseRequestProcessActionDto dto, CancellationToken cancellationToken = default)
        {
            try
            {
                var currentUserResult = await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                /*
                 * Tracked request.
                 * Items conditional warehouse routing için gerekli.
                 */
                var request = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .Include(x => x.CurrentStep)
                    .Include(x => x.Items)
                    .FirstOrDefaultAsync(x => x.Id == dto.PurchaseRequestId, cancellationToken);

                if (request == null)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                if (request.CurrentStepId == null || request.CurrentStep == null)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        "Talebin mevcut workflow adımı bulunamadı.",
                        StatusCode.BadRequest);
                }

                if (IsTerminalStatus(request.Status))
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        "Tamamlanmış, reddedilmiş veya iptal edilmiş talep üzerinde işlem yapılamaz.",
                        StatusCode.BadRequest);
                }

                var action = await _uow.Repository
                    .GetQueryable<PurchaseRequestAction>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.Id == dto.PurchaseRequestActionId
                            && x.PurchaseRequestStepId == request.CurrentStepId.Value
                            && x.IsActive, cancellationToken);

                if (action == null)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        "Seçilen aksiyon mevcut workflow adımı için geçerli değil.",
                        StatusCode.BadRequest);
                }

                if (action.RequiresDescription &&
                    string.IsNullOrWhiteSpace(dto.Description))
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        "Bu işlem için açıklama girilmesi zorunludur.",
                        StatusCode.BadRequest);
                }

                /*
                 * Draft ilk gönderimde henüz task oluşturmadık.
                 * Talep sahibi doğrudan SUBMIT çalıştırabilir.
                 */
                var isInitialDraft = IsStatus(request.Status, "Draft") && request.CurrentStep.IsInitial;

                PurchaseRequestTask? currentTask = null;

                if (isInitialDraft)
                {
                    if (request.RequesterUserId != user.Id)
                    {
                        return ResponseModel<PurchaseRequestDetailDto>.Fail(
                            "Bu talebi yalnızca talep sahibi işleme gönderebilir.",
                            StatusCode.BadRequest);
                    }

                    if (!CodeEquals(
                            action.Code,
                            ActionCodes.Submit))
                    {
                        /*
                         * Draft'ta CANCEL DeleteAsync üzerinden çalışabilir.
                         * ProcessAction için ana workflow aksiyonu SUBMIT.
                         */
                        return ResponseModel<PurchaseRequestDetailDto>.Fail(
                            "Taslak talep için bu aksiyon kullanılamaz.",
                            StatusCode.BadRequest);
                    }

                    if (request.Items.Count == 0)
                    {
                        return ResponseModel<PurchaseRequestDetailDto>.Fail(
                            "Talep workflow'a gönderilmeden önce en az bir ürün kalemi eklenmelidir.",
                            StatusCode.BadRequest);
                    }
                }
                else
                {
                    currentTask = await GetAuthorizedPendingTaskAsync(
                            request,
                            user,
                            cancellationToken);

                    if (currentTask == null)
                    {
                        return ResponseModel<PurchaseRequestDetailDto>.Fail(
                            "Bu workflow adımında işlem yapabileceğiniz aktif bir göreviniz bulunmuyor.",
                            StatusCode.BadRequest);
                    }
                }

                var targetStep = await ResolveTargetStepAsync(request, action, cancellationToken);

                if (targetStep == null)
                {
                    return ResponseModel<PurchaseRequestDetailDto>.Fail(
                        "Aksiyon için hedef workflow adımı belirlenemedi.",
                        StatusCode.BadRequest);
                }

                var previousStep = request.CurrentStep;

                var previousStatus = request.Status;

                var newStatus =
                    ResolveNewRequestStatus(
                        previousStep,
                        targetStep);

                /*
                 * Mevcut görev tamamlanıyor.
                 */
                if (currentTask != null)
                {
                    currentTask.Status =
                        GetTaskStatus("Completed");

                    currentTask.CompletedDate =
                        DateTimeOffset.Now;

                    currentTask.CompletedUserId =
                        user.Id;

                    ApplyUpdateAudit(
                        currentTask,
                        user.Id);
                }

                /*
                 * Request yeni step/status.
                 */
                request.CurrentStepId =
                    targetStep.Id;

                request.Status =
                    newStatus;

                if (targetStep.IsFinal)
                {
                    request.ClosedDate =
                        DateTimeOffset.Now;
                }
                else
                {
                    request.ClosedDate =
                        null;
                }

                ApplyUpdateAudit(
                    request,
                    user.Id);


                /*
                 * History.
                 */
                var history =
                    new PurchaseRequestHistory
                    {
                        PurchaseRequestId =
                            request.Id,

                        FromStepId =
                            previousStep.Id,

                        ToStepId =
                            targetStep.Id,

                        PurchaseRequestActionId =
                            action.Id,

                        Description =
                            string.IsNullOrWhiteSpace(dto.Description)
                                ? null
                                : dto.Description.Trim(),

                        PreviousStatus =
                            previousStatus,

                        NewStatus =
                            newStatus
                    };

                ApplyCreateAudit(
                    history,
                    user.Id);

                await _uow.Repository.AddAsync(
                    history,
                    cancellationToken);


                /*
                 * Terminal değilse sonraki görev oluşturulur.
                 *
                 * Burada:
                 * - user/role assignment zorunlu
                 * - duplicate Pending task yasak
                 */
                if (!targetStep.IsFinal)
                {
                    var createTaskResult =
                        await CreateTaskForStepAsync(
                            request,
                            targetStep,
                            user.Id,
                            cancellationToken);

                    if (!createTaskResult.IsSuccess)
                    {
                        return ResponseModel<PurchaseRequestDetailDto>.Fail(
                            createTaskResult.Message,
                            createTaskResult.StatusCode);
                    }
                }

                /*
                 * Tek Complete:
                 *
                 * Task complete
                 * Request update
                 * History insert
                 * Next task insert
                 *
                 * aynı DbContext SaveChanges transaction'ında yapılır.
                 */
                await _uow.Repository.CompleteAsync(cancellationToken);

                if (!targetStep.IsFinal)
                {
                    await SendPurchaseRequestAssignmentMailAsync(
                        request,
                        previousStep,
                        targetStep,
                        user.Id,
                        cancellationToken);
                }

                return await GetDetailInternalAsync(request.Id, cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                return ResponseModel<PurchaseRequestDetailDto>.Fail(
                    $"{Messages.DatabaseError}: {ex.GetBaseException().Message}",
                    StatusCode.Conflict);
            }
            catch (Exception ex)
            {
                return ResponseModel<PurchaseRequestDetailDto>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // ACTION
        // =====================================================

        public async Task<ResponseModel<List<PurchaseRequestActionGetDto>>> GetActionsAsync(long purchaseRequestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var current =
                    await GetCurrentUserAsync(cancellationToken);

                if (!current.IsSuccess)
                {
                    return ResponseModel<List<PurchaseRequestActionGetDto>>.Fail(
                        current.Message,
                        current.StatusCode);
                }

                var user = current.Data!;

                var request = await _uow.Repository
                    .GetQueryable<PurchaseRequest>()
                    .Include(x => x.CurrentStep)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == purchaseRequestId, cancellationToken);

                if (request == null)
                {
                    return ResponseModel<List<PurchaseRequestActionGetDto>>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                if (request.CurrentStepId == null)
                {
                    return ResponseModel<List<PurchaseRequestActionGetDto>>.Success(
                        new List<PurchaseRequestActionGetDto>());
                }

                if (IsTerminalStatus(request.Status))
                {
                    return ResponseModel<List<PurchaseRequestActionGetDto>>.Success(
                        new List<PurchaseRequestActionGetDto>());
                }

                var allowed =
                    request.CurrentStep != null &&
                    request.CurrentStep.IsInitial &&
                    IsStatus(request.Status, "Draft")
                        ? request.RequesterUserId == user.Id
                        : await UserCanProcessCurrentStepAsync(
                            request,
                            user,
                            cancellationToken);

                if (!allowed)
                {
                    return ResponseModel<List<PurchaseRequestActionGetDto>>.Success(
                        new List<PurchaseRequestActionGetDto>());
                }

                var actions = await _uow.Repository
                    .GetQueryable<PurchaseRequestAction>()
                    .AsNoTracking()
                    .Where(x =>
                        x.PurchaseRequestStepId ==
                        request.CurrentStepId.Value
                        &&
                        x.IsActive
                        &&
                        x.Code != ActionCodes.Cancel)
                    .OrderBy(x => x.OrderNo)
                    .ProjectToType<PurchaseRequestActionGetDto>(_config)
                    .ToListAsync(cancellationToken);

                return ResponseModel<List<PurchaseRequestActionGetDto>>.Success(
                    actions);
            }
            catch (Exception ex)
            {
                return ResponseModel<List<PurchaseRequestActionGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // HISTORY
        // =====================================================

        public async Task<ResponseModel<List<PurchaseRequestHistoryGetDto>>> GetHistoryAsync(long purchaseRequestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var current =
                    await GetCurrentUserAsync(cancellationToken);

                if (!current.IsSuccess)
                {
                    return ResponseModel<List<PurchaseRequestHistoryGetDto>>.Fail(
                        current.Message,
                        current.StatusCode);
                }

                var exists = await RequestExistsAsync(purchaseRequestId, cancellationToken);

                if (!exists)
                {
                    return ResponseModel<List<PurchaseRequestHistoryGetDto>>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                var result = await _uow.Repository
                    .GetQueryable<PurchaseRequestHistory>()
                    .AsNoTracking()
                    .Where(x =>
                        x.PurchaseRequestId ==
                        purchaseRequestId)
                    .OrderByDescending(x => x.CreatedDate)
                    .ProjectToType<PurchaseRequestHistoryGetDto>(_config)
                    .ToListAsync(cancellationToken);

                return ResponseModel<List<PurchaseRequestHistoryGetDto>>.Success(
                    result);
            }
            catch (Exception ex)
            {
                return ResponseModel<List<PurchaseRequestHistoryGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // TASK
        // =====================================================

        public async Task<ResponseModel<List<PurchaseRequestTaskGetDto>>> GetTasksAsync(long purchaseRequestId, CancellationToken cancellationToken = default)
        {
            try
            {
                var current =
                    await GetCurrentUserAsync(cancellationToken);

                if (!current.IsSuccess)
                {
                    return ResponseModel<List<PurchaseRequestTaskGetDto>>.Fail(
                        current.Message,
                        current.StatusCode);
                }

                var exists = await RequestExistsAsync(purchaseRequestId, cancellationToken);

                if (!exists)
                {
                    return ResponseModel<List<PurchaseRequestTaskGetDto>>.Fail(
                        Messages.RecordNotFound,
                        StatusCode.NotFound);
                }

                var result = await _uow.Repository
                    .GetQueryable<PurchaseRequestTask>()
                    .AsNoTracking()
                    .Where(x =>
                        x.PurchaseRequestId ==
                        purchaseRequestId)
                    .OrderByDescending(x => x.Id)
                    .ProjectToType<PurchaseRequestTaskGetDto>(_config)
                    .ToListAsync(cancellationToken);

                return ResponseModel<List<PurchaseRequestTaskGetDto>>.Success(
                    result);
            }
            catch (Exception ex)
            {
                return ResponseModel<List<PurchaseRequestTaskGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // STEP
        // =====================================================

        public async Task<ResponseModel<List<PurchaseRequestStepGetDto>>> GetStepsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var steps = await _uow.Repository
                    .GetQueryable<PurchaseRequestStep>()
                    .AsNoTracking()
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.OrderNo)
                    .ProjectToType<PurchaseRequestStepGetDto>(_config)
                    .ToListAsync(cancellationToken);

                return ResponseModel<List<PurchaseRequestStepGetDto>>.Success(
                    steps);
            }
            catch (Exception ex)
            {
                return ResponseModel<List<PurchaseRequestStepGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<PaginatedList<PurchaseRequestGetDto>>> GetMyRequestsAsync(QueryParams queryParams, CancellationToken cancellationToken = default)
        {
            try
            {
                queryParams ??= new QueryParams();

                var currentUserResult =
                    await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PaginatedList<PurchaseRequestGetDto>>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                var roleIds = user.Roles
                    .Select(x => x.Id)
                    .ToList();

                var pendingStatus =
                    GetTaskStatus("Pending");

                var draftStatus =
                    GetPurchaseStatus("Draft");

                var revisionStatus =
                    GetPurchaseStatus("RevisionRequired");

                IQueryable<PurchaseRequest> query =
                    _uow.Repository
                        .GetQueryable<PurchaseRequest>()
                        .AsNoTracking()
                        .Where(request =>

                            request.RequesterUserId == user.Id

                            &&

                            /*
                             * Şu anda tekrar talep sahibinin üzerinde
                             * bulunan kayıtları bu listeye alma.
                             */
                            !(
                                request.Status == draftStatus
                                ||
                                request.Status == revisionStatus

                                ||

                                _uow.Repository
                                    .GetQueryable<PurchaseRequestTask>()
                                    .Any(task =>
                                        task.PurchaseRequestId == request.Id
                                        &&
                                        task.PurchaseRequestStepId == request.CurrentStepId
                                        &&
                                        task.Status == pendingStatus
                                        &&
                                        (
                                            task.AssignedUserId == user.Id
                                            ||
                                            (
                                                task.AssignedRoleId.HasValue
                                                &&
                                                roleIds.Contains(task.AssignedRoleId.Value)
                                            )
                                        )
                                    )
                            )
                        );

                return await GetPagedResultAsync(
                    query,
                    queryParams,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return ResponseModel<PaginatedList<PurchaseRequestGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        public async Task<ResponseModel<PaginatedList<PurchaseRequestGetDto>>> GetMyProcessedRequestsAsync(QueryParams queryParams, CancellationToken cancellationToken = default)
        {
            try
            {
                queryParams ??= new QueryParams();

                var currentUserResult =
                    await GetCurrentUserAsync(cancellationToken);

                if (!currentUserResult.IsSuccess)
                {
                    return ResponseModel<PaginatedList<PurchaseRequestGetDto>>.Fail(
                        currentUserResult.Message,
                        currentUserResult.StatusCode);
                }

                var user = currentUserResult.Data!;

                IQueryable<PurchaseRequest> query =
                    _uow.Repository
                        .GetQueryable<PurchaseRequest>()
                        .AsNoTracking()
                        .Where(request =>
                            _uow.Repository
                                .GetQueryable<PurchaseRequestHistory>()
                                .Any(history =>
                                    history.PurchaseRequestId == request.Id
                                    &&
                                    history.CreatedUser == user.Id
                                )
                        );

                return await GetPagedResultAsync(
                    query,
                    queryParams,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                return ResponseModel<PaginatedList<PurchaseRequestGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.GetBaseException().Message}",
                    StatusCode.Error);
            }
        }


        // =====================================================
        // WORKFLOW HELPERS
        // =====================================================

        private async Task<ResponseModel<PaginatedList<PurchaseRequestGetDto>>> GetPagedResultAsync(IQueryable<PurchaseRequest> query, QueryParams queryParams, CancellationToken cancellationToken)
        {
            queryParams ??= new QueryParams();

            var page = queryParams.Page <= 0 ? 1 : queryParams.Page;

            var pageSize = queryParams.PageSize <= 0 ? 20 : Math.Min(queryParams.PageSize, 100);


            // -------------------------------------------------
            // SEARCH
            // -------------------------------------------------

            if (!string.IsNullOrWhiteSpace(queryParams.Search))
            {
                var search = queryParams.Search.Trim();

                var like = $"%{search}%";

                query = query.Where(x =>

                    EF.Functions.Like(x.RequestNo, like) ||
                    EF.Functions.Like(x.Subject, like) ||
                    (x.Description != null && EF.Functions.Like(x.Description, like)) ||
                    (x.RequesterUser != null && EF.Functions.Like(x.RequesterUser.TechnicianName, like)) ||
                    (x.ManagerUser != null && EF.Functions.Like(x.ManagerUser.TechnicianName, like)) ||
                    (x.SystemType != null && EF.Functions.Like(x.SystemType.Name, like)) ||
                    x.Items.Any(i => i.ProductName != null && EF.Functions.Like(i.ProductName, like))
                );
            }

            // -------------------------------------------------
            // SORT
            // -------------------------------------------------
            var sort = queryParams.Sort?.Trim().ToLowerInvariant();

            query = sort switch
            {
                "requestno" => queryParams.Desc ? query.OrderByDescending(x => x.RequestNo) : query.OrderBy(x => x.RequestNo),
                "subject" => queryParams.Desc ? query.OrderByDescending(x => x.Subject) : query.OrderBy(x => x.Subject),
                "requester" => queryParams.Desc ? query.OrderByDescending(x => x.RequesterUser!.TechnicianName) : query.OrderBy(x => x.RequesterUser!.TechnicianName),
                "manager" => queryParams.Desc ? query.OrderByDescending(x => x.ManagerUser!.TechnicianName) : query.OrderBy(x => x.ManagerUser!.TechnicianName),
                "status" => queryParams.Desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
                "currentstep" => queryParams.Desc ? query.OrderByDescending(x => x.CurrentStep!.OrderNo) : query.OrderBy(x => x.CurrentStep!.OrderNo),
                "createddate" => queryParams.Desc ? query.OrderByDescending(x => x.CreatedDate) : query.OrderBy(x => x.CreatedDate),
                "updateddate" => queryParams.Desc ? query.OrderByDescending(x => x.UpdatedDate) : query.OrderBy(x => x.UpdatedDate),
                "id" => queryParams.Desc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id),
                _ => query.OrderByDescending(x => x.Id)
            };

            // -------------------------------------------------
            // TOTAL COUNT
            // -------------------------------------------------

            var totalCount =
                await query.CountAsync(
                    cancellationToken);


            // -------------------------------------------------
            // PAGINATION + DTO
            // -------------------------------------------------

            var items =
                await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ProjectToType<PurchaseRequestGetDto>(_config)
                    .ToListAsync(cancellationToken);


            return ResponseModel<
                    PaginatedList<PurchaseRequestGetDto>>
                .Success(
                    new PaginatedList<PurchaseRequestGetDto>(
                        items,
                        totalCount,
                        page,
                        pageSize));
        }
        private IQueryable<PurchaseRequest> ApplyPurchaseRequestVisibilityFilter(IQueryable<PurchaseRequest> query, CurrentUserDto user)
        {
            var roleIds = user.Roles
                .Select(x => x.Id)
                .ToList();

            var pendingStatus =
                GetTaskStatus("Pending");

            var draftStatus =
                GetPurchaseStatus("Draft");

            var revisionRequiredStatus =
                GetPurchaseStatus("RevisionRequired");

            return query.Where(request =>

                /*
                 * 1. Kullanıcının kendi talebi:
                 *
                 * - Draft
                 * - RevisionRequired
                 *
                 * durumlarında kullanıcı kendi talebini görür.
                 */
                (
                    request.RequesterUserId == user.Id
                    &&
                    (
                        request.Status == draftStatus
                        ||
                        request.Status == revisionRequiredStatus
                    )
                )

                ||

                /*
                 * 2. Kullanıcının üzerinde aktif workflow görevi varsa.
                 *
                 * Görev:
                 * - doğrudan kullanıcıya atanmış olabilir
                 * - kullanıcının rollerinden birine atanmış olabilir
                 */
                _uow.Repository
                    .GetQueryable<PurchaseRequestTask>()
                    .Any(task =>

                        task.PurchaseRequestId == request.Id

                        &&

                        /*
                         * Eski bir task değil,
                         * request'in mevcut workflow adımındaki task olmalı.
                         */
                        task.PurchaseRequestStepId ==
                        request.CurrentStepId

                        &&

                        task.Status ==
                        pendingStatus

                        &&

                        (
                            /*
                             * Doğrudan kullanıcı ataması.
                             */
                            task.AssignedUserId ==
                            user.Id

                            ||

                            /*
                             * Rol bazlı atama.
                             */
                            (
                                task.AssignedRoleId.HasValue
                                &&
                                roleIds.Contains(
                                    task.AssignedRoleId.Value)
                            )
                        )
                    )
            );
        }
        private async Task<PurchaseRequestStep?> ResolveTargetStepAsync__(PurchaseRequest request, PurchaseRequestAction action, CancellationToken cancellationToken)
        {
            /*
             * Araştırma/Teklif:
             *
             * Draft SUBMIT doğrudan satın alma araştırmasına gider.
             *
             * Satın Alma ise Action.TargetStep üzerinden
             * Manager First Approval'a gider.
             */
            if (CodeEquals(action.Code, ActionCodes.Submit) && IsResearchAndOffer(request.RequestType))
            {
                return await FindStepByCodeAsync(StepCodes.PurchasingResearch, cancellationToken);
            }

            /*
             * Standart/configurable geçiş.
             */
            if (action.TargetStepId.HasValue)
            {
                return await _uow.Repository
                    .GetQueryable<PurchaseRequestStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == action.TargetStepId.Value && x.IsActive, cancellationToken);
            }

            /*
             * Conditional route:
             *
             * Procurement tamamlandıktan sonra
             * fiziksel/depo kontrolü gerektiren item varsa
             * WarehouseControl.
             *
             * Yoksa Accounting.
             */
            if (CodeEquals(request.CurrentStep?.Code, StepCodes.Procurement))
            {
                var requiresWarehouse = request.Items.Any(x => x.RequiresWarehouseControl);

                return await FindStepByCodeAsync(requiresWarehouse ? StepCodes.WarehouseControl : StepCodes.Accounting, cancellationToken);
            }

            return null;
        }

        private async Task<PurchaseRequestStep?> ResolveTargetStepAsync(PurchaseRequest request, PurchaseRequestAction action, CancellationToken cancellationToken)
        {
            /*
             * ResearchAndOffer:
             * Draft SUBMIT ile ilk yönetici onayını atlar
             * ve doğrudan satın alma araştırmasına gider.
             */
            if (CodeEquals(action.Code, ActionCodes.Submit) && IsResearchAndOffer(request.RequestType))
            {
                return await FindStepByCodeAsync(
                    StepCodes.PurchasingResearch,
                    cancellationToken);
            }

            /*
             * Araştırma sonucu talep sahibi tarafından uygun bulundu.
             *
             * NormalPurchase:
             * ikinci yönetici değerlendirmesine devam eder.
             *
             * ResearchAndOffer:
             * yalnızca araştırma/teklif süreci olduğu için tamamlanır.
             */
            if (CodeEquals(
                    request.CurrentStep?.Code,
                    StepCodes.RequesterResearchReview) &&
                CodeEquals(
                    action.Code,
                    ActionCodes.Approve))
            {
                return await FindStepByCodeAsync(
                    IsResearchAndOffer(request.RequestType)
                        ? StepCodes.Completed
                        : StepCodes.ManagerSecondApproval,
                    cancellationToken);
            }

            /*
             * Standart/configurable geçiş.
             */
            if (action.TargetStepId.HasValue)
            {
                return await _uow.Repository
                    .GetQueryable<PurchaseRequestStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == action.TargetStepId.Value &&
                            x.IsActive,
                        cancellationToken);
            }

            /*
             * Tedarik tamamlandıktan sonra:
             * depo kontrolü gerekiyor ise depo,
             * gerekmiyorsa muhasebe.
             */
            if (CodeEquals(
                    request.CurrentStep?.Code,
                    StepCodes.Procurement))
            {
                var requiresWarehouse =
                    request.Items.Any(
                        x => x.RequiresWarehouseControl);

                return await FindStepByCodeAsync(
                    requiresWarehouse
                        ? StepCodes.WarehouseControl
                        : StepCodes.Accounting,
                    cancellationToken);
            }

            return null;
        }


        private PurchaseRequestStatus ResolveNewRequestStatus(PurchaseRequestStep fromStep, PurchaseRequestStep targetStep)
        {
            if (targetStep.IsFinal)
            {
                if (ContainsCode(
                        targetStep.Code,
                        "REJECT"))
                {
                    return GetPurchaseStatus("Rejected");
                }

                if (ContainsCode(
                        targetStep.Code,
                        "CANCEL"))
                {
                    return GetPurchaseStatus("Cancelled");
                }

                return GetPurchaseStatus("Completed");
            }

            /*
             * Geri yönlü geçiş/revizyon.
             */
            if (targetStep.OrderNo <= fromStep.OrderNo)
            {
                return GetPurchaseStatus(
                    "RevisionRequired");
            }

            return GetPurchaseStatus(
                "InProgress");
        }


        private async Task<ResponseModel> CreateTaskForStepAsync(PurchaseRequest request, PurchaseRequestStep step, long createdUserId, CancellationToken cancellationToken)
        {
            if (step.IsFinal)
                return ResponseModel.Success();

            var pendingStatus =
                GetTaskStatus("Pending");

            var duplicate =
                await _uow.Repository
                    .GetQueryable<PurchaseRequestTask>()
                    .AnyAsync(
                        x =>
                            x.PurchaseRequestId ==
                            request.Id

                            &&

                            x.PurchaseRequestStepId ==
                            step.Id

                            &&

                            x.Status ==
                            pendingStatus,
                        cancellationToken);

            if (duplicate)
            {
                return ResponseModel.Fail(
                    $"Bu talep için {step.Name} adımında zaten aktif bir görev bulunmaktadır.",
                    StatusCode.Conflict);
            }


            long? assignedUserId = null;
            long? assignedRoleId = null;

            var code =
                NormalizeCode(step.Code);


            // ---------------------------------------------
            // Requester
            // ---------------------------------------------

            if (code == StepCodes.Draft ||
                code.Contains("REQUESTER"))
            {
                assignedUserId =
                    request.RequesterUserId;
            }

            // ---------------------------------------------
            // İlgili Yönetici
            // ---------------------------------------------

            else if (
                code.Contains("MANAGER") &&
                !code.Contains("MANAGEMENT"))
            {
                if (!request.ManagerUserId.HasValue)
                {
                    return ResponseModel.Fail(
                        $"{step.Name} adımı için ilgili yönetici tanımlı değil.",
                        StatusCode.BadRequest);
                }

                assignedUserId =
                    request.ManagerUserId.Value;
            }

            // ---------------------------------------------
            // Nihai Yönetim Onayı
            // ---------------------------------------------

            else if (code.Contains("MANAGEMENT"))
            {
                assignedRoleId =
                    await ResolveRoleIdAsync(
                        RoleAssignmentType.ManagementApproval,
                        cancellationToken);
            }

            // ---------------------------------------------
            // Satın Alma
            // ---------------------------------------------

            else if (
                code.Contains("PURCHAS") ||
                code.Contains("PROCUREMENT") ||
                code.Contains("INVOICE"))
            {
                assignedRoleId =
                    await ResolveRoleIdAsync(
                        RoleAssignmentType.Purchasing,
                        cancellationToken);
            }

            // ---------------------------------------------
            // Depo
            // ---------------------------------------------

            else if (code.Contains("WAREHOUSE"))
            {
                assignedRoleId =
                    await ResolveRoleIdAsync(
                        RoleAssignmentType.Warehouse,
                        cancellationToken);
            }

            // ---------------------------------------------
            // Muhasebe
            // ---------------------------------------------

            else if (
                code.Contains("ACCOUNT") ||
                code.Contains("FINANCE"))
            {
                assignedRoleId =
                    await ResolveRoleIdAsync(
                        RoleAssignmentType.Accounting,
                        cancellationToken);
            }


            /*
             * Analiz kuralı:
             * sahipsiz aktif task OLAMAZ.
             */
            if (!assignedUserId.HasValue &&
                !assignedRoleId.HasValue)
            {
                return ResponseModel.Fail(
                    $"{step.Name} adımı için kullanıcı veya rol ataması belirlenemedi.",
                    StatusCode.BadRequest);
            }


            var task =
                new PurchaseRequestTask
                {
                    PurchaseRequestId =
                        request.Id,

                    PurchaseRequestStepId =
                        step.Id,

                    AssignedUserId =
                        assignedUserId,

                    AssignedRoleId =
                        assignedRoleId,

                    Status =
                        pendingStatus
                };

            ApplyCreateAudit(
                task,
                createdUserId);

            await _uow.Repository.AddAsync(
                task,
                cancellationToken);

            return ResponseModel.Success();
        }


        private async Task<PurchaseRequestTask?> GetAuthorizedPendingTaskAsync(PurchaseRequest request, CurrentUserDto user, CancellationToken cancellationToken)
        {
            if (!request.CurrentStepId.HasValue)
                return null;

            var roleIds =
                user.Roles
                    .Select(x => x.Id)
                    .ToList();

            var pendingStatus =
                GetTaskStatus("Pending");

            return await _uow.Repository
                .GetQueryable<PurchaseRequestTask>()
                .FirstOrDefaultAsync(
                    x =>
                        x.PurchaseRequestId ==
                        request.Id

                        &&

                        x.PurchaseRequestStepId ==
                        request.CurrentStepId.Value

                        &&

                        x.Status ==
                        pendingStatus

                        &&

                        (
                            x.AssignedUserId ==
                            user.Id

                            ||

                            (
                                x.AssignedRoleId.HasValue
                                &&
                                roleIds.Contains(
                                    x.AssignedRoleId.Value)
                            )
                        ),
                    cancellationToken);
        }


        private async Task<bool> UserCanProcessCurrentStepAsync(PurchaseRequest request, CurrentUserDto user, CancellationToken cancellationToken)
        {
            var task =
                await GetAuthorizedPendingTaskAsync(
                    request,
                    user,
                    cancellationToken);

            return task != null;
        }

        private async Task CancelPendingTasksAsync(long purchaseRequestId, long completedUserId, CancellationToken cancellationToken)
        {
            var pending =
                GetTaskStatus("Pending");

            var cancelled =
                GetTaskStatus("Cancelled");

            var tasks = await _uow.Repository
                .GetQueryable<PurchaseRequestTask>()
                .Where(x =>
                    x.PurchaseRequestId ==
                    purchaseRequestId

                    &&

                    x.Status ==
                    pending)
                .ToListAsync(cancellationToken);

            foreach (var task in tasks)
            {
                task.Status =
                    cancelled;

                task.CompletedDate =
                    DateTimeOffset.Now;

                task.CompletedUserId =
                    completedUserId;

                ApplyUpdateAudit(
                    task,
                    completedUserId);
            }
        }

        private async Task SendPurchaseRequestAssignmentMailAsync(PurchaseRequest request, PurchaseRequestStep previousStep, PurchaseRequestStep targetStep, long createdUserId, CancellationToken cancellationToken)
        {
            try
            {
                if (targetStep.IsFinal)
                    return;

                var pendingStatus = GetTaskStatus("Pending");

                var task = await _uow.Repository
                    .GetQueryable<PurchaseRequestTask>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x =>
                        x.PurchaseRequestId == request.Id &&
                        x.PurchaseRequestStepId == targetStep.Id &&
                        x.Status == pendingStatus,
                        cancellationToken);

                if (task == null)
                {
                    _logger.LogWarning(
                        "Satın alma mail bildirimi için aktif görev bulunamadı. PurchaseRequestId: {PurchaseRequestId}, StepId: {StepId}",
                        request.Id,
                        targetStep.Id);

                    return;
                }

                var recipients = new List<(string Name, string Email)>();

                // -------------------------------------------------
                // USER ASSIGNMENT
                // -------------------------------------------------

                if (task.AssignedUserId.HasValue)
                {
                    var assignedUser = await _uow.Repository
                        .GetQueryable<User>()
                        .AsNoTracking()
                        .Where(x => x.Id == task.AssignedUserId.Value)
                        .Select(x => new
                        {
                            x.TechnicianName,
                            x.TechnicianEmail
                        })
                        .FirstOrDefaultAsync(cancellationToken);

                    if (assignedUser != null && !string.IsNullOrWhiteSpace(assignedUser.TechnicianEmail))
                    {
                        recipients.Add((
                            assignedUser.TechnicianName ?? "Kullanıcı",
                            assignedUser.TechnicianEmail.Trim()
                        ));
                    }
                }

                // -------------------------------------------------
                // ROLE ASSIGNMENT
                // -------------------------------------------------

                else if (task.AssignedRoleId.HasValue)
                {
                    var usersResult = await _userService.GetUserByRoleAsync(task.AssignedRoleId.Value);

                    if (usersResult.IsSuccess && usersResult.Data != null)
                    {
                        recipients.AddRange(
                            usersResult.Data
                                .Where(x => !string.IsNullOrWhiteSpace(x.TechnicianEmail))
                                .Select(x => (
                                    x.TechnicianName ?? "Kullanıcı",
                                    x.TechnicianEmail!.Trim()
                                )));
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Satın alma mail bildirimi için role bağlı kullanıcı bulunamadı. RoleId: {RoleId}, PurchaseRequestId: {PurchaseRequestId}",
                            task.AssignedRoleId.Value,
                            request.Id);
                    }
                }

                recipients = recipients
                    .GroupBy(x => x.Email, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .ToList();

                if (recipients.Count == 0)
                {
                    _logger.LogWarning(
                        "Satın alma mail bildirimi için geçerli mail adresi bulunamadı. PurchaseRequestId: {PurchaseRequestId}",
                        request.Id);

                    return;
                }

                var appUrl = (_appSettings.Value.FrontUrl ?? string.Empty).TrimEnd('/');
                var detailUrl = $"{appUrl}/crm/purchase-requests/{request.Id}";
                var processMessage = GetPurchaseRequestMailProcessMessage(targetStep);

                foreach (var recipient in recipients)
                {
                    var mail = new MailOutbox
                    {
                        RequestNo = request.RequestNo,
                        FromStepCode = previousStep.Code,
                        ToStepCode = targetStep.Code,
                        ToRecipients = recipient.Email,
                        Subject = "MGS Satın Alma Talebi Hk.",
                        BodyHtml = BuildPurchaseRequestAssignmentMailBody(recipient.Name, request, targetStep, processMessage, detailUrl),
                        CreatedUser = createdUserId,
                        CreatedDate = DateTime.Now
                    };

                    await _mailPushService.EnqueueAsync(mail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Satın alma talebi atama maili oluşturulurken hata oluştu. PurchaseRequestId: {PurchaseRequestId}",
                    request.Id);
            }
        }
        private static string GetPurchaseRequestMailProcessMessage(PurchaseRequestStep step)
        {
            var code = NormalizeCode(step.Code);

            return code switch
            {
                StepCodes.ManagerFirstApproval => "ilk yönetici onayınıza sunulmuştur.",
                StepCodes.ManagerSecondApproval => "ikinci yönetici değerlendirmenize sunulmuştur.",
                StepCodes.ManagementApproval => "yönetim onayınıza sunulmuştur.",
                StepCodes.PurchasingResearch => "satın alma araştırması ve teklif çalışması için tarafınıza atanmıştır.",
                StepCodes.RequesterResearchReview => "araştırma ve teklif sonuçlarını değerlendirmeniz için tarafınıza iletilmiştir.",
                StepCodes.Procurement => "satın alma işlemlerinin gerçekleştirilmesi için tarafınıza atanmıştır.",
                StepCodes.WarehouseControl => "depo kontrolünün gerçekleştirilmesi için tarafınıza atanmıştır.",
                StepCodes.Accounting => "muhasebe işlemlerinin gerçekleştirilmesi için tarafınıza atanmıştır.",
                StepCodes.RequesterDeliveryControl => "teslimat kontrolünün gerçekleştirilmesi için tarafınıza iletilmiştir.",
                StepCodes.InvoiceControl => "fatura kontrolünün gerçekleştirilmesi için tarafınıza atanmıştır.",
                _ => $"{step.Name} aşamasında işleminizi beklemektedir."
            };
        }


        private static string BuildPurchaseRequestAssignmentMailBody(
            string userName,
            PurchaseRequest request,
            PurchaseRequestStep step,
            string processMessage,
            string detailUrl)
        {
            return $@"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset=""UTF-8"">
                    </head>
                    <body style=""margin:0; padding:0; background-color:#f5f7fa; font-family:Arial, Helvetica, sans-serif;"">
                        <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f5f7fa; padding:24px 0;"">
                            <tr>
                                <td align=""center"">
                                    <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px; width:100%; background:#ffffff; border:1px solid #e5e7eb; border-radius:8px;"">
                                        <tr>
                                            <td style=""padding:24px 28px; border-bottom:1px solid #e5e7eb;"">
                                                <div style=""font-size:20px; font-weight:600; color:#1f2937;"">
                                                    Satın Alma Talebi
                                                </div>
                                                <div style=""font-size:13px; color:#6b7280; margin-top:4px;"">
                                                    {request.RequestNo}
                                                </div>
                                            </td>
                                        </tr>
                    
                                        <tr>
                                            <td style=""padding:28px;"">
                                                <p style=""margin:0 0 18px; font-size:15px; color:#374151;"">
                                                    Sayın <strong>{userName}</strong>,
                                                </p>
                    
                                                <p style=""margin:0 0 18px; font-size:15px; line-height:1.6; color:#374151;"">
                                                    <strong>{request.RequestNo}</strong> numaralı Satın Alma Talebi
                                                    {processMessage}
                                                </p>
                    
                                                <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""margin:20px 0; background:#f9fafb; border:1px solid #e5e7eb; border-radius:6px;"">
                                                    <tr>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#6b7280; width:140px;"">
                                                            Talep No
                                                        </td>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#111827; font-weight:600;"">
                                                            {request.RequestNo}
                                                        </td>
                                                    </tr>
                                                    <tr>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#6b7280; border-top:1px solid #e5e7eb;"">
                                                            Konu
                                                        </td>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#111827; border-top:1px solid #e5e7eb;"">
                                                            {request.Subject}
                                                        </td>
                                                    </tr>
                                                    <tr>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#6b7280; border-top:1px solid #e5e7eb;"">
                                                            İşlem
                                                        </td>
                                                        <td style=""padding:14px 16px; font-size:14px; color:#111827; border-top:1px solid #e5e7eb;"">
                                                            {step.Name}
                                                        </td>
                                                    </tr>
                                                </table>
                    
                                                <p style=""margin:0 0 20px; font-size:14px; line-height:1.6; color:#6b7280;"">
                                                    Talep üzerindeki işleminizi gerçekleştirmek için aşağıdaki butonu kullanabilirsiniz.
                                                </p>
                    
                                                <a href=""{detailUrl}""
                                                   style=""display:inline-block; padding:11px 20px; background:#2563eb; color:#ffffff; text-decoration:none; border-radius:6px; font-size:14px; font-weight:600;"">
                                                    Talebi Görüntüle
                                                </a>
                                            </td>
                                        </tr>
                    
                                        <tr>
                                            <td style=""padding:18px 28px; border-top:1px solid #e5e7eb; font-size:12px; color:#9ca3af;"">
                                                Bu e-posta MGS Satın Alma Talep Yönetimi tarafından otomatik olarak oluşturulmuştur.
                                            </td>
                                        </tr>
                                    </table>
                                </td>
                            </tr>
                        </table>
                    </body>
                    </html>";
        }

        // =====================================================
        // ROLE ASSIGNMENT
        // =====================================================

        private enum RoleAssignmentType
        {
            Purchasing,
            ManagementApproval,
            Warehouse,
            Accounting
        }


        private async Task<long?> ResolveRoleIdAsync(RoleAssignmentType assignmentType, CancellationToken cancellationToken)
        {
            IQueryable<Role> query =
                _uow.Repository
                    .GetQueryable<Role>()
                    .AsNoTracking();

            query = assignmentType switch
            {
                RoleAssignmentType.Purchasing =>
                    query.Where(x =>
                        x.Code == "PURCHASING" ||
                        x.Code == "PURCHASE" ||
                        x.Code == "SATIN_ALMA" ||
                        x.Name == "Satın Alma"),

                RoleAssignmentType.ManagementApproval =>
                    query.Where(x =>
                        x.Code == "MANAGEMENT_APPROVAL" ||
                        x.Code == "MANAGEMENT_APPROVER" ||
                        x.Code == "YONETICI_ONAY" ||
                        x.Name == "Yönetici Onayı"),

                RoleAssignmentType.Warehouse =>
                    query.Where(x =>
                        x.Code == "WAREHOUSE" ||
                        x.Code == "DEPO" ||
                        x.Name == "Depo"),

                RoleAssignmentType.Accounting =>
                    query.Where(x =>
                        x.Code == "ACCOUNTING" ||
                        x.Code == "MUHASEBE" ||
                        x.Name == "Muhasebe"),

                _ => query.Where(x => false)
            };

            return await query
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);
        }


        // =====================================================
        // VALIDATION
        // =====================================================

        private async Task<ResponseModel> ValidateRequestRelationsAsync(
            PurchaseRequest entity,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(entity.Subject))
            {
                return ResponseModel.Fail(
                    "Talep konusu zorunludur.",
                    StatusCode.BadRequest);
            }

            if (entity.ManagerUserId.HasValue)
            {
                var managerExists =
                    await _uow.Repository
                        .GetQueryable<User>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.ManagerUserId.Value,
                            cancellationToken);

                if (!managerExists)
                {
                    return ResponseModel.Fail(
                        "İlgili yönetici bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            if (entity.CustomerId.HasValue)
            {
                var customerExists =
                    await _uow.Repository
                        .GetQueryable<Customer>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.CustomerId.Value,
                            cancellationToken);

                if (!customerExists)
                {
                    return ResponseModel.Fail(
                        "Müşteri bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            if (entity.SystemTypeId.HasValue)
            {
                var systemExists =
                    await _uow.Repository
                        .GetQueryable<SystemType>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.SystemTypeId.Value,
                            cancellationToken);

                if (!systemExists)
                {
                    return ResponseModel.Fail(
                        "Sistem tipi bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            return ResponseModel.Success();
        }


        private async Task<ResponseModel> ValidateItemAsync(
            PurchaseRequestItem entity,
            CancellationToken cancellationToken)
        {
            if (entity.Quantity <= 0)
            {
                return ResponseModel.Fail(
                    "Ürün miktarı sıfırdan büyük olmalıdır.",
                    StatusCode.BadRequest);
            }

            /*
             * Product kartı yoksa serbest ürün adı zorunlu.
             */
            if (!entity.ProductId.HasValue &&
                string.IsNullOrWhiteSpace(entity.ProductName))
            {
                return ResponseModel.Fail(
                    "Ürün seçilmeli veya serbest ürün adı girilmelidir.",
                    StatusCode.BadRequest);
            }

            if (entity.ProductId.HasValue)
            {
                var productExists =
                    await _uow.Repository
                        .GetQueryable<Product>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.ProductId.Value,
                            cancellationToken);

                if (!productExists)
                {
                    return ResponseModel.Fail(
                        "Seçilen ürün bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            if (entity.AlternateProductId.HasValue)
            {
                var alternateExists =
                    await _uow.Repository
                        .GetQueryable<Product>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.AlternateProductId.Value,
                            cancellationToken);

                if (!alternateExists)
                {
                    return ResponseModel.Fail(
                        "Seçilen muadil ürün bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            if (entity.CurrencyTypeId.HasValue)
            {
                var currencyExists =
                    await _uow.Repository
                        .GetQueryable<CurrencyType>()
                        .AnyAsync(
                            x =>
                                x.Id ==
                                entity.CurrencyTypeId.Value,
                            cancellationToken);

                if (!currencyExists)
                {
                    return ResponseModel.Fail(
                        "Para birimi bulunamadı.",
                        StatusCode.BadRequest);
                }
            }

            if (entity.SupplierDiscountRate.HasValue &&
                (
                    entity.SupplierDiscountRate.Value < 0 ||
                    entity.SupplierDiscountRate.Value > 100
                ))
            {
                return ResponseModel.Fail(
                    "Tedarikçi indirim oranı 0 ile 100 arasında olmalıdır.",
                    StatusCode.BadRequest);
            }

            if (entity.SupplierListPrice < 0 ||
                entity.SupplierNetPrice < 0)
            {
                return ResponseModel.Fail(
                    "Fiyat bilgileri negatif olamaz.",
                    StatusCode.BadRequest);
            }

            return ResponseModel.Success();
        }


        // =====================================================
        // QUERY HELPERS
        // =====================================================

        private async Task<PurchaseRequest?> GetTrackedRequestAsync(long id, CancellationToken cancellationToken)
        {
            return await _uow.Repository
                .GetQueryable<PurchaseRequest>()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }


        private async Task<bool> RequestExistsAsync(long id, CancellationToken cancellationToken)
        {
            return await _uow.Repository
                .GetQueryable<PurchaseRequest>()
                .AsNoTracking()
                .AnyAsync(x => x.Id == id, cancellationToken);
        }


        private async Task<PurchaseRequestGetDto?> GetRequestDtoAsync(
            long id,
            CancellationToken cancellationToken)
        {
            return await _uow.Repository
                .GetQueryable<PurchaseRequest>()
                .AsNoTracking()
                .Where(x =>
                    x.Id == id)
                .ProjectToType<PurchaseRequestGetDto>(_config)
                .FirstOrDefaultAsync(cancellationToken);
        }


        private async Task<ResponseModel<PurchaseRequestDetailDto>> GetDetailInternalAsync(long id, CancellationToken cancellationToken)
        {
            var dto = await _uow.Repository
                .GetQueryable<PurchaseRequest>()
                .AsNoTracking()
                .Where(x =>
                    x.Id == id)
                .ProjectToType<PurchaseRequestDetailDto>(_config)
                .FirstOrDefaultAsync(cancellationToken);

            return dto == null
                ? ResponseModel<PurchaseRequestDetailDto>.Fail(
                    Messages.RecordNotFound,
                    StatusCode.NotFound)
                : ResponseModel<PurchaseRequestDetailDto>.Success(dto);
        }


        private async Task<PurchaseRequestStep?> FindStepByCodeAsync(string code, CancellationToken cancellationToken)
        {
            return await _uow.Repository
                .GetQueryable<PurchaseRequestStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Code == code &&
                        x.IsActive,
                    cancellationToken);
        }


        // =====================================================
        // CURRENT USER
        // =====================================================

        private async Task<ResponseModel<CurrentUserDto>> GetCurrentUserAsync(
            CancellationToken cancellationToken)
        {
            var user =
                await _currentUser.GetAsync(
                    cancellationToken);

            if (user == null ||
                !user.IsAuthenticated ||
                user.Id <= 0)
            {
                return ResponseModel<CurrentUserDto>.Fail(
                    "Giriş yapan kullanıcı bilgisi alınamadı.",
                    StatusCode.BadRequest);
            }
            return ResponseModel<CurrentUserDto>.Success(
                user);
        }


        // =====================================================
        // BUSINESS HELPERS
        // =====================================================

        private static bool IsEditableByRequester(
            PurchaseRequest request,
            long userId)
        {
            if (request.RequesterUserId != userId)
                return false;

            return IsStatus(
                request.Status,
                "Draft",
                "RevisionRequired");
        }


        private static bool IsTerminalStatus(
            PurchaseRequestStatus status)
        {
            return IsStatus(
                status,
                "Completed",
                "Rejected",
                "Cancelled");
        }


        private static bool IsResearchAndOffer(PurchaseRequestType requestType)
        {
            return requestType == PurchaseRequestType.ResearchAndOffer;
        }

        private static bool IsStatus(
            PurchaseRequestStatus current,
            params string[] names)
        {
            var currentName =
                current.ToString();

            return names.Any(
                x => string.Equals(
                    x,
                    currentName,
                    StringComparison.OrdinalIgnoreCase));
        }


        private static PurchaseRequestStatus GetPurchaseStatus(
            string name)
        {
            if (Enum.TryParse<PurchaseRequestStatus>(
                    name,
                    true,
                    out var value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"PurchaseRequestStatus enum değeri bulunamadı: {name}");
        }


        private static PurchaseRequestTaskStatus GetTaskStatus(
            string name)
        {
            if (Enum.TryParse<PurchaseRequestTaskStatus>(
                    name,
                    true,
                    out var value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"PurchaseRequestTaskStatus enum değeri bulunamadı: {name}");
        }


        private static string NormalizeCode(
            string? code)
        {
            return (code ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
        }


        private static bool CodeEquals(
            string? first,
            string? second)
        {
            return string.Equals(
                NormalizeCode(first),
                NormalizeCode(second),
                StringComparison.Ordinal);
        }


        private static bool ContainsCode(
            string? value,
            string search)
        {
            return NormalizeCode(value)
                .Contains(
                    NormalizeCode(search),
                    StringComparison.Ordinal);
        }


        // =====================================================
        // AUDIT
        // =====================================================

        private static void ApplyCreateAudit(
            dynamic entity,
            long userId)
        {
            entity.CreatedUser =
                userId;

            entity.CreatedDate =
                DateTimeOffset.Now;
        }


        private static void ApplyUpdateAudit(
            dynamic entity,
            long userId)
        {
            entity.UpdatedUser =
                userId;

            entity.UpdatedDate =
                DateTimeOffset.Now;
        }
    }
}