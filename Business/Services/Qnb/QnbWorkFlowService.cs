using Business.Interfaces;
using Business.Interfaces.Qnb;
using Business.UnitOfWork;
using ClosedXML.Excel;
using Core.Common;
using Core.Enums;
using Core.Enums.Qnb;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Dapper;
using Data.Concrete.EfCore.Context;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.Qnb;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerSystemAssignment;
using Model.Dtos.Notification;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Role;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbArchive;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbCustomerForm;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbFinalApproval;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbPricing;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReport;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbReviewLog;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequest;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWarehouse;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlow;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbWorkFlowStep;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Newtonsoft.Json;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;

namespace Business.Services.Qnb
{
    public partial class QnbWorkFlowService : IQnbWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly TypeAdapterConfig _config;
        private readonly IActivationRecordService _activationRecord;
        private readonly ILogger<QnbWorkFlowService> _logger;
        private readonly IMailPushService _mailPush;
        private readonly ICurrentUser _currentUser;
        private readonly INotificationService _notification;
        private readonly IMenuService _menuService;
        private readonly AppDataContext _ctx;

        public QnbWorkFlowService(
            IUnitOfWork uow,
            TypeAdapterConfig config,
            IAuthService authService,
            IActivationRecordService activationRecord,
            ILogger<QnbWorkFlowService> logger,
            IMailPushService mailPush,
            ICurrentUser currentUser,
            AppDataContext ctx,
            INotificationService notification,
            IMenuService menuService)
        {
            _uow = uow;
            _config = config;
            _activationRecord = activationRecord;
            _logger = logger;
            _mailPush = mailPush;
            _currentUser = currentUser;
            _ctx = ctx;
            _notification = notification;
            _menuService = menuService;
        }

        // -------------------- Customer Form / Services Request --------------------

        // 0 Müşteri kendi formunu oluşturulması ve Servis talebine gönderim.
        public async Task<ResponseModel<QnbCustomerFormGetDto>> CreateCustomerForm(QnbCustomerFormCreateDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                // Başlangıç WorkFlowStep'i Bul
                var targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "SR");

                if (targetStep is null)
                    return ResponseModel<QnbCustomerFormGetDto>.Fail("İş akışı hedef adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                // RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("QNB");
                    if (!rn.IsSuccess)
                        return ResponseModel<QnbCustomerFormGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                bool exists = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .AnyAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (exists)
                    return ResponseModel<QnbCustomerFormGetDto>.Fail("Aynı akış numarasi ile başka bir kayıt zaten var.", StatusCode.Conflict);

                var customerExist = await _uow.Repository.GetQueryable<Customer>().AsNoTracking()
                    .AnyAsync(c => c.Id == dto.CustomerId);
                if (!customerExist)
                    return ResponseModel<QnbCustomerFormGetDto>.Fail("Müşteri bulunamadı.", StatusCode.Conflict);

                var customerApproverExist = dto.CustomerApproverId.HasValue
                    ? await _uow.Repository.GetQueryable<ProgressApprover>().AsNoTracking().AnyAsync(ca => ca.Id == dto.CustomerApproverId.Value)
                    : true;
                if (!customerApproverExist)
                    return ResponseModel<QnbCustomerFormGetDto>.Fail("Müşteri yetkilisi bulunamadı.", StatusCode.Conflict);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Müşteri formu Oluşturma
                var customerForm = dto.Adapt<QnbCustomerForm>(_config);
                customerForm.CreatedDate = DateTime.Now;
                customerForm.CreatedUser = meId;
                customerForm.Status = QnbCustomerFormStatus.Draft;
                await _uow.Repository.AddAsync(customerForm);
                #endregion

                #region WorkFlow oluştur (aynı RequestNo ile)
                var wf = new QnbWorkFlow
                {
                    RequestNo = customerForm.RequestNo,
                    RequestTitle = dto.Title ?? "",
                    Priority = dto.Priority,
                    CurrentStepId = targetStep.Id,
                    CreatedDate = DateTime.Now,
                    CreatedUser = meId,
                    WorkFlowStatus = WorkFlowStatus.Pending,
                    IsAgreement = null,
                };
                await _uow.Repository.AddAsync(wf);
                #endregion

                #region Servis talebi oluşturma
                var request = customerForm.Adapt<QnbServicesRequest>(_config);
                request.CreatedDate = DateTime.Now;
                request.CreatedUser = meId;
                request.ServicesRequestStatus = ServicesRequestStatus.Draft;
                request.Id = 0;
                await _uow.Repository.AddAsync(request);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.ServiceRequestCreated,
                    request.RequestNo,
                    null,
                    dto.CustomerId,
                    targetStep.Code,
                    "CF",
                    "Müşteri talap formu oluşturuldu ve servis talebine gönderildi",
                    new
                    {
                        dto,
                        request.Id,
                    });
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForUserAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.GenericInfo,
                        Title = $"Talep {dto.RequestNo} oluşturuldu",
                        Message = $"{dto.RequestNo} numaralı talebiniz oluşturuldu ve servis talebine iletildi.",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "CF",
                        ToStepCode = "SR",
                    },
                    userId: meId
                );

                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.GenericInfo,
                        Title = $"Talep {dto.RequestNo} oluşturuldu",
                        Message = $"{dto.RequestNo} numaralı akış talebi müşteri tarafından iletildi.",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "CF",
                        ToStepCode = "SR",
                    },
                    roleCodes: ["PROJECTENGINEER", "ADMIN"]
                );
                #endregion

                return await GetCustomerFormByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateCustomerForm");
                return ResponseModel<QnbCustomerFormGetDto>.Fail($"CreateCustomerForm Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 1 Servis Talebi güncelleme adımı
        public async Task<ResponseModel<QnbServicesRequestGetDto>> UpdateServiceRequestAsync(QnbServicesRequestUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<QnbServicesRequest>(
                false,
                x => x.RequestNo == dto.RequestNo,
                includeExpression: RequestIncludes());

            if (entity is null)
                return ResponseModel<QnbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var wf = await _uow.Repository
                .GetQueryable<QnbWorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<QnbServicesRequestGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            // Ana talep bilgilerini güncelle
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = meId;
            wf.IsLocationValid = dto.IsLocationValid;
            wf.ApproverTechnicianId = dto.ApproverTechnicianId;
            wf.CustomerApproverName = dto.CustomerApproverName;
            wf.Priority = dto.Priority;
            wf.RequestTitle = dto.Title;
            _uow.Repository.Update(wf);

            dto.Adapt(entity, _config);
            entity.ServicesRequestStatus = ServicesRequestStatus.Draft;

            // Mevcut ürünleri çek (RequestNo bazlı)
            var existingProducts = await _uow.Repository
                .GetMultipleAsync<QnbServicesRequestProduct>(
                    asNoTracking: false,
                    whereExpression: x => x.RequestNo == dto.RequestNo);

            // Ürün listesi değişmişse:
            if (dto.Products is not null)
            {
                var updatedProducts = dto.Products
                    .GroupBy(p => p.ProductId)
                    .Select(g => g.First())
                    .ToDictionary(p => p.ProductId, p => p);

                existingProducts ??= new List<QnbServicesRequestProduct>();

                // Silinecek ürünler
                var toRemove = existingProducts
                    .Where(p => !updatedProducts.ContainsKey(p.ProductId))
                    .ToList();

                // Eklenecek ürünler
                var toAdd = updatedProducts
                    .Where(p => !existingProducts.Any(e => e.ProductId == p.Key))
                    .Select(p => p.Value)
                    .ToList();

                // Güncellenecek ürünler
                var toUpdate = existingProducts
                    .Where(p => updatedProducts.ContainsKey(p.ProductId))
                    .ToList();

                // ❌ Sil
                foreach (var prod in toRemove)
                    await _uow.Repository.HardDeleteAsync(prod);

                // ➕ Ekle
                foreach (var prod in toAdd)
                {
                    var entityProd = new QnbServicesRequestProduct
                    {
                        RequestNo = dto.RequestNo,
                        ProductId = prod.ProductId,
                        Quantity = prod.Quantity,
                        CustomerId = dto.CustomerId,
                    };
                    await _uow.Repository.AddAsync(entityProd);
                }

                // 🔁 Güncelle
                foreach (var prod in toUpdate)
                {
                    var dtoProd = updatedProducts[prod.ProductId];
                    prod.Quantity = dtoProd.Quantity;
                    prod.CustomerId = dto.CustomerId;
                    prod.RequestNo = dto.RequestNo;
                    prod.ProductId = dtoProd.ProductId;
                    _uow.Repository.Update(prod);
                }
            }
            else
            {
                foreach (var item in existingProducts)
                {
                    await _uow.Repository.HardDeleteAsync(item);
                }
            }

            await _uow.Repository.UpdateAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetServiceRequestByRequestNoAsync(entity.RequestNo);
        }

        // 2.1 Depoya Gönderim (Ürün var ise)
        public async Task<ResponseModel<QnbWarehouseGetDto>> SendWarehouseAsync(QnbSendWarehouseDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                if (request.ServicesRequestStatus == ServicesRequestStatus.WarehouseSubmitted)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);

                var product = await _uow.Repository
                    .GetQueryable<QnbServicesRequestProduct>(x => x.RequestNo == dto.RequestNo)
                    .ToListAsync();
                if (product is null || product.Count == 0)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("Bu talep için kayıtlı ürün bulunamadı. Depoya gönderim için en az bir ürün eklenmiş olmalıdır.", StatusCode.BadRequest);

                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "WH");

                var warehouse = await _uow.Repository
                    .GetQueryable<QnbWarehouse>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Depo Ekle/Güncelle
                if (warehouse == null)
                {
                    warehouse = new QnbWarehouse
                    {
                        RequestNo = request.RequestNo,
                        DeliveryDate = dto.DeliveryDate,
                        Description = string.Empty,
                        WarehouseStatus = WarehouseStatus.Pending
                    };
                    warehouse.CreatedDate = DateTime.Now;
                    warehouse.CreatedUser = meId;

                    warehouse = await _uow.Repository.AddAsync(warehouse);
                }
                else
                {
                    warehouse.UpdatedDate = DateTime.Now;
                    warehouse.UpdatedUser = meId;
                    warehouse.DeliveryDate = dto.DeliveryDate;
                    warehouse.WarehouseStatus = WarehouseStatus.Pending;
                    _uow.Repository.Update(warehouse);
                }
                #endregion

                #region Servis Talebi Güncelle
                request.WorkFlowStepId = targetStep.Id;
                request.QnbWorkFlowStep = null;
                request.UpdatedDate = DateTime.Now;
                request.UpdatedUser = meId;
                request.ServicesRequestStatus = ServicesRequestStatus.WarehouseSubmitted;
                _uow.Repository.Update(request);
                #endregion

                #region WorkFlow güncelle
                wf.CurrentStepId = targetStep.Id;
                wf.IsAgreement = null;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.WarehouseSent,
                    request.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "SR",
                    "WH",
                    "Talep depoya gönderildi",
                    new
                    {
                        DeliveryDate = dto.DeliveryDate,
                        Products = product.Select(x => new { x.ProductId, x.Quantity })
                    }
                );
                #endregion

                #region Bilgilendirme Maili
                await PushTransitionMailsAsync(
                    wf: wf,
                    fromCode: "SR",
                    toCode: "WH",
                    requestNo: dto.RequestNo,
                    customerName: request.Customer?.ContactName1
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} depoya gönderildi",
                        Message = $"Akış {"SR"} → {"WH"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "SR",
                        ToStepCode = "WH",
                        Payload = new
                        {
                            wfId = wf.Id,
                            deliveryDate = dto.DeliveryDate
                        }
                    },
                    roleCodes: ["WAREHOUSE", "ADMIN"]
                );
                #endregion

                return await GetWarehouseByRequestNoAsync(request.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendWarehouseAsync");
                return ResponseModel<QnbWarehouseGetDto>.Fail($"Depo gönderim sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 2.2 Depo Teslimatı ve Teknik servise Gönderim (Ürün var ise)
        public async Task<ResponseModel<QnbWarehouseGetDto>> CompleteDeliveryAsync(QnbCompleteDeliveryDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var warehouse = await _uow.Repository
                    .GetQueryable<QnbWarehouse>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (warehouse is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("Depo kaydı bulunamadı.", StatusCode.NotFound);

                var targetStep = await _uow.Repository
                    .GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .Where(x => x.Code != null && x.Code == "TS")
                    .Select(x => new { x.Id })
                    .FirstOrDefaultAsync();

                if (targetStep is null)
                    return ResponseModel<QnbWarehouseGetDto>.Fail("WorkFlowStep içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Teknik servis kaydı Ekle/Güncelle
                var technicalService = await _uow.Repository
                    .GetQueryable<QnbTechnicalService>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is { })
                {
                    technicalService.RequestNo = dto.RequestNo;
                    technicalService.ServiceTypeId = request.ServiceTypeId;
                    technicalService.StartTime = null;
                    technicalService.EndTime = null;
                    technicalService.StartLocation = string.Empty;
                    technicalService.EndLocation = string.Empty;
                    technicalService.Latitude = request.Customer.Latitude;
                    technicalService.Longitude = request.Customer.Longitude;
                    technicalService.ServicesStatus = TechnicalServiceStatus.Pending;
                    technicalService.ServicesCostStatus = request.ServicesCostStatus;
                    technicalService.UpdatedDate = DateTime.Now;
                    technicalService.UpdatedUser = meId;
                    _uow.Repository.Update(technicalService);
                }
                else
                {
                    technicalService = new QnbTechnicalService
                    {
                        RequestNo = dto.RequestNo,
                        ServiceTypeId = request.ServiceTypeId,
                        StartTime = null,
                        EndTime = null,
                        StartLocation = string.Empty,
                        EndLocation = string.Empty,
                        ProblemDescription = string.Empty,
                        ResolutionAndActions = string.Empty,
                        Latitude = request.Customer.Latitude,
                        Longitude = request.Customer.Longitude,
                        ServicesStatus = TechnicalServiceStatus.Pending,
                        ServicesCostStatus = request.ServicesCostStatus,
                        CreatedDate = DateTime.Now,
                        CreatedUser = meId
                    };
                    _uow.Repository.Add(technicalService);
                }
                #endregion

                #region Warehouse bilgilerini güncelle
                warehouse.DeliveryDate = dto.DeliveryDate;
                warehouse.Description = dto.Description;
                warehouse.WarehouseStatus = WarehouseStatus.Shipped;
                _uow.Repository.Update(warehouse);
                #endregion

                #region Workflow kaydı güncelle
                wf.CurrentStepId = targetStep.Id;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Ürünler Ekle/Güncelle
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<QnbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto.DeliveredProducts.ToDictionary(x => x.ProductId, x => x);

                // Güncelle veya Sil
                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        existing.Quantity = delivered.Quantity;
                        _uow.Repository.Update(existing);
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        _uow.Repository.HardDelete(existing);
                    }
                }

                // Yeni ürünleri ekle
                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new QnbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = dto.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.WorkFlowStepChanged,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "WH",
                    "TS",
                    "Depo teslimatı tamamlandı, Teknik Servise geçildi",
                    new
                    {
                        warehouse.Id,
                        tecnicianName = wf?.ApproverTechnician?.TechnicianName ?? "",
                        technicalServiceId = technicalService.Id,
                        DeliveredProducts = dto.DeliveredProducts?.Select(p => new { p.ProductId, p.Quantity })
                    }
                );
                #endregion

                #region Bilgilendirme Maili
                await PushTransitionMailsAsync(
                    wf, fromCode: "WH", toCode: "TS",
                    requestNo: dto.RequestNo,
                    customerName: request.Customer?.ContactName1
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                if (wf.ApproverTechnicianId.HasValue)
                {
                    await _notification.CreateForUserAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} teknik servise gönderildi",
                            Message = $"Akış {"WH"} → {"TS"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "SR",
                            ToStepCode = "TS",
                            Payload = new { wfId = wf.Id }
                        },
                        wf.ApproverTechnicianId.Value
                    );
                }
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} teknik servise gönderildi",
                        Message = $"Akış {"WH"} → {"TS"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "WH",
                        ToStepCode = "TS",
                        Payload = new { wfId = wf.Id }
                    },
                    roleCodes: ["TECHNICIAN", "ADMIN"]
                );
                #endregion

                return await GetWarehouseByIdAsync(warehouse.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteDeliveryAsync");
                return ResponseModel<QnbWarehouseGetDto>.Fail($" Depo Teslimatı  sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 2.3 Teknik Servis Gönderim (Ürün yok ise)
        public async Task<ResponseModel<QnbTechnicalServiceGetDto>> SendTechnicalServiceAsync(QnbSendTechnicalServiceDto dto)
        {
            try
            {
                #region Validasyonlar/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "TS");
                if (targetStep is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Teknik servis kaydını Ekle/Güncelle
                var technicalService = await _uow.Repository
                    .GetQueryable<QnbTechnicalService>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is { })
                {
                    technicalService.RequestNo = dto.RequestNo;
                    technicalService.ServiceTypeId = request.ServiceTypeId;
                    technicalService.StartTime = null;
                    technicalService.EndTime = null;
                    technicalService.StartLocation = string.Empty;
                    technicalService.EndLocation = string.Empty;
                    technicalService.Latitude = request.Customer.Latitude;
                    technicalService.Longitude = request.Customer.Longitude;
                    technicalService.ServicesStatus = TechnicalServiceStatus.Pending;
                    technicalService.ServicesCostStatus = request.ServicesCostStatus;
                    _uow.Repository.Update(technicalService);
                }
                else
                {
                    technicalService = new QnbTechnicalService
                    {
                        RequestNo = dto.RequestNo,
                        ServiceTypeId = request.ServiceTypeId,
                        StartTime = null,
                        EndTime = null,
                        StartLocation = string.Empty,
                        EndLocation = string.Empty,
                        ProblemDescription = string.Empty,
                        ResolutionAndActions = string.Empty,
                        Latitude = request.Customer.Latitude,
                        Longitude = request.Customer.Longitude,
                        ServicesStatus = TechnicalServiceStatus.Pending,
                        ServicesCostStatus = request.ServicesCostStatus,
                    };
                    _uow.Repository.Add(technicalService);
                }
                #endregion

                #region Servis Talebi
                request.ServicesRequestStatus = ServicesRequestStatus.TechnicialServiceSubmitted;
                #endregion

                #region Workflow Güncelle
                wf.CurrentStepId = targetStep.Id;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.WorkFlowStepChanged,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "SR",
                    "TS",
                    "Teknik servise gönderildi (ürün yok)",
                    new
                    {
                        tecnicianName = wf?.ApproverTechnician?.TechnicianName ?? "",
                        technicalServiceId = technicalService.Id,
                    }
                );
                #endregion

                #region Bilgilendirme Maili
                await PushTransitionMailsAsync(
                    wf, fromCode: "SR", toCode: "TS",
                    requestNo: dto.RequestNo,
                    customerName: request.Customer?.ContactName1
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                if (wf.ApproverTechnicianId.HasValue)
                {
                    await _notification.CreateForUserAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} teknik servise gönderildi",
                            Message = $"Akış {"SR"} → {"TS"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "SR",
                            ToStepCode = "TS",
                            Payload = new { wfId = wf.Id }
                        },
                        wf.ApproverTechnicianId.Value
                    );
                }

                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} teknik servise gönderildi",
                        Message = $"Akış {"SR"} → {"TS"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "SR",
                        ToStepCode = "TS",
                        Payload = new { wfId = wf.Id }
                    },
                    roleCodes: ["TECHNICIAN", "ADMIN"]
                );
                #endregion

                return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendTechnicalServiceAsync");
                return ResponseModel<QnbTechnicalServiceGetDto>.Fail($"Teknik Servis Gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }
        // 3 Teknik Servis Servisi Başlatma
        public async Task<ResponseModel<QnbTechnicalServiceGetDto>> StartService(QnbStartTechnicalServiceDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var customer = await _uow.Repository
                    .GetQueryable<Customer>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

                if (customer is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

                var technicalService = await _uow.Repository
                    .GetQueryable<QnbTechnicalService>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

                if (technicalService.ServicesStatus == TechnicalServiceStatus.InProgress)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Teknik servis zaten başlatılmış", StatusCode.Conflict);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Lokasyon kontrolü
                if (technicalService.IsLocationCheckRequired)
                {
                    if (string.IsNullOrEmpty(dto.Longitude) && !string.IsNullOrEmpty(dto.Latitude))
                    {
                        return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.InvalidCustomerLocation);
                    }
                    else
                    {
                        var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, dto.Latitude, dto.Longitude);
                        if (!locationResult.IsSuccess)
                        {
                            #region Hareket Loglama
                            await _activationRecord.LogQnbAsync(
                                WorkFlowActionType.LocationCheckFailed,
                                dto.RequestNo,
                                wf.Id,
                                request.CustomerId,
                                "TS",
                                "TS",
                                "Lokasyon kontrolü başarısız",
                                new { locationResult.Message }
                            );
                            #endregion

                            return ResponseModel<QnbTechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
                        }
                    }
                }
                #endregion

                #region Tekniks servisi güncelle
                technicalService.StartTime = DateTime.Now;
                technicalService.ServicesStatus = TechnicalServiceStatus.InProgress;
                technicalService.StartLocation = dto.StartLocation;
                technicalService.EndLocation = string.Empty;
                technicalService.UpdatedDate = DateTime.Now;
                technicalService.UpdatedUser = meId;
                _uow.Repository.Update(technicalService);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.TechnicalServiceStarted,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "TS",
                    "TS",
                    "Teknik servis başlatıldı",
                    new { dto.StartLocation, technicalService.Id }
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"{dto.RequestNo} Servis başladı",
                        Message = $"{dto.RequestNo} Numaralı talep servisi başladı",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "SR",
                        ToStepCode = "SR",
                        Payload = new { wfId = wf.Id }
                    },
                    roleCodes: ["PROJECTENGINEER", "TECHNICIAN", "ADMIN"]
                );
                #endregion

                return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartService");
                return ResponseModel<QnbTechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Başlatma   sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 3.1 Teknik Servis Servisi Tamamlama ve Fiyatlamaya gönderimi
        public async Task<ResponseModel<QnbTechnicalServiceGetDto>> FinishService(QnbFinishTechnicalServiceDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var customer = await _uow.Repository
                    .GetQueryable<Customer>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

                if (customer is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

                var technicalService = await _uow.Repository
                    .GetQueryable<QnbTechnicalService>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

                var targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "PRC");
                if (targetStep is null)
                    return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (PRC) tanımlı değil.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion
                #region Lokasyon kontrolü
                if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
                {
                    if (string.IsNullOrEmpty(dto.Longitude) && !string.IsNullOrEmpty(dto.Latitude))
                    {
                        return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.InvalidCustomerLocation);
                    }
                    else
                    {
                        var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, dto.Latitude, dto.Longitude);
                        if (!locationResult.IsSuccess)
                        {
                            #region Hareket Loglama
                            await _activationRecord.LogAsync(
                               WorkFlowActionType.LocationCheckFailed,
                               dto.RequestNo,
                               wf.Id,
                               request.CustomerId,
                               "TS",
                               "TS",
                               "Lokasyon kontrolü başarısız",
                               new { locationResult.Message }
                           );
                            #endregion

                            return ResponseModel<QnbTechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
                        }
                    }
                }
                #endregion

                #region Teknik Servis Kaydı güncelle
                technicalService.EndTime = DateTime.Now;
                technicalService.ServicesStatus = TechnicalServiceStatus.Completed;
                technicalService.ProblemDescription = dto.ProblemDescription;
                technicalService.ResolutionAndActions = dto.ResolutionAndActions;
                technicalService.ServiceTypeId = dto.ServiceTypeId;
                technicalService.EndLocation = dto.EndLocation;
                technicalService.ServicesCostStatus = dto.ServicesCostStatus;
                technicalService.UpdatedDate = DateTime.Now;
                technicalService.UpdatedUser = meId;
                _uow.Repository.Update(technicalService);
                #endregion

                #region Workflow güncelle
                wf.CurrentStepId = targetStep.Id;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Fiyatlamaya Gönder
                var pricing = await _uow.Repository
                    .GetQueryable<QnbPricing>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (pricing is null)
                {
                    pricing = new QnbPricing()
                    {
                        RequestNo = dto.RequestNo,
                        Status = PricingStatus.Pending,
                        Currency = "TRY",
                        Notes = string.Empty,
                        TotalAmount = 0,
                        CreatedDate = DateTime.Now,
                        CreatedUser = meId,
                    };
                    _uow.Repository.Add(pricing);
                }
                else
                {
                    pricing.Status = PricingStatus.Pending;
                    pricing.RequestNo = dto.RequestNo;
                    pricing.Currency = "TRY";
                    pricing.UpdatedDate = DateTime.Now;
                    pricing.UpdatedUser = meId;
                    _uow.Repository.Update(pricing);
                }
                #endregion

                #region Dosya Ekleme/Güncelleme işlemleri
                var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
                var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? "";
                var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "UploadsStorage");
                Directory.CreateDirectory(uploadRoot);

                static bool IsAllowed(string fileName, string? contentType)
                {
                    var ext = Path.GetExtension(fileName).ToLowerInvariant();
                    var okExt = new HashSet<string> { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
                    if (!okExt.Contains(ext)) return false;

                    if (contentType is null) return true;
                    contentType = contentType.ToLowerInvariant();
                    var okCt = new HashSet<string> { "image/jpeg", "image/png", "image/webp", "application/pdf" };
                    return okCt.Contains(contentType);
                }

                async Task<string?> SaveAsync(IFormFile file, CancellationToken ct)
                {
                    if (file.Length <= 0) return null;
                    if (!IsAllowed(file.FileName, file.ContentType))
                        throw new InvalidOperationException($"Desteklenmeyen dosya türü: {file.FileName}");

                    var ext = Path.GetExtension(file.FileName);
                    var name = $"{Guid.NewGuid()}{ext}";
                    var path = Path.Combine(uploadRoot, name);

                    await using var read = file.OpenReadStream();
                    await using var write = new FileStream(
                        path,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 1024 * 64,
                        options: FileOptions.Asynchronous | FileOptions.SequentialScan
                    );
                    await read.CopyToAsync(write, 1024 * 64, ct);

                    return name;
                }

                var toAddImages = new List<QnbTechnicalServiceImage>();
                var toAddFormImages = new List<QnbTechnicalServiceFormImage>();
                var savedFiles = new List<string>();

                try
                {
                    if (dto.ServiceImages is not null)
                    {
                        foreach (var f in dto.ServiceImages)
                        {
                            var url = await SaveAsync(f, CancellationToken.None);
                            if (url is null) continue;
                            toAddImages.Add(new QnbTechnicalServiceImage
                            {
                                QnbTechnicalServiceId = technicalService.Id,
                                Url = url,
                                Caption = "Servis Fotoğrafları"
                            });
                            savedFiles.Add(url);
                        }
                    }

                    if (dto.FormImages is not null)
                    {
                        foreach (var f in dto.FormImages)
                        {
                            var url = await SaveAsync(f, CancellationToken.None);
                            if (url is null) continue;
                            toAddFormImages.Add(new QnbTechnicalServiceFormImage
                            {
                                QnbTechnicalServiceId = technicalService.Id,
                                Url = url,
                                Caption = "Form Resmi"
                            });
                            savedFiles.Add(url);
                        }
                    }

                    if (toAddImages.Count > 0) await _uow.Repository.AddRangeAsync(toAddImages);
                    if (toAddFormImages.Count > 0) await _uow.Repository.AddRangeAsync(toAddFormImages);
                }
                catch
                {
                    throw;
                }
                #endregion

                #region Ürünler Güncellemesi
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<QnbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x)
                                    ?? new Dictionary<long, QnbServicesRequestProductCreateDto>();

                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        existing.Quantity = delivered.Quantity;
                        _uow.Repository.Update(existing);
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        _uow.Repository.HardDelete(existing);
                    }
                }

                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new QnbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.TechnicalServiceFinished,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "TS",
                    "PRC",
                    "Teknik servis tamamlandı ve fiyatlama aşamasına geçildi",
                    new
                    {
                        dto.ProblemDescription,
                        dto.ResolutionAndActions,
                        dto.ServiceTypeId,
                        dto.ServicesCostStatus,
                        Images = new
                        {
                            Service = toAddImages.Select(x => x.Url),
                            Form = toAddFormImages.Select(x => x.Url)
                        },
                        Products = dto.Products?.Select(p => new { p.ProductId, p.Quantity })
                    }
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo}  Servis işlemi tamamlandı ve fiyatlamaya gönderildi",
                        Message = $"Akış {"TS"} → {"PRC"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "TS",
                        ToStepCode = "PRC",
                    },
                    roleCodes: ["PROJECTENGINEER", "TECHNICIAN", "ADMIN"]
                );
                #endregion

                return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinishService");
                return ResponseModel<QnbTechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi   sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 4 Fiyatlama onay ve kontrole gönderim.
        public async Task<ResponseModel<QnbPricingGetDto>> ApprovePricing(QnbPricingUpdateDto dto)
        {
            try
            {
                #region Validasyonlar/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbPricingGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbPricingGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbPricingGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbPricingGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "APR");
                if (targetStep is null)
                    return ResponseModel<QnbPricingGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                var pricing = await _uow.Repository
                    .GetQueryable<QnbPricing>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (pricing is null)
                    return ResponseModel<QnbPricingGetDto>.Fail("Fiyatlama kaydı tanımlı değil.", StatusCode.BadRequest);

                var servicesRequest = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (servicesRequest is null)
                    return ResponseModel<QnbPricingGetDto>.Fail("Servis talebi kaydı bulunamadı.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Fiyatlama ve Workflow güncelleme
                pricing.Status = PricingStatus.Approved;
                pricing.UpdatedDate = DateTime.Now;
                pricing.UpdatedUser = meId;
                pricing.Notes = dto.Notes;
                pricing.TotalAmount = dto.TotalAmount;
                _uow.Repository.Update(pricing);

                wf.CurrentStepId = targetStep.Id;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Servis Maliyet Durumu Güncelleme
                servicesRequest.ServicesCostStatus = dto.ServicesCostStatus;
                _uow.Repository.Update(servicesRequest);
                #endregion

                #region Ürünler Güncellemesi
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<QnbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x)
                                    ?? new Dictionary<long, QnbServicesRequestProductCreateDto>();

                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        existing.Quantity = delivered.Quantity;
                        _uow.Repository.Update(existing);
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        _uow.Repository.HardDelete(existing);
                    }
                }

                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new QnbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Ürün Fiyat Sabitleme (4. Adım)
                await EnsurePricesCapturedFromDtoAsync(dto.RequestNo, dto.Products);
                #endregion

                #region Son Onaya Gönderim
                var finalApproval = await _uow.Repository
                    .GetQueryable<QnbFinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);
                if (finalApproval is null)
                {
                    finalApproval = new QnbFinalApproval
                    {
                        RequestNo = dto.RequestNo,
                        Status = FinalApprovalStatus.Pending,
                        CreatedDate = DateTime.Now,
                        CreatedUser = meId
                    };
                    _uow.Repository.Add(finalApproval);
                }
                else
                {
                    finalApproval.RequestNo = dto.RequestNo;
                    finalApproval.Status = FinalApprovalStatus.Pending;
                    finalApproval.UpdatedDate = DateTime.Now;
                    finalApproval.UpdatedUser = meId;
                    _uow.Repository.Update(finalApproval);
                }
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.PricingApproved,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "PRC",
                    "APR",
                    "Fiyatlama tamamlandı ve onay aşamasına geçildi",
                    new
                    {
                        dto.Notes,
                        TotalAmount = dto.Products?.Sum(x => x.Price),
                        dto.Status,
                        meId,
                        DateTime.Now,
                        Products = dto.Products?.Select(p => new
                        {
                            p.ProductId,
                            p.Quantity,
                            p.Price
                        }),
                    }
                );
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} son oanaya  gönderildi",
                        Message = $"Akış {"PRC"} → {"APR"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "PRC",
                        ToStepCode = "APR",
                    },
                    roleCodes: ["PROJECTENGINEER", "ADMIN"]
                );
                #endregion

                return await GetPricingByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApprovePricing");
                return ResponseModel<QnbPricingGetDto>.Fail($" Fiyatlama onay ve kontrole gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 5 Kontrol ve Son Onay (FinalApproval)
        public async Task<ResponseModel<QnbFinalApprovalGetDto>> FinalApprovalAsync(QnbFinalApprovalUpdateDto dto)
        {
            try
            {
                #region Validasyonlar/Kontroller
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("İlgili akış tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var statusCode = dto.FinalApprovalStatus == FinalApprovalStatus.CustomerApproval
                    ? "CAPR"
                    : dto.WorkFlowStatus switch
                    {
                        WorkFlowStatus.Cancelled => "CNC",
                        WorkFlowStatus.Complated => "CMP",
                        _ => "APR"
                    };

                var targetStep = await _uow.Repository
                    .GetQueryable<QnbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == statusCode);

                if (targetStep is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail($"Hedef iş akışı adımı {statusCode} tanımlı değil.", StatusCode.BadRequest);

                var existsFinalApproval = await _uow.Repository
                    .GetQueryable<QnbFinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (existsFinalApproval is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.BadRequest);

                if (existsFinalApproval.Status == FinalApprovalStatus.CustomerApproval)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail($"Hedef iş akışı müşteri onayında.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Workflow Güncelleme
                if (wf is not null)
                {
                    wf.CurrentStepId = targetStep.Id;
                    wf.UpdatedDate = DateTime.Now;
                    wf.UpdatedUser = meId;
                    wf.WorkFlowStatus = dto.FinalApprovalStatus == FinalApprovalStatus.CustomerApproval
                        ? WorkFlowStatus.Pending
                        : dto.WorkFlowStatus;
                    _uow.Repository.Update(wf);
                }
                #endregion

                #region Ürünler Güncellemesi
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<QnbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x)
                                    ?? new Dictionary<long, QnbServicesRequestProductCreateDto>();

                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        existing.Quantity = delivered.Quantity;
                        existing.CapturedUnitPrice = delivered.Price;
                        _uow.Repository.Update(existing);
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        _uow.Repository.HardDelete(existing);
                    }
                }

                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new QnbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Ürün Fiyat Sabitleme (5. Adım)
                await EnsurePricesCapturedFromDtoAsync(dto.RequestNo, dto.Products);
                #endregion

                #region Fiyatlama Güncelleme (FinalApproval)
                existsFinalApproval.Notes = dto.Notes;
                existsFinalApproval.Status = dto.WorkFlowStatus == WorkFlowStatus.Complated
                    ? FinalApprovalStatus.Approved
                    : (dto.WorkFlowStatus == WorkFlowStatus.Cancelled ? FinalApprovalStatus.Rejected : FinalApprovalStatus.Pending);

                existsFinalApproval.DecidedBy = meId;
                existsFinalApproval.UpdatedDate = DateTime.Now;
                existsFinalApproval.UpdatedUser = meId;

                existsFinalApproval.DiscountPercent = dto.DiscountPercent;
                existsFinalApproval.Status = dto.FinalApprovalStatus;

                _uow.Repository.Update(existsFinalApproval);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogQnbAsync(
                    WorkFlowActionType.FinalApprovalUpdated,
                    dto.RequestNo,
                    wf?.Id,
                    request.CustomerId,
                    fromStepCode: wf?.CurrentStep?.Code ?? "APR",
                    toStepCode: "APR",
                    "Kontrol ve Son Onay kaydı güncellendi.",
                    new
                    {
                        dto.Notes,
                        dto.WorkFlowStatus,
                        meId,
                        TotalAmount = dto.Products?.Sum(x => x.Price),
                        DateTime.Now,
                        Products = dto.Products?.Select(p => new
                        {
                            p.ProductId,
                            p.Quantity,
                            p.Price
                        })
                    }
                );
                #endregion

                #region Arşivleme
                if (dto.WorkFlowStatus == WorkFlowStatus.Complated || dto.WorkFlowStatus == WorkFlowStatus.Cancelled)
                {
                    var reason = dto.WorkFlowStatus == WorkFlowStatus.Complated ? "Completed" : "Cancelled";
                    await ArchiveWorkflowAsync(dto.RequestNo, reason);
                }
                #endregion

                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                if (dto.FinalApprovalStatus == FinalApprovalStatus.CustomerApproval)
                {
                    await _notification.CreateForRolesAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} oanaya  gönderildi",
                            Message = $"Akiş onaya gönderildi",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "APR",
                            ToStepCode = "CAPR",
                        },
                        roleCodes: ["CUSTOMER"]
                    );
                }
                #endregion

                return await GetFinalApprovalByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinalApprovalAsync");
                return ResponseModel<QnbFinalApprovalGetDto>.Fail($"  Kontrol ve Son Onay sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 6 Müşteri Onayı
        public async Task<ResponseModel<QnbFinalApprovalGetDto>> CustomerAgreementAsync(QnbCustomerAgreementDto dto)
        {
            try
            {
                #region Validasyonlar
                var wf = await _uow.Repository
                    .GetQueryable<QnbWorkFlow>()
                    .Include(x => x.CurrentStep)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.CurrentStep?.Code != "CAPR")
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("Bu işlem sadece QNB müşteri onay adımında yapılabilir.", StatusCode.BadRequest);

                var request = await _uow.Repository
                    .GetQueryable<QnbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var finalApproval = await _uow.Repository
                    .GetQueryable<QnbFinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (finalApproval is null)
                    return ResponseModel<QnbFinalApprovalGetDto>.Fail("FinalApproval kaydı bulunamadı.", StatusCode.NotFound);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                if (dto.IsAgreed)
                {
                    // Mutabık Kalındı: akış tamamlanır
                    finalApproval.CustomerNote = dto.CustomerNote;
                    finalApproval.CustomerApprovedBy = meId;
                    finalApproval.CustomerApprovedAt = DateTime.Now;
                    finalApproval.Status = FinalApprovalStatus.Approved;
                    _uow.Repository.Update(finalApproval);

                    wf.IsAgreement = true;
                    wf.WorkFlowStatus = WorkFlowStatus.Complated;
                    wf.UpdatedDate = DateTime.Now;
                    wf.UpdatedUser = meId;
                    _uow.Repository.Update(wf);

                    await _activationRecord.LogQnbAsync(
                        WorkFlowActionType.FinalApprovalUpdated,
                        dto.RequestNo,
                        wf.Id,
                        request.CustomerId,
                        fromStepCode: "CAPR",
                        toStepCode: "APR",
                        "QNB tarafından Mutabık Kalındı ve süreç tamamlandı.",
                        new { dto.CustomerNote }
                    );

                    await ArchiveWorkflowAsync(dto.RequestNo, "Completed");

                    await _notification.CreateForRolesAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} akış tamamlandı",
                            Message = $"QNB son onayı alındı. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "CAPR",
                            ToStepCode = "APR",
                        },
                        roleCodes: ["PROJECTENGINEER", "CUSTOMER", "ADMIN"]
                    );
                }

                await _uow.Repository.CompleteAsync();
                return await GetCustomerAgreementByRequestNoAsync(dto.RequestNo, FinalApprovalStatus.Approved);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CustomerAgreementAsync");
                return ResponseModel<QnbFinalApprovalGetDto>.Fail($"QNB müşteri onayı sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // Lokasyon Kontrolü Ezme Maili
        public async Task<ResponseModel> RequestLocationOverrideAsync(QnbOverrideLocationCheckDto dto)
        {
            // 1) Talep & WorkFlow & Customer & TechnicalService kontrolleri
            var request = await _uow.Repository
                .GetQueryable<QnbServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var wf = await _uow.Repository
                .GetQueryable<QnbWorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel.Fail("İlgili akış  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);
            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

            var customer = await _uow.Repository
                .GetQueryable<Customer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

            if (customer is null)
                return ResponseModel.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

            var technicalService = await _uow.Repository
                .GetQueryable<QnbTechnicalService>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            if (technicalService.IsLocationCheckRequired == false)
                return ResponseModel.Fail("Lokasyon kontrolü zaten devre dışı bırakılmış.", StatusCode.Conflict);

            var me = await _currentUser.GetAsync();
            var techUserId = me?.Id ?? 0;
            var techUserName = me?.TechnicianName ?? me?.Email ?? "Bilinmiyor";

            // 2) Konum alanlarını hazırla
            string custLat = customer.Latitude ?? "-";
            string custLon = customer.Longitude ?? "-";
            string techLat = dto.TechnicianLatitude ?? "-";
            string techLon = dto.TechnicianLongitude ?? "-";

            bool hasCustomerLoc = custLat != "-" && custLon != "-";
            bool hasTechnicianLoc = techLat != "-" && techLon != "-";

            string mapsLinkCustomer = hasCustomerLoc
                ? $"https://www.google.com/maps?q={custLat},{custLon}"
                : "#";

            string mapsLinkTechnician = hasTechnicianLoc
                ? $"https://www.google.com/maps?q={techLat},{techLon}"
                : "#";

            // 3) Mesafeyi güvenli hesapla
            static bool TryParseCoord(string s, out double v)
            {
                v = default;
                if (string.IsNullOrWhiteSpace(s) || s == "-") return false;
                s = s.Trim().Replace(" ", "").Replace(',', '.');
                return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
            }

            double? distanceKm = null;
            if (hasCustomerLoc && hasTechnicianLoc
                && TryParseCoord(techLat, out var tlat)
                && TryParseCoord(techLon, out var tlon)
                && TryParseCoord(custLat, out var clat)
                && TryParseCoord(custLon, out var clon))
            {
                distanceKm = GetDistanceInKm(tlat, tlon, clat, clon);
            }

            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.AppUrl?.TrimEnd('/');
            var subject = $"[Lokasyon Onayı] RequestNo: {dto.RequestNo} – {request.Customer?.ContactName1}";
            var distanceInfo = distanceKm.HasValue ? $"{Math.Round(distanceKm.Value, 2)} km" : "Hesaplanamadı";

            var customerLink = hasCustomerLoc
                ? $"<a href=\"{mapsLinkCustomer}\">Google Maps</a>"
                : string.Empty;

            var technicianLink = hasTechnicianLoc
                ? $"<a href=\"{mapsLinkTechnician}\">Google Maps</a>"
                : string.Empty;

            var viewLink = baseUrl is not null
                ? $"<p><a href=\"{baseUrl}/technical-service/{dto.RequestNo}\">Kaydı görüntüle</a></p>"
                : string.Empty;

            string customerLocRow = hasCustomerLoc
                ? $@"<p><b>Müşteri Konumu:</b> {custLat}, {custLon} {customerLink}</p>"
                : @"<p><b>Müşteri Konumu:</b> <span style=""color:#b00"">Kayıtlı değil / bulunamadı</span></p>";

            string technicianLocRow = hasTechnicianLoc
                ? $@"<p><b>Teknisyen Konumu:</b> {techLat}, {techLon} {technicianLink}</p>"
                : @"<p><b>Teknisyen Konumu:</b> <span style=""color:#b00"">Kayıtlı değil / bulunamadı</span></p>";

            var html = $@"
                 <div style=""font-family:Arial,sans-serif;font-size:14px"">
                     <h3>Teknik Servis Lokasyon Kontrol Aşımı Bilgisi</h3>
                     <p><b>Talep No:</b> {dto.RequestNo}</p>
                     <p><b>Talep Başlığı:</b> {wf.RequestTitle}</p>
                     <p><b>Müşteri:</b> {(request.Customer?.ContactName1 ?? "-")} </p>
                     <p><b>Teknisyen:</b> {techUserName}</p>
                     <hr/>
                     {customerLocRow}
                     {technicianLocRow}
                     <p><b>Kuş Uçuşu Mesafe:</b> {distanceInfo}</p>
                     {(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $"<p><b>Açıklama:</b> {System.Net.WebUtility.HtmlEncode(dto.Reason)}</p>")}
                     <hr/>
                     <p>Bilgi: Bu talep ile teknik servis için lokasyon kontrolü devre dışı bırakılmıştır </p>
                     {viewLink}
                 </div>";

            // Mail alıcıları
            var managerMails = new List<string>();
            var managerMailConfig = await _uow.Repository
                .GetQueryable<Configuration>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name == "TechnicalServiceManagerEmails");

            if (managerMailConfig is not null && !string.IsNullOrWhiteSpace(managerMailConfig.Value))
            {
                managerMails = managerMailConfig.Value
                    .Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (managerMails.Count == 0)
                return ResponseModel.Fail("Yönetici e-posta adresi tanımlı değil.", StatusCode.BadRequest);

            await _mailPush.EnqueueAsync(new MailOutbox
            {
                RequestNo = dto.RequestNo,
                FromStepCode = "TS",
                ToStepCode = "TS",
                ToRecipients = string.Join(";", managerMails),
                Subject = subject,
                BodyHtml = html,
                CreatedUser = me?.Id
            });

            technicalService.IsLocationCheckRequired = false;
            technicalService.UpdatedDate = DateTime.Now;
            technicalService.UpdatedUser = techUserId;
            _uow.Repository.Update(technicalService);

            await _uow.Repository.CompleteAsync();

            return ResponseModel.Success("Lokasyon kontrolü devre dışı bırakma talebi iletildi ve ilgili yöneticilere e-posta gönderildi.");
        }

        // ------------------------- Akışı bir önceki adıma geri alma --------------------------
        public async Task<ResponseModel<QnbWorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes)
        {
            var wf = await _uow.Repository.GetQueryable<QnbWorkFlow>(x => x.RequestNo == requestNo)
                .FirstOrDefaultAsync();

            if (wf is null)
                return ResponseModel<QnbWorkFlowGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled || wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<QnbWorkFlowGetDto>.Fail("İptal edilmiş veya tamamlanmış akışlar geri alınamaz.", StatusCode.Conflict);

            var servicesRequest = await _uow.Repository
                .GetQueryable<QnbServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
            if (servicesRequest is null)
                return ResponseModel<QnbWorkFlowGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var currentStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                .AsNoTracking()
                .Select(s => new { s.Id, s.Code })
                .FirstOrDefaultAsync(s => s.Id == wf.CurrentStepId);

            if (currentStep is null)
                return ResponseModel<QnbWorkFlowGetDto>.Fail("Akışın mevcut adımı bulunamadı.", StatusCode.NotFound);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var targetStep = new QnbWorkFlowStep();
            var warehouse = new QnbWarehouse();
            var technicalService = new QnbTechnicalService();
            var pricing = new QnbPricing();

            switch (currentStep.Code)
            {
                case "PRC": // Fiyatlama → TS
                    pricing = await _uow.Repository
                        .GetQueryable<QnbPricing>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (pricing != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Code == "TS");
                        if (targetStep is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                        technicalService = await _uow.Repository
                            .GetQueryable<QnbTechnicalService>()
                            .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                        if (technicalService is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı Teknik Servis tanımlı değil.", StatusCode.BadRequest);

                        technicalService.ServicesStatus = TechnicalServiceStatus.Pending;
                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = meId;

                        pricing.Status = PricingStatus.AwaitingReview;
                        pricing.UpdatedDate = DateTime.Now;
                        pricing.UpdatedUser = meId;
                        _uow.Repository.Update(technicalService);
                    }
                    break;

                case "TS": // Teknik Servis → SR
                    technicalService = await _uow.Repository
                        .GetQueryable<QnbTechnicalService>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (technicalService != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Code == "SR");
                        if (targetStep is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                        servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;
                        servicesRequest.UpdatedDate = DateTime.Now;
                        servicesRequest.UpdatedUser = meId;
                        _uow.Repository.Update(servicesRequest);

                        technicalService.ServicesStatus = TechnicalServiceStatus.AwaitingReview;
                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = meId;
                        _uow.Repository.Update(technicalService);
                    }
                    break;

                case "WH": // Depo → SR
                    warehouse = await _uow.Repository
                        .GetQueryable<QnbWarehouse>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                    if (warehouse != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Code == "SR");
                        if (targetStep is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                        warehouse.WarehouseStatus = WarehouseStatus.AwaitingReview;
                        warehouse.UpdatedDate = DateTime.Now;
                        warehouse.UpdatedUser = meId;
                        servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;
                        servicesRequest.UpdatedDate = DateTime.Now;
                        servicesRequest.UpdatedUser = meId;
                        _uow.Repository.Update(servicesRequest);
                    }
                    break;

                case "SR":
                    var serviceRequest = await _uow.Repository
                        .GetQueryable<QnbServicesRequest>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (serviceRequest != null)
                    {
                        serviceRequest.UpdatedDate = DateTime.Now;
                        serviceRequest.UpdatedUser = meId;
                        _uow.Repository.Update(serviceRequest);
                    }
                    break;

                case "CAPR":
                    var customerForm = await _uow.Repository
                        .GetQueryable<QnbCustomerForm>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (customerForm != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                            .AsNoTracking()
                            .FirstOrDefaultAsync(s => s.Code == "APR");
                        if (targetStep is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (APR) tanımlı değil.", StatusCode.BadRequest);
                        customerForm.UpdatedDate = DateTime.Now;
                        customerForm.UpdatedUser = meId;
                        customerForm.Status = QnbCustomerFormStatus.AwaitingReview;
                        _uow.Repository.Update(customerForm);

                        var approval = await _uow.Repository.GetQueryable<QnbFinalApproval>()
                            .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                        if (approval is null)
                            return ResponseModel<QnbWorkFlowGetDto>.Fail("Hedef iş akışı (APR) bulunamadı", StatusCode.BadRequest);

                        approval.Status = FinalApprovalStatus.Pending;
                        approval.UpdatedDate = DateTime.Now;
                        approval.UpdatedUser = meId;
                    }
                    break;

                default:
                    break;
            }

            if (targetStep.Code is null)
                return ResponseModel<QnbWorkFlowGetDto>.Fail("Herhangi bir işlem yapılamadı.", StatusCode.BadRequest);

            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = meId;
            _uow.Repository.Update(wf);

            await _activationRecord.LogQnbAsync(
                WorkFlowActionType.WorkFlowStepChanged,
                requestNo,
                wf.Id,
                servicesRequest.CustomerId,
                currentStep.Code,
                targetStep.Code,
                "Akış geri gönderildi",
                new { reviewNotes, targetStep = targetStep.Name }
            );

            var reviewLog = new QnbWorkFlowReviewLog
            {
                QnbWorkFlowId = wf.Id,
                RequestNo = requestNo,
                FromStepId = currentStep.Id,
                FromStepCode = currentStep.Code,
                ToStepId = targetStep.Id,
                ToStepCode = targetStep.Code,
                ReviewNotes = reviewNotes,
                CreatedUser = meId,
                CreatedDate = DateTime.Now
            };

            _uow.Repository.Add(reviewLog);

            await PushTransitionMailsAsync(
                wf, fromCode: currentStep.Code!, toCode: targetStep.Code!,
                requestNo: requestNo,
                customerName: servicesRequest.Customer?.ContactName1
            );

            await _uow.Repository.CompleteAsync();

            #region Notification Kaydı
            var dto = new NotificationCreateDto
            {
                Type = NotificationType.WorkflowSentBack,
                Title = $"Talep {requestNo} geri gönderildi",
                Message = $"Akış {currentStep.Code} → {targetStep.Code} geri alındı.",
                RequestNo = requestNo,
                FromStepCode = currentStep.Code,
                ToStepCode = targetStep.Code,
                ReviewNotes = reviewNotes,
                Payload = new { targetStep = targetStep.Name }
            };

            if (string.Equals(targetStep.Code, "TS", StringComparison.OrdinalIgnoreCase))
            {
                if (wf.ApproverTechnicianId.HasValue && wf.ApproverTechnicianId.Value > 0)
                {
                    dto.TargetUserIds = new List<long> { wf.ApproverTechnicianId.Value };
                    dto.TargetRoleCodes = null;
                }
                else
                {
                    dto.TargetUserIds = null;
                    dto.TargetRoleCodes = new List<string> { "SUBCONTRACTOR" };
                }
            }
            else
            {
                var stepToRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WH"] = "WAREHOUSE",
                    ["TS"] = "SUBCONTRACTOR"
                };

                if (stepToRole.TryGetValue(targetStep.Code ?? string.Empty, out var roleCode))
                {
                    dto.TargetUserIds = null;
                    dto.TargetRoleCodes = new List<string> { roleCode };
                }
            }

            await _notification.CreateAsync(dto);
            #endregion

            return ResponseModel<QnbWorkFlowGetDto>.Success(
                wf.Adapt<QnbWorkFlowGetDto>(_config)
            );
        }

        // ------------------------- Müşteri Yorum Mesajı --------------------------
        public async Task<ResponseModel> SendReviewMessage(QnbCustomerReviewMessageDto dto)
        {
            try
            {
                if (dto is null)
                    return ResponseModel.Fail("Gönderilen veri boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                    return ResponseModel.Fail("Talep numarası boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.FromStepCode) || string.IsNullOrWhiteSpace(dto.ToStepCode))
                    return ResponseModel.Fail("Kaynak ve hedef adım kodları boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.Message))
                    return ResponseModel.Fail("Gönderilecek mesaj boş olamaz.", StatusCode.BadRequest);

                var wf = await _uow.Repository.GetQueryable<QnbWorkFlow>()
                    .Where(x => !x.IsDeleted && x.RequestNo == dto.RequestNo)
                    .FirstOrDefaultAsync();

                if (wf is null)
                    return ResponseModel.Fail("İlgili akış bulunamadı.", StatusCode.Conflict);

                var fromStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .FirstOrDefaultAsync(x => x.Code == dto.FromStepCode);

                var toStep = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                    .FirstOrDefaultAsync(x => x.Code == dto.ToStepCode);

                if (fromStep is null || toStep is null)
                    return ResponseModel.Fail("Hedef adım veya kaynak adım bulunamadı.", StatusCode.Conflict);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                var reviewLog = new QnbWorkFlowReviewLog
                {
                    QnbWorkFlowId = wf.Id,
                    RequestNo = dto.RequestNo,
                    FromStepId = fromStep.Id,
                    FromStepCode = fromStep?.Code ?? "",
                    ToStepId = toStep.Id,
                    ToStepCode = toStep?.Code ?? "",
                    ReviewNotes = dto.Message.Trim(),
                    CreatedUser = meId,
                    CreatedDate = DateTime.Now
                };

                _uow.Repository.Add(reviewLog);
                await _uow.Repository.CompleteAsync();

                #region Notification Kaydı
                await _notification.CreateForRolesAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.GenericInfo,
                        Title = $"Mesaj İletildi ",
                        Message = $"{dto.RequestNo} numaralı akış talebi akış talebi ile ilgili bir mesajınız var.",
                        RequestNo = dto.RequestNo,
                        FromStepCode = fromStep?.Code,
                        ToStepCode = toStep?.Code,
                    },
                    roleCodes: ["PROJECTENGINEER", "ADMIN"]
                );
                #endregion

                return ResponseModel.Success("Mesaj gönderildi.", StatusCode.Ok);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "SendCustomerReviewMessage hata. RequestNo: {RequestNo}, FromStep: {FromStepCode}, ToStep: {ToStepCode}",
                    dto?.RequestNo, dto?.FromStepCode, dto?.ToStepCode);

                throw;
            }
        }

        // -------------------- Customer Form --------------------
        public async Task<ResponseModel<QnbCustomerFormGetDto>> GetCustomerFormByRequestNoAsync(string requestNo)
        {
            // 1) Ana DTO: CF + (WF last) + Customer
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<QnbCustomerForm>().AsNoTracking()
                where sr.RequestNo == requestNo

                join wf0 in _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(1)
                    .DefaultIfEmpty()
                select new QnbCustomerFormGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    Description = sr.Description,
                    Title = wf != null ? wf.RequestTitle : null,
                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerId = sr.CustomerId,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,
                    Priority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    Customer = sr.Customer == null ? null : new CustomerGetDto
                    {
                        Id = sr.Customer.Id,
                        SubscriberCode = sr.Customer.SubscriberCode,
                        SubscriberCompany = sr.Customer.SubscriberCompany,
                        SubscriberAddress = sr.Customer.SubscriberAddress,
                        City = sr.Customer.City,
                        District = sr.Customer.District,
                        LocationCode = sr.Customer.LocationCode,
                        ContactName1 = sr.Customer.ContactName1,
                        Phone1 = sr.Customer.Phone1,
                        Email1 = sr.Customer.Email1,
                        ContactName2 = sr.Customer.ContactName2,
                        Phone2 = sr.Customer.Phone2,
                        Email2 = sr.Customer.Email2,
                        CustomerShortCode = sr.Customer.CustomerShortCode,
                        CorporateLocationId = sr.Customer.CorporateLocationId,
                        Longitude = sr.Customer.Longitude,
                        Latitude = sr.Customer.Latitude,
                        InstallationDate = sr.Customer.InstallationDate,
                        WarrantyYears = sr.Customer.WarrantyYears,
                        CustomerGroupId = sr.Customer.CustomerGroupId,
                        CustomerTypeId = sr.Customer.CustomerTypeId,
                        Note = sr.Customer.Note,
                        CashCenter = sr.Customer.CashCenter,
                        LockType = sr.Customer.LockType,
                        Systems = sr.Customer.CustomerSystemAssignments
                            .Select(a => new CustomerSystemAssignmentGetDto
                            {
                                Id = a.Id,
                                CustomerId = a.CustomerId,
                                CustomerSystemId = a.CustomerSystemId,
                                HasMaintenanceContract = a.HasMaintenanceContract,
                                SystemName = a.CustomerSystem.Name,
                                SystemCode = a.CustomerSystem.Code,
                                CustomerName = a.Customer.SubscriberCompany,
                                CustomerShortCode = a.Customer.CustomerShortCode
                            })
                            .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<QnbCustomerFormGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // CustomerGroup + ProgressApprovers
            if (baseDto.Customer?.CustomerGroupId is long cgId)
            {
                baseDto.Customer.CustomerGroup = await _uow.Repository
                    .GetQueryable<CustomerGroup>()
                    .AsNoTracking()
                    .Where(g => g.Id == cgId)
                    .Select(g => new CustomerGroupGetDto
                    {
                        Id = g.Id,
                        GroupName = g.GroupName,
                        Code = g.Code,
                        ParentGroupId = g.ParentGroupId,
                        ParentGroupName = g.ParentGroup != null ? g.ParentGroup.GroupName : null,
                        ProgressApprovers = g.ProgressApprovers
                            .Select(pa => new ProgressApproverGetDto
                            {
                                Id = pa.Id,
                                FullName = pa.FullName,
                                Email = pa.Email,
                                CustomerGroupId = pa.CustomerGroupId,
                                CustomerGroupName = g.GroupName,
                                Phone = pa.Phone,
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync() ?? new CustomerGroupGetDto();
            }

            // 2) Ürünler (Tenant eklendi)
            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,
                    Quantity = p.Quantity,
                    EffectivePrice =
                        p.Customer.CustomerGroup.GroupProductPrices
                            .Where(gp => gp.ProductId == p.ProductId)
                            .Select(gp => (decimal?)gp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.CustomerProductPrices
                            .Where(cp => cp.ProductId == p.ProductId)
                            .Select(cp => (decimal?)cp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.Tenant.TenantProductPrices
                            .Where(tp => tp.ProductId == p.ProductId)
                            .Select(tp => (decimal?)tp.Price)
                            .FirstOrDefault()
                        ?? (decimal?)p.Product.Price
                        ?? 0m
                })
                .ToListAsync();

            // 3) Review logs (SR adımı)
            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x => x.RequestNo == requestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new QnbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    QnbWorkFlowId = x.QnbWorkFlowId,
                    RequestNo = x.RequestNo,
                    FromStepId = x.FromStepId,
                    FromStepCode = x.FromStepCode,
                    ToStepId = x.ToStepId,
                    ToStepCode = x.ToStepCode,
                    ReviewNotes = x.ReviewNotes,
                    CreatedDate = x.CreatedDate,
                    CreatedUser = x.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<QnbCustomerFormGetDto>.Success(baseDto);
        }

        // -------------------- Services Request --------------------
        private static Func<IQueryable<QnbServicesRequest>, IIncludableQueryable<QnbServicesRequest, object>>? RequestIncludes()
            => q => q
                .Include(x => x.Customer).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(x => x.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.ServiceType)
                .Include(x => x.CustomerApprover)
                .Include(x => x.QnbWorkFlowStep);

        public async Task<ResponseModel<PagedResult<QnbServicesRequestGetDto>>> GetRequestsAsync(QueryParams q)
        {
            var me = await _currentUser.GetAsync();
            if (me is null)
                return ResponseModel<PagedResult<QnbServicesRequestGetDto>>.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var page = q.Page <= 0 ? 1 : q.Page;
            var pageSize = q.PageSize <= 0 ? 20 : q.PageSize;

            var permittedSteps = await GetUserStepsByMenuPermission(me.Id) ?? new List<string>();
            var permittedSet = permittedSteps.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var technicianRoleRaw = await _uow.Repository
                .GetQueryable<Configuration>()
                .AsNoTracking()
                .Where(x => x.Name == "TechnicianRoleCode")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var technicianRoleCodes = CommonFunctions.ParseRoleCodes(technicianRoleRaw ?? "");

            var isTechnician = technicianRoleCodes.Count > 0 &&
                (me.Roles?.Any(r => technicianRoleCodes.Contains(r.Code,
                    StringComparer.OrdinalIgnoreCase)) ?? false);

            IQueryable<QnbWorkFlow> wfBase = _uow.Repository.GetQueryable<QnbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            var myId = me.Id;

            if (!isTechnician && permittedSet.Count == 0)
            {
                wfBase = wfBase.Where(_ => false);
            }
            else
            {
                wfBase = wfBase.Where(w =>
                    w.CurrentStep != null &&
                    permittedSet.Contains(w.CurrentStep.Code) &&
                    (!isTechnician || w.ApproverTechnicianId == myId)
                );
            }

            var allowedRequestNos = wfBase.Select(x => x.RequestNo);

            var query = _uow.Repository.GetQueryable<QnbServicesRequest>();
            query = RequestIncludes()!(query);

            query = query.Where(sr => allowedRequestNos.Contains(sr.RequestNo));

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();
                query = query.Where(x =>
                    x.RequestNo.Contains(term) ||
                    (x.Description != null && x.Description.Contains(term)));
            }

            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ProjectToType<QnbServicesRequestGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<QnbServicesRequestGetDto>>.Success(
                new PagedResult<QnbServicesRequestGetDto>(items, total, page, pageSize)
            );
        }

        public async Task<ResponseModel<QnbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id)
        {
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                where sr.Id == id
                join wf0 in _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(1)
                    .DefaultIfEmpty()
                select new QnbServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.QnbServiceTrackNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    ServicesCostStatus = sr.ServicesCostStatus,
                    Description = sr.Description,
                    Title = wf != null ? wf.RequestTitle : null,
                    IsProductRequirement = sr.IsProductRequirement,
                    IsMailSended = sr.IsMailSended,
                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerApproverName = sr.CustomerApprover.FullName != null ? sr.CustomerApprover.FullName : wf.CustomerApproverName,
                    CustomerId = sr.CustomerId,
                    CustomerName = sr.Customer != null ? sr.Customer.SubscriberCompany : null,
                    ServiceTypeId = sr.ServiceTypeId,
                    ServiceTypeName = sr.ServiceType != null ? sr.ServiceType.Name : null,
                    WorkFlowStepName = sr.QnbWorkFlowStep != null ? sr.QnbWorkFlowStep.Name : null,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,
                    ApproverTechnicianId = wf != null ? wf.ApproverTechnicianId : null,
                    IsLocationValid = wf != null && wf.IsLocationValid,
                    Priority = wf != null ? wf.Priority : WorkFlowPriority.Normal,
                    ServicesRequestStatus = sr.ServicesRequestStatus,

                    Customer = sr.Customer == null ? null : new CustomerGetDto
                    {
                        Id = sr.Customer.Id,
                        SubscriberCode = sr.Customer.SubscriberCode,
                        SubscriberCompany = sr.Customer.SubscriberCompany,
                        SubscriberAddress = sr.Customer.SubscriberAddress,
                        City = sr.Customer.City,
                        District = sr.Customer.District,
                        LocationCode = sr.Customer.LocationCode,
                        ContactName1 = sr.Customer.ContactName1,
                        Phone1 = sr.Customer.Phone1,
                        Email1 = sr.Customer.Email1,
                        ContactName2 = sr.Customer.ContactName2,
                        Phone2 = sr.Customer.Phone2,
                        Email2 = sr.Customer.Email2,
                        CustomerShortCode = sr.Customer.CustomerShortCode,
                        CorporateLocationId = sr.Customer.CorporateLocationId,
                        Longitude = sr.Customer.Longitude,
                        Latitude = sr.Customer.Latitude,
                        InstallationDate = sr.Customer.InstallationDate,
                        WarrantyYears = sr.Customer.WarrantyYears,
                        CustomerGroupId = sr.Customer.CustomerGroupId,
                        CustomerTypeId = sr.Customer.CustomerTypeId,
                        Note = sr.Customer.Note,
                        CashCenter = sr.Customer.CashCenter,
                        LockType = sr.Customer.LockType,
                        Systems = sr.Customer.CustomerSystemAssignments
                            .Select(a => new CustomerSystemAssignmentGetDto
                            {
                                Id = a.Id,
                                CustomerId = a.CustomerId,
                                CustomerSystemId = a.CustomerSystemId,
                                HasMaintenanceContract = a.HasMaintenanceContract,
                                SystemName = a.CustomerSystem.Name,
                                SystemCode = a.CustomerSystem.Code,
                                CustomerName = a.Customer.SubscriberCompany,
                                CustomerShortCode = a.Customer.CustomerShortCode
                            })
                            .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<QnbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == baseDto.RequestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,
                    Quantity = p.Quantity,
                    EffectivePrice =
                        p.Customer.CustomerGroup.GroupProductPrices
                            .Where(gp => gp.ProductId == p.ProductId)
                            .Select(gp => (decimal?)gp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.CustomerProductPrices
                            .Where(cp => cp.ProductId == p.ProductId)
                            .Select(cp => (decimal?)cp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.Tenant.TenantProductPrices
                            .Where(tp => tp.ProductId == p.ProductId)
                            .Select(tp => (decimal?)tp.Price)
                            .FirstOrDefault()
                        ?? (decimal?)p.Product.Price
                        ?? 0m
                })
                .ToListAsync();

            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x => x.RequestNo == baseDto.RequestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new QnbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    QnbWorkFlowId = x.QnbWorkFlowId,
                    RequestNo = x.RequestNo,
                    FromStepId = x.FromStepId,
                    FromStepCode = x.FromStepCode,
                    ToStepId = x.ToStepId,
                    ToStepCode = x.ToStepCode,
                    ReviewNotes = x.ReviewNotes,
                    CreatedDate = x.CreatedDate,
                    CreatedUser = x.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<QnbServicesRequestGetDto>.Success(baseDto);
        }

        public async Task<ResponseModel<QnbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo)
        {
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                where sr.RequestNo == requestNo
                join wf0 in _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(1)
                    .DefaultIfEmpty()
                select new QnbServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.QnbServiceTrackNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    ServicesCostStatus = sr.ServicesCostStatus,
                    Description = sr.Description,
                    Title = wf != null ? wf.RequestTitle : null,
                    IsProductRequirement = sr.IsProductRequirement,
                    IsMailSended = sr.IsMailSended,
                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerApproverName = sr.CustomerApprover.FullName != null ? sr.CustomerApprover.FullName : wf.CustomerApproverName,
                    CustomerId = sr.CustomerId,
                    CustomerName = sr.Customer != null ? sr.Customer.SubscriberCompany : null,
                    ServiceTypeId = sr.ServiceTypeId,
                    ServiceTypeName = sr.ServiceType != null ? sr.ServiceType.Name : null,
                    WorkFlowStepName = sr.QnbWorkFlowStep != null ? sr.QnbWorkFlowStep.Name : null,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,
                    ApproverTechnicianId = wf != null ? wf.ApproverTechnicianId : null,
                    IsLocationValid = wf != null && wf.IsLocationValid,
                    Priority = wf != null ? wf.Priority : WorkFlowPriority.Normal,
                    ServicesRequestStatus = sr.ServicesRequestStatus,

                    Customer = sr.Customer == null ? null : new CustomerGetDto
                    {
                        Id = sr.Customer.Id,
                        SubscriberCode = sr.Customer.SubscriberCode,
                        SubscriberCompany = sr.Customer.SubscriberCompany,
                        SubscriberAddress = sr.Customer.SubscriberAddress,
                        City = sr.Customer.City,
                        District = sr.Customer.District,
                        LocationCode = sr.Customer.LocationCode,
                        ContactName1 = sr.Customer.ContactName1,
                        Phone1 = sr.Customer.Phone1,
                        Email1 = sr.Customer.Email1,
                        ContactName2 = sr.Customer.ContactName2,
                        Phone2 = sr.Customer.Phone2,
                        Email2 = sr.Customer.Email2,
                        CustomerShortCode = sr.Customer.CustomerShortCode,
                        CorporateLocationId = sr.Customer.CorporateLocationId,
                        Longitude = sr.Customer.Longitude,
                        Latitude = sr.Customer.Latitude,
                        InstallationDate = sr.Customer.InstallationDate,
                        WarrantyYears = sr.Customer.WarrantyYears,
                        CustomerGroupId = sr.Customer.CustomerGroupId,
                        CustomerTypeId = sr.Customer.CustomerTypeId,
                        Note = sr.Customer.Note,
                        CashCenter = sr.Customer.CashCenter,
                        LockType = sr.Customer.LockType,
                        Systems = sr.Customer.CustomerSystemAssignments
                            .Select(a => new CustomerSystemAssignmentGetDto
                            {
                                Id = a.Id,
                                CustomerId = a.CustomerId,
                                CustomerSystemId = a.CustomerSystemId,
                                HasMaintenanceContract = a.HasMaintenanceContract,
                                SystemName = a.CustomerSystem.Name,
                                SystemCode = a.CustomerSystem.Code,
                                CustomerName = a.Customer.SubscriberCompany,
                                CustomerShortCode = a.Customer.CustomerShortCode
                            })
                            .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<QnbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            if (baseDto.Customer?.CustomerGroupId is long cgId)
            {
                baseDto.Customer.CustomerGroup = await _uow.Repository
                    .GetQueryable<CustomerGroup>()
                    .AsNoTracking()
                    .Where(g => g.Id == cgId)
                    .Select(g => new CustomerGroupGetDto
                    {
                        Id = g.Id,
                        GroupName = g.GroupName,
                        Code = g.Code,
                        ParentGroupId = g.ParentGroupId,
                        ParentGroupName = g.ParentGroup != null ? g.ParentGroup.GroupName : null,
                        ProgressApprovers = g.ProgressApprovers
                            .Select(pa => new ProgressApproverGetDto
                            {
                                Id = pa.Id,
                                FullName = pa.FullName,
                                Email = pa.Email,
                                CustomerGroupId = pa.CustomerGroupId,
                                CustomerGroupName = g.GroupName,
                                Phone = pa.Phone,
                            })
                            .ToList()
                    })
                    .FirstOrDefaultAsync() ?? new CustomerGroupGetDto();
            }

            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,
                    Quantity = p.Quantity,
                    EffectivePrice =
                        p.Customer.CustomerGroup.GroupProductPrices
                            .Where(gp => gp.ProductId == p.ProductId)
                            .Select(gp => (decimal?)gp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.CustomerProductPrices
                            .Where(cp => cp.ProductId == p.ProductId)
                            .Select(cp => (decimal?)cp.Price)
                            .FirstOrDefault()
                        ?? p.Customer.Tenant.TenantProductPrices
                            .Where(tp => tp.ProductId == p.ProductId)
                            .Select(tp => (decimal?)tp.Price)
                            .FirstOrDefault()
                        ?? (decimal?)p.Product.Price
                        ?? 0m
                })
                .ToListAsync();

            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x => x.RequestNo == requestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new QnbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    QnbWorkFlowId = x.QnbWorkFlowId,
                    RequestNo = x.RequestNo,
                    FromStepId = x.FromStepId,
                    FromStepCode = x.FromStepCode,
                    ToStepId = x.ToStepId,
                    ToStepCode = x.ToStepCode,
                    ReviewNotes = x.ReviewNotes,
                    CreatedDate = x.CreatedDate,
                    CreatedUser = x.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<QnbServicesRequestGetDto>.Success(baseDto);
        }

        public async Task<ResponseModel> DeleteRequestAsync(long id)
        {
            var entity = await _uow.Repository.GetSingleAsync<QnbServicesRequest>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            entity.IsDeleted = true;
            entity.UpdatedDate = DateTime.Now;

            await _uow.Repository.SoftDeleteAsync<QnbServicesRequest, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        // -------------------- Image URL helper (paylaşılan) --------------------
        private static string? NormalizeImageUrlInternal(string? urlOrFileName, string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(urlOrFileName))
                return urlOrFileName;

            if (urlOrFileName.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                urlOrFileName.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return urlOrFileName;
            }

            if (urlOrFileName.StartsWith("/"))
            {
                return string.IsNullOrEmpty(baseUrl) ? urlOrFileName : $"{baseUrl}{urlOrFileName}";
            }

            var relative = $"/uploads/{urlOrFileName}";
            return string.IsNullOrEmpty(baseUrl) ? relative : $"{baseUrl}{relative}";
        }

        // -------------------- Warehouse --------------------
        public async Task<ResponseModel<QnbWarehouseGetDto>> GetWarehouseByIdAsync(long id)
        {
            var qWarehouse = _uow.Repository.GetQueryable<QnbWarehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking();
            var qUsers = _uow.Repository.GetQueryable<User>().AsNoTracking();
            var qCreatedUsers = _uow.Repository.GetQueryable<User>().AsNoTracking();

            var dto = await (
                from w in qWarehouse
                where w.Id == id

                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(1)
                    .DefaultIfEmpty()

                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()

                join cru in qCreatedUsers on sr.CreatedUser equals cru.Id into cruj
                from cu in cruj.DefaultIfEmpty()

                join u0 in qUsers on wf.ApproverTechnicianId equals u0.Id into uj
                from u in uj.DefaultIfEmpty()

                select new QnbWarehouseGetDto
                {
                    Id = w.Id,
                    RequestNo = w.RequestNo,
                    DeliveryDate = w.DeliveryDate,
                    Description = w.Description,
                    WarehouseStatus = w.WarehouseStatus,

                    WorkFlowRequestTitle = wf != null ? wf.RequestTitle : null,
                    WorkFlowPriority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    ServicesRequest = sr == null
                        ? null
                        : new QnbServicesRequestGetDto
                        {
                            Id = sr.Id,
                            RequestNo = sr.RequestNo,
                            ServicesDate = sr.ServicesDate,
                            PlannedCompletionDate = sr.PlannedCompletionDate,
                            ServicesCostStatus = sr.ServicesCostStatus,
                            Title = wf.RequestTitle,
                            Description = sr.Description,
                            IsProductRequirement = sr.IsProductRequirement,
                            IsMailSended = sr.IsMailSended,
                            IsLocationValid = wf.IsLocationValid,
                            CustomerApproverId = sr.CustomerApproverId,
                            CustomerApproverName = wf.CustomerApproverName,
                            CustomerId = sr.CustomerId,
                            CustomerName = sr.Customer.ContactName1 ?? "",
                            ServiceTypeId = sr.ServiceTypeId,
                            CreatedDate = sr.CreatedDate,
                            UpdatedDate = sr.UpdatedDate,
                            CreatedUser = sr.CreatedUser,
                            UpdatedUser = sr.UpdatedUser,
                            IsDeleted = sr.IsDeleted,
                            Priority = sr.Priority,
                            ServicesRequestStatus = sr.ServicesRequestStatus,
                        },

                    Customer = sr != null && sr.Customer != null
                        ? new CustomerGetDto
                        {
                            Id = sr.Customer.Id,
                            SubscriberCode = sr.Customer.SubscriberCode,
                            SubscriberCompany = sr.Customer.SubscriberCompany,
                            SubscriberAddress = sr.Customer.SubscriberAddress,
                            City = sr.Customer.City,
                            District = sr.Customer.District,
                            LocationCode = sr.Customer.LocationCode,
                            ContactName1 = sr.Customer.ContactName1,
                            Phone1 = sr.Customer.Phone1,
                            Email1 = sr.Customer.Email1,
                            ContactName2 = sr.Customer.ContactName2,
                            Phone2 = sr.Customer.Phone2,
                            Email2 = sr.Customer.Email2,
                            CustomerShortCode = sr.Customer.CustomerShortCode,
                            CorporateLocationId = sr.Customer.CorporateLocationId,
                            Longitude = sr.Customer.Longitude,
                            Latitude = sr.Customer.Latitude,
                            InstallationDate = sr.Customer.InstallationDate,
                            WarrantyYears = sr.Customer.WarrantyYears,
                            CustomerGroupId = sr.Customer.CustomerGroupId,
                            CustomerTypeId = sr.Customer.CustomerTypeId,
                            Note = sr.Customer.Note,
                            CashCenter = sr.Customer.CashCenter,
                            LockType = sr.Customer.LockType,
                            Systems = sr.Customer.CustomerSystemAssignments
                                .Select(a => new CustomerSystemAssignmentGetDto
                                {
                                    Id = a.Id,
                                    CustomerId = a.CustomerId,
                                    CustomerSystemId = a.CustomerSystemId,
                                    HasMaintenanceContract = a.HasMaintenanceContract,
                                    SystemName = a.CustomerSystem.Name,
                                    SystemCode = a.CustomerSystem.Code,
                                    CustomerName = a.Customer.SubscriberCompany,
                                    CustomerShortCode = a.Customer.CustomerShortCode
                                })
                                .ToList()
                        }
                        : null,

                    CreatedUser = cu == null ? null : new UserGetDto
                    {
                        Id = cu.Id,
                        TechnicianCode = cu.TechnicianCode,
                        TechnicianCompany = cu.TechnicianCompany,
                        TechnicianAddress = cu.TechnicianAddress,
                        City = cu.City,
                        District = cu.District,
                        TechnicianName = cu.TechnicianName,
                        TechnicianPhone = cu.TechnicianPhone,
                        TechnicianEmail = cu.TechnicianEmail,
                        IsActive = cu.IsActive,
                    },

                    User = u == null ? null : new UserGetDto
                    {
                        Id = u.Id,
                        TechnicianCode = u.TechnicianCode,
                        TechnicianCompany = u.TechnicianCompany,
                        TechnicianAddress = u.TechnicianAddress,
                        City = u.City,
                        District = u.District,
                        TechnicianName = u.TechnicianName,
                        TechnicianPhone = u.TechnicianPhone,
                        TechnicianEmail = u.TechnicianEmail,
                        IsActive = u.IsActive,
                        Roles = u.UserRoles
                            .Select(ur => new RoleGetDto
                            {
                                Id = ur.Role.Id,
                                Name = ur.Role.Name,
                                Code = ur.Role.Code
                            })
                            .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<QnbWarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null
                })
                .ToListAsync();

            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new QnbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    QnbWorkFlowId = x.QnbWorkFlowId,
                    RequestNo = x.RequestNo,
                    FromStepId = x.FromStepId,
                    FromStepCode = x.FromStepCode,
                    ToStepId = x.ToStepId,
                    ToStepCode = x.ToStepCode,
                    ReviewNotes = x.ReviewNotes,
                    CreatedDate = x.CreatedDate,
                    CreatedUser = x.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<QnbWarehouseGetDto>.Success(dto);
        }

        public async Task<ResponseModel<QnbWarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo)
        {
            var qWarehouse = _uow.Repository.GetQueryable<QnbWarehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<QnbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking();
            var qUsers = _uow.Repository.GetQueryable<User>().AsNoTracking();

            var dto = await (
                from w in qWarehouse
                where w.RequestNo == requestNo

                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj
                    .OrderByDescending(x => x.CreatedDate)
                    .Take(1)
                    .DefaultIfEmpty()

                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()

                join u0 in qUsers on wf.ApproverTechnicianId equals u0.Id into uj
                from u in uj.DefaultIfEmpty()

                select new QnbWarehouseGetDto
                {
                    Id = w.Id,
                    RequestNo = w.RequestNo,
                    DeliveryDate = w.DeliveryDate,
                    Description = w.Description,
                    WarehouseStatus = w.WarehouseStatus,

                    WorkFlowRequestTitle = wf != null ? wf.RequestTitle : null,
                    WorkFlowPriority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    ServicesRequest = sr == null
                        ? null
                        : new QnbServicesRequestGetDto
                        {
                            Id = sr.Id,
                            RequestNo = sr.RequestNo,
                            ServicesDate = sr.ServicesDate,
                            PlannedCompletionDate = sr.PlannedCompletionDate,
                            ServicesCostStatus = sr.ServicesCostStatus,
                            Title = wf.RequestTitle,
                            Description = sr.Description,
                            IsProductRequirement = sr.IsProductRequirement,
                            IsMailSended = sr.IsMailSended,
                            IsLocationValid = wf.IsLocationValid,
                            CustomerApproverId = sr.CustomerApproverId,
                            CustomerApproverName = wf.CustomerApproverName,
                            CustomerId = sr.CustomerId,
                            CustomerName = sr.Customer.ContactName1 ?? "",
                            ServiceTypeId = sr.ServiceTypeId,
                            CreatedDate = sr.CreatedDate,
                            UpdatedDate = sr.UpdatedDate,
                            CreatedUser = sr.CreatedUser,
                            UpdatedUser = sr.UpdatedUser,
                            IsDeleted = sr.IsDeleted,
                            Priority = sr.Priority,
                            ServicesRequestStatus = sr.ServicesRequestStatus,
                        },

                    Customer = sr != null && sr.Customer != null
                        ? new CustomerGetDto
                        {
                            Id = sr.Customer.Id,
                            SubscriberCode = sr.Customer.SubscriberCode,
                            SubscriberCompany = sr.Customer.SubscriberCompany,
                            SubscriberAddress = sr.Customer.SubscriberAddress,
                            City = sr.Customer.City,
                            District = sr.Customer.District,
                            LocationCode = sr.Customer.LocationCode,
                            ContactName1 = sr.Customer.ContactName1,
                            Phone1 = sr.Customer.Phone1,
                            Email1 = sr.Customer.Email1,
                            ContactName2 = sr.Customer.ContactName2,
                            Phone2 = sr.Customer.Phone2,
                            Email2 = sr.Customer.Email2,
                            CustomerShortCode = sr.Customer.CustomerShortCode,
                            CorporateLocationId = sr.Customer.CorporateLocationId,
                            Longitude = sr.Customer.Longitude,
                            Latitude = sr.Customer.Latitude,
                            InstallationDate = sr.Customer.InstallationDate,
                            WarrantyYears = sr.Customer.WarrantyYears,
                            CustomerGroupId = sr.Customer.CustomerGroupId,
                            CustomerTypeId = sr.Customer.CustomerTypeId,
                            Note = sr.Customer.Note,
                            CashCenter = sr.Customer.CashCenter,
                            LockType = sr.Customer.LockType,
                            Systems = sr.Customer.CustomerSystemAssignments
                                .Select(a => new CustomerSystemAssignmentGetDto
                                {
                                    Id = a.Id,
                                    CustomerId = a.CustomerId,
                                    CustomerSystemId = a.CustomerSystemId,
                                    HasMaintenanceContract = a.HasMaintenanceContract,
                                    SystemName = a.CustomerSystem.Name,
                                    SystemCode = a.CustomerSystem.Code,
                                    CustomerName = a.Customer.SubscriberCompany,
                                    CustomerShortCode = a.Customer.CustomerShortCode
                                })
                                .ToList()
                        }
                        : null,

                    User = u == null ? null : new UserGetDto
                    {
                        Id = u.Id,
                        TechnicianCode = u.TechnicianCode,
                        TechnicianCompany = u.TechnicianCompany,
                        TechnicianAddress = u.TechnicianAddress,
                        City = u.City,
                        District = u.District,
                        TechnicianName = u.TechnicianName,
                        TechnicianPhone = u.TechnicianPhone,
                        TechnicianEmail = u.TechnicianEmail,
                        IsActive = u.IsActive,
                        Roles = u.UserRoles
                            .Select(ur => new RoleGetDto
                            {
                                Id = ur.Role.Id,
                                Name = ur.Role.Name,
                                Code = ur.Role.Code
                            })
                            .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<QnbWarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null
                })
                .ToListAsync();

            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new QnbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    QnbWorkFlowId = x.QnbWorkFlowId,
                    RequestNo = x.RequestNo,
                    FromStepId = x.FromStepId,
                    FromStepCode = x.FromStepCode,
                    ToStepId = x.ToStepId,
                    ToStepCode = x.ToStepCode,
                    ReviewNotes = x.ReviewNotes,
                    CreatedDate = x.CreatedDate,
                    CreatedUser = x.CreatedUser
                })
                .ToListAsync();

            return ResponseModel<QnbWarehouseGetDto>.Success(dto);
        }

        // -------------------- Technical Service --------------------
        public async Task<ResponseModel<QnbTechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<QnbTechnicalService>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .AsSplitQuery()
                .Include(x => x.QnbServiceRequestFormImages)
                .Include(x => x.QnbServicesImages)
                .Include(x => x.ServiceType)
                .ProjectToType<QnbTechnicalServiceGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<QnbTechnicalServiceGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Customer = await _uow.Repository
                .GetQueryable<QnbServicesRequest>()
                .AsNoTracking()
                .Where(sr => sr.RequestNo == requestNo && sr.Customer != null)
                .Select(sr => new CustomerGetDto
                {
                    Id = sr.Customer!.Id,
                    SubscriberCode = sr.Customer.SubscriberCode,
                    SubscriberCompany = sr.Customer.SubscriberCompany,
                    SubscriberAddress = sr.Customer.SubscriberAddress,
                    City = sr.Customer.City,
                    District = sr.Customer.District,
                    LocationCode = sr.Customer.LocationCode,
                    ContactName1 = sr.Customer.ContactName1,
                    Phone1 = sr.Customer.Phone1,
                    Email1 = sr.Customer.Email1,
                    ContactName2 = sr.Customer.ContactName2,
                    Phone2 = sr.Customer.Phone2,
                    Email2 = sr.Customer.Email2,
                    CustomerShortCode = sr.Customer.CustomerShortCode,
                    CorporateLocationId = sr.Customer.CorporateLocationId,
                    Longitude = sr.Customer.Longitude,
                    Latitude = sr.Customer.Latitude,
                    InstallationDate = sr.Customer.InstallationDate,
                    WarrantyYears = sr.Customer.WarrantyYears,
                    CustomerGroupId = sr.Customer.CustomerGroupId,
                    CustomerTypeId = sr.Customer.CustomerTypeId,
                    Note = sr.Customer.Note,
                    CashCenter = sr.Customer.CashCenter,
                    LockType = sr.Customer.LockType,
                    Systems = sr.Customer.CustomerSystemAssignments
                        .Select(a => new CustomerSystemAssignmentGetDto
                        {
                            Id = a.Id,
                            CustomerId = a.CustomerId,
                            CustomerSystemId = a.CustomerSystemId,
                            HasMaintenanceContract = a.HasMaintenanceContract,
                            SystemName = a.CustomerSystem.Name,
                            SystemCode = a.CustomerSystem.Code,
                            CustomerName = a.Customer.SubscriberCompany,
                            CustomerShortCode = a.Customer.CustomerShortCode
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            dto.Products = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new QnbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    PriceCurrency = p.CapturedCurrency
                        ?? (p.Product != null ? p.Product.PriceCurrency : null),
                    ProductPrice = p.CapturedUnitPrice
                        ?? (p.Product != null ? (decimal?)p.Product.Price : null)
                        ?? 0m,
                    EffectivePrice = p.CapturedUnitPrice ?? 0m,
                })
                .ToListAsync();

            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "TS" || x.ToStepCode == "TS"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<QnbWorkFlowReviewLogDto>(_config)
                .ToListAsync();

            // IMAGE URL NORMALİZASYONU
            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? "";

            if (dto.ServicesImages != null)
            {
                foreach (var img in dto.ServicesImages)
                {
                    img.Url = NormalizeImageUrlInternal(img.Url, baseUrl) ?? string.Empty;
                }
            }

            if (dto.ServiceRequestFormImages != null)
            {
                foreach (var img in dto.ServiceRequestFormImages)
                {
                    img.Url = NormalizeImageUrlInternal(img.Url, baseUrl) ?? string.Empty;
                }
            }

            return ResponseModel<QnbTechnicalServiceGetDto>.Success(dto);
        }

        // -------------------- Pricing --------------------
        public async Task<ResponseModel<QnbPricingGetDto>> GetPricingByRequestNoAsync(string requestNo)
        {
            var qPricing = _uow.Repository.GetQueryable<QnbPricing>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking();

            var dto = await (
                from pr in qPricing
                where pr.RequestNo == requestNo
                join sr0 in qRequest on pr.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new QnbPricingGetDto
                {
                    Id = pr.Id,
                    RequestNo = pr.RequestNo,
                    Status = pr.Status,
                    Currency = pr.Currency,
                    Notes = pr.Notes,
                    TotalAmount = pr.TotalAmount,
                    CreatedDate = pr.CreatedDate,
                    CreatedUser = pr.CreatedUser,
                    UpdatedDate = pr.UpdatedDate,
                    UpdatedUser = pr.UpdatedUser,
                    OracleNo = sr != null ? sr.QnbServiceTrackNo : null,
                    ServicesCostStatus = sr != null ? sr.ServicesCostStatus : ServicesCostStatus.Unknown,

                    Customer = sr != null && sr.Customer != null
                        ? new CustomerGetDto
                        {
                            Id = sr.Customer.Id,
                            SubscriberCode = sr.Customer.SubscriberCode,
                            SubscriberCompany = sr.Customer.SubscriberCompany,
                            SubscriberAddress = sr.Customer.SubscriberAddress,
                            City = sr.Customer.City,
                            District = sr.Customer.District,
                            LocationCode = sr.Customer.LocationCode,
                            ContactName1 = sr.Customer.ContactName1,
                            Phone1 = sr.Customer.Phone1,
                            Email1 = sr.Customer.Email1,
                            ContactName2 = sr.Customer.ContactName2,
                            Phone2 = sr.Customer.Phone2,
                            Email2 = sr.Customer.Email2,
                            CustomerShortCode = sr.Customer.CustomerShortCode,
                            CorporateLocationId = sr.Customer.CorporateLocationId,
                            Longitude = sr.Customer.Longitude,
                            Latitude = sr.Customer.Latitude,
                            InstallationDate = sr.Customer.InstallationDate,
                            WarrantyYears = sr.Customer.WarrantyYears,
                            CustomerGroupId = sr.Customer.CustomerGroupId,
                            CustomerTypeId = sr.Customer.CustomerTypeId,
                            Note = sr.Customer.Note,
                            CashCenter = sr.Customer.CashCenter,
                            LockType = sr.Customer.LockType,
                            Systems = sr.Customer.CustomerSystemAssignments
                                .Select(a => new CustomerSystemAssignmentGetDto
                                {
                                    Id = a.Id,
                                    CustomerId = a.CustomerId,
                                    CustomerSystemId = a.CustomerSystemId,
                                    HasMaintenanceContract = a.HasMaintenanceContract,
                                    SystemName = a.CustomerSystem.Name,
                                    SystemCode = a.CustomerSystem.Code,
                                    CustomerName = a.Customer.SubscriberCompany,
                                    CustomerShortCode = a.Customer.CustomerShortCode
                                })
                                .ToList()
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<QnbPricingGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var productEntities = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerProductPrices)
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    bool captured = p.IsPriceCaptured;
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)
                        : p.GetEffectivePrice();

                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    return new QnbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,
                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,
                        PriceCurrency = currency,
                        ProductPrice = effectivePrice,
                        EffectivePrice = effectivePrice,
                        TotalPrice = effectivePrice * p.Quantity
                    };
                })
                .ToList();

            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "PRC" || x.ToStepCode == "PRC"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<QnbWorkFlowReviewLogDto>(_config)
                .ToListAsync();

            return ResponseModel<QnbPricingGetDto>.Success(dto);
        }

        // -------------------- FinalApproval shared helpers --------------------
        private async Task FillFinalApprovalDetailsAsync(QnbFinalApprovalGetDto dto)
        {
            // Ürünler
            var productEntities = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerProductPrices)
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    bool captured = p.IsPriceCaptured;
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)
                        : p.GetEffectivePrice();

                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    return new QnbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,
                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,
                        PriceCurrency = currency,
                        ProductPrice = effectivePrice,
                        EffectivePrice = effectivePrice,
                        TotalPrice = effectivePrice * p.Quantity
                    };
                })
                .ToList();

            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<QnbWorkFlowReviewLogDto>(_config)
                .ToListAsync();

            var techService = await _uow.Repository
                .GetQueryable<QnbTechnicalService>()
                .AsNoTracking()
                .Where(ts => ts.RequestNo == dto.RequestNo)
                .Include(ts => ts.QnbServiceRequestFormImages)
                .Include(ts => ts.QnbServicesImages)
                .FirstOrDefaultAsync();

            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? "";

            if (techService != null)
            {
                if (techService.QnbServicesImages != null && techService.QnbServicesImages.Any())
                {
                    dto.ServicesImages = techService.QnbServicesImages
                        .Select(img => new QnbTechnicalServiceImageGetDto
                        {
                            Id = img.Id,
                            QnbTechnicalServiceId = img.QnbTechnicalServiceId,
                            Url = NormalizeImageUrlInternal(img.Url, baseUrl) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }

                if (techService.QnbServiceRequestFormImages != null && techService.QnbServiceRequestFormImages.Any())
                {
                    dto.ServiceRequestFormImages = techService.QnbServiceRequestFormImages
                        .Select(img => new TechnicalServiceFormImageGetDto
                        {
                            Id = img.Id,
                            Url = NormalizeImageUrlInternal(img.Url, baseUrl) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }
            }
        }

        private static QnbFinalApprovalGetDto? BuildFinalApprovalHeaderFromCustomer(
            QnbFinalApproval fa, QnbServicesRequest? sr)
        {
            return new QnbFinalApprovalGetDto
            {
                Id = fa.Id,
                RequestNo = fa.RequestNo,
                Notes = fa.Notes,
                DecidedBy = fa.DecidedBy,
                Status = fa.Status,
                DiscountPercent = fa.DiscountPercent,
                Customer = sr != null && sr.Customer != null
                    ? new CustomerGetDto
                    {
                        Id = sr.Customer.Id,
                        SubscriberCode = sr.Customer.SubscriberCode,
                        SubscriberCompany = sr.Customer.SubscriberCompany,
                        SubscriberAddress = sr.Customer.SubscriberAddress,
                        City = sr.Customer.City,
                        District = sr.Customer.District,
                        LocationCode = sr.Customer.LocationCode,
                        ContactName1 = sr.Customer.ContactName1,
                        Phone1 = sr.Customer.Phone1,
                        Email1 = sr.Customer.Email1,
                        ContactName2 = sr.Customer.ContactName2,
                        Phone2 = sr.Customer.Phone2,
                        Email2 = sr.Customer.Email2,
                        CustomerShortCode = sr.Customer.CustomerShortCode,
                        CorporateLocationId = sr.Customer.CorporateLocationId,
                        Longitude = sr.Customer.Longitude,
                        Latitude = sr.Customer.Latitude,
                        InstallationDate = sr.Customer.InstallationDate,
                        WarrantyYears = sr.Customer.WarrantyYears,
                        CustomerGroupId = sr.Customer.CustomerGroupId,
                        CustomerTypeId = sr.Customer.CustomerTypeId,
                        Note = sr.Customer.Note,
                        CashCenter = sr.Customer.CashCenter,
                        LockType = sr.Customer.LockType,
                        Systems = sr.Customer.CustomerSystemAssignments
                            .Select(a => new CustomerSystemAssignmentGetDto
                            {
                                Id = a.Id,
                                CustomerId = a.CustomerId,
                                CustomerSystemId = a.CustomerSystemId,
                                HasMaintenanceContract = a.HasMaintenanceContract,
                                SystemName = a.CustomerSystem != null ? a.CustomerSystem.Name : null,
                                SystemCode = a.CustomerSystem != null ? a.CustomerSystem.Code : null,
                                CustomerName = a.Customer != null ? a.Customer.SubscriberCompany : null,
                                CustomerShortCode = a.Customer != null ? a.Customer.CustomerShortCode : null
                            })
                            .ToList()
                    }
                    : null
            };
        }

        public async Task<ResponseModel<QnbFinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo)
        {
            var pair = await (
                from fa in _uow.Repository.GetQueryable<QnbFinalApproval>().AsNoTracking()
                where fa.RequestNo == requestNo
                join sr0 in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                    .Include(s => s.Customer).ThenInclude(c => c.CustomerSystemAssignments)
                    on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { fa, sr }
            ).FirstOrDefaultAsync();

            if (pair is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var dto = BuildFinalApprovalHeaderFromCustomer(pair.fa, pair.sr);
            if (dto is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            await FillFinalApprovalDetailsAsync(dto);
            return ResponseModel<QnbFinalApprovalGetDto>.Success(dto);
        }

        public async Task<ResponseModel<QnbFinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id)
        {
            var pair = await (
                from fa in _uow.Repository.GetQueryable<QnbFinalApproval>().AsNoTracking()
                where fa.Id == id
                join sr0 in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                    .Include(s => s.Customer).ThenInclude(c => c.CustomerSystemAssignments)
                    on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { fa, sr }
            ).FirstOrDefaultAsync();

            if (pair is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var dto = BuildFinalApprovalHeaderFromCustomer(pair.fa, pair.sr);
            if (dto is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            await FillFinalApprovalDetailsAsync(dto);
            return ResponseModel<QnbFinalApprovalGetDto>.Success(dto);
        }

        public async Task<ResponseModel<QnbFinalApprovalGetDto>> GetCustomerAgreementByRequestNoAsync(
            string requestNo, FinalApprovalStatus status = FinalApprovalStatus.CustomerApproval)
        {
            var pair = await (
                from fa in _uow.Repository.GetQueryable<QnbFinalApproval>().AsNoTracking()
                where fa.RequestNo == requestNo && fa.Status == status
                join sr0 in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                    .Include(s => s.Customer).ThenInclude(c => c.CustomerSystemAssignments)
                    on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { fa, sr }
            ).FirstOrDefaultAsync();

            if (pair is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var dto = BuildFinalApprovalHeaderFromCustomer(pair.fa, pair.sr);
            if (dto is null)
                return ResponseModel<QnbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            await FillFinalApprovalDetailsAsync(dto);
            return ResponseModel<QnbFinalApprovalGetDto>.Success(dto);
        }

        // -------------------- WorkFlowStep CRUD --------------------
        public async Task<ResponseModel<PagedResult<QnbWorkFlowStepGetDto>>> GetStepsAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<QnbWorkFlowStep>();
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.Name.Contains(q.Search) || (x.Code ?? "").Contains(q.Search));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Name)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<QnbWorkFlowStepGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<QnbWorkFlowStepGetDto>>
                .Success(new PagedResult<QnbWorkFlowStepGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<QnbWorkFlowStepGetDto>> GetStepByIdAsync(long id)
        {
            var dto = await _uow.Repository.GetQueryable<QnbWorkFlowStep>()
                .Where(x => x.Id == id)
                .ProjectToType<QnbWorkFlowStepGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<QnbWorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            return ResponseModel<QnbWorkFlowStepGetDto>.Success(dto);
        }

        public async Task<ResponseModel<QnbWorkFlowStepGetDto>> CreateStepAsync(QnbWorkFlowStepCreateDto dto)
        {
            var entity = dto.Adapt<QnbWorkFlowStep>(_config);
            await _uow.Repository.AddAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }

        public async Task<ResponseModel<QnbWorkFlowStepGetDto>> UpdateStepAsync(QnbWorkFlowStepUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<QnbWorkFlowStep>(false, x => x.Id == dto.Id);
            if (entity is null)
                return ResponseModel<QnbWorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Adapt(entity, _config);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }

        public async Task<ResponseModel> DeleteStepAsync(long id)
        {
            var entity = await _uow.Repository.GetSingleAsync<QnbWorkFlowStep>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            await _uow.Repository.HardDeleteAsync<QnbWorkFlowStep, long>(entity);
            await _uow.Repository.CompleteAsync();

            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        // -------------------- WorkFlow --------------------
        public async Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "QNB")
        {
            prefix ??= "QNB";
            var datePart = DateTime.Now.ToString("yyyyMMdd");

            for (int i = 0; i < 10; i++)
            {
                int rnd = RandomNumberGenerator.GetInt32(1000, 10000);
                string candidate = $"{prefix}-{datePart}-{rnd}";

                var query = _uow.Repository.GetQueryable<QnbWorkFlow>();
                bool exists = await query.AsNoTracking()
                                         .AnyAsync(x => x.RequestNo == candidate && !x.IsDeleted);

                if (!exists)
                    return ResponseModel<string>.Success(candidate, "Yeni Akış Numarası üretildi.");
            }

            return ResponseModel<string>.Fail("Benzersiz RequestNo üretilemedi, lütfen tekrar deneyin.");
        }

        public async Task<ResponseModel<PagedResult<QnbWorkFlowGetDto>>> GetWorkFlowsAsync(QnbWorkFlowQueryParams q)
        {
            q.Normalize(maxPageSize: 200);

            var me = await _currentUser.GetAsync();
            if (me is null)
                return ResponseModel<PagedResult<QnbWorkFlowGetDto>>.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var page = q.Page;
            var pageSize = q.PageSize;

            var permittedSteps = await GetUserStepsByMenuPermission(me.Id) ?? new List<string>();
            var permittedSet = permittedSteps.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var technicianRoleRaw = await _uow.Repository
                .GetQueryable<Configuration>()
                .AsNoTracking()
                .Where(x => x.Name == "TechnicianRoleCode")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var technicianRoleCodes = CommonFunctions.ParseRoleCodes(technicianRoleRaw ?? "");

            var isTechnician = technicianRoleCodes.Count > 0 &&
                (me.Roles?.Any(r => technicianRoleCodes.Contains(r.Code,
                    StringComparer.OrdinalIgnoreCase)) ?? false);

            var pendingStatus = WorkFlowStatus.Pending;

            IQueryable<QnbWorkFlow> wfBase = _uow.Repository.GetQueryable<QnbWorkFlow>()
                .AsNoTracking()
                .Include(x => x.CurrentStep)
                .Include(x => x.ApproverTechnician)
                .Where(x => !x.IsDeleted && x.WorkFlowStatus == pendingStatus);

            var myId = me.Id;

            if (!isTechnician && permittedSet.Count == 0)
            {
                wfBase = wfBase.Where(_ => false);
            }
            else
            {
                wfBase = wfBase.Where(w =>
                    w.CurrentStep != null &&
                    permittedSet.Contains(w.CurrentStep.Code) &&
                    (!isTechnician || w.ApproverTechnicianId == myId)
                );
            }

            if (q.CurrentStepId.HasValue)
                wfBase = wfBase.Where(w => w.CurrentStepId == q.CurrentStepId.Value);

            if (!string.IsNullOrWhiteSpace(q.StepCode))
            {
                var stepCode = q.StepCode.Trim();
                wfBase = wfBase.Where(w => w.CurrentStep != null && w.CurrentStep.Code == stepCode);
            }

            if (q.Priority.HasValue)
                wfBase = wfBase.Where(w => w.Priority == q.Priority.Value);

            if (q.Priorities != null && q.Priorities.Count > 0)
                wfBase = wfBase.Where(w => q.Priorities.Contains(w.Priority));

            if (q.StartDate.HasValue)
                wfBase = wfBase.Where(w => w.CreatedDate >= q.StartDate.Value);

            if (q.EndDate.HasValue)
                wfBase = wfBase.Where(w => w.CreatedDate <= q.EndDate.Value);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();
                wfBase = wfBase.Where(x => x.RequestNo.Contains(term) || x.RequestTitle.Contains(term));
            }

            var qJoined =
                from wf in wfBase
                join sr0 in _uow.Repository.GetQueryable<QnbServicesRequest>().AsNoTracking()
                     on wf.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { wf, sr };

            var total = await qJoined.CountAsync();

            var finalQuery = qJoined;

            if (!string.IsNullOrWhiteSpace(q.Sort))
            {
                var sortLower = q.Sort.ToLowerInvariant();

                if (sortLower == "requestno")
                    finalQuery = q.Desc ? qJoined.OrderByDescending(x => x.wf.RequestNo) : qJoined.OrderBy(x => x.wf.RequestNo);
                else if (sortLower == "requesttitle")
                    finalQuery = q.Desc ? qJoined.OrderByDescending(x => x.wf.RequestTitle) : qJoined.OrderBy(x => x.wf.RequestTitle);
                else if (sortLower == "priority")
                    finalQuery = q.Desc ? qJoined.OrderByDescending(x => x.wf.Priority) : qJoined.OrderBy(x => x.wf.Priority);
                else if (sortLower == "createddate")
                    finalQuery = q.Desc ? qJoined.OrderByDescending(x => x.wf.CreatedDate) : qJoined.OrderBy(x => x.wf.CreatedDate);
                else if (sortLower == "updateddate")
                    finalQuery = q.Desc ? qJoined.OrderByDescending(x => x.wf.UpdatedDate) : qJoined.OrderBy(x => x.wf.UpdatedDate);
                else
                    finalQuery = qJoined.OrderByDescending(x => x.wf.CreatedDate);
            }
            else
            {
                finalQuery = qJoined.OrderByDescending(x => x.wf.CreatedDate);
            }

            var items = await finalQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new QnbWorkFlowGetDto
                {
                    Id = x.wf.Id,
                    RequestTitle = x.wf.RequestTitle,
                    RequestNo = x.wf.RequestNo,
                    CurrentStepId = x.wf.CurrentStepId.GetValueOrDefault(),
                    Priority = x.wf.Priority,
                    WorkFlowStatus = x.wf.WorkFlowStatus,
                    IsAgreement = x.wf.IsAgreement,
                    CreatedDate = x.wf.CreatedDate,
                    UpdatedDate = x.wf.UpdatedDate,
                    CreatedUser = x.wf.CreatedUser,
                    UpdatedUser = x.wf.UpdatedUser,
                    IsDeleted = x.wf.IsDeleted,
                    ApproverTechnicianId = x.wf.ApproverTechnicianId,

                    ApproverTechnician = x.wf.ApproverTechnician == null
                        ? null
                        : new UserGetDto
                        {
                            Id = x.wf.ApproverTechnician.Id,
                            TechnicianName = x.wf.ApproverTechnician.TechnicianName,
                            TechnicianPhone = x.wf.ApproverTechnician.TechnicianPhone,
                            TechnicianAddress = x.wf.ApproverTechnician.TechnicianAddress,
                            City = x.wf.ApproverTechnician.City,
                            District = x.wf.ApproverTechnician.District,
                            TechnicianEmail = x.wf.ApproverTechnician.TechnicianEmail,
                        },

                    CustomerCode = x.sr == null ? null : (x.sr.Customer == null ? null : x.sr.Customer.SubscriberCode),
                    CustomerName = x.sr == null ? null : (x.sr.Customer == null ? null : x.sr.Customer.SubscriberCompany),
                    CustomerAddress = x.sr == null ? null : (x.sr.Customer == null ? null : x.sr.Customer.SubscriberAddress),

                    CurrentStep = x.wf.CurrentStep == null
                        ? null
                        : new QnbWorkFlowStepGetDto
                        {
                            Id = x.wf.CurrentStep.Id,
                            Name = x.wf.CurrentStep.Name,
                            Code = x.wf.CurrentStep.Code
                        }
                })
                .ToListAsync();

            return ResponseModel<PagedResult<QnbWorkFlowGetDto>>
                .Success(new PagedResult<QnbWorkFlowGetDto>(items, total, page, pageSize));
        }

        public async Task<ResponseModel> DeleteWorkFlowAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var entity = await _uow.Repository.GetSingleAsync<QnbWorkFlow>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            entity.IsDeleted = true;
            entity.UpdatedDate = DateTime.Now;
            entity.UpdatedUser = meId;
            _uow.Repository.Update(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        public async Task<ResponseModel> CancelWorkFlowAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var entity = await _uow.Repository.GetSingleAsync<QnbWorkFlow>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("İptal edilecek kayıt bulunamadı.", StatusCode.NotFound);

            entity.WorkFlowStatus = WorkFlowStatus.Cancelled;
            entity.UpdatedDate = DateTime.Now;
            entity.UpdatedUser = meId;
            _uow.Repository.Update(entity);
            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }


        // ------------------------ Report (single) ------------------------
        public async Task<ResponseModel<QnbWorkFlowReportDto>> GetReportAsync(string requestNo)
        {
            // 1) WorkFlow + CurrentStep + ApproverTechnician
            var wf = await _uow.Repository.GetQueryable<QnbWorkFlow>()
                .Include(x => x.CurrentStep)
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<QnbWorkFlowReportDto>.Fail("Akış bulunamadı.", StatusCode.NotFound);

            var dto = new QnbWorkFlowReportDto
            {
                RequestNo = requestNo,
                Header = new HeaderSectionDto
                {
                    Title = wf.RequestTitle,
                    WorkFlowStatus = wf.WorkFlowStatus.ToString(),
                    CurrentStepId = wf.CurrentStepId,
                    CurrentStepCode = wf.CurrentStep?.Code,
                    IsAgreement = wf.IsAgreement,
                    IsLocationValid = wf.IsLocationValid,
                    CustomerApproverName = wf.CustomerApproverName,
                    ApproverTechnicianId = wf.ApproverTechnicianId,
                    ApproverTechnicianName = wf.ApproverTechnician?.TechnicianName,
                    ApproverTechnicianEmail = wf.ApproverTechnician?.TechnicianEmail,
                    ApproverTechnicianCode = wf.ApproverTechnician?.TechnicianCode,
                    Priority = (int)wf.Priority
                }
            };

            // 2) ServicesRequest + Customer
            var sr = await _uow.Repository.GetQueryable<QnbServicesRequest>()
                .AsNoTracking()
                .Include(x => x.ServiceType)
                .Include(x => x.Customer)
                    .ThenInclude(c => c.CustomerGroup)
                        .ThenInclude(g => g.ProgressApprovers)
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

            if (sr is not null)
            {
                dto.ServiceRequest = new ServiceRequestSectionDto
                {
                    Id = sr.Id,
                    OracleNo = sr.QnbServiceTrackNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    ServicesCostStatus = sr.ServicesCostStatus.ToString(),
                    Description = sr.Description,
                    IsProductRequirement = sr.IsProductRequirement,
                    WorkFlowStepId = sr.WorkFlowStepId,
                    CustomerApproverId = sr.CustomerApproverId,
                    ServiceTypeId = sr.ServiceTypeId,
                    ServiceTypeName = sr.ServiceType?.Name,
                    Priority = sr.Priority.ToString(),
                    ServicesRequestStatus = sr.ServicesRequestStatus.ToString()
                };

                if (sr.Customer is not null)
                {
                    dto.Customer = new CustomerSectionDto
                    {
                        Id = sr.Customer.Id,
                        SubscriberCode = sr.Customer.SubscriberCode,
                        SubscriberCompany = sr.Customer.SubscriberCompany,
                        SubscriberAddress = sr.Customer.SubscriberAddress,
                        City = sr.Customer.City,
                        District = sr.Customer.District,
                        LocationCode = sr.Customer.LocationCode,
                        ContactName1 = sr.Customer.ContactName1,
                        Phone1 = sr.Customer.Phone1,
                        Email1 = sr.Customer.Email1,
                        CustomerShortCode = sr.Customer.CustomerShortCode,
                        CorporateLocationId = sr.Customer.CorporateLocationId,
                        Longitude = sr.Customer.Longitude,
                        Latitude = sr.Customer.Latitude,
                        InstallationDate = sr.Customer.InstallationDate,
                        WarrantyYears = sr.Customer.WarrantyYears
                    };

                    if (sr.Customer.CustomerGroup is not null)
                    {
                        dto.Customer.CustomerGroup = new CustomerGroupLiteDto
                        {
                            Id = sr.Customer.CustomerGroup.Id,
                            GroupName = sr.Customer.CustomerGroup.GroupName,
                            Code = sr.Customer.CustomerGroup.Code,
                            ParentGroupId = sr.Customer.CustomerGroup.ParentGroupId,
                            ProgressApprovers = sr.Customer.CustomerGroup.ProgressApprovers?
                                .Select(p => new ProgressApproverLiteDto
                                {
                                    Id = p.Id,
                                }).ToList() ?? new()
                        };
                    }
                }
            }

            // 3) Ürün satırları
            var lines = await _uow.Repository.GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.Tenant)
                        .ThenInclude(t => t.TenantProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerGroup)
                        .ThenInclude(g => g.GroupProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerProductPrices)
                .Where(p => p.RequestNo == requestNo)
                .ToListAsync();

            foreach (var p in lines)
            {
                bool captured = p.IsPriceCaptured;
                decimal unit = captured
                    ? (p.CapturedUnitPrice ?? 0m)
                    : p.GetEffectivePrice();

                string currency = captured
                    ? (p.CapturedCurrency ?? p.Product?.PriceCurrency ?? "TRY")
                    : (p.Product?.PriceCurrency ?? "TRY");

                decimal total = captured
                    ? (p.CapturedTotal ?? unit * p.Quantity)
                    : unit * p.Quantity;

                string src = captured
                    ? (p.CapturedSource?.ToString() ?? "Standard")
                    : (p.Customer?.CustomerGroup?.GroupProductPrices?.Any(g => g.ProductId == p.ProductId) == true ? "Group"
                       : p.Customer?.CustomerProductPrices?.Any(c => c.ProductId == p.ProductId) == true ? "Customer"
                       : p.Customer?.Tenant?.TenantProductPrices?.Any(t => t.ProductId == p.ProductId) == true ? "Tenant"
                       : "Standard");

                dto.Products.Add(new ProductLineDto
                {
                    Id = p.Id,
                    ProductId = p.ProductId,
                    ProductCode = p.Product?.ProductCode,
                    ProductName = p.Product?.Description,
                    Quantity = p.Quantity,
                    IsPriceCaptured = captured,
                    UnitPrice = unit,
                    Currency = currency,
                    LineTotal = total,
                    PriceSource = src
                });
            }

            // 4) TechnicalService + Images
            var ts = await _uow.Repository.GetQueryable<QnbTechnicalService>()
                .AsNoTracking()
                .Include(t => t.QnbServicesImages)
                .Include(t => t.QnbServiceRequestFormImages)
                .Include(t => t.ServiceType)
                .FirstOrDefaultAsync(t => t.RequestNo == requestNo);

            if (ts is not null)
            {
                dto.TechnicalService = new TechnicalServiceSectionDto
                {
                    Id = ts.Id,
                    ServiceTypeId = ts.ServiceTypeId,
                    ServiceTypeName = ts.ServiceType?.Name,
                    StartTime = ts.StartTime,
                    EndTime = ts.EndTime,
                    ProblemDescription = ts.ProblemDescription,
                    ResolutionAndActions = ts.ResolutionAndActions,
                    Latitude = ts.Latitude,
                    Longitude = ts.Longitude,
                    StartLocation = ts.StartLocation,
                    EndLocation = ts.EndLocation,
                    IsLocationCheckRequired = ts.IsLocationCheckRequired,
                    ServicesStatus = ts.ServicesStatus.ToString(),
                    ServicesCostStatus = ts.ServicesCostStatus.ToString(),
                    ServiceImages = ts.QnbServicesImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList(),
                    FormImages = ts.QnbServiceRequestFormImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList()
                };
            }

            // 5) Warehouse
            var wh = await _uow.Repository.GetQueryable<QnbWarehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.RequestNo == requestNo);
            if (wh is not null)
            {
                dto.Warehouse = new WarehouseSectionDto
                {
                    Id = wh.Id,
                    DeliveryDate = wh.DeliveryDate,
                    Description = wh.Description,
                    WarehouseStatus = wh.WarehouseStatus.ToString()
                };
            }

            // 6) Pricing
            var pr = await _uow.Repository.GetQueryable<QnbPricing>()
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.RequestNo == requestNo);
            if (pr is not null)
            {
                dto.Pricing = new PricingSectionDto
                {
                    Id = pr.Id,
                    Status = pr.Status.ToString(),
                    Currency = pr.Currency,
                    Notes = pr.Notes,
                    TotalAmount = pr.TotalAmount
                };
            }

            // 7) FinalApproval
            var fa = await _uow.Repository.GetQueryable<QnbFinalApproval>()
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.RequestNo == requestNo);
            if (fa is not null)
            {
                dto.FinalApproval = new FinalApprovalSectionDto
                {
                    Id = fa.Id,
                    Status = fa.Status.ToString(),
                    Notes = fa.Notes,
                    DecidedBy = fa.DecidedBy,
                    DecidedByUserName = null
                };
            }

            // 8) Review Logs
            dto.ReviewLogs = await _uow.Repository.GetQueryable<QnbWorkFlowReviewLog>()
                .AsNoTracking()
                .Where(l => l.RequestNo == requestNo)
                .OrderBy(l => l.CreatedDate)
                .Select(l => new ReviewLogDto
                {
                    Id = l.Id,
                    FromStepId = l.FromStepId,
                    FromStepCode = l.FromStepCode,
                    ToStepId = l.ToStepId,
                    ToStepCode = l.ToStepCode,
                    ReviewNotes = l.ReviewNotes,
                    CreatedUser = l.CreatedUser,
                    CreatedDate = l.CreatedDate
                })
                .ToListAsync();

            // 9) Özet
            dto.Currency = dto.Products.Select(p => p.Currency).FirstOrDefault() ?? (dto.Pricing?.Currency ?? "TRY");
            dto.Subtotal = dto.Products.Sum(p => p.LineTotal);
            dto.DiscountTotal = 0;
            dto.GrandTotal = dto.Subtotal;

            return ResponseModel<QnbWorkFlowReportDto>.Success(dto);
        }

        // ------------------------ Report Search (SP) ------------------------
        public async Task<PagedResult<QnbWorkFlowReportListItemDto>> GetReportsAsync(QnbReportQueryParams q)
        {
            int commandTimeoutSeconds = 60;
            var conn = _ctx.Database.GetDbConnection();
            var mustClose = false;
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync();
                mustClose = true;
            }

            var efTx = _ctx.Database.CurrentTransaction?.GetDbTransaction();

            try
            {
                var p = BuildReportParams(q);

                var rows = await conn.QueryAsync<ReportRowDto>(new CommandDefinition(
                    "qnb.usp_ReportSearchQnb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeoutSeconds
                ));

                var list = new List<QnbWorkFlowReportListItemDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    if (total == 0) total = r.TotalCount;

                    list.Add(new QnbWorkFlowReportListItemDto
                    {
                        RequestNo = r.RequestNo,
                        Title = r.Title,
                        WorkFlowStatus = (WorkFlowStatus)r.WorkFlowStatus,
                        StepCode = r.StepCode,
                        CreatedDate = r.CreatedDate,
                        CustomerId = r.CustomerId,
                        CustomerName = r.CustomerName,
                        City = r.City,
                        District = r.District,
                        ServicesDate = r.ServicesDate,
                        ServiceTypeId = r.ServiceTypeId,
                        ServiceTypeName = r.ServiceTypeName,
                        TechnicianId = r.TechnicianId,
                        TechnicianName = r.TechnicianName,
                        Currency = r.Currency ?? "TRY",
                        Subtotal = r.Subtotal,
                        HasImages = q.HasImages ?? false
                    });
                }

                return new PagedResult<QnbWorkFlowReportListItemDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        public async Task<PagedResult<QnbWorkFlowReportLineDto>> GetReportLinesAsync(QnbReportQueryParams q)
        {
            q.Normalize(500);

            var conn = _ctx.Database.GetDbConnection();
            var mustClose = false;
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync();
                mustClose = true;
            }

            var efTx = _ctx.Database.CurrentTransaction?.GetDbTransaction();

            try
            {
                var p = BuildReportParams(q);

                var rows = await conn.QueryAsync<QnbReportLineRowDto>(new CommandDefinition(
                    "qnb.usp_ReportSearch_LinesQnb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 60
                ));

                var list = new List<QnbWorkFlowReportLineDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    if (total == 0) total = r.TotalCount;

                    list.Add(new QnbWorkFlowReportLineDto
                    {
                        RequestNo = r.RequestNo,
                        City = r.City,
                        CustomerName = r.CustomerName,
                        ProductCode = r.ProductCode,
                        LocationCode = r.LocationCode,
                        ProductOracleCode = r.ProductOracleCode,
                        ProductDefinition = r.ProductDefinition,
                        ServiceDate = r.ServiceDate,
                        ServiceOracleNo = r.ServiceOracleNo,
                        WorkOrder = r.WorkOrder,
                        Quantity = r.Quantity,
                        LineUnitPriceTL = r.LineUnitPriceTL,
                        LineTotalTL = r.LineTotalTL,
                        LineUnitPriceUSD = r.LineUnitPriceUSD,
                        LineTotalUSD = r.LineTotalUSD,
                        GLCode = r.GLCode,
                        MGSDescription = r.MGSDescription,
                        ContractNo = r.Contract_No,
                        CostType = r.CostType,
                        Description = r.Description,
                        InstallationDate = r.InstallationDate,
                        DiscountPercent = r.DiscountPercent,
                    });
                }

                return new PagedResult<QnbWorkFlowReportLineDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        public async Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(QnbReportQueryParams q)
        {
            q.Normalize(500);
            var exportPage = 1;
            var exportPageSize = 1_000_000;

            var conn = _ctx.Database.GetDbConnection();
            var mustClose = false;
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync();
                mustClose = true;
            }
            var efTx = _ctx.Database.CurrentTransaction?.GetDbTransaction();

            try
            {
                var p = BuildReportParams(q, overridePage: exportPage, overridePageSize: exportPageSize);

                var rows = await conn.QueryAsync<QnbReportLineRowDto>(new CommandDefinition(
                    "qnb.usp_ReportSearch_LinesQnb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 180
                ));

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Report");

                var c = 1;
                ws.Cell(1, c++).Value = "Sıra No";
                ws.Cell(1, c++).Value = "Talep No";
                ws.Cell(1, c++).Value = "Şehir";
                ws.Cell(1, c++).Value = "Lokasyon Adı";
                ws.Cell(1, c++).Value = "Ürün Kodu";
                ws.Cell(1, c++).Value = "Lokasyon Kodu";
                ws.Cell(1, c++).Value = "Ürün Oracle Kodu";
                ws.Cell(1, c++).Value = "Ürün Tanımı";
                ws.Cell(1, c++).Value = "Servis Tarihi";
                ws.Cell(1, c++).Value = "Servis Oracle No";
                ws.Cell(1, c++).Value = "İş Emri";
                ws.Cell(1, c++).Value = "Hakediş Adet";
                ws.Cell(1, c++).Value = "Satır Birim Fiyat (TL)";
                ws.Cell(1, c++).Value = "Satır Toplam (TL)";
                ws.Cell(1, c++).Value = "Satır Birim Fiyat (USD)";
                ws.Cell(1, c++).Value = "Satır Toplam (USD)";
                ws.Cell(1, c++).Value = "GL Kodu";
                ws.Cell(1, c++).Value = "MGS Açıklama";
                ws.Cell(1, c++).Value = "Sözleşme No";
                ws.Cell(1, c++).Value = "İşlem Tipi";
                ws.Cell(1, c++).Value = "Açıklama";
                ws.Cell(1, c++).Value = "Montaj Tarihi";
                ws.Cell(1, c++).Value = "İndirim Oranı";

                ws.Range(1, 1, 1, c - 1).Style.Font.SetBold();
                var headerRange = ws.Range(1, 1, 1, c - 1);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Font.FontColor = XLColor.Black;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws.Row(1).Height = 22;
                headerRange.Style.Font.Bold = true;

                var r = 2;
                int siraNo = 1;
                foreach (var x in rows)
                {
                    c = 1;
                    ws.Cell(r, c++).Value = siraNo++;
                    ws.Cell(r, c++).Value = x.RequestNo;
                    ws.Cell(r, c++).Value = x.City;
                    ws.Cell(r, c++).Value = x.CustomerName;
                    ws.Cell(r, c++).Value = x.ProductCode;
                    ws.Cell(r, c++).Value = x.LocationCode;
                    ws.Cell(r, c++).Value = x.ProductOracleCode;
                    ws.Cell(r, c++).Value = x.ProductDefinition;

                    var svcDateCell = ws.Cell(r, c++);
                    if (x.ServiceDate.HasValue)
                    {
                        svcDateCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                    }

                    ws.Cell(r, c++).Value = x.ServiceOracleNo;
                    ws.Cell(r, c++).Value = x.WorkOrder;
                    ws.Cell(r, c++).Value = x.Quantity;

                    var uTL = ws.Cell(r, c++); uTL.Value = x.LineUnitPriceTL; uTL.Style.NumberFormat.Format = "#,##0.00";
                    var tTL = ws.Cell(r, c++); tTL.Value = x.LineTotalTL; tTL.Style.NumberFormat.Format = "#,##0.00";
                    var uUS = ws.Cell(r, c++); uUS.Value = x.LineUnitPriceUSD; uUS.Style.NumberFormat.Format = "#,##0.00";
                    var tUS = ws.Cell(r, c++); tUS.Value = x.LineTotalUSD; tUS.Style.NumberFormat.Format = "#,##0.00";

                    ws.Cell(r, c++).Value = x.GLCode;
                    ws.Cell(r, c++).Value = x.MGSDescription;
                    ws.Cell(r, c++).Value = x.Contract_No;
                    ws.Cell(r, c++).Value = x.CostType;
                    ws.Cell(r, c++).Value = x.Description;

                    var instDateCell = ws.Cell(r, c++);
                    if (x.InstallationDate.HasValue)
                    {
                        instDateCell.Style.DateFormat.Format = "yyyy-MM-dd";
                    }

                    var disc = ws.Cell(r, c++);
                    disc.Value = x.DiscountPercent;
                    disc.Style.NumberFormat.Format = "0.00%";

                    r++;
                }

                ws.Columns().AdjustToContents();

                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                var bytes = ms.ToArray();

                var fileName = $"QnbServisTalepleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return (bytes, fileName, contentType);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        // ------------------------ Report SP param helper ------------------------
        private static DynamicParameters BuildReportParams(QnbReportQueryParams q, int? overridePage = null, int? overridePageSize = null)
        {
            var p = new DynamicParameters();
            p.Add("@Page", overridePage ?? q.Page);
            p.Add("@PageSize", overridePageSize ?? q.PageSize);
            p.Add("@SortBy", q.SortBy);

            p.Add("@CreatedFrom", q.CreatedFrom);
            p.Add("@CreatedTo", q.CreatedTo);
            p.Add("@ServicesDateFrom", q.ServicesDateFrom);
            p.Add("@ServicesDateTo", q.ServicesDateTo);

            p.Add("@Search", q.Search);
            p.Add("@RequestNo", q.RequestNo);
            p.Add("@CustomerId", q.CustomerId);
            p.Add("@CustomerName", q.CustomerName);
            p.Add("@TechnicianId", q.TechnicianId);
            p.Add("@ServiceTypeId", q.ServiceTypeId);
            p.Add("@StepCode", q.StepCode);

            p.Add("@IsAgreement", q.IsAgreement);
            p.Add("@IsLocationValid", q.IsLocationValid);
            p.Add("@HasImages", q.HasImages);

            string? csvWF = (q.WorkFlowStatuses is { Count: > 0 }) ? string.Join(",", q.WorkFlowStatuses.Select(s => (int)s)) : null;
            string? csvTS = (q.TechnicalStatuses is { Count: > 0 }) ? string.Join(",", q.TechnicalStatuses.Select(s => (int)s)) : null;
            string? csvPR = (q.PricingStatuses is { Count: > 0 }) ? string.Join(",", q.PricingStatuses.Select(s => (int)s)) : null;
            string? csvFA = (q.FinalApprovalStatuses is { Count: > 0 }) ? string.Join(",", q.FinalApprovalStatuses.Select(s => (int)s)) : null;

            p.Add("@WorkFlowStatusesCsv", csvWF);
            p.Add("@TechStatusesCsv", csvTS);
            p.Add("@PricingStatusesCsv", csvPR);
            p.Add("@FinalStatusesCsv", csvFA);

            p.Add("@ProductId", q.ProductId);
            p.Add("@ProductCode", q.ProductCode);

            return p;
        }

        // SP satır tipi (sadece bu service kullanır)
        private sealed class ReportRowDto
        {
            public int TotalCount { get; set; }
            public string RequestNo { get; set; } = default!;
            public string? Title { get; set; }
            public int WorkFlowStatus { get; set; }
            public string? StepCode { get; set; }
            public DateTimeOffset CreatedDate { get; set; }
            public long CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? City { get; set; }
            public string? District { get; set; }
            public DateTimeOffset ServicesDate { get; set; }
            public long ServiceTypeId { get; set; }
            public string? ServiceTypeName { get; set; }
            public long? TechnicianId { get; set; }
            public string? TechnicianName { get; set; }
            public decimal Subtotal { get; set; }
            public string Currency { get; set; } = "TRY";
        }


        // ------------------------ Archive — public ------------------------
        public async Task<ResponseModel<PagedResult<QnbWorkFlowArchiveListDto>>> GetArchiveListAsync(QnbWorkFlowArchiveFilterDto filter)
        {
            try
            {
                var q = _uow.Repository
                    .GetQueryable<QnbWorkFlowArchive>()
                    .AsNoTracking();

                if (!string.IsNullOrWhiteSpace(filter.RequestNo))
                {
                    var rn = filter.RequestNo.Trim();
                    q = q.Where(x => x.RequestNo.Contains(rn));
                }

                if (!string.IsNullOrWhiteSpace(filter.ArchiveReason))
                {
                    var reason = filter.ArchiveReason.Trim();
                    q = q.Where(x => x.ArchiveReason == reason);
                }

                if (filter.ArchivedFrom.HasValue)
                    q = q.Where(x => x.ArchivedAt >= filter.ArchivedFrom.Value);

                if (filter.ArchivedTo.HasValue)
                    q = q.Where(x => x.ArchivedAt <= filter.ArchivedTo.Value);

                var projected = q
                    .Select(a => new
                    {
                        a.Id,
                        a.RequestNo,
                        a.ArchiveReason,
                        a.ArchivedAt,
                        a.CustomerJson,
                        a.ApproverTechnicianJson,
                        a.QnbWorkFlowJson
                    })
                    .OrderByDescending(x => x.ArchivedAt);

                var page = filter.Page <= 0 ? 1 : filter.Page;
                var pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

                var totalCount = await projected.CountAsync();

                var pageRows = await projected
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var list = new List<QnbWorkFlowArchiveListDto>(pageRows.Count);

                foreach (var a in pageRows)
                {
                    string? customerName = null;
                    string? technicianName = null;
                    string? wfStatus = null;

                    try
                    {
                        var customer = JsonConvert.DeserializeObject<Customer>(a.CustomerJson);
                        customerName = customer?.ContactName1 ?? customer?.SubscriberCompany;
                    }
                    catch { }

                    try
                    {
                        var tech = JsonConvert.DeserializeObject<User>(a.ApproverTechnicianJson);
                        technicianName = tech?.TechnicianName;
                    }
                    catch { }

                    try
                    {
                        var wf = JsonConvert.DeserializeObject<QnbWorkFlow>(a.QnbWorkFlowJson);
                        wfStatus = wf?.WorkFlowStatus.ToString();
                    }
                    catch { }

                    list.Add(new QnbWorkFlowArchiveListDto
                    {
                        Id = a.Id,
                        RequestNo = a.RequestNo,
                        ArchiveReason = a.ArchiveReason,
                        ArchivedAt = a.ArchivedAt,
                        CustomerName = customerName,
                        TechnicianName = technicianName,
                        WorkFlowStatus = wfStatus
                    });
                }

                if (!string.IsNullOrWhiteSpace(filter.CustomerName))
                {
                    var cn = filter.CustomerName.Trim().ToLowerInvariant();
                    list = list
                        .Where(x => !string.IsNullOrEmpty(x.CustomerName) &&
                                    x.CustomerName!.ToLowerInvariant().Contains(cn))
                        .ToList();
                }

                if (!string.IsNullOrWhiteSpace(filter.TechnicianName))
                {
                    var tn = filter.TechnicianName.Trim().ToLowerInvariant();
                    list = list
                        .Where(x => !string.IsNullOrEmpty(x.TechnicianName) &&
                                    x.TechnicianName!.ToLowerInvariant().Contains(tn))
                        .ToList();
                }

                var paged = new PagedResult<QnbWorkFlowArchiveListDto>(
                    Items: list,
                    TotalCount: totalCount,
                    Page: page,
                    PageSize: pageSize
                );

                return ResponseModel<PagedResult<QnbWorkFlowArchiveListDto>>.Success(paged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveListAsync");
                return ResponseModel<PagedResult<QnbWorkFlowArchiveListDto>>.Fail(
                    $"Arşiv kayıtları getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        public async Task<ResponseModel<QnbWorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id)
        {
            try
            {
                var archive = await _uow.Repository
                    .GetQueryable<QnbWorkFlowArchive>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (archive is null)
                {
                    return ResponseModel<QnbWorkFlowArchiveDetailDto>.Fail(
                        "Arşiv kaydı bulunamadı.",
                        StatusCode.NotFound
                    );
                }

                var dto = BuildArchiveDetailDto(archive);
                return ResponseModel<QnbWorkFlowArchiveDetailDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveDetailByIdAsync");
                return ResponseModel<QnbWorkFlowArchiveDetailDto>.Fail(
                    $"Arşiv detayı getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        public async Task<ResponseModel<QnbWorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestNo))
                {
                    return ResponseModel<QnbWorkFlowArchiveDetailDto>.Fail(
                        "RequestNo boş olamaz.",
                        StatusCode.BadRequest
                    );
                }

                var rn = requestNo.Trim();

                var archive = await _uow.Repository
                    .GetQueryable<QnbWorkFlowArchive>()
                    .AsNoTracking()
                    .Where(x => x.RequestNo == rn)
                    .OrderByDescending(x => x.ArchivedAt)
                    .FirstOrDefaultAsync();

                if (archive is null)
                {
                    return ResponseModel<QnbWorkFlowArchiveDetailDto>.Fail(
                        "Arşiv kaydı bulunamadı.",
                        StatusCode.NotFound
                    );
                }

                var dto = BuildArchiveDetailDto(archive);
                return ResponseModel<QnbWorkFlowArchiveDetailDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveDetailByRequestNoAsync");
                return ResponseModel<QnbWorkFlowArchiveDetailDto>.Fail(
                    $"Arşiv detayı getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        // ------------------------ Archive — internal ------------------------
        private async Task ArchiveWorkflowAsync(string requestNo, string archiveReason, CancellationToken ct = default)
        {
            var servicesRequest = await _uow.Repository
                .GetQueryable<QnbServicesRequest>()
                .Include(x => x.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            if (servicesRequest is null)
                return;

            var customer = servicesRequest.Customer;

            var workFlow = await _uow.Repository
                .GetQueryable<QnbWorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted, ct);

            var products = await _uow.Repository
                .GetQueryable<QnbServicesRequestProduct>()
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ToListAsync(ct);

            ProgressApprover? customerApprover = null;
            if (servicesRequest.CustomerApproverId.HasValue)
            {
                customerApprover = await _uow.Repository
                    .GetQueryable<ProgressApprover>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == servicesRequest.CustomerApproverId.Value, ct);
            }

            var technicalService = await _uow.Repository
                .GetQueryable<QnbTechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            var serviceImages = technicalService is null
                ? new List<QnbTechnicalServiceImage>()
                : await _uow.Repository
                    .GetQueryable<QnbTechnicalServiceImage>()
                    .AsNoTracking()
                    .Where(x => x.QnbTechnicalServiceId == technicalService.Id)
                    .ToListAsync(ct);

            var formImages = technicalService is null
                ? new List<QnbTechnicalServiceFormImage>()
                : await _uow.Repository
                    .GetQueryable<QnbTechnicalServiceFormImage>()
                    .AsNoTracking()
                    .Where(x => x.QnbTechnicalServiceId == technicalService.Id)
                    .ToListAsync(ct);

            var warehouse = await _uow.Repository
                .GetQueryable<QnbWarehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            var pricing = await _uow.Repository
                .GetQueryable<QnbPricing>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            var finalApproval = await _uow.Repository
                .GetQueryable<QnbFinalApproval>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            var reviewLogs = await _uow.Repository
                .GetQueryable<QnbWorkFlowReviewLog>()
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync(ct);

            // Resim DTO'ları (Base64 kapalı — YKB ile aynı davranış)
            var serviceImageDtos = serviceImages.Select(img => new ArchiveImageDto
            {
                Id = img.Id,
                Url = img.Url,
                Caption = img.Caption,
            }).ToList();

            var formImageDtos = formImages.Select(img => new ArchiveImageDto
            {
                Id = img.Id,
                Url = img.Url,
                Caption = img.Caption,
            }).ToList();

            // JSON serialize
            var archive = new QnbWorkFlowArchive
            {
                RequestNo = requestNo,
                ArchivedAt = DateTime.Now,
                ArchiveReason = archiveReason,

                QnbServicesRequestJson = JsonConvert.SerializeObject(servicesRequest),
                QnbServicesRequestProductsJson = JsonConvert.SerializeObject(products),
                CustomerJson = JsonConvert.SerializeObject(customer),
                ApproverTechnicianJson = JsonConvert.SerializeObject(workFlow?.ApproverTechnician),
                CustomerApproverJson = JsonConvert.SerializeObject(customerApprover),
                QnbWorkFlowJson = JsonConvert.SerializeObject(workFlow),
                QnbWorkFlowReviewLogsJson = JsonConvert.SerializeObject(reviewLogs),
                QnbTechnicalServiceJson = JsonConvert.SerializeObject(technicalService),
                QnbTechnicalServiceImagesJson = JsonConvert.SerializeObject(serviceImageDtos),
                QnbTechnicalServiceFormImagesJson = JsonConvert.SerializeObject(formImageDtos),
                QnbWarehouseJson = JsonConvert.SerializeObject(warehouse),
                QnbPricingJson = JsonConvert.SerializeObject(pricing),
                QnbFinalApprovalJson = JsonConvert.SerializeObject(finalApproval)
            };

            await _uow.Repository.AddAsync(archive);
            // Commit — çağıran tarafta yapılacak.
        }

        private QnbWorkFlowArchiveDetailDto BuildArchiveDetailDto(QnbWorkFlowArchive archive)
        {
            QnbServicesRequest? servicesRequest = null;
            List<QnbServicesRequestProduct> products = new();
            Customer? customer = null;
            User? approverTechnician = null;
            ProgressApprover? customerApprover = null;
            QnbWorkFlow? wf = null;
            List<QnbWorkFlowReviewLog> reviewLogs = new();
            QnbTechnicalService? technicalService = null;
            List<ArchiveImageDto> serviceImages = new();
            List<ArchiveImageDto> formImages = new();
            QnbWarehouse? warehouse = null;
            QnbPricing? pricing = null;
            QnbFinalApproval? finalApproval = null;

            try { servicesRequest = JsonConvert.DeserializeObject<QnbServicesRequest>(archive.QnbServicesRequestJson); } catch { }
            try { products = JsonConvert.DeserializeObject<List<QnbServicesRequestProduct>>(archive.QnbServicesRequestProductsJson) ?? new(); } catch { }
            try { customer = JsonConvert.DeserializeObject<Customer>(archive.CustomerJson); } catch { }
            try { approverTechnician = JsonConvert.DeserializeObject<User>(archive.ApproverTechnicianJson); } catch { }
            try { customerApprover = JsonConvert.DeserializeObject<ProgressApprover>(archive.CustomerApproverJson); } catch { }
            try { wf = JsonConvert.DeserializeObject<QnbWorkFlow>(archive.QnbWorkFlowJson); } catch { }
            try { reviewLogs = JsonConvert.DeserializeObject<List<QnbWorkFlowReviewLog>>(archive.QnbWorkFlowReviewLogsJson) ?? new(); } catch { }
            try { technicalService = JsonConvert.DeserializeObject<QnbTechnicalService>(archive.QnbTechnicalServiceJson); } catch { }
            try { serviceImages = JsonConvert.DeserializeObject<List<ArchiveImageDto>>(archive.QnbTechnicalServiceImagesJson) ?? new(); } catch { }
            try { formImages = JsonConvert.DeserializeObject<List<ArchiveImageDto>>(archive.QnbTechnicalServiceFormImagesJson) ?? new(); } catch { }
            try { warehouse = JsonConvert.DeserializeObject<QnbWarehouse>(archive.QnbWarehouseJson); } catch { }
            try { pricing = JsonConvert.DeserializeObject<QnbPricing>(archive.QnbPricingJson); } catch { }
            try { finalApproval = JsonConvert.DeserializeObject<QnbFinalApproval>(archive.QnbFinalApprovalJson); } catch { }


            // --------------------------------------------------------------------
            //  🔹 IMAGE URL NORMALİZASYONU (FileUrl bazlı)
            // --------------------------------------------------------------------
            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.FileUrl?.TrimEnd('/') ?? "";

            string? NormalizeImageUrl(string? urlOrFileName)
            {
                if (string.IsNullOrWhiteSpace(urlOrFileName))
                    return urlOrFileName;

                // 1) Zaten tam URL ise dokunma
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

                // 3) Sadece dosya adı ise
                var relative = $"/uploads/{urlOrFileName}";

                return string.IsNullOrEmpty(baseUrl)
                    ? relative
                    : $"{baseUrl}{relative}";
            }

            // Arşiv servis görselleri
            if (serviceImages != null)
            {
                foreach (var img in serviceImages)
                {
                    img.Url = NormalizeImageUrl(img.Url) ?? img.Url;
                }
            }

            // Arşiv form görselleri
            if (formImages != null)
            {
                foreach (var img in formImages)
                {
                    img.Url = NormalizeImageUrl(img.Url) ?? img.Url;
                }
            }
            // --------------------------------------------------------------------


            var snapshot = new QnbWorkFlowArchiveSnapshotDto
            {
                ServicesRequest = servicesRequest,
                Products = products,
                Customer = customer,
                ApproverTechnician = approverTechnician,
                CustomerApprover = customerApprover,
                WorkFlow = wf,
                WorkFlowReviewLogs = reviewLogs,
                TechnicalService = technicalService,
                ServiceImages = serviceImages,
                FormImages = formImages,
                Warehouse = warehouse,
                Pricing = pricing,
                FinalApproval = finalApproval
            };

            return new QnbWorkFlowArchiveDetailDto
            {
                Id = archive.Id,
                RequestNo = archive.RequestNo,
                ArchivedAt = archive.ArchivedAt,
                ArchiveReason = archive.ArchiveReason,
                Snapshot = snapshot
            };
        }

        // ------------------------ Lokasyon parse + mesafe ------------------------
        private static bool TryParseLatLon(string? s, out double value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            s = s.Trim().Replace(" ", "").Replace(',', '.');
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }

        private async Task<ResponseModel> IsTechnicianInValidLocation(string? lat1, string? lon1, string? lat2, string? lon2)
        {
            var cfg = await _uow.Repository.GetSingleAsync<Configuration>(false, x => x.Name == "TechnicianCustomerMinDistanceKm");
            if (cfg is null)
                return ResponseModel.Fail("Konum kontrolü için gerekli 'TechnicianCustomerMinDistanceKm' tanımı bulunamadı.", StatusCode.NotFound);

            if (!TryParseLatLon(cfg.Value, out var minDistanceKm))
                return ResponseModel.Fail("'TechnicianCustomerMinDistanceKm' değeri sayısal formatta değil.", StatusCode.InvalidConfiguration);

            if (string.IsNullOrWhiteSpace(lat1) || string.IsNullOrWhiteSpace(lon1))
                return ResponseModel.Fail("Müşteri lokasyonu geçersiz veya eksik.", StatusCode.InvalidCustomerLocation);

            if (!TryParseLatLon(lat1, out var latitude1) || !TryParseLatLon(lon1, out var longitude1))
                return ResponseModel.Fail("Müşteri lokasyonu hatalı formatta.", StatusCode.InvalidCustomerLocation);

            if (string.IsNullOrWhiteSpace(lat2) || string.IsNullOrWhiteSpace(lon2))
                return ResponseModel.Fail("Teknisyen lokasyonu geçersiz veya eksik.", StatusCode.InvalidTechnicianLocation);

            if (!TryParseLatLon(lat2, out var latitude2) || !TryParseLatLon(lon2, out var longitude2))
                return ResponseModel.Fail("Teknisyen lokasyonu hatalı formatta.", StatusCode.InvalidTechnicianLocation);

            var distance = GetDistanceInKm(latitude1, longitude1, latitude2, longitude2);

            var distanceFormatted = distance.ToString("F2", CultureInfo.InvariantCulture);
            var minDistanceFormatted = minDistanceKm.ToString("F2", CultureInfo.InvariantCulture);

            if (distance > minDistanceKm)
                return ResponseModel.Fail(
                    $"Mevcut konumunuz müşteri konumuna {distanceFormatted} km uzaklıkta, izin verilen maksimum mesafe {minDistanceFormatted} km.",
                    StatusCode.DistanceNotSatisfied
                );

            return ResponseModel.Success();
        }

        private static double GetDistanceInKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            double latRad1 = ToRadians(lat1);
            double lonRad1 = ToRadians(lon1);
            double latRad2 = ToRadians(lat2);
            double lonRad2 = ToRadians(lon2);

            double deltaLat = latRad2 - latRad1;
            double deltaLon = lonRad2 - lonRad1;

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(latRad1) * Math.Cos(latRad2) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        private static double ToRadians(double deg) => deg * (Math.PI / 180);

        // ------------------------ Mail helpers ------------------------
        private async Task<List<string>> ResolveWarehouseEmailsAsync(CancellationToken ct = default)
        {
            var WH_CODES_UP = new[] { "WH", "WAREHOUSE", "DEPO" };

            var emails = await _uow.Repository.GetQueryable<User>()
                .AsNoTracking()
                .Where(u => !u.IsDeleted
                    && u.UserRoles.Any(ur =>
                        ur.Role != null
                        && ur.Role.Code != null
                        && WH_CODES_UP.Contains(ur.Role.Code.ToUpper())))
                .Select(u => string.IsNullOrWhiteSpace(u.TechnicianEmail) ? "" : u.TechnicianEmail)
                .Where(mail => !string.IsNullOrWhiteSpace(mail))
                .Distinct()
                .ToListAsync(ct);

            return emails!;
        }

        private static string? GetTechnicianEmail(QnbWorkFlow wf)
        {
            return wf?.ApproverTechnician?.TechnicianEmail;
        }

        private async Task PushTransitionMailsAsync(QnbWorkFlow wf, string fromCode, string toCode, string requestNo, string? customerName)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            // 1) Teknisyen — TS yönüne gidişler
            var techMail = GetTechnicianEmail(wf);
            if (!string.IsNullOrWhiteSpace(techMail) && (toCode == "TS"))
            {
                var (subject, html) = BuildToTechnician(requestNo, fromCode, toCode, customerName);
                await _mailPush.EnqueueAsync(new MailOutbox
                {
                    RequestNo = requestNo,
                    FromStepCode = fromCode,
                    ToStepCode = toCode,
                    ToRecipients = techMail,
                    Subject = subject,
                    BodyHtml = html,
                    CreatedUser = meId
                });
            }

            // 2) Depo — WH yönüne gidişler
            if (toCode == "WH")
            {
                var whMails = await ResolveWarehouseEmailsAsync();
                if (whMails.Count > 0)
                {
                    var (subject, html) = BuildToWarehouse(requestNo, fromCode, toCode, customerName);
                    await _mailPush.EnqueueAsync(new MailOutbox
                    {
                        RequestNo = requestNo,
                        FromStepCode = fromCode,
                        ToStepCode = toCode,
                        ToRecipients = string.Join(";", whMails),
                        Subject = subject,
                        BodyHtml = html,
                        CreatedUser = meId
                    });
                }
            }
        }

        private static (string subject, string html) BuildToTechnician(string requestNo, string fromCode, string toCode, string? customerName)
        {
            var subject = $"[{requestNo}] Akış güncellendi: {fromCode} → {toCode}";
            var html = $@"
                <div style='font-family:Arial'>
                    <h3>İş Akışı Güncellemesi</h3>
                    <p><b>Talep No:</b> {requestNo}</p>
                    <p><b>Aşama:</b> {fromCode} → {toCode}</p>
                    {(string.IsNullOrWhiteSpace(customerName) ? "" : $"<p><b>Müşteri:</b> {System.Net.WebUtility.HtmlEncode(customerName)}</p>")}
                    <p>Teknik servis için yeni bir adım oluştu. Lütfen kontrol ediniz.</p>
                </div>";
            return (subject, html);
        }

        private static (string subject, string html) BuildToWarehouse(string requestNo, string fromCode, string toCode, string? customerName)
        {
            var subject = $"[{requestNo}] Depo bilgilendirmesi: {fromCode} → {toCode}";
            var html = $@"
                 <div style='font-family:Arial'>
                     <h3>Depo Talep Bildirimi</h3>
                     <p><b>Talep No:</b> {requestNo}</p>
                     <p><b>Aşama:</b> {fromCode} → {toCode}</p>
                     {(string.IsNullOrWhiteSpace(customerName) ? "" : $"<p><b>Müşteri:</b> {System.Net.WebUtility.HtmlEncode(customerName)}</p>")}
                     <p>Servis Talebi ilgili adımda. Lütfen hazırlık/işlem yapınız.</p>
                 </div>";
            return (subject, html);
        }

        // ------------------------ Menu permission → step codes ------------------------
        private async Task<List<string>> GetUserStepsByMenuPermission(long userId)
        {
            var permissionList = await _menuService.GetByUserIdAsync(userId);

            if (permissionList is null || permissionList.Count == 0)
                return new List<string>();

            // Qnb menü adı → adım kodu
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["QnbServiceRequestWarehouse"] = "WH",
                ["QnbServiceRequestPricing"] = "PRC",
                ["QnbCancelledFlows"] = "CNC",
                ["QnbCustomerServiceRequestCreate"] = "CF",
                ["QnbServiceRequestCustomerAgreement"] = "CAPR",
                ["QnbServiceRequestFinalApproval"] = "APR",
                ["QnbServiceRequestCreate"] = "SR",
                ["QnbServiceRequestComplate"] = "CMP",
                ["QnbServiceRequestTechnicalService"] = "TS",
            };

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in permissionList)
            {
                if (p?.Name is null) continue;
                if (!(p.CanView || p.CanEdit)) continue;

                if (map.TryGetValue(p.Name, out var code))
                    result.Add(code);
            }

            return result.ToList();
        }

        // ------------------------ Ürün fiyat sabitleme ------------------------
        private async Task<ResponseModel> EnsurePricesCapturedFromDtoAsync(
            string requestNo,
            IEnumerable<QnbServicesRequestProductCreateDto>? productsDto)
        {
            var dtoDict = (productsDto ?? Enumerable.Empty<QnbServicesRequestProductCreateDto>())
                .ToDictionary(x => x.ProductId, x => x);

            if (!dtoDict.Any())
                return ResponseModel.Success();

            var list = await _uow.Repository.GetQueryable<QnbServicesRequestProduct>()
                .Include(x => x.Product)
                .Where(x => x.RequestNo == requestNo)
                .ToListAsync();

            if (list.Count == 0)
                return ResponseModel.Success();

            foreach (var p in list)
            {
                if (!dtoDict.TryGetValue(p.ProductId, out var dtoItem))
                    continue;

                var unit = dtoItem.Price;
                var currency = p.Product?.PriceCurrency ?? "TRY";
                var total = unit * p.Quantity;

                p.CapturedSource = CapturedPriceSource.Standard;
                p.CapturedUnitPrice = unit;
                p.CapturedCurrency = currency;
                p.CapturedTotal = total;
                p.CapturedAt = DateTime.Now;
                p.IsPriceCaptured = true;

                _uow.Repository.Update(p);
            }

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success();
        }
    }
}



