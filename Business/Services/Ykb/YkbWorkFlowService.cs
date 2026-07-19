using Azure.Core;
using Business.Interfaces;
using Business.Interfaces.Manitou;
using Business.Interfaces.Ykb;
using Business.Services.Manitou;
using Business.UnitOfWork;
using ClosedXML.Excel;
using Core.Common;
using Core.Enums;
using Core.Enums.Ykb;
using Core.Settings.Concrete;
using Core.Utilities.Constants;
using Core.Utilities.IoC;
using Dapper;
using Data.Concrete.EfCore.Context;
using DocumentFormat.OpenXml.Office2010.Excel;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Model.Concrete;
using Model.Concrete.Qnb;
using Model.Concrete.WorkFlows;
using Model.Concrete.Ykb;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerSystemAssignment;
using Model.Dtos.Manitou;
using Model.Dtos.Notification;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Role;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos;
using Model.Dtos.WorkFlowDtos.FinalApproval;
using Model.Dtos.WorkFlowDtos.QnbDtos.QnbTechnicalService;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbAttachment;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbCustomerForm;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbFinalApproval;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbPricing;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReport;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbReviewLog;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequest;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalService;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbTechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWarehouse;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlow;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbWorkFlowStep;
using Model.Dtos.WorkOrderType;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using WorkOrderTypeLiteDto = Model.Dtos.WorkFlowDtos.Report.WorkOrderTypeLiteDto;

namespace Business.Services.Ykb
{
    public class YkbWorkFlowService : IYkbWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly TypeAdapterConfig _config;
        private readonly IActivationRecordService _activationRecord;
        private readonly ILogger<YkbWorkFlowService> _logger;
        private readonly IMailPushService _mailPush;
        private readonly ICurrentUser _currentUser;
        private readonly INotificationService _notification;
        private readonly IMenuService _menuService;
        private readonly IManitouApiService _manitouApiService;
        private readonly AppDataContext _ctx;


        public YkbWorkFlowService(IUnitOfWork uow, TypeAdapterConfig config, IAuthService authService, IActivationRecordService activationRecord,
            ILogger<YkbWorkFlowService> logger, IMailPushService mailPush, ICurrentUser currentUser, AppDataContext ctx, INotificationService notification, IMenuService menuService, IManitouApiService manitouApiService)
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
            _manitouApiService = manitouApiService;
        }

        /// -------------------- ServicesRequest --------------------
        //0 Müşteri kendi formunu oluşturulması ve Servis talebine gönderim.  
        public async Task<ResponseModel<YkbCustomerFormGetDto>> CreateCustomerForm(YkbCustomerFormCreateDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                // Başlangıç WorkFlowStep'i Bul
                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "SR"); // Örn: 'SR' (Services Request) kodu ile başlangıç adımı

                if (targetStep is null)
                    return ResponseModel<YkbCustomerFormGetDto>.Fail("İş akışı hedef adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                // RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("YKB");
                    if (!rn.IsSuccess)
                        return ResponseModel<YkbCustomerFormGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                bool exists = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .AnyAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (exists)
                    return ResponseModel<YkbCustomerFormGetDto>.Fail("Aynı akış numarasi ile başka bir kayıt zaten var.", StatusCode.Conflict);


                var customerExist = await _uow.Repository.GetQueryable<Customer>().AsNoTracking().AnyAsync(c => c.Id == dto.CustomerId);
                if (!customerExist)
                    return ResponseModel<YkbCustomerFormGetDto>.Fail("Müşteri bulunamadı.", StatusCode.Conflict);

                var customerApproverExist = dto.CustomerApproverId.HasValue ? await _uow.Repository.GetQueryable<ProgressApprover>().AsNoTracking().AnyAsync(ca => ca.Id == dto.CustomerApproverId.Value) : true;
                if (!customerApproverExist)
                    return ResponseModel<YkbCustomerFormGetDto>.Fail("Müşteri yetkilisi bulunamadı.", StatusCode.Conflict);

                var (ykbWorkOrderTypeIds, ykbWorkOrderTypeError) = await ValidateWorkOrderTypeIdsAsync(dto.WorkOrderTypeIds);
                if (ykbWorkOrderTypeError is not null)
                    return ResponseModel<YkbCustomerFormGetDto>.Fail(ykbWorkOrderTypeError, StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Müşteri formu Oluşturma 
                var customerForm = dto.Adapt<YkbCustomerForm>(_config);
                customerForm.CreatedDate = DateTime.Now;
                customerForm.CreatedUser = meId;
                customerForm.Status = Core.Enums.Ykb.YkbCustomerFormStatus.Draft;
                await _uow.Repository.AddAsync(customerForm);
                #endregion

                #region  WorkFlow oluştur (aynı RequestNo ile)
                var wf = new YkbWorkFlow
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
                var request = customerForm.Adapt<YkbServicesRequest>(_config);
                request.CreatedDate = DateTime.Now;
                request.CreatedUser = meId;
                request.ServicesRequestStatus = ServicesRequestStatus.Draft;
                request.Id = 0;
                request.YkbServicesRequestWorkOrderTypes = ykbWorkOrderTypeIds
                    .Select(wotId => new YkbServicesRequestWorkOrderType { WorkOrderTypeId = wotId })
                    .ToList();
                await _uow.Repository.AddAsync(request);
                #endregion 

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
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
                        Message = $"{dto.RequestNo} numaralı servis talebi müşteri tarafından iletildi.",
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
                return ResponseModel<YkbCustomerFormGetDto>.Fail($"CreateCustomerForm Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }
        //1 Servis Talebi güncelleme adımı :
        public async Task<ResponseModel<YkbServicesRequestGetDto>> UpdateServiceRequestAsync(YkbServicesRequestUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<YkbServicesRequest>(
                false,
                x => x.RequestNo == dto.RequestNo,
                includeExpression: RequestIncludes());

            if (entity is null)
                return ResponseModel<YkbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var wf = await _uow.Repository
            .GetQueryable<YkbWorkFlow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<YkbServicesRequestGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);


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
                .GetMultipleAsync<YkbServicesRequestProduct>(
                    asNoTracking: false, // track etsin ki güncelleme/silmede kullanılabilsin
                    whereExpression: x => x.RequestNo == dto.RequestNo);
            // Ürün listesi değişmişse:
            if (dto.Products is not null)
            {
                // Yeni ürün setini dictionary olarak hazırla (ProductId bazlı)
                var updatedProducts = dto.Products
                    .GroupBy(p => p.ProductId)
                    .Select(g => g.First()) // Aynı ürün tekrar varsa tek al
                    .ToDictionary(p => p.ProductId, p => p);



                // Koleksiyonlar null olabilir, önlem al
                existingProducts ??= new List<YkbServicesRequestProduct>();

                // Silinecek ürünler (DB'de var ama DTO'da yok)
                var toRemove = existingProducts
                    .Where(p => !updatedProducts.ContainsKey(p.ProductId))
                    .ToList();

                // Eklenecek ürünler (DTO'da var ama DB'de yok)
                var toAdd = updatedProducts
                    .Where(p => !existingProducts.Any(e => e.ProductId == p.Key))
                    .Select(p => p.Value)
                    .ToList();

                // Güncellenecek ürünler (hem var hem değişmiş)
                var toUpdate = existingProducts
                    .Where(p => updatedProducts.ContainsKey(p.ProductId))
                    .ToList();

                // ❌ Sil
                foreach (var prod in toRemove)
                    await _uow.Repository.HardDeleteAsync(prod);

                // ➕ Ekle
                foreach (var prod in toAdd)
                {
                    var entityProd = new YkbServicesRequestProduct
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

            #region İş Emri Türleri Güncellemesi
            if (dto.WorkOrderTypeIds is not null)
            {
                var ykbSrEntity = await _uow.Repository.GetQueryable<YkbServicesRequest>()
                    .Include(x => x.YkbServicesRequestWorkOrderTypes)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (ykbSrEntity is not null)
                {
                    var (validatedWotIds, wotError) = await ValidateWorkOrderTypeIdsAsync(dto.WorkOrderTypeIds);
                    if (wotError is null)
                        SyncYkbWorkOrderTypes(ykbSrEntity, validatedWotIds);
                }
            }
            #endregion

            await _uow.Repository.UpdateAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetServiceRequestByRequestNoAsync(entity.RequestNo);
        }

        //2.1 Depoya Gönderim  (Ürün var ise)
        public async Task<ResponseModel<YkbWarehouseGetDto>> SendWarehouseAsync(YkbSendWarehouseDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                //Talep getir (tracking kapalı)
                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                if (request.ServicesRequestStatus == ServicesRequestStatus.WarehouseSubmitted)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);


                var product = await _uow.Repository.GetQueryable<YkbServicesRequestProduct>(x => x.RequestNo == dto.RequestNo).ToListAsync();
                if (product is null || product.Count == 0)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Bu talep için kayıtlı ürün bulunamadı. Depoya gönderim için en az bir ürün eklenmiş olmalıdır.", StatusCode.BadRequest);

                //WorkFlow getir
                var wf = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);



                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "WH");

                //Warehouse kaydını getir (varsa)
                var warehouse = await _uow.Repository
                    .GetQueryable<YkbWarehouse>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Depo Ekle/Güncelle
                //Yoksa oluştur
                if (warehouse == null)
                {
                    warehouse = new YkbWarehouse
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

                //Varsa güncelle
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
                request.YkbWorkFlowStep = null;
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
                await _activationRecord.LogYkbAsync(
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


                // Commit
                await _uow.Repository.CompleteAsync();

                #region Notiification Kayıd
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

                //Güncel talebi döndür
                return await GetWarehouseByRequestNoAsync(request.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendWarehouseAsync");
                return ResponseModel<YkbWarehouseGetDto>.Fail($"Depo gönderim sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //2.2 Depo Teslimatı ve Teknik servise Gönderim (Ürün var ise)
        public async Task<ResponseModel<YkbWarehouseGetDto>> CompleteDeliveryAsync(YkbCompleteDeliveryDto dto)
        {

            try
            {
                #region Validasyon/Kontroller
                var wf = await _uow.Repository
                   .GetQueryable<YkbWorkFlow>()
                   .Include(x => x.ApproverTechnician)
                   .AsNoTracking()
                   .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);


                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var warehouse = await _uow.Repository
                    .GetQueryable<YkbWarehouse>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (warehouse is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Depo kaydı bulunamadı.", StatusCode.NotFound);


                // 🔹 WorkFlow güncelle
                var targetStep = await _uow.Repository
                    .GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .Where(x => x.Code != null && x.Code == "TS")
                    .Select(x => new { x.Id })
                    .FirstOrDefaultAsync();

                if (targetStep is null)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("WorkFlowStep içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);


                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Teknik servis kaydı Ekle/Güncelle

                var technicalService = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                //Varsa Teknik servis kaydını güncelle
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
                //Yoksa Teknik servis kaydı oluştur
                else
                {
                    technicalService = new YkbTechnicalService
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

                #region Warehouse  bilgilerini güncelle
                warehouse.DeliveryDate = dto.DeliveryDate;
                warehouse.Description = dto.Description;
                warehouse.WarehouseStatus = WarehouseStatus.Shipped;
                _uow.Repository.Update(warehouse);
                #endregion

                #region Wordflow kaydı güncelle

                wf.CurrentStepId = targetStep.Id;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                #region Ürünler  Ekle/Güncelle
                // 🔹 ServicesRequestProduct senkronizasyonu
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<YkbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                // Dictionary ile hızlı karşılaştırma
                var deliveredDict = dto.DeliveredProducts.ToDictionary(x => x.ProductId, x => x);

                // 1️ Güncelle veya Sil (mevcut ürünler üzerinden)
                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        // Güncelle
                        existing.Quantity = delivered.Quantity;
                        _uow.Repository.Update(existing);

                        // Güncellenen ürünü işaretle (artık yeniden eklenmeyecek)
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        // Delivered listede yok → Sil
                        _uow.Repository.HardDelete(existing);
                    }
                }

                // 2️ Yeni ürünleri ekle (DeliveredProducts'ta olup DB'de olmayanlar)
                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new YkbServicesRequestProduct
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
                await _activationRecord.LogYkbAsync(
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

                // 🔹 Değişiklikleri kaydet
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
                            FromStepCode = "WH",
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

                // 🔹 Son durumu döndür
                return await GetWarehouseByIdAsync(warehouse.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteDeliveryAsync");
                return ResponseModel<YkbWarehouseGetDto>.Fail($" Depo Teslimatı  sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        //2.3 Teknik Servis Gönderim  (Ürün yok ise)
        public async Task<ResponseModel<YkbTechnicalServiceGetDto>> SendTechnicalServiceAsync(YkbSendTechnicalServiceDto dto)
        {
            try
            {
                #region Validasyonlar/Kontroller

                var wf = await _uow.Repository
                  .GetQueryable<YkbWorkFlow>()
                  .Include(x => x.ApproverTechnician)
                  .AsNoTracking()
                  .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);


                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == "TS");
                if (targetStep is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);


                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Teknik servis kaydını Ekle/Güncelle
                var technicalService = await _uow.Repository
                     .GetQueryable<YkbTechnicalService>()
                     .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                //Varsa Teknik servis kaydını güncelle
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
                    // 🔹 Teknik servis kaydı oluştur
                    technicalService = new YkbTechnicalService
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
                await _activationRecord.LogYkbAsync(
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

                // 🔹 Değişiklikleri kaydet
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
                // 🔹 Son durumu döndür
                return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendTechnicalServiceAsync");
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail($"Teknik Servis Gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        // 3 Teknik Servis Servisi Başlatma 
        public async Task<ResponseModel<YkbTechnicalServiceGetDto>> StartService(YkbStartTechnicalServiceDto dto)
        {

            try
            {

                #region Validasyon/Kontroller
                //WorkFlow getir
                var wf = await _uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);



                var request = await _uow.Repository
                   .GetQueryable<YkbServicesRequest>()
                   .Include(x => x.Customer)
                   .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var customer = await _uow.Repository
                    .GetQueryable<Customer>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

                if (customer is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

                var technicalService = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

                if (technicalService.ServicesStatus == TechnicalServiceStatus.InProgress)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Teknik servis zaten başlatılmış", StatusCode.Conflict);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Lokasyon kontrolü
                if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
                {
                    if (string.IsNullOrEmpty(dto.Longitude) && !string.IsNullOrEmpty(dto.Latitude))
                    {
                        return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.InvalidCustomerLocation);
                    }
                    else
                    {
                        var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, dto.Latitude, dto.Longitude);
                        if (!locationResult.IsSuccess)
                        {
                            #region Hareket Loglama
                            await _activationRecord.LogYkbAsync(
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

                            return ResponseModel<YkbTechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
                        }
                    }
                }


                #endregion

                #region Tekniks servisi güncelle
                technicalService.StartTime = DateTime.Now;
                technicalService.ServicesStatus = TechnicalServiceStatus.InProgress;
                technicalService.StartLocation = dto.StartLocation;
                technicalService.EndLocation = string.Empty;//Henüz servis bitmediği için boş bırakılıyor
                technicalService.UpdatedDate = DateTime.Now;
                technicalService.UpdatedUser = meId;
                _uow.Repository.Update(technicalService);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
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
                     Type = NotificationType.GenericInfo,
                     FromStepCode = "TS",
                     ToStepCode = "TS",
                     Title = $"Talep {dto.RequestNo} için teknik servis başlatıldı",
                     Message = $"{dto.RequestNo} numaralı talebin teknik servis işlemi başlatıldı.",
                     RequestNo = dto.RequestNo,
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
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Başlatma   sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        // 3.1 Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi
        public async Task<ResponseModel<YkbTechnicalServiceGetDto>> FinishService(YkbFinishTechnicalServiceDto dto)
        {
            try
            {

                #region Validasyon/Kontroller
                var wf = await _uow.Repository
                   .GetQueryable<YkbWorkFlow>()
                   .Include(x => x.ApproverTechnician)
                   .AsNoTracking()
                   .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);


                var request = await _uow.Repository
                   .GetQueryable<YkbServicesRequest>()
                   .Include(x => x.Customer)
                   .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var customer = await _uow.Repository
                    .GetQueryable<Customer>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

                if (customer is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

                var technicalService = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (technicalService is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "PRC");
                if (targetStep is null)
                    return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (PRC) tanımlı değil.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                var isTestEnabled = await IsManitouTechnicalServiceTestEnabledAsync(customer.TenantId);

                if (isTestEnabled)
                {
                    var activeWorkingExists = await _uow.Repository
                        .GetQueryable<YkbTechnicalServiceWorkSession>()
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.RequestNo == dto.RequestNo &&
                            x.IsActive &&
                            !x.IsCompleted &&
                            !x.IsDeleted);

                    if (activeWorkingExists)
                    {
                        return ResponseModel<YkbTechnicalServiceGetDto>.Fail(
                            "Aktif çalışma/test kaydı bitirilmeden teknik servis tamamlanamaz.",
                            StatusCode.Conflict);
                    }

                    var completedWorkingExists = await _uow.Repository
                        .GetQueryable<YkbTechnicalServiceWorkSession>()
                        .AsNoTracking()
                        .AnyAsync(x =>
                            x.RequestNo == dto.RequestNo &&
                            x.IsCompleted &&
                            !x.IsDeleted);

                    if (!completedWorkingExists)
                    {
                        return ResponseModel<YkbTechnicalServiceGetDto>.Fail(
                            "Teknik servis tamamlanmadan önce çalışma/test işlemi tamamlanmalıdır.",
                            StatusCode.Conflict);
                    }
                }
                #endregion

                #region Lokasyon kontrolü
                if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
                {
                    if (string.IsNullOrEmpty(dto.Longitude) && !string.IsNullOrEmpty(dto.Latitude))
                    {
                        return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.InvalidCustomerLocation);
                    }
                    else
                    {
                        var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, dto.Latitude, dto.Longitude);
                        if (!locationResult.IsSuccess)
                        {
                            #region Hareket Loglama
                            await _activationRecord.LogYkbAsync(
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

                            return ResponseModel<YkbTechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
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
                .GetQueryable<YkbPricing>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (pricing is null)
                {
                    pricing = new YkbPricing()
                    {
                        RequestNo = dto.RequestNo,
                        Status = PricingStatus.Pending,
                        Currency = "TRY", ///İncelencek
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
                //var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
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

                    // DB’de sadece dosya adını tutalım (URL hesaplamasını dışarıda yaparız)
                    return name;
                }

                var toAddImages = new List<YkbTechnicalServiceImage>();
                var toAddFormImages = new List<YkbTechnicalServiceFormImage>();
                var savedFiles = new List<string>(); // olası temizlik için

                try
                {
                    if (dto.ServiceImages is not null)
                    {
                        foreach (var f in dto.ServiceImages)
                        {
                            var url = await SaveAsync(f, CancellationToken.None);
                            if (url is null) continue;
                            toAddImages.Add(new YkbTechnicalServiceImage
                            {
                                YkbTechnicalServiceId = technicalService.Id,
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
                            toAddFormImages.Add(new YkbTechnicalServiceFormImage
                            {
                                YkbTechnicalServiceId = technicalService.Id,
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
                // 🔹 ServicesRequestProduct senkronizasyonu
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<YkbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                // Dictionary ile hızlı karşılaştırma
                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x) ?? new Dictionary<long, YkbServicesRequestProductCreateDto>();
                // 1️ Güncelle veya Sil (mevcut ürünler üzerinden)
                foreach (var existing in existingProducts)
                {
                    if (deliveredDict.TryGetValue(existing.ProductId, out var delivered))
                    {
                        // Güncelle
                        existing.Quantity = delivered.Quantity;
                        _uow.Repository.Update(existing);

                        // Güncellenen ürünü işaretle (artık yeniden eklenmeyecek)
                        deliveredDict.Remove(existing.ProductId);
                    }
                    else
                    {
                        // Tekniks Servis listede yok → Sil
                        _uow.Repository.HardDelete(existing);
                    }
                }

                // 2️ Yeni ürünleri ekle (TekniksServiste'te olup DB'de olmayanlar)
                foreach (var newItem in deliveredDict.Values)
                {
                    var newEntity = new YkbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }

                #endregion

                #region İş Emri Türleri Güncellemesi
                if (dto.WorkOrderTypeIds is not null)
                {
                    var ykbSrEntity = await _uow.Repository.GetQueryable<YkbServicesRequest>()
                        .Include(x => x.YkbServicesRequestWorkOrderTypes)
                        .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                    if (ykbSrEntity is not null)
                    {
                        var (validatedWotIds, wotError) = await ValidateWorkOrderTypeIdsAsync(dto.WorkOrderTypeIds);
                        if (wotError is null)
                            SyncYkbWorkOrderTypes(ykbSrEntity, validatedWotIds);
                    }
                }
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
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
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi   sırasında hata: {ex.Message}", StatusCode.Error);
            }


        }

        // 4 Fiyatlama onay ve kontrole gönderim.
        public async Task<ResponseModel<YkbPricingGetDto>> ApprovePricing(YkbPricingUpdateDto dto)
        {
            WorkflowAttachmentChangeSet? attachmentChangeSet = null;
            var attachmentsCommitted = false;
            try
            {
                #region Validasyonlar/Kontroller

                var wf = await _uow.Repository
                  .GetQueryable<YkbWorkFlow>()
                  .Include(x => x.ApproverTechnician)
                  .AsNoTracking()
                  .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbPricingGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbPricingGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbPricingGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbPricingGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);


                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "APR");
                if (targetStep is null)
                    return ResponseModel<YkbPricingGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                var pricing = await _uow.Repository
                   .GetQueryable<YkbPricing>()
                   .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (pricing is null)
                    return ResponseModel<YkbPricingGetDto>.Fail("Fiyatlama kaydı tanımlı değil.", StatusCode.BadRequest);


                var servicesRequest = await _uow.Repository
                  .GetQueryable<YkbServicesRequest>()
                  .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (servicesRequest is null)
                    return ResponseModel<YkbPricingGetDto>.Fail("Servis talebi kaydı bulunamadı.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;


                #endregion

                #region Fiyatlama ve Workflow  güncelleme 
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

                #region  Servis Maliyet Durumu Güncelleme 
                servicesRequest.ServicesCostStatus = dto.ServicesCostStatus;
                _uow.Repository.Update(servicesRequest);
                #endregion

                #region Ürünler Güncellemesi
                // 🔹 ServicesRequestProduct senkronizasyonu
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<YkbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x)
                                    ?? new Dictionary<long, YkbServicesRequestProductCreateDto>();

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
                    var newEntity = new YkbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Dosya Ekleme 
                attachmentChangeSet = await ApplyWorkflowAttachmentChangesAsync(
                         requestNo: dto.RequestNo,
                         attachments: dto.Attachments,
                         deletedAttachmentIds: dto.DeletedAttachmentIds,
                         replacedAttachments: dto.ReplacedAttachments,
                         stepCode: "PRC");
                #endregion


                #region Son Onaya Gönderim 
                var finalApproval = await _uow.Repository
                        .GetQueryable<YkbFinalApproval>()
                        .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);
                if (finalApproval is null)
                {
                    finalApproval = new YkbFinalApproval
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
                await _activationRecord.LogYkbAsync(
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

                #region Dosya Silme
                attachmentsCommitted = true;
                if (attachmentChangeSet is not null)
                {
                    // Eski dosyalar ancak DB commit başarılı olduktan sonra silinir.
                    DeleteWorkflowAttachmentPhysicalFiles(
                        attachmentChangeSet.OldStoredFileNames);
                }
                #endregion

                #region Ürün Fiyat Sabitleme (4. Adım)
                // 🔹 Artık fiyatı dto.Products listesinden alıyoruz
                await EnsurePricesCapturedFromDtoAsync(dto.RequestNo, dto.Products);
                #endregion

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
            catch (InvalidDataException ex)
            {
                if (!attachmentsCommitted && attachmentChangeSet is not null)
                {
                    DeleteWorkflowAttachmentPhysicalFiles(
                        attachmentChangeSet.NewStoredFileNames);
                }

                _logger.LogWarning(
                    ex,
                    "ApprovePricing dosya doğrulama hatası. RequestNo: {RequestNo}",
                    dto?.RequestNo);

                return ResponseModel<YkbPricingGetDto>.Fail(
                    ex.Message,
                    StatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                if (!attachmentsCommitted && attachmentChangeSet is not null)
                {
                    DeleteWorkflowAttachmentPhysicalFiles(
                        attachmentChangeSet.NewStoredFileNames);
                }
                _logger.LogError(ex, "ApprovePricing");
                return ResponseModel<YkbPricingGetDto>.Fail(
                    $"Fiyatlama onay ve kontrole gönderim sırasında hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        // 5  Kontrol ve Son Onay (FinalApproval) — CREATE
        public async Task<ResponseModel<YkbFinalApprovalGetDto>> FinalApprovalAsync(YkbFinalApprovalUpdateDto dto)
        {
            WorkflowAttachmentChangeSet? attachmentChangeSet = null;
            var attachmentsCommitted = false;

            try
            {
                #region  Validasyonlar/Kontroller
                // 1) WorkFlow & Request kontrolleri
                var wf = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("İlgili akış tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var isTerminalStatus =
                 dto.WorkFlowStatus == WorkFlowStatus.Complated ||
                 dto.WorkFlowStatus == WorkFlowStatus.Cancelled;
                var statusCode = dto.FinalApprovalStatus == FinalApprovalStatus.CustomerApproval
                    ? "CAPR"
                    : dto.WorkFlowStatus switch
                    {
                        WorkFlowStatus.Cancelled => "CNC",
                        WorkFlowStatus.Complated => "CMP",
                        _ => "APR"
                    };


                // 2) Hedef adım: APR (Approval / Final Approval)
                var targetStep = await _uow.Repository
                    .GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == statusCode);

                if (targetStep is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail($"Hedef iş akışı adımı {statusCode} tanımlı değil.", StatusCode.BadRequest);



                // 3) FinalApproval var mı? (unique: RequestNo)
                var existsFinalApproval = await _uow.Repository
                    .GetQueryable<YkbFinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (existsFinalApproval is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.BadRequest);

                if (existsFinalApproval.Status == FinalApprovalStatus.CustomerApproval)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail($"Hedef iş akışı müşteri onayında.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                #endregion

                #region Workflow Güncelleme
                if (wf is not null)
                {
                    wf.CurrentStepId = targetStep.Id;
                    wf.UpdatedDate = DateTime.Now;
                    wf.UpdatedUser = meId;
                    wf.WorkFlowStatus = dto.FinalApprovalStatus == FinalApprovalStatus.CustomerApproval ? WorkFlowStatus.Pending : dto.WorkFlowStatus;
                    wf.IsAgreement = wf.WorkFlowStatus switch
                    {
                        WorkFlowStatus.Complated => true,
                        WorkFlowStatus.Cancelled => false,
                        _ => null
                    };
                    _uow.Repository.Update(wf);
                }
                #endregion

                #region Ürünler Güncellemesi
                var existingProducts = await _uow.Repository
                    .GetMultipleAsync<YkbServicesRequestProduct>(
                        asNoTracking: false,
                        whereExpression: x => x.RequestNo == dto.RequestNo
                    );

                var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x)
                                    ?? new Dictionary<long, YkbServicesRequestProductCreateDto>();

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
                    var newEntity = new YkbServicesRequestProduct
                    {
                        CustomerId = request.CustomerId,
                        RequestNo = request.RequestNo,
                        ProductId = newItem.ProductId,
                        Quantity = newItem.Quantity,
                    };
                    _uow.Repository.Add(newEntity);
                }
                #endregion

                #region Fiyatlama Güncelleme (FinalApproval)
                existsFinalApproval.Notes = dto.Notes;
                existsFinalApproval.Status = dto.WorkFlowStatus == WorkFlowStatus.Complated
                    ? FinalApprovalStatus.Approved
                    : (dto.WorkFlowStatus == WorkFlowStatus.Cancelled ? FinalApprovalStatus.Rejected : FinalApprovalStatus.Pending);

                existsFinalApproval.DecidedBy = meId;
                existsFinalApproval.UpdatedDate = DateTime.Now;
                existsFinalApproval.UpdatedUser = meId;

                // 💡 yeni alanlar
                existsFinalApproval.DiscountPercent = dto.DiscountPercent;
                existsFinalApproval.Status = dto.FinalApprovalStatus;

                _uow.Repository.Update(existsFinalApproval);
                #endregion

                #region Dosya Ekleme 
                attachmentChangeSet = await ApplyWorkflowAttachmentChangesAsync(
                         requestNo: dto.RequestNo,
                         attachments: dto.Attachments,
                         deletedAttachmentIds: dto.DeletedAttachmentIds,
                         replacedAttachments: dto.ReplacedAttachments,
                         stepCode: "PRC");
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
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

                #region Aktif Çalışmayı Zorunlu Bitirme

                if (isTerminalStatus)
                {
                    var forceFinishResult = await ForceFinishActiveWorkingByRequestNoAsync(
                        dto.RequestNo,
                        dto.WorkFlowStatus == WorkFlowStatus.Complated
                            ? "Akış tamamlandığı için çalışma zorunlu olarak bitirildi."
                            : "Akış iptal edildiği için çalışma zorunlu olarak bitirildi.");

                    if (!forceFinishResult.Success)
                    {
                        return ResponseModel<YkbFinalApprovalGetDto>.Fail(
                            forceFinishResult.ErrorMessage!,
                            StatusCode.Error);
                    }
                }

                #endregion

                #region Aeşivleme
                // 🔹 Eğer süreç tamamlandıysa arşive at
                if (isTerminalStatus)
                {
                    var reason = dto.WorkFlowStatus == WorkFlowStatus.Complated ? "Tamamlandı" : "İptal";
                    await ArchiveWorkflowAsync(dto.RequestNo, reason);
                }
                #endregion 

                await _uow.Repository.CompleteAsync();

                #region Dosya Silme
                attachmentsCommitted = true;
                if (attachmentChangeSet is not null)
                {
                    // Eski dosyalar ancak DB commit başarılı olduktan sonra silinir.
                    DeleteWorkflowAttachmentPhysicalFiles(attachmentChangeSet.OldStoredFileNames);
                }
                #endregion 

                #region Ürün Fiyat Sabitleme (5. Adım)  
                ///MZK Not: Yeni eklenen ürünlerin işlenmesi için CompleteAsync() sonrasına alındı
                await EnsurePricesCapturedFromDtoAsync(dto.RequestNo, dto.Products);
                #endregion


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
                return ResponseModel<YkbFinalApprovalGetDto>.Fail($"  Kontrol ve Son Onay sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }


        // 6 Müşteri Onayı
        public async Task<ResponseModel<YkbFinalApprovalGetDto>> CustomerAgreementAsync(YkbCustomerAgreementDto dto)
        {
            try
            {
                #region Validasyonlar

                var wf = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.CurrentStep)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.CurrentStep?.Code != "CAPR")
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("Bu işlem sadece YKB müşteri onay adımında yapılabilir.", StatusCode.BadRequest);

                var request = await _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .Include(x => x.Customer)
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                var finalApproval = await _uow.Repository
                    .GetQueryable<YkbFinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (finalApproval is null)
                    return ResponseModel<YkbFinalApprovalGetDto>.Fail("FinalApproval kaydı bulunamadı.", StatusCode.NotFound);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                #endregion

                if (dto.IsAgreed)
                {
                    // 🔹 Mutabık Kalındı: akış tamamlanır
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

                    await _activationRecord.LogYkbAsync(
                        WorkFlowActionType.FinalApprovalUpdated,
                        dto.RequestNo,
                        wf.Id,
                        request.CustomerId,
                        fromStepCode: "CAPR",
                        toStepCode: "APR",
                        "YKB tarafından Mutabık Kalındı ve süreç tamamlandı.",
                        new { dto.CustomerNote }
                    );

                    await ArchiveWorkflowAsync(dto.RequestNo, "Completed");

                    await _notification.CreateForRolesAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} akış tamamlandı",
                            Message = $"YKB son onayı alındı. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
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
                return ResponseModel<YkbFinalApprovalGetDto>.Fail($"YKB müşteri onayı sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }


        //Lokasyon Kontrolü  Ezme Maili 
        public async Task<ResponseModel> RequestLocationOverrideAsync(YkbOverrideLocationCheckDto dto)
        {
            // 1) Talep & WorkFlow & Customer & TechnicalService kontrolleri
            var request = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var wf = await _uow.Repository
                .GetQueryable<YkbWorkFlow>()
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
                .GetQueryable<YkbTechnicalService>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            if (technicalService.IsLocationCheckRequired == false)
                return ResponseModel.Success("Lokasyon kontrolü zaten devre dışı bırakılmış.");

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

            // 3) Mesafeyi güvenli hesapla (virgül/nokta normalize)
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

            // 4) Link parçaları (sadece varsa üret)
            var customerLink = hasCustomerLoc
                ? $"<a href=\"{mapsLinkCustomer}\">Google Maps</a>"
                : string.Empty;

            var technicianLink = hasTechnicianLoc
                ? $"<a href=\"{mapsLinkTechnician}\">Google Maps</a>"
                : string.Empty;

            var viewLink = baseUrl is not null
                ? $"<p><a href=\"{baseUrl}/technical-service/{dto.RequestNo}\">Kaydı görüntüle</a></p>"
                : string.Empty;

            // 5) Konum satırlarını koşullu yaz
            string customerLocRow = hasCustomerLoc
                ? $@"<p><b>Müşteri Konumu:</b> {custLat}, {custLon} {customerLink}</p>"
                : @"<p><b>Müşteri Konumu:</b> <span style=""color:#b00"">Kayıtlı değil / bulunamadı</span></p>";

            string technicianLocRow = hasTechnicianLoc
                ? $@"<p><b>Teknisyen Konumu:</b> {techLat}, {techLon} {technicianLink}</p>"
                : @"<p><b>Teknisyen Konumu:</b> <span style=""color:#b00"">Kayıtlı değil / bulunamadı</span></p>";

            // 6) Mail HTML
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

            // 7) Mail alıcıları
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

            // 8) Mail outbox’a yaz
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

            // 9) Lokasyon kontrolünü kapat ve kaydet
            technicalService.IsLocationCheckRequired = false;
            technicalService.UpdatedDate = DateTime.Now;
            technicalService.UpdatedUser = techUserId;
            _uow.Repository.Update(technicalService);

            await _uow.Repository.CompleteAsync();

            // 10) Sonuç
            return ResponseModel.Success("Lokasyon kontrolü devre dışı bırakma talebi iletildi ve ilgili yöneticilere e-posta gönderildi.");
        }

        ///----------------------------- 

        //--------------------- Customer Form ----------------------------
        public async Task<ResponseModel<YkbCustomerFormGetDto>> GetCustomerFormByRequestNoAsync(string requestNo)
        {
            // 1) Ana kayıt sadece YkbCustomerForm üzerinden alınır.
            var baseDto = await _uow.Repository
                .GetQueryable<YkbCustomerForm>()
                .AsNoTracking()
                .Where(sr => sr.RequestNo == requestNo)
                .Select(sr => new YkbCustomerFormGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    YkbServiceTrackNo = sr.YkbServiceTrackNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    Description = sr.Description,

                    Title = null,

                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerId = sr.CustomerId,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,

                    // YkbCustomerForm içinde bu iki kolon yok.
                    // Aşağıda YkbServicesRequest'ten doldurulacak.
                    ServiceTypeId = null,

                    Priority = sr.Priority,

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
                })
                .FirstOrDefaultAsync();

            if (baseDto is null)
            {
                return ResponseModel<YkbCustomerFormGetDto>.Fail(
                    "Kayıt bulunamadı.",
                    StatusCode.NotFound
                );
            }

            // 2) Workflow bilgisi ayrı sorgu
            var latestWorkflow = await _uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.RequestNo == requestNo)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new
                {
                    x.RequestTitle,
                    x.Priority
                })
                .FirstOrDefaultAsync();

            if (latestWorkflow != null)
            {
                baseDto.Title = latestWorkflow.RequestTitle;
                baseDto.Priority = latestWorkflow.Priority;
            }

            // 3) ServicesCostStatus ve ServiceTypeId sadece YkbServicesRequest'ten gelir
            var latestServicesRequest = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted && x.RequestNo == requestNo)
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new
                {
                    x.ServicesCostStatus,
                    x.ServiceTypeId
                })
                .FirstOrDefaultAsync();

            if (latestServicesRequest != null)
            {
                baseDto.ServicesCostStatus = latestServicesRequest.ServicesCostStatus;
                baseDto.ServiceTypeId = latestServicesRequest.ServiceTypeId;
            }

            // 4) CustomerGroup + ProgressApprovers
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

            // 5) Ürünler
            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product != null ? p.Product.PriceCurrency : null,

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

            // 6) Review logs
            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(
                    x => x.RequestNo == requestNo &&
                         (x.FromStepCode == "SR" || x.ToStepCode == "SR")
                )
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new YkbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    YkbWorkFlowId = x.YkbWorkFlowId,
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

            // 7) İş Emri Türleri (GetCustomerFormByRequestNoAsync)
            baseDto.WorkOrderTypes = await _uow.Repository
                .GetQueryable<YkbServicesRequestWorkOrderType>()
                .AsNoTracking()
                .Where(x => x.YkbServicesRequest.RequestNo == requestNo)
                .OrderBy(x => x.WorkOrderType.Name)
                .Select(x => new WorkOrderTypeGetDto
                {
                    Id = x.WorkOrderTypeId,
                    Name = x.WorkOrderType.Name,
                    Code = x.WorkOrderType.Code
                })
                .ToListAsync();

            baseDto.WorkOrderTypeIds = baseDto.WorkOrderTypes.Select(x => x.Id).ToList();

            return ResponseModel<YkbCustomerFormGetDto>.Success(baseDto);
        }
        // -------------------- Services Request --------------------
        private static Func<IQueryable<YkbServicesRequest>, IIncludableQueryable<YkbServicesRequest, object>>? RequestIncludes()
            => q => q
                .Include(x => x.Customer).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(x => x.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.ServiceType)
                .Include(x => x.CustomerApprover)
                .Include(x => x.CustomerApprover)
                .Include(x => x.YkbWorkFlowStep);
        public async Task<ResponseModel<PagedResult<YkbServicesRequestGetDto>>> GetRequestsAsync(QueryParams q)
        {
            var me = await _currentUser.GetAsync();
            if (me is null)
                return ResponseModel<PagedResult<YkbServicesRequestGetDto>>.Fail("Kullanıcı bulunamadı.", StatusCode.Unauthorized);

            var page = q.Page <= 0 ? 1 : q.Page;
            var pageSize = q.PageSize <= 0 ? 20 : q.PageSize;

            // ✅ Permission step codes (WH, PRC, TS, ...)
            var permittedSteps = await GetUserStepsByMenuPermission(me.Id) ?? new List<string>();
            var permittedSet = permittedSteps.ToHashSet(StringComparer.OrdinalIgnoreCase);

            //✅“Teknisyen” rolüne sahip ise sadece kendi üzerindeki ve Teknik Servis adımındaki  akışları görebilir
            // Çoklu rol kodu desteği
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

            // 🧱 1) Role/permission’a göre filtrelenmiş WorkFlow sorgusu
            IQueryable<YkbWorkFlow> wfBase = _uow.Repository.GetQueryable<YkbWorkFlow>()
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

            // Bu kullanıcının görebileceği RequestNo’lar
            var allowedRequestNos = wfBase.Select(x => x.RequestNo);

            // 🧱 2) ServicesRequest base query + include'lar
            var query = _uow.Repository.GetQueryable<YkbServicesRequest>();
            query = RequestIncludes()!(query);

            // WorkFlow ilişkisine göre filtre (IN (subquery))
            query = query.Where(sr => allowedRequestNos.Contains(sr.RequestNo));

            // 🔍 Search filtresi
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
                .ProjectToType<YkbServicesRequestGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<YkbServicesRequestGetDto>>.Success(
                new PagedResult<YkbServicesRequestGetDto>(items, total, page, pageSize)
            );
        }
        public async Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id)
        {
            var now = DateTimeOffset.Now;

            // 1) Ana DTO: SR + (WF last) + Customer (warranty türetmeleri)
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking()
                where sr.Id == id

                // left join: aynı RequestNo’ya sahip ve silinmemiş workflow’lar
                join wf0 in _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)  // “en güncel” workflow tercih ediliyorsa
                    .Take(1)
                    .DefaultIfEmpty()
                select new YkbServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.YkbServiceTrackNo,
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
                    WorkFlowStepName = sr.YkbWorkFlowStep != null ? sr.YkbWorkFlowStep.Name : null,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,
                    ApproverTechnicianId = wf != null ? wf.ApproverTechnicianId : null,
                    IsLocationValid = wf != null && wf.IsLocationValid,
                    Priority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    ServicesRequestStatus = sr.ServicesRequestStatus,

                    // 🔹 Customer alt DTO + warranty türetmeleri
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

                             // Ekranda göstermek için:
                             SystemName = a.CustomerSystem.Name,
                             SystemCode = a.CustomerSystem.Code,

                             // İstersen müşteri bilgilerini de doldurabiliriz:
                             CustomerName = a.Customer.SubscriberCompany,
                             CustomerShortCode = a.Customer.CustomerShortCode
                         })
                      .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<YkbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Ürünler (tek bağımsız sorgu — Tenant fiyatı eklendi)
            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == baseDto.RequestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,

                    Quantity = p.Quantity,

                    // 🆕 EF-translatable EffectivePrice (Tenant eklendi)
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
                            .FirstOrDefault() // 🆕 Tenant fiyatı
                        ?? (decimal?)p.Product.Price
                        ?? 0m
                })
                .ToListAsync();

            // 3) Review logs (tek bağımsız sorgu — SR adımıyla sınırlı)
            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x => x.RequestNo == baseDto.RequestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new YkbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    YkbWorkFlowId = x.YkbWorkFlowId,
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

            // 4) İş Emri Türleri (GetServiceRequestByIdAsync)
            baseDto.WorkOrderTypes = await _uow.Repository
                .GetQueryable<YkbServicesRequestWorkOrderType>()
                .AsNoTracking()
                .Where(x => x.YkbServicesRequest.RequestNo == baseDto.RequestNo)
                .OrderBy(x => x.WorkOrderType.Name)
                .Select(x => new WorkOrderTypeGetDto
                {
                    Id = x.WorkOrderTypeId,
                    Name = x.WorkOrderType.Name,
                    Code = x.WorkOrderType.Code
                })
                .ToListAsync();

            baseDto.WorkOrderTypeIds = baseDto.WorkOrderTypes.Select(x => x.Id).ToList();

            return ResponseModel<YkbServicesRequestGetDto>.Success(baseDto);
        }
        public async Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo)
        {
            var now = DateTimeOffset.Now;

            // 1) Ana DTO: SR + (WF last) + Customer (warranty türetmeleri)
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking()
                where sr.RequestNo == requestNo

                // left join: aynı RequestNo’ya sahip ve silinmemiş workflow’lar
                join wf0 in _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)  // “en güncel” workflow tercih ediliyorsa
                    .Take(1)
                    .DefaultIfEmpty()
                select new YkbServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.YkbServiceTrackNo,
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
                    WorkFlowStepName = sr.YkbWorkFlowStep != null ? sr.YkbWorkFlowStep.Name : null,
                    CreatedDate = sr.CreatedDate,
                    UpdatedDate = sr.UpdatedDate,
                    CreatedUser = sr.CreatedUser,
                    UpdatedUser = sr.UpdatedUser,
                    IsDeleted = sr.IsDeleted,
                    ApproverTechnicianId = wf != null ? wf.ApproverTechnicianId : null,
                    IsLocationValid = wf != null && wf.IsLocationValid,
                    Priority = wf != null ? wf.Priority : WorkFlowPriority.Normal,
                    ServicesRequestStatus = sr.ServicesRequestStatus,

                    // 🔹 Customer alt DTO + warranty türetmeleri
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

                             // Ekranda göstermek için:
                             SystemName = a.CustomerSystem.Name,
                             SystemCode = a.CustomerSystem.Code,

                             // İstersen müşteri bilgilerini de doldurabiliriz:
                             CustomerName = a.Customer.SubscriberCompany,
                             CustomerShortCode = a.Customer.CustomerShortCode
                         })
                      .ToList()
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<YkbServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);


            // NEW: CustomerGroup + ProgressApprovers (tek ek sorgu)
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
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,

                    Quantity = p.Quantity,

                    // 🆕 EF-translatable EffectivePrice (Tenant eklendi)
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
                            .FirstOrDefault() // 🆕 Tenant fiyatı
                        ?? (decimal?)p.Product.Price
                        ?? 0m
                })
                .ToListAsync();

            // 3) Review logs (tek bağımsız sorgu — SR adımıyla sınırlı)
            baseDto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x => x.RequestNo == requestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new YkbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    YkbWorkFlowId = x.YkbWorkFlowId,
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

            // 4) İş Emri Türleri (GetServiceRequestByRequestNoAsync)
            baseDto.WorkOrderTypes = await _uow.Repository
                .GetQueryable<YkbServicesRequestWorkOrderType>()
                .AsNoTracking()
                .Where(x => x.YkbServicesRequest.RequestNo == requestNo)
                .OrderBy(x => x.WorkOrderType.Name)
                .Select(x => new WorkOrderTypeGetDto
                {
                    Id = x.WorkOrderTypeId,
                    Name = x.WorkOrderType.Name,
                    Code = x.WorkOrderType.Code
                })
                .ToListAsync();

            baseDto.WorkOrderTypeIds = baseDto.WorkOrderTypes.Select(x => x.Id).ToList();

            return ResponseModel<YkbServicesRequestGetDto>.Success(baseDto);
        }
        public async Task<ResponseModel> DeleteRequestAsync(long id)
        {

            // 1) Entity’yi getir (tracked olsun ki güncelleme/replace çalışsın)
            var entity = await _uow.Repository.GetSingleAsync<YkbServicesRequest>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.IsDeleted = true;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa

            // 3) SoftDelete çağrısı -> 2 tip argümanı verin ve entity gönderin
            await _uow.Repository.SoftDeleteAsync<YkbServicesRequest, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        //-------------------------Akışı bir önceki adıma geri alma işlemi----------------------------
        public async Task<ResponseModel<YkbWorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes)
        {
            //WorkFlow'u (Akışı) Getir
            var wf = await _uow.Repository.GetQueryable<YkbWorkFlow>(x => x.RequestNo == requestNo)
                .FirstOrDefaultAsync();

            if (wf is null)
                return ResponseModel<YkbWorkFlowGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled || wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<YkbWorkFlowGetDto>.Fail("İptal edilmiş veya tamamlanmış akışlar geri alınamaz.", StatusCode.Conflict);

            var servicesRequest = await _uow.Repository
               .GetQueryable<YkbServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
            if (servicesRequest is null)
                return ResponseModel<YkbWorkFlowGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var currentStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                .AsNoTracking()
                .Select(s => new { s.Id, s.Code })
                .FirstOrDefaultAsync(s => s.Id == wf.CurrentStepId);

            if (currentStep is null)
                return ResponseModel<YkbWorkFlowGetDto>.Fail("Akışın mevcut adımı bulunamadı.", StatusCode.NotFound);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var targetStep = new YkbWorkFlowStep();
            var warehouse = new YkbWarehouse();
            var technicalService = new YkbTechnicalService();
            var pricing = new YkbPricing();
            // Mevcut Adım Koduna Göre Dinamik Güncelleme
            switch (currentStep.Code)
            {
                case "PRC": // Teknik Servis Adımı (TechnicalService)
                    pricing = await _uow.Repository
                       .GetQueryable<YkbPricing>()
                       .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (pricing != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                          .AsNoTracking()
                          .FirstOrDefaultAsync(s => s.Code == "TS");
                        if (targetStep is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                        technicalService = await _uow.Repository
                             .GetQueryable<YkbTechnicalService>()
                             .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                        if (technicalService is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı Teknik Servis tanımlı değil.", StatusCode.BadRequest);

                        technicalService.ServicesStatus = TechnicalServiceStatus.Pending;
                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = meId;

                        pricing.Status = PricingStatus.AwaitingReview;
                        pricing.UpdatedDate = DateTime.Now;
                        pricing.UpdatedUser = meId;
                        _uow.Repository.Update(technicalService);
                    }

                    break;

                case "TS": // Teknik Servis Adımı (TechnicalService)
                    technicalService = await _uow.Repository
                       .GetQueryable<YkbTechnicalService>()
                       .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (technicalService != null)
                    {
                        //Ürün var ise depoya geri gönder
                        //if (servicesRequest.IsProductRequirement)
                        //{
                        //    //Depo Adımına Geri
                        //    targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                        //       .AsNoTracking()
                        //       .FirstOrDefaultAsync(s => s.Code == "WH");
                        //    if (targetStep is null)
                        //        return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı adımı (WH) tanımlı değil.", StatusCode.BadRequest);

                        //    warehouse = await _uow.Repository
                        //   .GetQueryable<Warehouse>()
                        //   .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                        //    if (warehouse is null)
                        //        return ResponseModel<WorkFlowGetDto>.Fail("Depo Kaydı Bulunamadı.", StatusCode.BadRequest);

                        //    warehouse.WarehouseStatus = WarehouseStatus.Pending;
                        //    warehouse.UpdatedDate = DateTime.Now;
                        //    warehouse.UpdatedUser = meId;
                        //    _uow.Repository.Update(warehouse);
                        //}
                        ////Ürün yok ise direkt servis talebine geri gönder
                        //else
                        //{
                        targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                       .AsNoTracking()
                       .FirstOrDefaultAsync(s => s.Code == "SR");
                        if (targetStep is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                        servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;

                        servicesRequest.UpdatedDate = DateTime.Now;
                        servicesRequest.UpdatedUser = meId;
                        _uow.Repository.Update(servicesRequest);
                        //}

                        technicalService.ServicesStatus = TechnicalServiceStatus.AwaitingReview;

                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = meId;
                        _uow.Repository.Update(technicalService);
                    }

                    break;

                case "WH": // Depo Adımı (Warehouse)
                           // Depo adımında bir durum (status) alanı olmadığını varsayarak sadece IsSended bayrağını sıfırlayabiliriz
                    warehouse = await _uow.Repository
                        .GetQueryable<YkbWarehouse>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                    if (warehouse != null)
                    {

                        targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                         .AsNoTracking()
                         .FirstOrDefaultAsync(s => s.Code == "SR");
                        if (targetStep is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);


                        warehouse.WarehouseStatus = WarehouseStatus.AwaitingReview;
                        warehouse.UpdatedDate = DateTime.Now;
                        warehouse.UpdatedUser = meId;
                        servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;
                        servicesRequest.UpdatedDate = DateTime.Now;
                        servicesRequest.UpdatedUser = meId;
                        _uow.Repository.Update(servicesRequest);
                    }
                    break;

                case "SR": // Servis Talebi Adımı (ServicesRequest)
                    var serviceRequest = await _uow.Repository
                        .GetQueryable<YkbServicesRequest>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (serviceRequest != null)
                    {
                        serviceRequest.UpdatedDate = DateTime.Now;
                        serviceRequest.UpdatedUser = meId;
                        _uow.Repository.Update(serviceRequest);
                    }
                    break;

                case "CAPR": // Servis Talebi Adımı (ServicesRequest)
                    var customerForm = await _uow.Repository
                        .GetQueryable<YkbCustomerForm>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (customerForm != null)
                    {

                        targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Code == "APR");
                        if (targetStep is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı adımı (APR) tanımlı değil.", StatusCode.BadRequest);
                        customerForm.UpdatedDate = DateTime.Now;
                        customerForm.UpdatedUser = meId;
                        customerForm.Status = YkbCustomerFormStatus.AwaitingReview;
                        _uow.Repository.Update(customerForm);


                        var approval = await _uow.Repository.GetQueryable<YkbFinalApproval>().FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                        if (approval is null)
                            return ResponseModel<YkbWorkFlowGetDto>.Fail("Hedef iş akışı (APR) bulunamadı", StatusCode.BadRequest);

                        approval.Status = FinalApprovalStatus.Pending;
                        approval.UpdatedDate = DateTime.Now;
                        approval.UpdatedUser = meId;


                    }
                    break;


                default:
                    break;
            }
            if (targetStep.Code is null)
                return ResponseModel<YkbWorkFlowGetDto>.Fail("Herhangi bir işlem yapılamadı.", StatusCode.BadRequest);
            //Ana WorkFlow'u Yeni Adıma Güncelle
            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = meId;
            _uow.Repository.Update(wf);

            ///Aktivite Kaydı Yaz
            await _activationRecord.LogYkbAsync(
                WorkFlowActionType.WorkFlowStepChanged,
                requestNo,
                wf.Id,
                servicesRequest.CustomerId,
                currentStep.Code,
                targetStep.Code,
                "Akış geri gönderildi",
                new { reviewNotes, targetStep = targetStep.Name }
            );

            /// Gözden geçirme logu yaz
            var reviewLog = new YkbWorkFlowReviewLog
            {
                YkbWorkFlowId = wf.Id,
                RequestNo = requestNo,
                FromStepId = currentStep.Id,          // mevcut (eski) adım id
                FromStepCode = currentStep.Code,          // mevcut (eski) adım kodu
                ToStepId = targetStep.Id,             // hedef (yeni) adım id
                ToStepCode = targetStep.Code,           // hedef (yeni) adım kodu
                ReviewNotes = reviewNotes,
                CreatedUser = meId,
                CreatedDate = DateTime.Now
            };

            _uow.Repository.Add(reviewLog);

            /// Mail Gönderimi
            await PushTransitionMailsAsync(
                 wf, fromCode: currentStep.Code!, toCode: targetStep.Code!,
                 requestNo: requestNo,
                 customerName: servicesRequest.Customer?.ContactName1
            );

            ///Değişiklikleri Kaydet
            await _uow.Repository.CompleteAsync();


            #region Notification Kaydı
            // targetStep.Code'ye göre hedefi belirle
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


            // 1) Özel durum: TS → teknisyene bildir
            if (string.Equals(targetStep.Code, "TS", StringComparison.OrdinalIgnoreCase))
            {
                if (wf.ApproverTechnicianId.HasValue && wf.ApproverTechnicianId.Value > 0)
                {
                    dto.TargetUserIds = new List<long> { wf.ApproverTechnicianId.Value };
                    dto.TargetRoleCodes = null; // kullanıcıya gidiyor
                }
                else
                {
                    // güvenli fallback: teknisyen yoksa TS için rol at
                    dto.TargetUserIds = null;
                    dto.TargetRoleCodes = new List<string> { "SUBCONTRACTOR" };
                }
            }
            else
            {
                // 2) Diğer adımlar: adım kodu → rol kodu haritası
                var stepToRole = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["WH"] = "WAREHOUSE",
                    ["TS"] = "SUBCONTRACTOR" // ileride lazım olursa, else içinde de destekler
                                             // İstersen buraya PRC→"PRICING", SR→"SERVICE_REQUEST" vb. ekleyebilirsin.
                };

                if (stepToRole.TryGetValue(targetStep.Code ?? string.Empty, out var roleCode))
                {
                    dto.TargetUserIds = null;
                    dto.TargetRoleCodes = new List<string> { roleCode };
                }
                else
                {
                    // hiç eşleşme yoksa: istersen no-op yapabilir ya da loglayabilirsin
                    // dto.TargetRoleCodes = new List<string> { "DEFAULT_ROLE" };
                }
            }

            // Kayıt
            await _notification.CreateAsync(dto);
            #endregion

            /// Dönüş tipi WorkFlow GetDto olarak ayarlandı.
            return ResponseModel<YkbWorkFlowGetDto>.Success(
                wf.Adapt<YkbWorkFlowGetDto>(_config)
            );
        }

        // -------------------- Warehouse --------------------
        public async Task<ResponseModel<YkbWarehouseGetDto>> GetWarehouseByIdAsync(long id)
        {
            var qWarehouse = _uow.Repository.GetQueryable<YkbWarehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();
            var qUsers = _uow.Repository.GetQueryable<User>().AsNoTracking(); // <-- eklendi
            var qCreatedUsers = _uow.Repository.GetQueryable<User>().AsNoTracking(); // <-- eklendi
            // HEADER: Warehouse + (left) WorkFlow + (left) ServicesRequest (+ Customer) (+ User)
            var dto = await (
                from w in qWarehouse
                where w.Id == id

                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj
                    .OrderByDescending(x => x.CreatedDate)   // en güncel WF
                    .Take(1)
                    .DefaultIfEmpty()

                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()

                    //CreatedUser
                join cru in qCreatedUsers on sr.CreatedUser equals cru.Id into cruj
                from cu in cruj.DefaultIfEmpty()
                    // 🔹 ApproverTechnician (User) join
                join u0 in qUsers on wf.ApproverTechnicianId equals u0.Id into uj
                from u in uj.DefaultIfEmpty()

                select new YkbWarehouseGetDto
                {
                    Id = w.Id,
                    RequestNo = w.RequestNo,
                    DeliveryDate = w.DeliveryDate,
                    Description = w.Description,
                    WarehouseStatus = w.WarehouseStatus,

                    // WorkFlow
                    WorkFlowRequestTitle = wf != null ? wf.RequestTitle : null,
                    WorkFlowPriority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    // ServicesRequest
                    //ServicesRequestDescription = sr != null ? sr.Description : null,
                    ServicesRequest = sr == null
                          ? null
                          : new YkbServicesRequestGetDto
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
                              Priority = sr.Priority, // sr tarafında varsa
                              ServicesRequestStatus = sr.ServicesRequestStatus,
                          },

                    // Customer
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null,

                    //Created Users
                    CreatedUser =
                    cu == null
                          ? null
                          : new UserGetDto
                          {
                              Id = cu.Id,
                              TechnicianCode = cu.TechnicianCode,          // örn. "TEK-001"
                              TechnicianCompany = cu.TechnicianCompany,       // varsa şirket/kurum adı
                              TechnicianAddress = cu.TechnicianAddress,       // adres
                              City = cu.City,
                              District = cu.District,
                              TechnicianName = cu.TechnicianName,          // ya da u.FullName kullanıyorsan buraya koy
                              TechnicianPhone = cu.TechnicianPhone,         // tel
                              TechnicianEmail = cu.TechnicianEmail,         // e-posta
                              IsActive = cu.IsActive,
                          },

                    // 🔹 User (WorkFlow.ApproverTechnician)
                    User = u == null
                          ? null
                          : new UserGetDto
                          {
                              Id = u.Id,
                              TechnicianCode = u.TechnicianCode,          // örn. "TEK-001"
                              TechnicianCompany = u.TechnicianCompany,       // varsa şirket/kurum adı
                              TechnicianAddress = u.TechnicianAddress,       // adres
                              City = u.City,
                              District = u.District,
                              TechnicianName = u.TechnicianName,          // ya da u.FullName kullanıyorsan buraya koy
                              TechnicianPhone = u.TechnicianPhone,         // tel
                              TechnicianEmail = u.TechnicianEmail,         // e-posta
                              IsActive = u.IsActive,

                              // Roller (Include gerektirmez; alt-sorgu olarak çevrilir)
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
                return ResponseModel<YkbWarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: depo aşamasında fiyat yok
            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null
                    // Fiyat alanları (ProductPrice/EffectivePrice/PriceCurrency) depoda gösterilmiyor
                })
                .ToListAsync();

            // REVIEW LOG’LARI (Warehouse adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new YkbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    YkbWorkFlowId = x.YkbWorkFlowId,
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

            return ResponseModel<YkbWarehouseGetDto>.Success(dto);
        }
        public async Task<ResponseModel<YkbWarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo)
        {
            var qWarehouse = _uow.Repository.GetQueryable<YkbWarehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();
            var qUsers = _uow.Repository.GetQueryable<User>().AsNoTracking();

            // HEADER: Warehouse + (left) WorkFlow + (left) ServicesRequest (+ Customer) (+ User)
            var dto = await (
                from w in qWarehouse
                where w.RequestNo == requestNo

                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj
                    .OrderByDescending(x => x.CreatedDate)   // en güncel WF
                    .Take(1)
                    .DefaultIfEmpty()

                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()

                    // 🔹 ApproverTechnician (User) join
                join u0 in qUsers on wf.ApproverTechnicianId equals u0.Id into uj
                from u in uj.DefaultIfEmpty()

                select new YkbWarehouseGetDto
                {
                    Id = w.Id,
                    RequestNo = w.RequestNo,
                    DeliveryDate = w.DeliveryDate,
                    Description = w.Description,
                    WarehouseStatus = w.WarehouseStatus,

                    // WorkFlow
                    WorkFlowRequestTitle = wf != null ? wf.RequestTitle : null,
                    WorkFlowPriority = wf != null ? wf.Priority : WorkFlowPriority.Normal,

                    // ServicesRequest
                    //ServicesRequestDescription = sr != null ? sr.Description : null,
                    ServicesRequest = sr == null
                          ? null
                          : new YkbServicesRequestGetDto
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
                              Priority = sr.Priority, // sr tarafında varsa
                              ServicesRequestStatus = sr.ServicesRequestStatus,
                          },

                    // Customer
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null,

                    // 🔹 User (WorkFlow.ApproverTechnician)
                    User = u == null
                          ? null
                          : new UserGetDto
                          {
                              Id = u.Id,
                              TechnicianCode = u.TechnicianCode,          // örn. "TEK-001"
                              TechnicianCompany = u.TechnicianCompany,       // varsa şirket/kurum adı
                              TechnicianAddress = u.TechnicianAddress,       // adres
                              City = u.City,
                              District = u.District,
                              TechnicianName = u.TechnicianName,          // ya da u.FullName kullanıyorsan buraya koy
                              TechnicianPhone = u.TechnicianPhone,         // tel
                              TechnicianEmail = u.TechnicianEmail,         // e-posta
                              IsActive = u.IsActive,

                              // Roller (Include gerektirmez; alt-sorgu olarak çevrilir)
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
                return ResponseModel<YkbWarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: depo aşamasında fiyat yok
            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null
                    // Fiyat alanları (ProductPrice/EffectivePrice/PriceCurrency) depoda gösterilmiyor
                })
                .ToListAsync();

            // REVIEW LOG’LARI (Warehouse adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new YkbWorkFlowReviewLogDto
                {
                    Id = x.Id,
                    YkbWorkFlowId = x.YkbWorkFlowId,
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

            return ResponseModel<YkbWarehouseGetDto>.Success(dto);
        }


        // -------------------- Teknical Services --------------------
        public async Task<ResponseModel<YkbTechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<YkbTechnicalService>();

            // HEADER (mevcut mapster config'ine göre)
            var entity = await query
                     .AsNoTracking()
                     .Where(x => x.RequestNo == requestNo)
                     .AsSplitQuery()
                     .Include(x => x.YkbServiceRequestFormImages)
                     .Include(x => x.YkbServicesImages)
                     .Include(x => x.ServiceType)
                     .FirstOrDefaultAsync();

            if (entity is null)
            {
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail(
                    "Kayıt bulunamadı.",
                    StatusCode.NotFound);
            }

            var dto = entity.Adapt<YkbTechnicalServiceGetDto>(_config);

            if (dto is null)
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.ServiceRequestFormImages = entity.YkbServiceRequestFormImages
                .Select(img => img.Adapt<YkbTechnicalServiceFormImageGetDto>(_config))
                .ToList();
            dto.ServicesImages = entity.YkbServicesImages
                .Select(img => img.Adapt<YkbTechnicalServiceImageGetDto>(_config))
                .ToList();

            // --- Customer: ServicesRequest üzerinden tek sorguda projeksiyon ---
            dto.Customer = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .AsNoTracking()
                .Where(sr => sr.RequestNo == requestNo && sr.Customer != null)
                .Include(sr => sr.Customer).ThenInclude(c => c.Tenant)
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
                    TenantId = sr.Customer.TenantId,
                    IsTechnicalServiceTestEnabled = sr.Customer.Tenant.IsTechnicalServiceTestEnabled,
                    SerialNo = sr.Customer.SerialNo,
                    Systems = sr.Customer.CustomerSystemAssignments
                                 .Select(a => new CustomerSystemAssignmentGetDto
                                 {
                                     Id = a.Id,
                                     CustomerId = a.CustomerId,
                                     CustomerSystemId = a.CustomerSystemId,
                                     HasMaintenanceContract = a.HasMaintenanceContract,

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                })
                .FirstOrDefaultAsync();

            // ÜRÜNLER: teknisyen fiyat görmeyecek → price alanlarını projekte etmiyoruz
            dto.Products = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,

                    // ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,

                    // 🔹 Para birimi: sabitlenmiş (Captured) varsa onu kullan
                    PriceCurrency = p.CapturedCurrency
                        ?? (p.Product != null ? p.Product.PriceCurrency : null),

                    // 🔹 Ürün fiyatı: sabitlenmiş birim fiyat
                    // (Frontend'de ProductPrice kullanıyorsan burada CapturedUnitPrice'ı döndürmek mantıklı)
                    ProductPrice = p.CapturedUnitPrice
                       ?? (p.Product != null ? (decimal?)p.Product.Price : null)
                       ?? 0m,

                    // 🔹 EffectivePrice: artık runtime hesap yok,
                    // sabitlenmiş birim fiyat = ekranda görünen "esas fiyat"
                    EffectivePrice = p.CapturedUnitPrice
                         ?? 0m,
                })
                .ToListAsync();

            // GÖZDEN GEÇİR (TS adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "TS" || x.ToStepCode == "TS"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<YkbWorkFlowReviewLogDto>(_config)
                .ToListAsync();



            // --------------------------------------------------------------------
            //  🔹 IMAGE URL NORMALİZASYONU (FileUrl bazlı)
            // --------------------------------------------------------------------
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

            // Service resimleri
            if (dto.ServicesImages != null)
            {
                foreach (var img in dto.ServicesImages)
                {
                    img.Url = NormalizeImageUrl(img.Url);
                }
            }

            // Form resimleri
            if (dto.ServiceRequestFormImages != null)
            {
                foreach (var img in dto.ServiceRequestFormImages)
                {
                    img.Url = NormalizeImageUrl(img.Url);
                }
            }
            // --------------------------------------------------------------------

            // İş Emri Türleri (GetTechnicalServiceByRequestNoAsync)
            dto.WorkOrderTypes = await _uow.Repository
                .GetQueryable<YkbServicesRequestWorkOrderType>()
                .AsNoTracking()
                .Where(x => x.YkbServicesRequest.RequestNo == requestNo)
                .OrderBy(x => x.WorkOrderType.Name)
                .Select(x => new WorkOrderTypeGetDto
                {
                    Id = x.WorkOrderTypeId,
                    Name = x.WorkOrderType.Name,
                    Code = x.WorkOrderType.Code
                })
                .ToListAsync();

            dto.WorkOrderTypeIds = dto.WorkOrderTypes.Select(x => x.Id).ToList();


            // Servis başlığı WorkFlow.RequestTitle'dan,
            // servis açıklaması ServicesRequest.Description alanından alınır.
            var serviceHeader = await (
                from sr in _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .AsNoTracking()
                join wf in _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .AsNoTracking()

                    on sr.RequestNo equals wf.RequestNo into workflowJoin

                from wf in workflowJoin.DefaultIfEmpty()

                where sr.RequestNo == dto.RequestNo

                select new
                {
                    ServiceTitle = wf != null ? wf.RequestTitle : null,
                    ServiceDescription = sr.Description
                }
            ).FirstOrDefaultAsync();

            dto.ServiceTitle = serviceHeader?.ServiceTitle ?? string.Empty;
            dto.ServiceDescription = serviceHeader?.ServiceDescription ?? string.Empty;

            return ResponseModel<YkbTechnicalServiceGetDto>.Success(dto);
        }
        /// ------------------ Pricing -----------------------------------
        public async Task<ResponseModel<YkbPricingGetDto>> GetPricingByRequestNoAsync(string requestNo)
        {
            var qPricing = _uow.Repository.GetQueryable<YkbPricing>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();

            // HEADER: Pricing (zorunlu) + ServicesRequest (left) + Customer (projection)
            var dto = await (
                from pr in qPricing
                where pr.RequestNo == requestNo
                join sr0 in qRequest on pr.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new YkbPricingGetDto
                {
                    // Pricing
                    Id = pr.Id,
                    RequestNo = pr.RequestNo,
                    Status = pr.Status,
                    Currency = pr.Currency,
                    Notes = pr.Notes,
                    TotalAmount = pr.TotalAmount,

                    // Audit (Pricing)
                    CreatedDate = pr.CreatedDate,
                    CreatedUser = pr.CreatedUser,
                    UpdatedDate = pr.UpdatedDate,
                    UpdatedUser = pr.UpdatedUser,

                    // ServicesRequest
                    OracleNo = sr != null ? sr.YkbServiceTrackNo : null,
                    ServicesCostStatus = sr != null ? sr.ServicesCostStatus : ServicesCostStatus.Unknown,

                    // Customer (yalnızca gerekli alanlar + WarrantyYears)
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbPricingGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır
            var productEntities = await _uow.Repository
                        .GetQueryable<YkbServicesRequestProduct>()
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
                    var captured = p.IsPriceCaptured;

                    var effectivePrice = captured
                        ? p.CapturedUnitPrice ?? 0m
                        : p.GetEffectivePrice();

                    var currency = captured
                        ? p.CapturedCurrency ?? p.Product?.PriceCurrency ?? "TRY"
                        : p.Product?.PriceCurrency ?? "TRY";

                    var totalPrice = captured
                        ? p.CapturedTotal ?? effectivePrice * p.Quantity
                        : effectivePrice * p.Quantity;

                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        CustomerId = p.CustomerId ?? 0,
                        CustomerName = p.Customer?.SubscriberCompany,
                        Quantity = p.Quantity,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,

                        PriceCurrency = currency,

                        ProductPrice = effectivePrice,
                        EffectivePrice = effectivePrice,
                        TotalPrice = totalPrice,

                        IsServiceFeeProduct = p.Product?.IsServiceFeeProduct,

                        ServiceFeePercentage = p.Product?.ServiceFeePercentage,

                        IsPriceCaptured = p.IsPriceCaptured,
                        CapturedUnitPrice = p.CapturedUnitPrice,
                        CapturedCurrency = p.CapturedCurrency,
                        CapturedTotal = p.CapturedTotal
                    };
                })
                .ToList();


            // REVIEW LOG’LARI (Pricing adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "PRC" || x.ToStepCode == "PRC"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<YkbWorkFlowReviewLogDto>(_config)
                .ToListAsync();


            //Dosyalar: Attachments (Pricing adımı)
            dto.Attachments = await GetWorkflowAttachmentsAsync(dto.RequestNo);
            dto.CanEditAttachments = true;
            return ResponseModel<YkbPricingGetDto>.Success(dto);
        }

        //----------------------FinalApproval ---------------------------------------------------

        public async Task<ResponseModel<YkbFinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo)
        {
            var qFinal = _uow.Repository.GetQueryable<YkbFinalApproval>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();

            // HEADER: FinalApproval + (left) ServicesRequest -> Customer
            var dto = await (
                from fa in qFinal
                where fa.RequestNo == requestNo
                join sr0 in qRequest on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new YkbFinalApprovalGetDto
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);


            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır (Tenant eklendi)
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)                           // 🆕
                    .ThenInclude(c => c.Tenant)                     // 🆕
                        .ThenInclude(t => t.TenantProductPrices)    // 🆕
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
                        : p.GetEffectivePrice(); // 🆕 Tenant dahil hesaplar

                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        IsServiceFeeProduct = p.Product?.IsServiceFeeProduct,
                        ServiceFeePercentage = p.Product?.ServiceFeePercentage,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,
                        PriceCurrency = currency,
                        ProductPrice = effectivePrice,
                        EffectivePrice = effectivePrice,
                        TotalPrice = effectivePrice * p.Quantity
                    };
                })
                .ToList();


            // REVIEW LOG’ları (APR adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<YkbWorkFlowReviewLogDto>(_config)
                .ToListAsync();

            // RESİMLER: TechnicalService üzerinden form ve service resimlerini çek
            var qTechnicalService = _uow.Repository.GetQueryable<YkbTechnicalService>().AsNoTracking();
            var techService = await qTechnicalService
                .Where(ts => ts.RequestNo == dto.RequestNo)
                .Include(ts => ts.YkbServiceRequestFormImages)
                .Include(ts => ts.YkbServicesImages)
                .FirstOrDefaultAsync();

            // --------------------------------------------------------------------
            //  🔹 IMAGE URL NORMALİZASYONU (FileUrl bazlı)
            // --------------------------------------------------------------------
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

            if (techService != null)
            {
                // Service resimleri
                if (techService.YkbServicesImages != null && techService.YkbServicesImages.Any())
                {
                    dto.ServicesImages = techService.YkbServicesImages
                        .Select(img => new YkbTechnicalServiceImageGetDto
                        {
                            Id = img.Id,
                            YkbTechnicalServiceId = img.YkbTechnicalServiceId,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }

                // Form resimleri
                if (techService.YkbServiceRequestFormImages != null && techService.YkbServiceRequestFormImages.Any())
                {
                    dto.ServiceRequestFormImages = techService.YkbServiceRequestFormImages
                        .Select(img => new YkbTechnicalServiceFormImageGetDto
                        {
                            Id = img.Id,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }
            }
            // --------------------------------------------------------------------


            // Ekli Dosyalar
            dto.Attachments = await GetWorkflowAttachmentsAsync(dto.RequestNo);
            dto.CanEditAttachments = true;

            return ResponseModel<YkbFinalApprovalGetDto>.Success(dto);
        }
        public async Task<ResponseModel<YkbFinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id)
        {
            var qFinal = _uow.Repository.GetQueryable<YkbFinalApproval>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();

            // HEADER: FinalApproval + (left) ServicesRequest -> Customer
            var dto = await (
                from fa in qFinal
                where fa.Id == id
                join sr0 in qRequest on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new YkbFinalApprovalGetDto
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır (Tenant eklendi)
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)                           // 🆕
                    .ThenInclude(c => c.Tenant)                     // 🆕
                        .ThenInclude(t => t.TenantProductPrices)    // 🆕
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
                        : p.GetEffectivePrice(); // 🆕 Tenant dahil hesaplar

                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        IsServiceFeeProduct = p.Product?.IsServiceFeeProduct,
                        ServiceFeePercentage = p.Product?.ServiceFeePercentage,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,
                        PriceCurrency = currency,
                        ProductPrice = effectivePrice,
                        EffectivePrice = effectivePrice,
                        TotalPrice = effectivePrice * p.Quantity
                    };
                })
                .ToList();

            // REVIEW LOG’ları (APR adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<YkbWorkFlowReviewLogDto>(_config)
                .ToListAsync();



            // RESİMLER: TechnicalService üzerinden form ve service resimlerini çek
            var qTechnicalService = _uow.Repository.GetQueryable<YkbTechnicalService>().AsNoTracking();
            var techService = await qTechnicalService
                .Where(ts => ts.RequestNo == dto.RequestNo)
                .Include(ts => ts.YkbServiceRequestFormImages)
                .Include(ts => ts.YkbServicesImages)
                .FirstOrDefaultAsync();

            // --------------------------------------------------------------------
            //  🔹 IMAGE URL NORMALİZASYONU (FileUrl bazlı)
            // --------------------------------------------------------------------
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

            if (techService != null)
            {
                // Service resimleri
                if (techService.YkbServicesImages != null && techService.YkbServicesImages.Any())
                {
                    dto.ServicesImages = techService.YkbServicesImages
                        .Select(img => new YkbTechnicalServiceImageGetDto
                        {
                            Id = img.Id,
                            YkbTechnicalServiceId = img.YkbTechnicalServiceId,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }

                // Form resimleri
                if (techService.YkbServiceRequestFormImages != null && techService.YkbServiceRequestFormImages.Any())
                {
                    dto.ServiceRequestFormImages = techService.YkbServiceRequestFormImages
                        .Select(img => new YkbTechnicalServiceFormImageGetDto
                        {
                            Id = img.Id,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }
            }
            // --------------------------------------------------------------------

            // Ekli Dosyalar
            dto.Attachments = await GetWorkflowAttachmentsAsync(dto.RequestNo);
            dto.CanEditAttachments = true;
            return ResponseModel<YkbFinalApprovalGetDto>.Success(dto);
        }

        public async Task<ResponseModel> SendReviewMessage(YkbCustomerReviewMessageDto dto)
        {
            try
            {
                // 0) Basit validasyonlar
                if (dto is null)
                    return ResponseModel.Fail("Gönderilen veri boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                    return ResponseModel.Fail("Talep numarası boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.FromStepCode) || string.IsNullOrWhiteSpace(dto.ToStepCode))
                    return ResponseModel.Fail("Kaynak ve hedef adım kodları boş olamaz.", StatusCode.BadRequest);

                if (string.IsNullOrWhiteSpace(dto.Message))
                    return ResponseModel.Fail("Gönderilecek mesaj boş olamaz.", StatusCode.BadRequest);

                // 1) İlgili workflow’u bul
                var wf = await _uow.Repository.GetQueryable<YkbWorkFlow>()
                    .Where(x => !x.IsDeleted && x.RequestNo == dto.RequestNo)
                    .FirstOrDefaultAsync();

                if (wf is null)
                    return ResponseModel.Fail("İlgili akış bulunamadı.", StatusCode.Conflict);

                // 2) Adımları bul
                var fromStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .FirstOrDefaultAsync(x => x.Code == dto.FromStepCode);

                var toStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .FirstOrDefaultAsync(x => x.Code == dto.ToStepCode);

                if (fromStep is null || toStep is null)
                    return ResponseModel.Fail("Hedef adım veya kaynak adım bulunamadı.", StatusCode.Conflict);

                // 3) Kullanıcı bilgisi
                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                // 4) Kayıt oluştur
                var reviewLog = new YkbWorkFlowReviewLog
                {
                    YkbWorkFlowId = wf.Id,
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
                        Message = $"{dto.RequestNo} numaralı servis talebi ile ilgili bir  gözden geçir mesajınız var.",
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

        //-----------------------Customer Agreement ---------------------------------------------------
        public async Task<ResponseModel<YkbFinalApprovalGetDto>> GetCustomerAgreementByRequestNoAsync(string requestNo, FinalApprovalStatus status = FinalApprovalStatus.CustomerApproval)
        {
            var qFinal = _uow.Repository.GetQueryable<YkbFinalApproval>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking();
            var qTechnicalService = _uow.Repository.GetQueryable<YkbTechnicalService>().AsNoTracking();

            // HEADER: FinalApproval + (left) ServicesRequest -> Customer
            var dto = await (
                from fa in qFinal
                where fa.RequestNo == requestNo && fa.Status == status
                join sr0 in qRequest on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new YkbFinalApprovalGetDto
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

                                     // Ekranda göstermek için:
                                     SystemName = a.CustomerSystem.Name,
                                     SystemCode = a.CustomerSystem.Code,

                                     // İstersen müşteri bilgilerini de doldurabiliriz:
                                     CustomerName = a.Customer.SubscriberCompany,
                                     CustomerShortCode = a.Customer.CustomerShortCode
                                 })
                                .ToList()
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbFinalApprovalGetDto>.Fail("Bu adımda kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır (Tenant eklendi)
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)                           // 🆕
                    .ThenInclude(c => c.Tenant)                     // 🆕
                        .ThenInclude(t => t.TenantProductPrices)    // 🆕
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
                        : p.GetEffectivePrice(); // 🆕 Tenant dahil hesaplar

                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    return new YkbServicesRequestProductGetDto
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


            // REVIEW LOG’ları (APR adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<YkbWorkFlowReviewLogDto>(_config)
                .ToListAsync();

            // RESİMLER: TechnicalService üzerinden form ve service resimlerini çek
            var techService = await qTechnicalService
                .Where(ts => ts.RequestNo == dto.RequestNo)
                .Include(ts => ts.YkbServiceRequestFormImages)
                .Include(ts => ts.YkbServicesImages)
                .FirstOrDefaultAsync();

            // --------------------------------------------------------------------
            //  🔹 IMAGE URL NORMALİZASYONU (FileUrl bazlı)
            // --------------------------------------------------------------------
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

            if (techService != null)
            {
                // Service resimleri
                if (techService.YkbServicesImages != null && techService.YkbServicesImages.Any())
                {
                    dto.ServicesImages = techService.YkbServicesImages
                        .Select(img => new YkbTechnicalServiceImageGetDto
                        {
                            Id = img.Id,
                            YkbTechnicalServiceId = img.YkbTechnicalServiceId,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }

                // Form resimleri
                if (techService.YkbServiceRequestFormImages != null && techService.YkbServiceRequestFormImages.Any())
                {
                    dto.ServiceRequestFormImages = techService.YkbServiceRequestFormImages
                        .Select(img => new YkbTechnicalServiceFormImageGetDto
                        {
                            Id = img.Id,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }
            }
            // --------------------------------------------------------------------

            // Ekli Dosyalar
            dto.Attachments = await GetWorkflowAttachmentsAsync(dto.RequestNo);
            dto.CanEditAttachments = false;// Müşteri onay ekranında yalnızca görüntüleme yapılabilir.


            return ResponseModel<YkbFinalApprovalGetDto>.Success(dto);
        }

        // -------------------- WorkFlowStep --------------------
        public async Task<ResponseModel<PagedResult<YkbWorkFlowStepGetDto>>> GetStepsAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<YkbWorkFlowStep>();
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.Name.Contains(q.Search) || (x.Code ?? "").Contains(q.Search));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Name)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<YkbWorkFlowStepGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<YkbWorkFlowStepGetDto>>
                .Success(new PagedResult<YkbWorkFlowStepGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<YkbWorkFlowStepGetDto>> GetStepByIdAsync(long id)
        {
            var dto = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                .Where(x => x.Id == id)
                .ProjectToType<YkbWorkFlowStepGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbWorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            return ResponseModel<YkbWorkFlowStepGetDto>.Success(dto);
        }

        public async Task<ResponseModel<YkbWorkFlowStepGetDto>> CreateStepAsync(YkbWorkFlowStepCreateDto dto)
        {
            var entity = dto.Adapt<YkbWorkFlowStep>(_config);
            await _uow.Repository.AddAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }

        public async Task<ResponseModel<YkbWorkFlowStepGetDto>> UpdateStepAsync(YkbWorkFlowStepUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<YkbWorkFlowStep>(false, x => x.Id == dto.Id);
            if (entity is null)
                return ResponseModel<YkbWorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Adapt(entity, _config);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }
        public async Task<ResponseModel> DeleteStepAsync(long id)
        {
            // 1) Kaydı (tracked) getir
            var entity = await _uow.Repository.GetSingleAsync<YkbWorkFlowStep>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);
            // 2) Soft delete uygula (entity + 2 tip argümanı ver)
            await _uow.Repository.HardDeleteAsync<YkbWorkFlowStep, long>(entity);

            // 3) Commit
            await _uow.Repository.CompleteAsync();

            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        // -------------------- WorkFlow (tanım) --------------------
        public async Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "YKB")
        {
            prefix ??= "YKB";
            var datePart = DateTime.Now.ToString("yyyyMMdd");

            // En fazla 10 deneme: çakışma olursa tekrar üret
            for (int i = 0; i < 10; i++)
            {
                // Kriptografik güvenli 4 haneli sayı
                int rnd = RandomNumberGenerator.GetInt32(1000, 10000);
                string candidate = $"{prefix}-{datePart}-{rnd}";

                // WorkFlow tablosunda var mı?
                var query = _uow.Repository.GetQueryable<YkbWorkFlow>();
                bool exists = await query.AsNoTracking()
                                         .AnyAsync(x => x.RequestNo == candidate && !x.IsDeleted);

                if (!exists)
                    return ResponseModel<string>.Success(candidate, "Yeni Akış Numarası üretildi.");
            }
            // Çok istisnai durumda buraya düşer
            return ResponseModel<string>.Fail("Benzersiz RequestNo üretilemedi, lütfen tekrar deneyin.");
        }

        public async Task<ResponseModel<PagedResult<YkbWorkFlowGetDto>>> GetWorkFlowsAsync(YkbWorkFlowQueryParams q)
        {
            q.Normalize(maxPageSize: 200);

            var me = await _currentUser.GetAsync();
            if (me is null)
                return ResponseModel<PagedResult<YkbWorkFlowGetDto>>.Fail(
                    "Kullanıcı bulunamadı.",
                    StatusCode.Unauthorized);

            var page = q.Page;
            var pageSize = q.PageSize;

            var pendingStatus = WorkFlowStatus.Pending;
            var myId = me.Id;

            // Kullanıcının yetkili olduğu YKB adım kodları:
            // CF, SR, WH, TS, PRC, APR, CAPR, CNC, CMP
            var permittedSteps = await GetUserStepsByMenuPermission(me.Id) ?? new List<string>();

            var permittedStepCodes = permittedSteps
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Çoklu teknisyen rol kodu desteği
            var technicianRoleRaw = await _uow.Repository
                .GetQueryable<Configuration>()
                .AsNoTracking()
                .Where(x => x.Name == "TechnicianRoleCode")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var technicianRoleCodes = CommonFunctions.ParseRoleCodes(technicianRoleRaw ?? "");

            var isTechnician = technicianRoleCodes.Count > 0 &&
                (me.Roles?.Any(r =>
                    technicianRoleCodes.Contains(r.Code, StringComparer.OrdinalIgnoreCase)) ?? false);

            IQueryable<YkbWorkFlow> wfBase = _uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .AsNoTracking()
                .Where(w =>
                    !w.IsDeleted &&
                    w.WorkFlowStatus == pendingStatus);

            // Yetki filtresi
            if (!isTechnician && permittedStepCodes.Count == 0)
            {
                wfBase = wfBase.Where(_ => false);
            }
            else
            {
                wfBase = wfBase.Where(w =>
                    w.CurrentStepId.HasValue &&
                    w.CurrentStep != null &&
                    w.CurrentStep.Code != null &&
                    permittedStepCodes.Contains(w.CurrentStep.Code) &&
                    (!isTechnician || w.ApproverTechnicianId == myId)
                );
            }

            // WorkFlowStep filtreleme - ID
            if (q.CurrentStepId.HasValue)
            {
                wfBase = wfBase.Where(w => w.CurrentStepId == q.CurrentStepId.Value);
            }

            // WorkFlowStep filtreleme - Code
            if (!string.IsNullOrWhiteSpace(q.StepCode))
            {
                var stepCode = q.StepCode.Trim();

                wfBase = wfBase.Where(w =>
                    w.CurrentStep != null &&
                    w.CurrentStep.Code == stepCode);
            }

            // Priority filtreleme - tekil
            if (q.Priority.HasValue)
            {
                wfBase = wfBase.Where(w => w.Priority == q.Priority.Value);
            }

            // Priority filtreleme - çoklu
            if (q.Priorities is { Count: > 0 })
            {
                var priorities = q.Priorities;
                wfBase = wfBase.Where(w => priorities.Contains(w.Priority));
            }

            // CreatedDate başlangıç filtresi
            if (q.StartDate.HasValue)
            {
                wfBase = wfBase.Where(w => w.CreatedDate >= q.StartDate.Value);
            }

            // CreatedDate bitiş filtresi
            if (q.EndDate.HasValue)
            {
                var endDate = q.EndDate.Value;

                // Frontend sadece tarih gönderirse örn: 2026-06-14 00:00,
                // tüm günü kapsaması için bir sonraki günün başına kadar alıyoruz.
                if (endDate.TimeOfDay == TimeSpan.Zero)
                {
                    var endExclusive = new DateTimeOffset(endDate.Date.AddDays(1), endDate.Offset);
                    wfBase = wfBase.Where(w => w.CreatedDate < endExclusive);
                }
                else
                {
                    wfBase = wfBase.Where(w => w.CreatedDate <= endDate);
                }
            }

            var usersQuery = _uow.Repository
                .GetQueryable<User>()
                .AsNoTracking();

            var stepsQuery = _uow.Repository
                .GetQueryable<YkbWorkFlowStep>()
                .AsNoTracking();

            var serviceRequestsQuery = _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .AsNoTracking();

            var customerFormsQuery = _uow.Repository
                .GetQueryable<YkbCustomerForm>()
                .AsNoTracking();

            var customersQuery = _uow.Repository
                .GetQueryable<Customer>()
                .AsNoTracking();

            var serviceTypesQuery = _uow.Repository
                .GetQueryable<ServiceType>()
                .AsNoTracking();

            var progressApproversQuery = _uow.Repository
                .GetQueryable<ProgressApprover>()
                .AsNoTracking();

            var qJoined =
                from wf in wfBase

                join step0 in stepsQuery
                    on wf.CurrentStepId equals (long?)step0.Id into stepJoin
                from step in stepJoin.DefaultIfEmpty()

                join sr0 in serviceRequestsQuery
                    on wf.RequestNo equals sr0.RequestNo into srJoin
                from sr in srJoin.DefaultIfEmpty()

                join cf0 in customerFormsQuery
                    on wf.RequestNo equals cf0.RequestNo into cfJoin
                from cf in cfJoin.DefaultIfEmpty()

                join srCustomer0 in customersQuery
                    on sr.CustomerId equals (long?)srCustomer0.Id into srCustomerJoin
                from srCustomer in srCustomerJoin.DefaultIfEmpty()

                join cfCustomer0 in customersQuery
                    on cf.CustomerId equals cfCustomer0.Id into cfCustomerJoin
                from cfCustomer in cfCustomerJoin.DefaultIfEmpty()

                join serviceType0 in serviceTypesQuery
                    on sr.ServiceTypeId equals (long?)serviceType0.Id into serviceTypeJoin
                from serviceType in serviceTypeJoin.DefaultIfEmpty()

                join createdUser0 in usersQuery
                    on wf.CreatedUser equals createdUser0.Id into createdUserJoin
                from createdUser in createdUserJoin.DefaultIfEmpty()

                join approverTechnician0 in usersQuery
                    on wf.ApproverTechnicianId equals (long?)approverTechnician0.Id into approverTechnicianJoin
                from approverTechnician in approverTechnicianJoin.DefaultIfEmpty()

                select new
                {
                    wf,
                    step,
                    sr,
                    cf,
                    srCustomer,
                    cfCustomer,
                    serviceType,
                    createdUser,
                    approverTechnician
                };

            // Servis maliyet durumu filtresi
            if (q.ServicesCostStatus.HasValue)
            {
                qJoined = qJoined.Where(x =>
                    x.sr != null &&
                    x.sr.ServicesCostStatus == q.ServicesCostStatus.Value);
            }

            if (q.ServicesCostStatuses is { Count: > 0 })
            {
                var costStatuses = q.ServicesCostStatuses;

                qJoined = qJoined.Where(x =>
                    x.sr != null &&
                    costStatuses.Contains(x.sr.ServicesCostStatus));
            }

            // Servis türü filtresi
            if (q.ServiceTypeId.HasValue)
            {
                qJoined = qJoined.Where(x =>
                    x.sr != null &&
                    x.sr.ServiceTypeId == q.ServiceTypeId.Value);
            }

            if (q.ServiceTypeIds is { Count: > 0 })
            {
                var serviceTypeIds = q.ServiceTypeIds;

                qJoined = qJoined.Where(x =>
                    x.sr != null &&
                    x.sr.ServiceTypeId.HasValue &&
                    serviceTypeIds.Contains(x.sr.ServiceTypeId.Value));
            }

            // İl filtresi
            if (!string.IsNullOrWhiteSpace(q.City))
            {
                var city = q.City.Trim();

                qJoined = qJoined.Where(x =>
                    (
                        x.srCustomer != null &&
                        x.srCustomer.City != null &&
                        x.srCustomer.City.Contains(city)
                    )
                    ||
                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.City != null &&
                        x.cfCustomer.City.Contains(city)
                    ));
            }

            if (q.Cities is { Count: > 0 })
            {
                var cities = q.Cities;

                qJoined = qJoined.Where(x =>
                    (
                        x.srCustomer != null &&
                        x.srCustomer.City != null &&
                        cities.Contains(x.srCustomer.City)
                    )
                    ||
                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.City != null &&
                        cities.Contains(x.cfCustomer.City)
                    ));
            }

            // Müşteri grubu filtresi
            if (q.CustomerGroupId.HasValue)
            {
                var customerGroupId = q.CustomerGroupId.Value;

                qJoined = qJoined.Where(x =>
                    (
                        x.srCustomer != null &&
                        x.srCustomer.CustomerGroupId == customerGroupId
                    )
                    ||
                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.CustomerGroupId == customerGroupId
                    ));
            }

            // Hakediş temsilcisi filtresi
            if (q.ProgressApproverId.HasValue)
            {
                var progressApproverId = q.ProgressApproverId.Value;

                qJoined = qJoined.Where(x =>
                    (
                        x.srCustomer != null &&
                        x.srCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.Id == progressApproverId &&
                            pa.CustomerGroupId == x.srCustomer.CustomerGroupId.Value)
                    )
                    ||
                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.Id == progressApproverId &&
                            pa.CustomerGroupId == x.cfCustomer.CustomerGroupId.Value)
                    ));
            }

            if (!string.IsNullOrWhiteSpace(q.ProgressApproverSearch))
            {
                var approverTerm = q.ProgressApproverSearch.Trim();

                qJoined = qJoined.Where(x =>
                    (
                        x.srCustomer != null &&
                        x.srCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.CustomerGroupId == x.srCustomer.CustomerGroupId.Value &&
                            (
                                (pa.FullName != null && pa.FullName.Contains(approverTerm)) ||
                                (pa.Email != null && pa.Email.Contains(approverTerm)) ||
                                (pa.Phone != null && pa.Phone.Contains(approverTerm))
                            ))
                    )
                    ||
                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.CustomerGroupId == x.cfCustomer.CustomerGroupId.Value &&
                            (
                                (pa.FullName != null && pa.FullName.Contains(approverTerm)) ||
                                (pa.Email != null && pa.Email.Contains(approverTerm)) ||
                                (pa.Phone != null && pa.Phone.Contains(approverTerm))
                            ))
                    ));
            }

            // Detaylı Search
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();

                var priorityAliases = new Dictionary<WorkFlowPriority, string[]>
{
    { WorkFlowPriority.Low, new[] { "Düşük", "Dusuk", "Low" } },

    { WorkFlowPriority.Normal, new[] { "Normal", "Orta" } },

    { WorkFlowPriority.High, new[] { "Yüksek", "Yuksek", "High" } },

    { WorkFlowPriority.Urgent, new[] { "Acil", "Kritik", "Urgent" } },

    {
        WorkFlowPriority.Region1Normal,
        new[]
        {
            "1. Bölge Normal",
            "1.Bölge Normal",
            "1 Bolge Normal",
            "1. Bolge Normal",
            "Bölge 1 Normal",
            "Bolge 1 Normal",
            "Region1Normal"
        }
    },

    {
        WorkFlowPriority.Region1Urgent,
        new[]
        {
            "1. Bölge Acil",
            "1.Bölge Acil",
            "1 Bolge Acil",
            "1. Bolge Acil",
            "1. Bölge Kritik",
            "1 Bolge Kritik",
            "Bölge 1 Acil",
            "Bolge 1 Acil",
            "Region1Urgent"
        }
    },

    {
        WorkFlowPriority.Region2Urgent,
        new[]
        {
            "2. Bölge Acil",
            "2.Bölge Acil",
            "2 Bolge Acil",
            "2. Bolge Acil",
            "2. Bölge Kritik",
            "2 Bolge Kritik",
            "Bölge 2 Acil",
            "Bolge 2 Acil",
            "Region2Urgent"
        }
    },

    {
        WorkFlowPriority.Region2Normal,
        new[]
        {
            "2. Bölge Normal",
            "2.Bölge Normal",
            "2 Bolge Normal",
            "2. Bolge Normal",
            "Bölge 2 Normal",
            "Bolge 2 Normal",
            "Region2Normal"
        }
    },

    {
        WorkFlowPriority.Region3Urgent,
        new[]
        {
            "3. Bölge Acil",
            "3.Bölge Acil",
            "3 Bolge Acil",
            "3. Bolge Acil",
            "3. Bölge Kritik",
            "3 Bolge Kritik",
            "Bölge 3 Acil",
            "Bolge 3 Acil",
            "Region3Urgent"
        }
    },

    {
        WorkFlowPriority.Region3Normal,
        new[]
        {
            "3. Bölge Normal",
            "3.Bölge Normal",
            "3 Bolge Normal",
            "3. Bolge Normal",
            "Bölge 3 Normal",
            "Bolge 3 Normal",
            "Region3Normal"
        }
    }
};

                var workflowStatusAliases = new Dictionary<WorkFlowStatus, string[]>
        {
            { WorkFlowStatus.Pending, new[] { "Beklemede", "Pending" } },
            { WorkFlowStatus.Complated, new[] { "Tamamlandı", "Tamamlandi", "Completed", "Complated" } },
            { WorkFlowStatus.Cancelled, new[] { "İptal", "Iptal", "İptal Edildi", "Iptal Edildi", "Cancelled" } }
        };

                var serviceCostStatusAliases = new Dictionary<ServicesCostStatus, string[]>
        {
            { ServicesCostStatus.Unknown, new[] { "Belirtilmemiş", "Belirtilmemis", "Unknown" } },
            { ServicesCostStatus.NotRequired, new[] { "Ücret gerekmiyor", "Ucret gerekmiyor", "Ücretsiz", "Ucretsiz", "Not Required" } },
            { ServicesCostStatus.Chargeable, new[] { "Ücretli", "Ucretli", "Müşteri öder", "Musteri oder", "Chargeable" } },
            { ServicesCostStatus.Maintenance, new[] { "Bakım", "Bakim", "Bakım kapsamında", "Bakim kapsaminda", "Maintenance" } }
        };

                var priorityMatches = CommonFunctions.MatchEnumValues(term, priorityAliases);
                var workflowStatusMatches = CommonFunctions.MatchEnumValues(term, workflowStatusAliases);
                var serviceCostStatusMatches = CommonFunctions.MatchEnumValues(term, serviceCostStatusAliases);

                var hasPriorityMatches = priorityMatches.Count > 0;
                var hasWorkflowStatusMatches = workflowStatusMatches.Count > 0;
                var hasServiceCostStatusMatches = serviceCostStatusMatches.Count > 0;

                var hasLong = long.TryParse(term, out var longValue);

                var parsedDate = default(DateTimeOffset);

                var hasDate =
                    (term.Contains('.') || term.Contains('/') || term.Contains('-')) &&
                    DateTimeOffset.TryParse(
                        term,
                        CultureInfo.GetCultureInfo("tr-TR"),
                        DateTimeStyles.AssumeLocal,
                        out parsedDate);

                var searchDateStartOffset = default(DateTimeOffset);
                var searchDateEndOffset = default(DateTimeOffset);
                var searchDateStartDate = default(DateTime);
                var searchDateEndDate = default(DateTime);

                if (hasDate)
                {
                    searchDateStartOffset = new DateTimeOffset(parsedDate.Date, parsedDate.Offset);
                    searchDateEndOffset = searchDateStartOffset.AddDays(1);

                    searchDateStartDate = parsedDate.Date;
                    searchDateEndDate = searchDateStartDate.AddDays(1);
                }

                qJoined = qJoined.Where(x =>
                    // WorkFlow
                    x.wf.RequestNo.Contains(term) ||
                    x.wf.RequestTitle.Contains(term) ||

                    // Step
                    (
                        x.step != null &&
                        (
                            x.step.Name.Contains(term) ||
                            (x.step.Code != null && x.step.Code.Contains(term))
                        )
                    ) ||

                    // YKB Services Request
                    (
                        x.sr != null &&
                        (
                            x.sr.RequestNo.Contains(term) ||
                            (x.sr.YkbServiceTrackNo != null && x.sr.YkbServiceTrackNo.Contains(term)) ||
                            (x.sr.Description != null && x.sr.Description.Contains(term))
                        )
                    ) ||

                    // YKB Customer Form
                    (
                        x.cf != null &&
                        (
                            x.cf.RequestNo.Contains(term) ||
                            (x.cf.YkbServiceTrackNo != null && x.cf.YkbServiceTrackNo.Contains(term)) ||
                            (x.cf.Description != null && x.cf.Description.Contains(term))
                        )
                    ) ||

                    // Service Type
                    (
                        x.serviceType != null &&
                        (
                            x.serviceType.Name.Contains(term) ||
                            (x.serviceType.ContractNumber != null && x.serviceType.ContractNumber.Contains(term))
                        )
                    ) ||

                    // Customer from ServicesRequest
                    (
                        x.srCustomer != null &&
                        (
                            (x.srCustomer.SubscriberCode != null && x.srCustomer.SubscriberCode.Contains(term)) ||
                            (x.srCustomer.SubscriberCompany != null && x.srCustomer.SubscriberCompany.Contains(term)) ||
                            (x.srCustomer.SubscriberAddress != null && x.srCustomer.SubscriberAddress.Contains(term)) ||
                            (x.srCustomer.City != null && x.srCustomer.City.Contains(term)) ||
                            (x.srCustomer.District != null && x.srCustomer.District.Contains(term)) ||
                            (x.srCustomer.LocationCode != null && x.srCustomer.LocationCode.Contains(term)) ||
                            (x.srCustomer.ContactName1 != null && x.srCustomer.ContactName1.Contains(term)) ||
                            (x.srCustomer.Phone1 != null && x.srCustomer.Phone1.Contains(term)) ||
                            (x.srCustomer.Email1 != null && x.srCustomer.Email1.Contains(term)) ||
                            (x.srCustomer.ContactName2 != null && x.srCustomer.ContactName2.Contains(term)) ||
                            (x.srCustomer.Phone2 != null && x.srCustomer.Phone2.Contains(term)) ||
                            (x.srCustomer.Email2 != null && x.srCustomer.Email2.Contains(term)) ||
                            (x.srCustomer.CustomerShortCode != null && x.srCustomer.CustomerShortCode.Contains(term)) ||
                            (x.srCustomer.CorporateLocationId != null && x.srCustomer.CorporateLocationId.Contains(term)) ||
                            (x.srCustomer.Note != null && x.srCustomer.Note.Contains(term)) ||
                            (x.srCustomer.LockType != null && x.srCustomer.LockType.Contains(term)) ||
                            (x.srCustomer.CashCenter != null && x.srCustomer.CashCenter.Contains(term))
                        )
                    ) ||

                    // Customer from CustomerForm
                    (
                        x.cfCustomer != null &&
                        (
                            (x.cfCustomer.SubscriberCode != null && x.cfCustomer.SubscriberCode.Contains(term)) ||
                            (x.cfCustomer.SubscriberCompany != null && x.cfCustomer.SubscriberCompany.Contains(term)) ||
                            (x.cfCustomer.SubscriberAddress != null && x.cfCustomer.SubscriberAddress.Contains(term)) ||
                            (x.cfCustomer.City != null && x.cfCustomer.City.Contains(term)) ||
                            (x.cfCustomer.District != null && x.cfCustomer.District.Contains(term)) ||
                            (x.cfCustomer.LocationCode != null && x.cfCustomer.LocationCode.Contains(term)) ||
                            (x.cfCustomer.ContactName1 != null && x.cfCustomer.ContactName1.Contains(term)) ||
                            (x.cfCustomer.Phone1 != null && x.cfCustomer.Phone1.Contains(term)) ||
                            (x.cfCustomer.Email1 != null && x.cfCustomer.Email1.Contains(term)) ||
                            (x.cfCustomer.ContactName2 != null && x.cfCustomer.ContactName2.Contains(term)) ||
                            (x.cfCustomer.Phone2 != null && x.cfCustomer.Phone2.Contains(term)) ||
                            (x.cfCustomer.Email2 != null && x.cfCustomer.Email2.Contains(term)) ||
                            (x.cfCustomer.CustomerShortCode != null && x.cfCustomer.CustomerShortCode.Contains(term)) ||
                            (x.cfCustomer.CorporateLocationId != null && x.cfCustomer.CorporateLocationId.Contains(term)) ||
                            (x.cfCustomer.Note != null && x.cfCustomer.Note.Contains(term)) ||
                            (x.cfCustomer.LockType != null && x.cfCustomer.LockType.Contains(term)) ||
                            (x.cfCustomer.CashCenter != null && x.cfCustomer.CashCenter.Contains(term))
                        )
                    ) ||

                    // Created User
                    (
                        x.createdUser != null &&
                        (
                            (x.createdUser.TechnicianName != null && x.createdUser.TechnicianName.Contains(term)) ||
                            (x.createdUser.TechnicianEmail != null && x.createdUser.TechnicianEmail.Contains(term)) ||
                            (x.createdUser.TechnicianPhone != null && x.createdUser.TechnicianPhone.Contains(term)) ||
                            (x.createdUser.City != null && x.createdUser.City.Contains(term)) ||
                            (x.createdUser.District != null && x.createdUser.District.Contains(term))
                        )
                    ) ||

                    // Approver Technician
                    (
                        x.approverTechnician != null &&
                        (
                            (x.approverTechnician.TechnicianName != null && x.approverTechnician.TechnicianName.Contains(term)) ||
                            (x.approverTechnician.TechnicianEmail != null && x.approverTechnician.TechnicianEmail.Contains(term)) ||
                            (x.approverTechnician.TechnicianPhone != null && x.approverTechnician.TechnicianPhone.Contains(term)) ||
                            (x.approverTechnician.City != null && x.approverTechnician.City.Contains(term)) ||
                            (x.approverTechnician.District != null && x.approverTechnician.District.Contains(term))
                        )
                    ) ||

                    // Hakediş temsilcisi araması
                    (
                        x.srCustomer != null &&
                        x.srCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.CustomerGroupId == x.srCustomer.CustomerGroupId.Value &&
                            (
                                (pa.FullName != null && pa.FullName.Contains(term)) ||
                                (pa.Email != null && pa.Email.Contains(term)) ||
                                (pa.Phone != null && pa.Phone.Contains(term))
                            ))
                    ) ||

                    (
                        x.cfCustomer != null &&
                        x.cfCustomer.CustomerGroupId.HasValue &&
                        progressApproversQuery.Any(pa =>
                            pa.CustomerGroupId == x.cfCustomer.CustomerGroupId.Value &&
                            (
                                (pa.FullName != null && pa.FullName.Contains(term)) ||
                                (pa.Email != null && pa.Email.Contains(term)) ||
                                (pa.Phone != null && pa.Phone.Contains(term))
                            ))
                    ) ||

                    // Enum aramaları
                    (
                        hasPriorityMatches &&
                        priorityMatches.Contains(x.wf.Priority)
                    ) ||

                    (
                        hasWorkflowStatusMatches &&
                        workflowStatusMatches.Contains(x.wf.WorkFlowStatus)
                    ) ||

                    (
                        x.sr != null &&
                        hasServiceCostStatusMatches &&
                        serviceCostStatusMatches.Contains(x.sr.ServicesCostStatus)
                    ) ||

                    // Sayısal arama
                    (
                        hasLong &&
                        (
                            x.wf.Id == longValue ||
                            x.wf.CreatedUser == longValue ||
                            x.wf.UpdatedUser == longValue ||
                            x.wf.ApproverTechnicianId == longValue ||
                            (x.sr != null && x.sr.Id == longValue) ||
                            (x.sr != null && x.sr.CustomerId == longValue) ||
                            (x.sr != null && x.sr.ServiceTypeId == longValue) ||
                            (x.cf != null && x.cf.Id == longValue) ||
                            (x.cf != null && x.cf.CustomerId == longValue) ||
                            (x.srCustomer != null && x.srCustomer.Id == longValue) ||
                            (x.cfCustomer != null && x.cfCustomer.Id == longValue) ||
                            (x.serviceType != null && x.serviceType.Id == longValue)
                        )
                    ) ||

                    // Tarih arama - WorkFlow
                    (
                        hasDate &&
                        x.wf.CreatedDate >= searchDateStartOffset &&
                        x.wf.CreatedDate < searchDateEndOffset
                    ) ||

                    (
                        hasDate &&
                        x.wf.UpdatedDate.HasValue &&
                        x.wf.UpdatedDate.Value >= searchDateStartOffset &&
                        x.wf.UpdatedDate.Value < searchDateEndOffset
                    ) ||

                    // Tarih arama - YkbServicesRequest
                    (
                        hasDate &&
                        x.sr != null &&
                        x.sr.ServicesDate >= searchDateStartOffset &&
                        x.sr.ServicesDate < searchDateEndOffset
                    ) ||

                    (
                        hasDate &&
                        x.sr != null &&
                        x.sr.PlannedCompletionDate.HasValue &&
                        x.sr.PlannedCompletionDate.Value >= searchDateStartOffset &&
                        x.sr.PlannedCompletionDate.Value < searchDateEndOffset
                    ) ||

                    // Tarih arama - YkbCustomerForm
                    (
                        hasDate &&
                        x.cf != null &&
                        x.cf.ServicesDate >= searchDateStartDate &&
                        x.cf.ServicesDate < searchDateEndDate
                    ) ||

                    (
                        hasDate &&
                        x.cf != null &&
                        x.cf.PlannedCompletionDate.HasValue &&
                        x.cf.PlannedCompletionDate.Value >= searchDateStartDate &&
                        x.cf.PlannedCompletionDate.Value < searchDateEndDate
                    )
                );
            }

            var total = await qJoined.CountAsync();

            // Sıralama
            var finalQuery = qJoined;

            if (!string.IsNullOrWhiteSpace(q.Sort))
            {
                var sortLower = q.Sort.ToLowerInvariant();

                switch (sortLower)
                {
                    case "requestno":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.wf.RequestNo)
                            : qJoined.OrderBy(x => x.wf.RequestNo);
                        break;

                    case "requesttitle":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.wf.RequestTitle)
                            : qJoined.OrderBy(x => x.wf.RequestTitle);
                        break;

                    case "priority":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.wf.Priority)
                            : qJoined.OrderBy(x => x.wf.Priority);
                        break;

                    case "createddate":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.wf.CreatedDate)
                            : qJoined.OrderBy(x => x.wf.CreatedDate);
                        break;

                    case "updateddate":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.wf.UpdatedDate)
                            : qJoined.OrderBy(x => x.wf.UpdatedDate);
                        break;

                    case "servicetype":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.serviceType != null ? x.serviceType.Name : string.Empty)
                            : qJoined.OrderBy(x => x.serviceType != null ? x.serviceType.Name : string.Empty);
                        break;

                    case "city":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x =>
                                x.srCustomer != null
                                    ? x.srCustomer.City
                                    : x.cfCustomer != null
                                        ? x.cfCustomer.City
                                        : string.Empty)
                            : qJoined.OrderBy(x =>
                                x.srCustomer != null
                                    ? x.srCustomer.City
                                    : x.cfCustomer != null
                                        ? x.cfCustomer.City
                                        : string.Empty);
                        break;

                    case "servicescoststatus":
                        finalQuery = q.Desc
                            ? qJoined.OrderByDescending(x => x.sr != null ? (int)x.sr.ServicesCostStatus : -1)
                            : qJoined.OrderBy(x => x.sr != null ? (int)x.sr.ServicesCostStatus : -1);
                        break;

                    default:
                        finalQuery = qJoined.OrderByDescending(x => x.wf.CreatedDate);
                        break;
                }
            }
            else
            {
                finalQuery = qJoined.OrderByDescending(x => x.wf.CreatedDate);
            }

            var items = await finalQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new YkbWorkFlowGetDto
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
                    CreatedUserFullName = x.createdUser == null
                        ? null
                        : x.createdUser.TechnicianName,

                    UpdatedUser = x.wf.UpdatedUser,
                    IsDeleted = x.wf.IsDeleted,

                    ApproverTechnicianId = x.wf.ApproverTechnicianId,

                    ApproverTechnician = x.approverTechnician == null
                        ? null
                        : new UserGetDto
                        {
                            Id = x.approverTechnician.Id,
                            TechnicianName = x.approverTechnician.TechnicianName,
                            TechnicianPhone = x.approverTechnician.TechnicianPhone,
                            TechnicianAddress = x.approverTechnician.TechnicianAddress,
                            City = x.approverTechnician.City,
                            District = x.approverTechnician.District,
                            TechnicianEmail = x.approverTechnician.TechnicianEmail,
                        },

                    CustomerCode = x.srCustomer != null
                        ? x.srCustomer.SubscriberCode
                        : x.cfCustomer != null
                            ? x.cfCustomer.SubscriberCode
                            : null,

                    CustomerName = x.srCustomer != null
                        ? x.srCustomer.SubscriberCompany
                        : x.cfCustomer != null
                            ? x.cfCustomer.SubscriberCompany
                            : null,

                    CustomerAddress = x.srCustomer != null
                        ? x.srCustomer.SubscriberAddress
                        : x.cfCustomer != null
                            ? x.cfCustomer.SubscriberAddress
                            : null,

                    CurrentStep = x.step == null
                        ? null
                        : new YkbWorkFlowStepGetDto
                        {
                            Id = x.step.Id,
                            Name = x.step.Name,
                            Code = x.step.Code
                        }

                        // DTO içinde bu alanlar varsa açabilirsin:
                        // CustomerCity = x.srCustomer != null ? x.srCustomer.City : x.cfCustomer != null ? x.cfCustomer.City : null,
                        // CustomerDistrict = x.srCustomer != null ? x.srCustomer.District : x.cfCustomer != null ? x.cfCustomer.District : null,
                        // ServiceTypeId = x.serviceType == null ? null : x.serviceType.Id,
                        // ServiceTypeName = x.serviceType == null ? null : x.serviceType.Name,
                        // ServicesCostStatus = x.sr == null ? null : x.sr.ServicesCostStatus,
                })
                .ToListAsync();

            return ResponseModel<PagedResult<YkbWorkFlowGetDto>>
                .Success(new PagedResult<YkbWorkFlowGetDto>(items, total, page, pageSize));
        }
        public async Task<ResponseModel> DeleteWorkFlowAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            // 1) Entity’yi getir (tracked olsun ki güncelleme/replace çalışsın)
            var entity = await _uow.Repository.GetSingleAsync<YkbWorkFlow>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);


            if (!string.IsNullOrWhiteSpace(entity.RequestNo))
            {
                var finishResult = await ForceFinishActiveWorkingByRequestNoAsync(
                    entity.RequestNo,
                    "Akış silindiği için çalışma zorunlu olarak bitirildi.");

                if (!finishResult.Success)
                {
                    return ResponseModel.Fail(
                        finishResult.ErrorMessage!,
                        StatusCode.Error);
                }
            }

            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.IsDeleted = true;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa
            entity.UpdatedUser = meId;
            _uow.Repository.Update(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        public async Task<ResponseModel> CancelWorkFlowAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            var entity = await _uow.Repository.GetSingleAsync<YkbWorkFlow>(
              asNoTracking: false,
              x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("İptal edilecek kayıt bulunamadı.", StatusCode.NotFound);


            var forceFinishResult = await ForceFinishActiveWorkingByRequestNoAsync(entity.RequestNo, "Akış iptal edildiği için çalışma zorunlu olarak bitirildi.");
            if (!forceFinishResult.Success)
            {
                return ResponseModel.Fail(
                    forceFinishResult.ErrorMessage!,
                    StatusCode.Error);
            }
            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.WorkFlowStatus = WorkFlowStatus.Cancelled;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa
            entity.UpdatedUser = meId;
            _uow.Repository.Update(entity);
            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        //------------------------ Report ------------------------
        public async Task<ResponseModel<YkbWorkFlowReportDto>> GetReportAsync(string requestNo)
        {
            // 1) WorkFlow + CurrentStep + ApproverTechnician
            var wf = await _uow.Repository.GetQueryable<YkbWorkFlow>()
                .Include(x => x.CurrentStep)
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<YkbWorkFlowReportDto>.Fail("Akış bulunamadı.", StatusCode.NotFound);

            var dto = new YkbWorkFlowReportDto
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

            // 2) ServicesRequest + Customer(+Group+Approvers) + ServiceType
            var sr = await _uow.Repository.GetQueryable<YkbServicesRequest>()
                .AsNoTracking()
                .Include(x => x.ServiceType)
                .Include(x => x.Customer)
                    .ThenInclude(c => c.CustomerGroup)
                        .ThenInclude(g => g.ProgressApprovers)
                .Include(x => x.YkbServicesRequestWorkOrderTypes)
                    .ThenInclude(x => x.WorkOrderType)
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

            if (sr is not null)
            {
                dto.ServiceRequest = new ServiceRequestSectionDto
                {
                    Id = sr.Id,
                    OracleNo = sr.YkbServiceTrackNo,
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
                    ServicesRequestStatus = sr.ServicesRequestStatus.ToString(),
                    WorkOrderTypes = sr.YkbServicesRequestWorkOrderTypes
                        .Select(x => new WorkOrderTypeLiteDto
                        {
                            Id = x.WorkOrderType.Id,
                            Name = x.WorkOrderType.Name,
                            Code = x.WorkOrderType.Code
                        })
                        .ToList()
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

            // 3) Ürün satırları (captured-first + Tenant eklendi)
            var lines = await _uow.Repository.GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.Tenant)                     // 🆕
                        .ThenInclude(t => t.TenantProductPrices)    // 🆕
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
                    : p.GetEffectivePrice(); // 🆕 GetEffectivePrice artık Tenant'ı içerir

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
                       : p.Customer?.Tenant?.TenantProductPrices?.Any(t => t.ProductId == p.ProductId) == true ? "Tenant" // 🆕
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
            var ts = await _uow.Repository.GetQueryable<YkbTechnicalService>()
                .AsNoTracking()
                .Include(t => t.YkbServicesImages)
                .Include(t => t.YkbServiceRequestFormImages)
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
                    ServiceImages = ts.YkbServicesImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList(),
                    FormImages = ts.YkbServiceRequestFormImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList()
                };
            }

            // 5) Warehouse
            var wh = await _uow.Repository.GetQueryable<YkbWarehouse>()
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
            var pr = await _uow.Repository.GetQueryable<YkbPricing>()
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
            var fa = await _uow.Repository.GetQueryable<YkbFinalApproval>()
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
                    // İstersen user lookup ile isim de koyabilirsin
                    DecidedByUserName = null
                };
            }

            // 8) Review Logs
            dto.ReviewLogs = await _uow.Repository.GetQueryable<YkbWorkFlowReviewLog>()
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

            // 9) Özet toplamlar (Captured-first)
            dto.Currency = dto.Products.Select(p => p.Currency).FirstOrDefault() ?? (dto.Pricing?.Currency ?? "TRY");
            dto.Subtotal = dto.Products.Sum(p => p.LineTotal);
            dto.DiscountTotal = 0; // ileride indirimin varsa hesapla
            dto.GrandTotal = dto.Subtotal; // + kargo/ek gider vs. eklenebilir

            return ResponseModel<YkbWorkFlowReportDto>.Success(dto);
        }
        public async Task<PagedResult<YkbWorkFlowReportListItemDto>> GetReportsAsync(YkbReportQueryParams q)
        {
            int commandTimeoutSeconds = 60;
            // 1) EF bağlantısını al ve (gerekirse) aç
            var conn = _ctx.Database.GetDbConnection();
            var mustClose = false;
            if (conn.State == ConnectionState.Closed)
            {
                await conn.OpenAsync();
                mustClose = true; // metot bitiminde kapatacağız (DbContext dispose etmeden)
            }

            // 2) EF’de aktif transaction varsa paylaş
            var efTx = _ctx.Database.CurrentTransaction?.GetDbTransaction();

            try
            {
                // 3) Dapper parametreleri
                var p = new DynamicParameters();
                p.Add("@Page", q.Page);
                p.Add("@PageSize", q.PageSize);
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

                string csvWF = (q.WorkFlowStatuses is { Count: > 0 }) ? string.Join(",", q.WorkFlowStatuses.Select(s => (int)s)) : null;
                string csvTS = (q.TechnicalStatuses is { Count: > 0 }) ? string.Join(",", q.TechnicalStatuses.Select(s => (int)s)) : null;
                string csvPR = (q.PricingStatuses is { Count: > 0 }) ? string.Join(",", q.PricingStatuses.Select(s => (int)s)) : null;
                string csvFA = (q.FinalApprovalStatuses is { Count: > 0 }) ? string.Join(",", q.FinalApprovalStatuses.Select(s => (int)s)) : null;

                p.Add("@WorkFlowStatusesCsv", csvWF);
                p.Add("@TechStatusesCsv", csvTS);
                p.Add("@PricingStatusesCsv", csvPR);
                p.Add("@FinalStatusesCsv", csvFA);

                p.Add("@ProductId", q.ProductId);
                p.Add("@ProductCode", q.ProductCode);

                // 4) SP çağrısı (tipli DTO ile)
                var rows = await conn.QueryAsync<ReportRowDto>(new CommandDefinition(
                    "ykb.usp_ReportSearchYkb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeoutSeconds
                ));

                var list = new List<YkbWorkFlowReportListItemDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    // Total’ı her satırdan alıyoruz (window COUNT), ilk satırdaki değer sayfa için yeterli
                    if (total == 0) total = r.TotalCount;

                    list.Add(new YkbWorkFlowReportListItemDto
                    {
                        RequestNo = r.RequestNo,
                        Title = r.Title,
                        WorkFlowStatus = (WorkFlowStatus)r.WorkFlowStatus,
                        StepCode = r.StepCode,
                        CreatedDate = r.CreatedDate,      // DateTimeOffset
                        CustomerId = r.CustomerId,
                        CustomerName = r.CustomerName,
                        City = r.City,
                        District = r.District,
                        ServicesDate = r.ServicesDate,     // DateTimeOffset
                        ServiceTypeId = r.ServiceTypeId,
                        ServiceTypeName = r.ServiceTypeName,
                        TechnicianId = r.TechnicianId,
                        TechnicianName = r.TechnicianName,
                        Currency = r.Currency ?? "TRY",
                        Subtotal = r.Subtotal,
                        HasImages = q.HasImages ?? false // SP’de döndürürsen r.HasImages
                    });
                }

                return new PagedResult<YkbWorkFlowReportListItemDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                // 5) Bağlantıyı biz açtıysak kibarca kapat
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
        public async Task<PagedResult<YkbWorkFlowReportLineDto>> GetReportLinesAsync(YkbReportQueryParams q)
        {
            // Güvenli bound
            q.Normalize(500);

            // EF bağlantısı
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
                // Dapper parametreleri (SP ile birebir)
                var p = new DynamicParameters();
                p.Add("@Page", q.Page);
                p.Add("@PageSize", q.PageSize);
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

                // Çağrı: yeni SP adı
                var rows = await conn.QueryAsync<YkbReportLineRowDto>(new CommandDefinition(
                    "ykb.usp_ReportSearch_LinesYkb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 60
                ));

                var list = new List<YkbWorkFlowReportLineDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    if (total == 0) total = r.TotalCount;

                    list.Add(new YkbWorkFlowReportLineDto
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

                return new PagedResult<YkbWorkFlowReportLineDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }


        public async Task<ResponseModel<PagedResult<YkbBasicReportListDto>>> GetYkbBasicWorkFlowReportAsync(YkbBasicReportQueryParams q)
        {
            try
            {
                q ??= new YkbBasicReportQueryParams();
                q.Normalize(maxPageSize: 200);

                var wfQuery = _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var customerFormQuery = _uow.Repository
                    .GetQueryable<YkbCustomerForm>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var srQuery = _uow.Repository
                    .GetQueryable<YkbServicesRequest>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var whQuery = _uow.Repository
                    .GetQueryable<YkbWarehouse>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var tsQuery = _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var pricingQuery = _uow.Repository
                    .GetQueryable<YkbPricing>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var finalApprovalQuery = _uow.Repository
                    .GetQueryable<YkbFinalApproval>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted);

                var userQuery = _uow.Repository
                    .GetQueryable<User>()
                    .AsNoTracking();

                // -------------------------
                // WorkFlow ana filtreleri
                // -------------------------

                if (!string.IsNullOrWhiteSpace(q.Search))
                {
                    var term = q.Search.Trim();

                    wfQuery = wfQuery.Where(w =>
                        w.RequestNo.Contains(term) ||
                        w.RequestTitle.Contains(term) ||

                        customerFormQuery.Any(cf =>
                            cf.RequestNo == w.RequestNo &&
                            (
                                (cf.Description != null && cf.Description.Contains(term)) ||
                                (cf.YkbServiceTrackNo != null && cf.YkbServiceTrackNo.Contains(term)) ||
                                (cf.Customer != null && cf.Customer.SubscriberCompany != null && cf.Customer.SubscriberCompany.Contains(term)) ||
                                (cf.Customer != null && cf.Customer.SubscriberCode != null && cf.Customer.SubscriberCode.Contains(term))
                            )
                        ) ||

                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            (
                                (sr.Description != null && sr.Description.Contains(term)) ||
                                (sr.YkbServiceTrackNo != null && sr.YkbServiceTrackNo.Contains(term)) ||
                                (sr.Customer != null && sr.Customer.SubscriberCompany != null && sr.Customer.SubscriberCompany.Contains(term)) ||
                                (sr.Customer != null && sr.Customer.SubscriberCode != null && sr.Customer.SubscriberCode.Contains(term))
                            )
                        ) ||

                        userQuery.Any(u =>
                            w.ApproverTechnicianId == u.Id &&
                            u.TechnicianName != null &&
                            u.TechnicianName.Contains(term)
                        )
                    );
                }

                if (!string.IsNullOrWhiteSpace(q.RequestNo))
                {
                    wfQuery = wfQuery.Where(w => w.RequestNo == q.RequestNo);
                }

                if (!string.IsNullOrWhiteSpace(q.YkbServiceTrackNo))
                {
                    wfQuery = wfQuery.Where(w =>
                        customerFormQuery.Any(cf =>
                            cf.RequestNo == w.RequestNo &&
                            cf.YkbServiceTrackNo != null &&
                            cf.YkbServiceTrackNo.Contains(q.YkbServiceTrackNo)) ||

                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.YkbServiceTrackNo != null &&
                            sr.YkbServiceTrackNo.Contains(q.YkbServiceTrackNo))
                    );
                }

                if (q.CurrentStepId.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.CurrentStepId == q.CurrentStepId.Value);
                }

                if (!string.IsNullOrWhiteSpace(q.StepCode))
                {
                    wfQuery = wfQuery.Where(w =>
                        w.CurrentStep != null &&
                        w.CurrentStep.Code == q.StepCode);
                }

                if (q.ApproverTechnicianId.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.ApproverTechnicianId == q.ApproverTechnicianId.Value);
                }

                if (q.CreatedUserId.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.CreatedUser == q.CreatedUserId.Value);
                }

                if (q.Priority.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.Priority == q.Priority.Value);
                }

                if (q.Priorities is { Count: > 0 })
                {
                    wfQuery = wfQuery.Where(w => q.Priorities.Contains(w.Priority));
                }

                if (q.WorkFlowStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.WorkFlowStatus == q.WorkFlowStatus.Value);
                }

                if (q.WorkFlowStatuses is { Count: > 0 })
                {
                    wfQuery = wfQuery.Where(w => q.WorkFlowStatuses.Contains(w.WorkFlowStatus));
                }

                if (q.IsAgreement.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.IsAgreement == q.IsAgreement.Value);
                }

                if (q.IsLocationValid.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.IsLocationValid == q.IsLocationValid.Value);
                }

                if (q.CreatedFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.CreatedDate >= q.CreatedFrom.Value);
                }

                if (q.CreatedTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w => w.CreatedDate <= q.CreatedTo.Value);
                }



                // -------------------------
                // CustomerForm filtreleri - CF
                // -------------------------

                if (q.CustomerFormStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        customerFormQuery.Any(cf =>
                            cf.RequestNo == w.RequestNo &&
                            cf.Status == q.CustomerFormStatus.Value));
                }

                if (q.CustomerFormServicesDateFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        customerFormQuery.Any(cf =>
                            cf.RequestNo == w.RequestNo &&
                            cf.ServicesDate >= q.CustomerFormServicesDateFrom.Value));
                }

                if (q.CustomerFormServicesDateTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        customerFormQuery.Any(cf =>
                            cf.RequestNo == w.RequestNo &&
                            cf.ServicesDate <= q.CustomerFormServicesDateTo.Value));
                }

                // -------------------------
                // ServicesRequest filtreleri - SR
                // -------------------------

                if (q.CustomerId.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.CustomerId == q.CustomerId.Value));
                }

                if (q.ServiceTypeId.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.ServiceTypeId == q.ServiceTypeId.Value));
                }

                if (q.ServicesRequestStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.ServicesRequestStatus == q.ServicesRequestStatus.Value));
                }

                if (q.ServicesCostStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.ServicesCostStatus == q.ServicesCostStatus.Value));
                }

                if (q.IsProductRequirement.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.IsProductRequirement == q.IsProductRequirement.Value));
                }

                if (q.ServicesDateFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.ServicesDate >= q.ServicesDateFrom.Value));
                }

                if (q.ServicesDateTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.ServicesDate <= q.ServicesDateTo.Value));
                }

                // -------------------------
                // TechnicalService filtreleri - TS
                // -------------------------

                if (q.TechnicalServiceStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        tsQuery.Any(ts =>
                            ts.RequestNo == w.RequestNo &&
                            ts.ServicesStatus == q.TechnicalServiceStatus.Value));
                }

                if (q.TechnicalStartFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        tsQuery.Any(ts =>
                            ts.RequestNo == w.RequestNo &&
                            ts.StartTime.HasValue &&
                            ts.StartTime.Value >= q.TechnicalStartFrom.Value));
                }

                if (q.TechnicalStartTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        tsQuery.Any(ts =>
                            ts.RequestNo == w.RequestNo &&
                            ts.StartTime.HasValue &&
                            ts.StartTime.Value <= q.TechnicalStartTo.Value));
                }

                if (q.TechnicalEndFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        tsQuery.Any(ts =>
                            ts.RequestNo == w.RequestNo &&
                            ts.EndTime.HasValue &&
                            ts.EndTime.Value >= q.TechnicalEndFrom.Value));
                }

                if (q.TechnicalEndTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        tsQuery.Any(ts =>
                            ts.RequestNo == w.RequestNo &&
                            ts.EndTime.HasValue &&
                            ts.EndTime.Value <= q.TechnicalEndTo.Value));
                }

                // -------------------------
                // Pricing filtreleri - PRC
                // -------------------------

                if (q.PricingStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        pricingQuery.Any(pr =>
                            pr.RequestNo == w.RequestNo &&
                            pr.Status == q.PricingStatus.Value));
                }

                // -------------------------
                // FinalApproval filtreleri - APR / CAPR / CMP / CNC
                // -------------------------

                if (q.FinalApprovalStatus.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        finalApprovalQuery.Any(fa =>
                            fa.RequestNo == w.RequestNo &&
                            fa.Status == q.FinalApprovalStatus.Value));
                }

                if (q.CustomerApprovedFrom.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        finalApprovalQuery.Any(fa =>
                            fa.RequestNo == w.RequestNo &&
                            fa.CustomerApprovedAt.HasValue &&
                            fa.CustomerApprovedAt.Value >= q.CustomerApprovedFrom.Value));
                }

                if (q.CustomerApprovedTo.HasValue)
                {
                    wfQuery = wfQuery.Where(w =>
                        finalApprovalQuery.Any(fa =>
                            fa.RequestNo == w.RequestNo &&
                            fa.CustomerApprovedAt.HasValue &&
                            fa.CustomerApprovedAt.Value <= q.CustomerApprovedTo.Value));
                }

                // -------------------------
                // WorkOrder Filtresi 
                // -------------------------
                if (q.WorkOrderTypeIds is { Count: > 0 })
                {
                    var workOrderTypeIds = q.WorkOrderTypeIds
                        .Where(x => x > 0)
                        .Distinct()
                        .ToList();

                    wfQuery = wfQuery.Where(w =>
                        srQuery.Any(sr =>
                            sr.RequestNo == w.RequestNo &&
                            sr.YkbServicesRequestWorkOrderTypes.Any(wot =>
                                workOrderTypeIds.Contains(wot.WorkOrderTypeId)
                            )
                        )
                    );
                }

                // -------------------------
                // Count
                // -------------------------

                var total = await wfQuery.CountAsync();

                // -------------------------
                // Sıralama
                // -------------------------

                wfQuery = q.SortBy?.ToLowerInvariant() switch
                {
                    "requestno" => q.SortDesc
                        ? wfQuery.OrderByDescending(w => w.RequestNo)
                        : wfQuery.OrderBy(w => w.RequestNo),

                    "priority" => q.SortDesc
                        ? wfQuery.OrderByDescending(w => w.Priority)
                        : wfQuery.OrderBy(w => w.Priority),

                    "workflowstatus" => q.SortDesc
                        ? wfQuery.OrderByDescending(w => w.WorkFlowStatus)
                        : wfQuery.OrderBy(w => w.WorkFlowStatus),

                    "currentstep" => q.SortDesc
                        ? wfQuery.OrderByDescending(w => w.CurrentStep!.Code)
                        : wfQuery.OrderBy(w => w.CurrentStep!.Code),

                    _ => q.SortDesc
                        ? wfQuery.OrderByDescending(w => w.CreatedDate)
                        : wfQuery.OrderBy(w => w.CreatedDate)
                };

                // -------------------------
                // Sayfalı Workflow kayıtları
                // -------------------------

                var workflows = await wfQuery
                    .Skip((q.Page - 1) * q.PageSize)
                    .Take(q.PageSize)
                    .Select(w => new
                    {
                        w.Id,
                        w.RequestNo,
                        w.RequestTitle,
                        w.CurrentStepId,
                        CurrentStepCode = w.CurrentStep != null ? w.CurrentStep.Code : null,
                        CurrentStepName = w.CurrentStep != null ? w.CurrentStep.Name : null,
                        w.Priority,
                        w.WorkFlowStatus,
                        w.CreatedDate,
                        w.UpdatedDate,
                        w.CreatedUser,
                        w.ApproverTechnicianId,
                        w.IsAgreement,
                        w.IsLocationValid
                    })
                    .ToListAsync();

                var requestNos = workflows
                    .Select(x => x.RequestNo)
                    .Distinct()
                    .ToList();

                if (requestNos.Count == 0)
                {
                    return ResponseModel<PagedResult<YkbBasicReportListDto>>.Success(
                        new PagedResult<YkbBasicReportListDto>(
                            new List<YkbBasicReportListDto>(),
                            total,
                            q.Page,
                            q.PageSize
                        )
                    );
                }

                // -------------------------
                // Sayfadaki RequestNo detayları
                // -------------------------

                var customerForms = await customerFormQuery
                    .Where(cf => requestNos.Contains(cf.RequestNo))
                    .Select(cf => new
                    {
                        cf.Id,
                        cf.RequestNo,
                        cf.YkbServiceTrackNo,
                        cf.ServicesDate,
                        cf.PlannedCompletionDate,
                        cf.CustomerId,
                        CustomerCode = cf.Customer != null ? cf.Customer.SubscriberCode : null,
                        CustomerName = cf.Customer != null ? cf.Customer.SubscriberCompany : null,
                        CustomerCity = cf.Customer != null ? cf.Customer.City : null,
                        CustomerDistrict = cf.Customer != null ? cf.Customer.District : null,
                        cf.Status
                    })
                    .ToListAsync();

                var servicesRequests = await srQuery
                    .Where(sr => requestNos.Contains(sr.RequestNo))
                    .Select(sr => new
                    {
                        sr.Id,
                        sr.RequestNo,
                        sr.YkbServiceTrackNo,
                        sr.CustomerId,
                        CustomerCode = sr.Customer != null ? sr.Customer.SubscriberCode : null,
                        CustomerName = sr.Customer != null ? sr.Customer.SubscriberCompany : null,
                        CustomerCity = sr.Customer != null ? sr.Customer.City : null,
                        CustomerDistrict = sr.Customer != null ? sr.Customer.District : null,
                        sr.ServiceTypeId,
                        ServiceTypeName = sr.ServiceType != null ? sr.ServiceType.Name : null,
                        sr.ServicesDate,
                        sr.PlannedCompletionDate,
                        sr.IsProductRequirement,
                        sr.ServicesCostStatus,
                        sr.ServicesRequestStatus
                    })
                    .ToListAsync();

                var warehouses = await whQuery
                    .Where(wh => requestNos.Contains(wh.RequestNo))
                    .Select(wh => new
                    {
                        wh.Id,
                        wh.RequestNo,
                        wh.WarehouseStatus,
                        wh.DeliveryDate
                    })
                    .ToListAsync();

                var technicalServices = await tsQuery
                    .Where(ts => requestNos.Contains(ts.RequestNo))
                    .Select(ts => new
                    {
                        ts.Id,
                        ts.RequestNo,
                        ts.ServicesStatus,
                        ts.StartTime,
                        ts.EndTime
                    })
                    .ToListAsync();

                var pricings = await pricingQuery
                    .Where(pr => requestNos.Contains(pr.RequestNo))
                    .Select(pr => new
                    {
                        pr.RequestNo,
                        pr.Status,
                        pr.TotalAmount,
                        pr.Currency
                    })
                    .ToListAsync();

                var finalApprovals = await finalApprovalQuery
                    .Where(fa => requestNos.Contains(fa.RequestNo))
                    .Select(fa => new
                    {
                        fa.RequestNo,
                        fa.Status,
                        fa.DiscountPercent,
                        fa.Notes,
                        fa.CustomerNote,
                        fa.CustomerApprovedBy,
                        fa.CustomerApprovedAt
                    })
                    .ToListAsync();

                var userIds = workflows
                    .SelectMany(x => new long?[]
                    {
                x.CreatedUser,
                x.ApproverTechnicianId
                    })
                    .Concat(finalApprovals.Select(x => x.CustomerApprovedBy))
                    .Where(x => x.HasValue && x.Value > 0)
                    .Select(x => x!.Value)
                    .Distinct()
                    .ToList();

                var users = await userQuery
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new
                    {
                        u.Id,
                        u.TechnicianName,
                        u.TechnicianEmail,
                        u.City,
                        u.District
                    })
                    .ToListAsync();

                // -------------------------
                // Dictionary hazırlığı
                // -------------------------

                var customerFormDict = customerForms
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(
                        x => x.Key,
                        x => x.OrderByDescending(y => y.Id).First()
                    );

                var srDict = servicesRequests
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(
                        x => x.Key,
                        x => x.OrderByDescending(y => y.Id).First()
                    );



                var whDict = warehouses
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(
                        x => x.Key,
                        x => x.OrderByDescending(y => y.Id).First()
                    );

                var tsDict = technicalServices
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(
                        x => x.Key,
                        x => x.OrderByDescending(y => y.Id).First()
                    );

                var pricingDict = pricings
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(x => x.Key, x => x.First());

                var finalApprovalDict = finalApprovals
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(x => x.Key, x => x.First());

                var ykbWorkOrderTypes = await _uow.Repository
                    .GetQueryable<YkbServicesRequestWorkOrderType>()
                    .AsNoTracking()
                    .Where(x => requestNos.Contains(x.YkbServicesRequest.RequestNo))
                    .Select(x => new
                    {
                        RequestNo = x.YkbServicesRequest.RequestNo,
                        x.WorkOrderType.Id,
                        x.WorkOrderType.Name,
                        x.WorkOrderType.Code
                    })
                    .ToListAsync();

                var ykbWotDict = ykbWorkOrderTypes
                    .GroupBy(x => x.RequestNo)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Select(w => new WorkOrderTypeLiteDto { Id = w.Id, Name = w.Name, Code = w.Code }).ToList()
                    );

                var userDict = users
                    .GroupBy(x => x.Id)
                    .ToDictionary(x => x.Key, x => x.First());

                // -------------------------
                // DTO oluştur
                // -------------------------

                var items = workflows.Select(w =>
                {
                    customerFormDict.TryGetValue(w.RequestNo, out var cf);
                    srDict.TryGetValue(w.RequestNo, out var sr);
                    whDict.TryGetValue(w.RequestNo, out var wh);
                    tsDict.TryGetValue(w.RequestNo, out var ts);
                    pricingDict.TryGetValue(w.RequestNo, out var pricing);
                    finalApprovalDict.TryGetValue(w.RequestNo, out var finalApproval);

                    userDict.TryGetValue(w.CreatedUser, out var createdUser);

                    var technician = w.ApproverTechnicianId.HasValue &&
                                     userDict.TryGetValue(w.ApproverTechnicianId.Value, out var techUser)
                        ? techUser
                        : null;

                    var customerApprovedByUser = finalApproval?.CustomerApprovedBy.HasValue == true &&
                                                 userDict.TryGetValue(finalApproval.CustomerApprovedBy.Value, out var customerUser)
                        ? customerUser
                        : null;

                    double? durationMinutes = null;

                    if (ts?.StartTime != null && ts?.EndTime != null)
                    {
                        durationMinutes = Math.Round(
                            (ts.EndTime.Value - ts.StartTime.Value).TotalMinutes,
                            2
                        );
                    }

                    return new YkbBasicReportListDto
                    {
                        WorkFlowId = w.Id,

                        RequestNo = w.RequestNo,
                        RequestTitle = w.RequestTitle,

                        YkbServiceTrackNo =
                            !string.IsNullOrWhiteSpace(sr?.YkbServiceTrackNo)
                                ? sr.YkbServiceTrackNo
                                : cf?.YkbServiceTrackNo,

                        CurrentStepId = w.CurrentStepId,
                        CurrentStepCode = w.CurrentStepCode,
                        CurrentStepName = w.CurrentStepName,

                        Priority = w.Priority,
                        WorkFlowStatus = w.WorkFlowStatus,

                        CreatedDate = w.CreatedDate,
                        UpdatedDate = w.UpdatedDate,

                        CreatedUserId = w.CreatedUser,
                        CreatedUserName = createdUser?.TechnicianName,

                        ApproverTechnicianId = w.ApproverTechnicianId,
                        ApproverTechnicianName = technician?.TechnicianName,
                        ApproverTechnicianEmail = technician?.TechnicianEmail,
                        TechnicianCity = technician?.City,
                        TechnicianDistrict = technician?.District,

                        CustomerId = sr?.CustomerId ?? cf?.CustomerId,
                        CustomerCode = sr?.CustomerCode ?? cf?.CustomerCode,
                        CustomerName = sr?.CustomerName ?? cf?.CustomerName,
                        CustomerCity = sr?.CustomerCity ?? cf?.CustomerCity,
                        CustomerDistrict = sr?.CustomerDistrict ?? cf?.CustomerDistrict,

                        ServiceTypeId = sr?.ServiceTypeId,
                        ServiceTypeName = sr?.ServiceTypeName,

                        CustomerFormStatus = cf?.Status,
                        CustomerFormServicesDate = cf?.ServicesDate,
                        CustomerFormPlannedCompletionDate = cf?.PlannedCompletionDate,

                        ServicesDate = sr?.ServicesDate,
                        PlannedCompletionDate = sr?.PlannedCompletionDate,

                        IsAgreement = w.IsAgreement,
                        IsLocationValid = w.IsLocationValid,
                        IsProductRequirement = sr?.IsProductRequirement,

                        ServicesCostStatus = sr?.ServicesCostStatus,
                        ServicesRequestStatus = sr?.ServicesRequestStatus,

                        WarehouseStatus = wh?.WarehouseStatus,
                        WarehouseDeliveryDate = wh?.DeliveryDate,

                        TechnicalServiceStatus = ts?.ServicesStatus,
                        TechnicalStartTime = ts?.StartTime,
                        TechnicalEndTime = ts?.EndTime,
                        TechnicalServiceDurationMinutes = durationMinutes,

                        PricingStatus = pricing?.Status,
                        PricingTotalAmount = pricing?.TotalAmount,
                        Currency = pricing?.Currency,

                        FinalApprovalStatus = finalApproval?.Status,
                        DiscountPercent = finalApproval?.DiscountPercent,
                        FinalApprovalNotes = finalApproval?.Notes,



                        CustomerNote = finalApproval?.CustomerNote,
                        CustomerApprovedBy = finalApproval?.CustomerApprovedBy,
                        CustomerApprovedByName = customerApprovedByUser?.TechnicianName,
                        CustomerApprovedAt = finalApproval?.CustomerApprovedAt,

                        WorkOrderTypes = ykbWotDict.TryGetValue(w.RequestNo, out var ykbWotList)
                            ? ykbWotList
                            : new List<WorkOrderTypeLiteDto>()
                    };
                }).ToList();

                return ResponseModel<PagedResult<YkbBasicReportListDto>>.Success(
                    new PagedResult<YkbBasicReportListDto>(
                        items,
                        total,
                        q.Page,
                        q.PageSize
                    )
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetYkbBasicWorkFlowReportAsync");

                return ResponseModel<PagedResult<YkbBasicReportListDto>>.Fail(
                    $"YKB workflow raporu getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        public async Task<(byte[] Content, string FileName, string ContentType)> ExportYkbBasicWorkFlowReportAsync(YkbBasicReportQueryParams q)
        {
            q ??= new YkbBasicReportQueryParams();

            /*
             * Mevcut liste metodunuz Normalize(maxPageSize: 200) kullandığı için
             * export sırasında da içeride 200'er kayıt çekiyoruz.
             *
             * Kullanıcıya pagination dönmüyor; tüm filtrelenmiş kayıtlar Excel'e yazılıyor.
             * Bu yaklaşım büyük veri setlerinde tek seferde milyonlarca kaydı belleğe alma
             * ve SQL parametre limiti risklerini azaltır.
             */
            const int internalPageSize = 200;

            // Excel'de 1 başlık satırı dahil maksimum 1.048.576 satır bulunabilir.
            const int excelMaxRow = 1_048_576;

            try
            {
                using var workbook = new XLWorkbook();

                var sheetNumber = 1;
                var rowNumber = 2;
                var sequenceNo = 1;

                var worksheet = CreateBasicReportWorksheet(workbook, sheetNumber);

                var page = 1;

                while (true)
                {
                    // Request'ten gelen Page/PageSize export için dikkate alınmaz.
                    q.Page = page;
                    q.PageSize = internalPageSize;

                    var result = await GetYkbBasicWorkFlowReportAsync(q);

                    if (result.Data is null)
                    {
                        throw new InvalidOperationException(
                            "YKB temel rapor verisi export için alınamadı.");
                    }

                    var items = result.Data.Items;

                    if (items is null || items.Count == 0)
                    {
                        break;
                    }

                    foreach (var item in items)
                    {
                        // Bir sheet dolarsa yeni sheet aç.
                        if (rowNumber > excelMaxRow)
                        {
                            sheetNumber++;
                            worksheet = CreateBasicReportWorksheet(workbook, sheetNumber);
                            rowNumber = 2;
                        }

                        WriteBasicReportRow(
                            worksheet,
                            rowNumber,
                            sequenceNo,
                            item);

                        rowNumber++;
                        sequenceNo++;
                    }

                    // Son sayfa geldiyse çık.
                    if (items.Count < internalPageSize)
                    {
                        break;
                    }

                    page++;
                }

                // Çok büyük raporlarda tüm hücreler için AdjustToContents ciddi yavaşlık yaratır.
                // Sadece ilk 100 satıra göre genişlik hesaplanır.
                foreach (var ws in workbook.Worksheets)
                {
                    var lastRowForWidth = Math.Min(ws.LastRowUsed()?.RowNumber() ?? 1, 100);
                    ws.Columns(1, 54).AdjustToContents(1, lastRowForWidth);

                    ws.SheetView.FreezeRows(1);
                    ws.Range(1, 1, 1, 54).SetAutoFilter();

                    ws.Columns().Style.Alignment.Vertical =
                        XLAlignmentVerticalValues.Center;
                }

                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);

                var fileName = $"YKB_Temel_Rapor_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

                const string contentType =
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

                return (memoryStream.ToArray(), fileName, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExportYkbBasicWorkFlowReportAsync");

                throw;
            }
        }

        private static IXLWorksheet CreateBasicReportWorksheet(XLWorkbook workbook, int sheetNumber)
        {
            var sheetName = sheetNumber == 1
                ? "YKB Temel Rapor"
                : $"YKB Temel Rapor {sheetNumber}";

            var ws = workbook.Worksheets.Add(sheetName);

            var headers = new[]
            {
                         "Sıra No",
                         "Workflow Id",
                         "Talep No",
                         "Talep Başlığı",
                         "YKB Servis Takip No",

                         "Mevcut Adım Id",
                         "Mevcut Adım Kodu",
                         "Mevcut Adım Adı",
                         "Mevcut Adım Gösterim Adı",

                         "Öncelik",
                         "İş Akışı Durumu",

                         "Oluşturulma Tarihi",
                         "Güncellenme Tarihi",

                         "Oluşturan Kullanıcı Id",
                         "Oluşturan Kullanıcı",

                         "Onaylayan Teknisyen Id",
                         "Onaylayan Teknisyen",
                         "Onaylayan Teknisyen E-Posta",
                         "Teknisyen İl",
                         "Teknisyen İlçe",

                         "Müşteri Id",
                         "Müşteri Kodu",
                         "Müşteri Adı",
                         "Müşteri İl",
                         "Müşteri İlçe",

                         "Servis Türü Id",
                         "Servis Türü",
                         "İş Emri Türleri",

                         "Müşteri Form Durumu",
                         "Müşteri Form Servis Tarihi",
                         "Müşteri Form Planlanan Tamamlanma",

                         "Servis Talep Tarihi",
                         "Planlanan Tamamlanma Tarihi",

                         "Sözleşmeli Mi",
                         "Konum Geçerli Mi",
                         "Ürün Gereksinimi Var Mı",

                         "Servis Maliyet Durumu",
                         "Servis Talep Durumu",

                         "Depo Durumu",
                         "Depo Teslim Tarihi",

                         "Teknik Servis Durumu",
                         "Teknik Başlangıç Tarihi",
                         "Teknik Bitiş Tarihi",
                         "Teknik Servis Süresi (Dakika)",

                         "Fiyatlandırma Durumu",
                         "Fiyatlandırma Toplam Tutar",
                         "Para Birimi",

                         "Son Onay Durumu",
                         "İndirim Oranı",

                         "Son Onay Notu",
                         "Müşteri Notu",

                         "Müşteri Onaylayan Id",
                         "Müşteri Onaylayan",
                         "Müşteri Onay Tarihi",

                         "Son Aktivite Tarihi"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }

            var headerRange = ws.Range(1, 1, 1, headers.Length);

            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            headerRange.Style.Font.FontColor = XLColor.Black;
            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

            ws.Row(1).Height = 32;

            return ws;
        }

        private static void WriteBasicReportRow(IXLWorksheet ws, int row, int sequenceNo, YkbBasicReportListDto x)
        {
            var c = 1;

            ws.Cell(row, c++).Value = sequenceNo;
            ws.Cell(row, c++).Value = x.WorkFlowId;

            ws.Cell(row, c++).Value = x.RequestNo ?? string.Empty;
            ws.Cell(row, c++).Value = x.RequestTitle ?? string.Empty;
            ws.Cell(row, c++).Value = x.YkbServiceTrackNo ?? string.Empty;

            SetNullableLong(ws.Cell(row, c++), x.CurrentStepId);
            ws.Cell(row, c++).Value = x.CurrentStepCode ?? string.Empty;
            ws.Cell(row, c++).Value = x.CurrentStepName ?? string.Empty;
            ws.Cell(row, c++).Value = x.CurrentStepDisplayName ?? string.Empty;

            ws.Cell(row, c++).Value = x.PriorityName;
            ws.Cell(row, c++).Value = x.WorkFlowStatusName;

            SetDateTime(ws.Cell(row, c++), x.CreatedDate);
            SetDateTime(ws.Cell(row, c++), x.UpdatedDate);

            ws.Cell(row, c++).Value = x.CreatedUserId;
            ws.Cell(row, c++).Value = x.CreatedUserName ?? string.Empty;

            SetNullableLong(ws.Cell(row, c++), x.ApproverTechnicianId);
            ws.Cell(row, c++).Value = x.ApproverTechnicianName ?? string.Empty;
            ws.Cell(row, c++).Value = x.ApproverTechnicianEmail ?? string.Empty;
            ws.Cell(row, c++).Value = x.TechnicianCity ?? string.Empty;
            ws.Cell(row, c++).Value = x.TechnicianDistrict ?? string.Empty;

            SetNullableLong(ws.Cell(row, c++), x.CustomerId);
            ws.Cell(row, c++).Value = x.CustomerCode ?? string.Empty;
            ws.Cell(row, c++).Value = x.CustomerName ?? string.Empty;
            ws.Cell(row, c++).Value = x.CustomerCity ?? string.Empty;
            ws.Cell(row, c++).Value = x.CustomerDistrict ?? string.Empty;

            SetNullableLong(ws.Cell(row, c++), x.ServiceTypeId);
            ws.Cell(row, c++).Value = x.ServiceTypeName ?? string.Empty;

            ws.Cell(row, c++).Value = FormatWorkOrderTypes(x.WorkOrderTypes);

            ws.Cell(row, c++).Value = GetEnumText(x.CustomerFormStatus);
            SetDateTime(ws.Cell(row, c++), x.CustomerFormServicesDate);
            SetDateTime(ws.Cell(row, c++), x.CustomerFormPlannedCompletionDate);

            SetDateTime(ws.Cell(row, c++), x.ServicesDate);
            SetDateTime(ws.Cell(row, c++), x.PlannedCompletionDate);

            ws.Cell(row, c++).Value = BoolText(x.IsAgreement);
            ws.Cell(row, c++).Value = BoolText(x.IsLocationValid);
            ws.Cell(row, c++).Value = BoolText(x.IsProductRequirement);

            ws.Cell(row, c++).Value = GetEnumText(x.ServicesCostStatus);
            ws.Cell(row, c++).Value = GetEnumText(x.ServicesRequestStatus);

            ws.Cell(row, c++).Value = GetEnumText(x.WarehouseStatus);
            SetDateTime(ws.Cell(row, c++), x.WarehouseDeliveryDate);

            ws.Cell(row, c++).Value = GetEnumText(x.TechnicalServiceStatus);
            SetDateTime(ws.Cell(row, c++), x.TechnicalStartTime);
            SetDateTime(ws.Cell(row, c++), x.TechnicalEndTime);
            SetDouble(ws.Cell(row, c++), x.TechnicalServiceDurationMinutes, "#,##0.00");

            ws.Cell(row, c++).Value = GetEnumText(x.PricingStatus);
            SetDecimal(ws.Cell(row, c++), x.PricingTotalAmount, "#,##0.00");
            ws.Cell(row, c++).Value = x.Currency ?? string.Empty;

            ws.Cell(row, c++).Value = GetEnumText(x.FinalApprovalStatus);
            SetDecimal(ws.Cell(row, c++), x.DiscountPercent, "0.00%");

            ws.Cell(row, c++).Value = x.FinalApprovalNotes ?? string.Empty;
            ws.Cell(row, c++).Value = x.CustomerNote ?? string.Empty;

            SetNullableLong(ws.Cell(row, c++), x.CustomerApprovedBy);
            ws.Cell(row, c++).Value = x.CustomerApprovedByName ?? string.Empty;
            SetDateTime(ws.Cell(row, c++), x.CustomerApprovedAt);

            SetDateTime(ws.Cell(row, c++), x.LastActivityDate);

            // Uzun not alanları için satır taşması.
            ws.Cell(row, 51).Style.Alignment.WrapText = true;
            ws.Cell(row, 52).Style.Alignment.WrapText = true;
        }

        private static void SetNullableLong(IXLCell cell, long? value)
        {
            if (!value.HasValue)
                return;

            cell.Value = value.Value;
        }

        private static void SetDateTime(IXLCell cell, DateTimeOffset? value, string format = "dd.MM.yyyy HH:mm")
        {
            if (!value.HasValue)
                return;

            // Değer mutlaka atanmalı; sadece DateFormat vermek hücreyi doldurmaz.
            cell.Value = value.Value.DateTime;
            cell.Style.DateFormat.Format = format;
        }
        private static void SetDateTime(IXLCell cell, DateTime? value, string format = "dd.MM.yyyy HH:mm")
        {
            if (!value.HasValue)
                return;

            // Değer mutlaka atanmalı; sadece DateFormat vermek hücreyi doldurmaz.
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = format;
        }

        private static void SetDecimal(IXLCell cell, decimal? value, string format)
        {
            if (!value.HasValue)
                return;

            cell.Value = value.Value;
            cell.Style.NumberFormat.Format = format;
        }

        private static void SetDouble(IXLCell cell, double? value, string format)
        {
            if (!value.HasValue)
                return;

            cell.Value = value.Value;
            cell.Style.NumberFormat.Format = format;
        }

        private static string BoolText(bool? value)
        {
            return value switch
            {
                true => "Evet",
                false => "Hayır",
                _ => "-"
            };
        }

        private static string GetEnumText<TEnum>(TEnum? value) where TEnum : struct, Enum
        {
            if (!value.HasValue)
                return "-";

            var enumValue = value.Value;

            var member = typeof(TEnum)
                .GetMember(enumValue.ToString())
                .FirstOrDefault();

            var displayName = member?
                .GetCustomAttributes(typeof(DisplayAttribute), inherit: false)
                .OfType<DisplayAttribute>()
                .FirstOrDefault()?
                .GetName();

            return string.IsNullOrWhiteSpace(displayName)
                ? enumValue.ToString()
                : displayName;
        }

        private static string FormatWorkOrderTypes(List<WorkOrderTypeLiteDto>? workOrderTypes)
        {
            if (workOrderTypes is null || workOrderTypes.Count == 0)
                return string.Empty;

            return string.Join(", ",
                workOrderTypes.Select(x =>
                {
                    if (!string.IsNullOrWhiteSpace(x.Code) &&
                        !string.IsNullOrWhiteSpace(x.Name))
                    {
                        return $"{x.Code} - {x.Name}";
                    }

                    return x.Name ?? x.Code ?? string.Empty;
                })
                .Where(x => !string.IsNullOrWhiteSpace(x)));
        }
        //excel export 
        public async Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(YkbReportQueryParams q)
        {
            // 🔒 Filtreleri güvenli hale getir (ama sayfalama yok)
            q.Normalize(500);
            // Pagination kapatıyoruz
            var exportPage = 1;
            var exportPageSize = 1_000_000; // pratik çözüm: çok büyük bir limit

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
                // Dapper parametreleri (SP ile birebir)
                var p = new DynamicParameters();
                p.Add("@Page", exportPage);
                p.Add("@PageSize", exportPageSize);
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

                // SP çağrısı
                var rows = await conn.QueryAsync<YkbReportLineRowDto>(new CommandDefinition(
                    "ykb.usp_ReportSearch_LinesYkb",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 180 // export uzun sürebilir
                ));

                // Excel oluştur
                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Report");

                // Başlıklar (TR) + Sıra No ilk sütun
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

                // Stil: header bold
                ws.Range(1, 1, 1, c - 1).Style.Font.SetBold();

                // Başlık stilini gri yap + yazıyı beyaz/ortala + alt kenarlık
                var headerRange = ws.Range(1, 1, 1, c - 1);
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Font.FontColor = XLColor.Black;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                // (İsteğe bağlı) başlık satır yüksekliği ve kalın font
                ws.Row(1).Height = 22;
                headerRange.Style.Font.Bold = true;

                // Veri satırları
                var r = 2;
                int siraNo = 1;
                foreach (var x in rows)
                {
                    c = 1;
                    ws.Cell(r, c++).Value = siraNo++;                // Sıra No
                    ws.Cell(r, c++).Value = x.RequestNo;             // Talep No
                    ws.Cell(r, c++).Value = x.City;                  // Şehir
                    ws.Cell(r, c++).Value = x.CustomerName;          // Müşteri Adı
                    ws.Cell(r, c++).Value = x.ProductCode;           // Ürün Kodu
                    ws.Cell(r, c++).Value = x.LocationCode;          // Lokasyon Kodu
                    ws.Cell(r, c++).Value = x.ProductOracleCode;     // Oracle Ürün Kodu
                    ws.Cell(r, c++).Value = x.ProductDefinition;     // Ürün Tanımı

                    var svcDateCell = ws.Cell(r, c++);               // Servis Tarihi
                    if (x.ServiceDate.HasValue)
                    {
                        //svcDateCell.Value = x.ServiceDate.Value;
                        svcDateCell.Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                    }

                    ws.Cell(r, c++).Value = x.ServiceOracleNo;       // Oracle Servis No
                    ws.Cell(r, c++).Value = x.WorkOrder;             // İş Emri
                    ws.Cell(r, c++).Value = x.Quantity;              // Miktar

                    var uTL = ws.Cell(r, c++); uTL.Value = x.LineUnitPriceTL; uTL.Style.NumberFormat.Format = "#,##0.00";
                    var tTL = ws.Cell(r, c++); tTL.Value = x.LineTotalTL; tTL.Style.NumberFormat.Format = "#,##0.00";
                    var uUS = ws.Cell(r, c++); uUS.Value = x.LineUnitPriceUSD; uUS.Style.NumberFormat.Format = "#,##0.00";
                    var tUS = ws.Cell(r, c++); tUS.Value = x.LineTotalUSD; tUS.Style.NumberFormat.Format = "#,##0.00";

                    ws.Cell(r, c++).Value = x.GLCode;               // GL Kodu
                    ws.Cell(r, c++).Value = x.MGSDescription;       // MGS Açıklama
                    ws.Cell(r, c++).Value = x.Contract_No;          // Sözleşme No
                    ws.Cell(r, c++).Value = x.CostType;             // Maliyet Tipi
                    ws.Cell(r, c++).Value = x.Description;          // Açıklama

                    var instDateCell = ws.Cell(r, c++);             // Montaj Tarihi
                    if (x.InstallationDate.HasValue)
                    {
                        //instDateCell.Value = x.InstallationDate.Value;
                        instDateCell.Style.DateFormat.Format = "yyyy-MM-dd";
                    }

                    var disc = ws.Cell(r, c++);                     // İndirim Oranı
                    disc.Value = x.DiscountPercent;
                    disc.Style.NumberFormat.Format = "0.00%";

                    r++;
                }

                // Otomatik kolon genişlikleri
                ws.Columns().AdjustToContents();

                // Byte[]
                using var ms = new MemoryStream();
                wb.SaveAs(ms);
                var bytes = ms.ToArray();

                var fileName = $"ServisTalepleri_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
                const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return (bytes, fileName, contentType);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        //Arşiv 
        public async Task<ResponseModel<PagedResult<YkbWorkFlowArchiveListDto>>> GetArchiveListAsync(YkbWorkFlowArchiveFilterDto filter)
        {
            try
            {
                var q = _uow.Repository
                    .GetQueryable<YkbWorkFlowArchive>()
                    .AsNoTracking();

                // --- DB taraflı filtreler ---
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
                {
                    q = q.Where(x => x.ArchivedAt >= filter.ArchivedFrom.Value);
                }

                if (filter.ArchivedTo.HasValue)
                {
                    q = q.Where(x => x.ArchivedAt <= filter.ArchivedTo.Value);
                }

                // --- Projection: sadece gereken kolonlar ---
                var projected = q
                    .Select(a => new
                    {
                        a.Id,
                        a.RequestNo,
                        a.ArchiveReason,
                        a.ArchivedAt,
                        a.CustomerJson,
                        a.ApproverTechnicianJson,
                        a.YkbWorkFlowJson
                    })
                    .OrderByDescending(x => x.ArchivedAt); // En son arşivler üstte

                // --- Sayfalama parametreleri ---
                var page = filter.Page <= 0 ? 1 : filter.Page;
                var pageSize = filter.PageSize <= 0 ? 50 : filter.PageSize;

                // Toplam kayıt sayısı (DB filtrelerine göre)
                var totalCount = await projected.CountAsync();

                // İlgili sayfadaki satırları çek
                var pageRows = await projected
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // --- JSON'dan DTO'ya dönüştürme ---
                var list = new List<YkbWorkFlowArchiveListDto>(pageRows.Count);

                foreach (var a in pageRows)
                {
                    string? customerName = null;
                    string? technicianName = null;
                    string? wfStatus = null;

                    // Müşteri adı
                    try
                    {
                        var customer = JsonConvert.DeserializeObject<Customer>(a.CustomerJson);
                        customerName = customer?.ContactName1 ?? customer?.SubscriberCompany;
                    }
                    catch
                    {
                        // loglamak istersen buraya ek log yazabilirsin
                    }

                    // Teknisyen adı
                    try
                    {
                        var tech = JsonConvert.DeserializeObject<User>(a.ApproverTechnicianJson);
                        technicianName = tech?.TechnicianName;
                    }
                    catch
                    {
                    }

                    // WorkFlow durumu
                    try
                    {
                        var wf = JsonConvert.DeserializeObject<YkbWorkFlow>(a.YkbWorkFlowJson);
                        wfStatus = wf?.WorkFlowStatus.ToString();
                    }
                    catch
                    {
                    }

                    list.Add(new YkbWorkFlowArchiveListDto
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

                // (Opsiyonel) CustomerName / TechnicianName filtrelerini sadece bu sayfa üzerinde uygula
                if (!string.IsNullOrWhiteSpace(filter.CustomerName))
                {
                    var cn = filter.CustomerName.Trim().ToLowerInvariant();
                    list = list
                        .Where(x => !string.IsNullOrEmpty(x.CustomerName) &&
                                    x.CustomerName!.ToLowerInvariant().Contains(cn))
                        .ToList();
                    // Not: totalCount DB'den geldiği için bu filtreyi totalCount'a yansıtmıyoruz.
                }

                if (!string.IsNullOrWhiteSpace(filter.TechnicianName))
                {
                    var tn = filter.TechnicianName.Trim().ToLowerInvariant();
                    list = list
                        .Where(x => !string.IsNullOrEmpty(x.TechnicianName) &&
                                    x.TechnicianName!.ToLowerInvariant().Contains(tn))
                        .ToList();
                }

                // --- Sonuç ---
                var paged = new PagedResult<YkbWorkFlowArchiveListDto>(
                    Items: list,
                    TotalCount: totalCount,
                    Page: page,
                    PageSize: pageSize
                );

                return ResponseModel<PagedResult<YkbWorkFlowArchiveListDto>>.Success(paged);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveListAsync");
                return ResponseModel<PagedResult<YkbWorkFlowArchiveListDto>>.Fail(
                    $"Arşiv kayıtları getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        public async Task<ResponseModel<YkbWorkFlowArchiveDetailDto>> GetArchiveDetailByIdAsync(long id)
        {
            try
            {
                var archive = await _uow.Repository
                    .GetQueryable<YkbWorkFlowArchive>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);

                if (archive is null)
                {
                    return ResponseModel<YkbWorkFlowArchiveDetailDto>.Fail(
                        "Arşiv kaydı bulunamadı.",
                        StatusCode.NotFound
                    );
                }

                var dto = BuildArchiveDetailDto(archive);
                return ResponseModel<YkbWorkFlowArchiveDetailDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveDetailByIdAsync");
                return ResponseModel<YkbWorkFlowArchiveDetailDto>.Fail(
                    $"Arşiv detayı getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        public async Task<ResponseModel<YkbWorkFlowArchiveDetailDto>> GetArchiveDetailByRequestNoAsync(string requestNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestNo))
                {
                    return ResponseModel<YkbWorkFlowArchiveDetailDto>.Fail(
                        "RequestNo boş olamaz.",
                        StatusCode.BadRequest
                    );
                }

                var rn = requestNo.Trim();

                var archive = await _uow.Repository
                    .GetQueryable<YkbWorkFlowArchive>()
                    .AsNoTracking()
                    .Where(x => x.RequestNo == rn)
                    .OrderByDescending(x => x.ArchivedAt)
                    .FirstOrDefaultAsync();

                if (archive is null)
                {
                    return ResponseModel<YkbWorkFlowArchiveDetailDto>.Fail(
                        "Arşiv kaydı bulunamadı.",
                        StatusCode.NotFound
                    );
                }

                var dto = BuildArchiveDetailDto(archive);
                return ResponseModel<YkbWorkFlowArchiveDetailDto>.Success(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetArchiveDetailByRequestNoAsync");
                return ResponseModel<YkbWorkFlowArchiveDetailDto>.Fail(
                    $"Arşiv detayı getirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error
                );
            }
        }

        /// --------------------- Arşivleme  ---------------------
        private async Task ArchiveWorkflowAsync(string requestNo, string archiveReason, CancellationToken ct = default)
        {
            // 1) Ana kayıtlar
            var servicesRequest = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .Include(x => x.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            if (servicesRequest is null)
                return; // veya exception/log

            var customer = servicesRequest.Customer;

            var workFlow = await _uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted, ct);

            var products = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ToListAsync(ct);

            // CustomerApprover
            ProgressApprover? customerApprover = null;
            if (servicesRequest.CustomerApproverId.HasValue)
            {
                customerApprover = await _uow.Repository
                    .GetQueryable<ProgressApprover>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == servicesRequest.CustomerApproverId.Value, ct);
            }

            // Teknik servis + resimler
            var technicalService = await _uow.Repository
                .GetQueryable<YkbTechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            var serviceImages = await _uow.Repository
                .GetQueryable<YkbTechnicalServiceImage>()
                .AsNoTracking()
                .Where(x => x.YkbTechnicalServiceId == technicalService.Id)
                .ToListAsync(ct);

            var formImages = await _uow.Repository
                .GetQueryable<YkbTechnicalServiceFormImage>()
                .AsNoTracking()
                .Where(x => x.YkbTechnicalServiceId == technicalService.Id)
                .ToListAsync(ct);

            // Depo
            var warehouse = await _uow.Repository
                .GetQueryable<YkbWarehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            // Pricing
            var pricing = await _uow.Repository
                .GetQueryable<YkbPricing>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            // FinalApproval
            var finalApproval = await _uow.Repository
                .GetQueryable<YkbFinalApproval>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo, ct);

            // ReviewLog
            var reviewLogs = await _uow.Repository
                .GetQueryable<YkbWorkFlowReviewLog>()
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .OrderBy(x => x.CreatedDate)
                .ToListAsync(ct);

            // 2) Resimleri base64'e çevir
            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "UploadsStorage");

            async Task<string?> ReadBase64Async(string url)
            {
                if (string.IsNullOrWhiteSpace(url))
                    return null;

                var path = Path.Combine(uploadRoot, url);
                if (!File.Exists(path))
                    return null;

                var bytes = await File.ReadAllBytesAsync(path, ct);
                return Convert.ToBase64String(bytes);
            }

            var serviceImageDtos = new List<ArchiveImageDto>();
            foreach (var img in serviceImages)
            {
                serviceImageDtos.Add(new ArchiveImageDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    Caption = img.Caption,
                    //Base64 = await ReadBase64Async(img.Url)
                });
            }

            var formImageDtos = new List<ArchiveImageDto>();
            foreach (var img in formImages)
            {
                formImageDtos.Add(new ArchiveImageDto
                {
                    Id = img.Id,
                    Url = img.Url,
                    Caption = img.Caption,
                    //Base64 = await ReadBase64Async(img.Url)
                });
            }

            // 3) JSON string’leri hazırla
            var servicesRequestJson = JsonConvert.SerializeObject(servicesRequest);
            var productsJson = JsonConvert.SerializeObject(products);
            var customerJson = JsonConvert.SerializeObject(customer);
            var approverTechnicianJson = JsonConvert.SerializeObject(workFlow?.ApproverTechnician);
            var customerApproverJson = JsonConvert.SerializeObject(customerApprover);
            var workFlowJson = JsonConvert.SerializeObject(workFlow);
            var reviewLogsJson = JsonConvert.SerializeObject(reviewLogs);
            var technicalServiceJson = JsonConvert.SerializeObject(technicalService);
            var techServiceImagesJson = JsonConvert.SerializeObject(serviceImageDtos);
            var techServiceFormImagesJson = JsonConvert.SerializeObject(formImageDtos);
            var warehouseJson = JsonConvert.SerializeObject(warehouse);
            var pricingJson = JsonConvert.SerializeObject(pricing);
            var finalApprovalJson = JsonConvert.SerializeObject(finalApproval);

            // 4) Arşiv kaydı oluştur
            var archive = new YkbWorkFlowArchive
            {
                RequestNo = requestNo,
                ArchivedAt = DateTime.Now,
                ArchiveReason = archiveReason,

                YkbServicesRequestJson = servicesRequestJson,
                YkbServicesRequestProductsJson = productsJson,
                CustomerJson = customerJson,
                ApproverTechnicianJson = approverTechnicianJson,
                CustomerApproverJson = customerApproverJson,
                YkbWorkFlowJson = workFlowJson,
                YkbWorkFlowReviewLogsJson = reviewLogsJson,
                YkbTechnicalServiceJson = technicalServiceJson,
                YkbTechnicalServiceImagesJson = techServiceImagesJson,
                YkbTechnicalServiceFormImagesJson = techServiceFormImagesJson,
                YkbWarehouseJson = warehouseJson,
                YkbPricingJson = pricingJson,
                YkbFinalApprovalJson = finalApprovalJson
            };

            await _uow.Repository.AddAsync(archive);
            // Commit’i dışarıda (çağıran methodda) yapacağız.
        }

        //-------------Private-------------

        // Tek noktadan güvenli parse (boş, " ", virgül/nokta farkı vb.)
        private static bool TryParseLatLon(string? s, out double value)
        {
            value = default;
            if (string.IsNullOrWhiteSpace(s)) return false;
            // ondalık ayırıcıyı normalize et
            s = s.Trim().Replace(" ", "").Replace(',', '.');
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        private async Task<ResponseModel> IsTechnicianInValidLocation(string? lat1, string? lon1, string? lat2, string? lon2)
        {
            // --- Config oku (min mesafe)
            var cfg = await _uow.Repository.GetSingleAsync<Configuration>(false, x => x.Name == "TechnicianCustomerMinDistanceKm");
            if (cfg is null)
                return ResponseModel.Fail("Konum kontrolü için gerekli 'TechnicianCustomerMinDistanceKm' tanımı bulunamadı.", StatusCode.NotFound);

            // Güvenli parse: boş/format hatasında 0 değil, bilinçli hata dönelim
            if (!TryParseLatLon(cfg.Value, out var minDistanceKm))
                return ResponseModel.Fail("'TechnicianCustomerMinDistanceKm' değeri sayısal formatta değil.", StatusCode.InvalidConfiguration);

            // --- 1) Müşteri lokasyonu zorunlu
            if (string.IsNullOrWhiteSpace(lat1) || string.IsNullOrWhiteSpace(lon1))
                return ResponseModel.Fail("Müşteri lokasyonu geçersiz veya eksik.", StatusCode.InvalidCustomerLocation);

            if (!TryParseLatLon(lat1, out var latitude1) || !TryParseLatLon(lon1, out var longitude1))
                return ResponseModel.Fail("Müşteri lokasyonu hatalı formatta.", StatusCode.InvalidCustomerLocation);

            // --- 2) Teknisyen lokasyonu zorunlu
            if (string.IsNullOrWhiteSpace(lat2) || string.IsNullOrWhiteSpace(lon2))
                return ResponseModel.Fail("Teknisyen lokasyonu geçersiz veya eksik.", StatusCode.InvalidTechnicianLocation);

            if (!TryParseLatLon(lat2, out var latitude2) || !TryParseLatLon(lon2, out var longitude2))
                return ResponseModel.Fail("Teknisyen lokasyonu hatalı formatta.", StatusCode.InvalidTechnicianLocation);

            // --- 3) Mesafe hesabı
            var distance = GetDistanceInKm(latitude1, longitude1, latitude2, longitude2);

            // Sunulacak metin formatı
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
            const double R = 6371; // Dünya yarıçapı (km)
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

            return R * c; // km cinsinden döner
        }
        private static double ToRadians(double deg) => deg * (Math.PI / 180);
        private async Task<List<string>> ResolveWarehouseEmailsAsync(CancellationToken ct = default)
        {
            // Depo rol kodları (case-insensitive karşılaştırma için üst versiyonunu da alıyoruz)
            var WH_CODES = new[] { "WH", "WAREHOUSE", "Depo" };
            var WH_CODES_UP = WH_CODES.Select(x => x.ToUpperInvariant()).ToArray();

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
        private static string? GetTechnicianEmail(YkbWorkFlow wf)
        {
            return wf?.ApproverTechnician?.TechnicianEmail;
        }
        private async Task PushTransitionMailsAsync(YkbWorkFlow wf, string fromCode, string toCode, string requestNo, string? customerName)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            // 1) Teknisyen’e — TS yönüne gidişler ve TS’den geri dönüşler
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

            // 2) Depo — WH yönüne gidişler ve WH’den geri dönüşler
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
        private async Task<List<string>> GetUserStepsByMenuPermission(long userId)
        {
            var permissionList = await _menuService.GetByUserIdAsync(userId);

            if (permissionList is null || permissionList.Count == 0)
                return new List<string>();

            // Name -> StepCode map
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["YkbServiceRequestWarehouse"] = "WH",
                ["YkbServiceRequestPricing"] = "PRC",
                ["YkbCancelledFlows"] = "CNC",
                ["YkbCustomerServiceRequestCreate"] = "CF",
                ["YkbServiceRequestCustomerAgreement"] = "CAPR",
                ["YkbServiceRequestFinalApproval"] = "APR",
                ["YkbServiceRequestCreate"] = "SR",
                ["YkbServiceRequestComplate"] = "CMP",
                ["YkbServiceRequestTechnicalService"] = "TS",
            };

            // permissionList içinde Name'i map'te olanlar -> code listesine ekle (unique)
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

        private YkbWorkFlowArchiveDetailDto BuildArchiveDetailDto(YkbWorkFlowArchive archive)
        {
            YkbServicesRequest? servicesRequest = null;
            List<YkbServicesRequestProduct> products = new();
            Customer? customer = null;
            User? approverTechnician = null;
            ProgressApprover? customerApprover = null;
            YkbWorkFlow? wf = null;
            List<YkbWorkFlowReviewLog> reviewLogs = new();
            YkbTechnicalService? technicalService = null;
            List<ArchiveImageDto> serviceImages = new();
            List<ArchiveImageDto> formImages = new();
            YkbWarehouse? warehouse = null;
            YkbPricing? pricing = null;
            YkbFinalApproval? finalApproval = null;

            try { servicesRequest = JsonConvert.DeserializeObject<YkbServicesRequest>(archive.YkbServicesRequestJson); } catch { }
            try { products = JsonConvert.DeserializeObject<List<YkbServicesRequestProduct>>(archive.YkbServicesRequestProductsJson) ?? new(); } catch { }
            try { customer = JsonConvert.DeserializeObject<Customer>(archive.CustomerJson); } catch { }
            try { approverTechnician = JsonConvert.DeserializeObject<User>(archive.ApproverTechnicianJson); } catch { }
            try { customerApprover = JsonConvert.DeserializeObject<ProgressApprover>(archive.CustomerApproverJson); } catch { }
            try { wf = JsonConvert.DeserializeObject<YkbWorkFlow>(archive.YkbWorkFlowJson); } catch { }
            try { reviewLogs = JsonConvert.DeserializeObject<List<YkbWorkFlowReviewLog>>(archive.YkbWorkFlowReviewLogsJson) ?? new(); } catch { }
            try { technicalService = JsonConvert.DeserializeObject<YkbTechnicalService>(archive.YkbTechnicalServiceJson); } catch { }
            try { serviceImages = JsonConvert.DeserializeObject<List<ArchiveImageDto>>(archive.YkbTechnicalServiceImagesJson) ?? new(); } catch { }
            try { formImages = JsonConvert.DeserializeObject<List<ArchiveImageDto>>(archive.YkbTechnicalServiceFormImagesJson) ?? new(); } catch { }
            try { warehouse = JsonConvert.DeserializeObject<YkbWarehouse>(archive.YkbWarehouseJson); } catch { }
            try { pricing = JsonConvert.DeserializeObject<YkbPricing>(archive.YkbPricingJson); } catch { }
            try { finalApproval = JsonConvert.DeserializeObject<YkbFinalApproval>(archive.YkbFinalApprovalJson); } catch { }

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

            var snapshot = new YkbWorkFlowArchiveSnapshotDto
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

            return new YkbWorkFlowArchiveDetailDto
            {
                Id = archive.Id,
                RequestNo = archive.RequestNo,
                ArchivedAt = archive.ArchivedAt,
                ArchiveReason = archive.ArchiveReason,
                Snapshot = snapshot
            };
        }

        /// Servis Ürünleri Fiyat savbitleme
        private async Task<ResponseModel> EnsurePricesCapturedFromDtoAsync(string requestNo, IEnumerable<YkbServicesRequestProductCreateDto>? productsDto)
        {
            var dtoDict = (productsDto ??
                           Enumerable.Empty<YkbServicesRequestProductCreateDto>())
                .ToDictionary(x => x.ProductId, x => x);

            if (!dtoDict.Any())
                return ResponseModel.Success();

            // Talebe ait ürünleri ve ürün bilgilerini getir.
            var list = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .Include(x => x.Product)
                .Where(x => x.RequestNo == requestNo)
                .ToListAsync();

            if (list.Count == 0)
                return ResponseModel.Success();

            /*
             * Hizmet bedeli olmayan ürünlerin toplamlarını
             * para birimi bazında hesapla.
             *
             * Hizmet bedeli ürünleri hiçbir şekilde
             * başka bir hizmet bedelinin matrahına dahil edilmez.
             */
            var baseTotalsByCurrency =
                new Dictionary<string, decimal>(
                    StringComparer.OrdinalIgnoreCase
                );

            foreach (var product in list)
            {
                // Gönderilen ürün listesinde bulunmayan satırı hesaba katma.
                if (!dtoDict.TryGetValue(product.ProductId, out var dtoItem))
                    continue;

                // Hizmet bedeli ürünleri matraha dahil edilmez.
                if (product.Product?.IsServiceFeeProduct == true)
                    continue;

                var currency = string.IsNullOrWhiteSpace(
                    product.Product?.PriceCurrency
                )
                    ? "TRY"
                    : product.Product.PriceCurrency
                        .Trim()
                        .ToUpperInvariant();

                // Normal ürünlerin fiyatı mevcut davranışta olduğu gibi DTO'dan gelir.
                var unitPrice = dtoItem.Price ?? 0m;

                var lineTotal = unitPrice * product.Quantity;

                baseTotalsByCurrency[currency] =
                    baseTotalsByCurrency.TryGetValue(
                        currency,
                        out var currentTotal
                    )
                        ? currentTotal + lineTotal
                        : lineTotal;
            }

            /*
             * Normal ürünlerin fiyatlarını DTO'dan al,
             * hizmet bedeli ürünlerini ise hesaplayarak sabitle.
             */
            foreach (var product in list)
            {
                if (!dtoDict.TryGetValue(product.ProductId, out var dtoItem))
                    continue;

                var currency = string.IsNullOrWhiteSpace(
                    product.Product?.PriceCurrency
                )
                    ? "TRY"
                    : product.Product.PriceCurrency
                        .Trim()
                        .ToUpperInvariant();

                decimal? unitPrice;

                if (product.Product?.IsServiceFeeProduct == true)
                {
                    var percentage =
                        product.Product.ServiceFeePercentage ?? 0m;

                    var currencyBaseTotal =
                        baseTotalsByCurrency.TryGetValue(
                            currency,
                            out var calculatedBaseTotal
                        )
                            ? calculatedBaseTotal
                            : 0m;

                    /*
                     * Hizmet Bedeli Birim Fiyatı =
                     * Aynı para birimindeki normal ürünlerin toplamı
                     * × Hizmet bedeli yüzdesi
                     * ÷ 100
                     */
                    unitPrice = Math.Round(
                        currencyBaseTotal * percentage / 100m,
                        2,
                        MidpointRounding.AwayFromZero
                    );
                }
                else
                {
                    // Normal ürünlerde mevcut davranış korunur.
                    unitPrice = dtoItem.Price;
                }

                /*
                 * Hizmet bedeli için hesaplanan değer birim fiyattır.
                 * Satır toplamı yine adet × birim fiyat şeklindedir.
                 */
                var totalPrice = unitPrice * product.Quantity;

                product.CapturedSource =
                    CapturedPriceSource.Standard;

                product.CapturedUnitPrice = unitPrice;
                product.CapturedCurrency = currency;
                product.CapturedTotal = totalPrice;
                product.CapturedAt = DateTime.Now;
                product.IsPriceCaptured = true;

                _uow.Repository.Update(product);
            }

            await _uow.Repository.CompleteAsync();

            return ResponseModel.Success();
        }


        private async Task<(List<long> Ids, string? Error)> ValidateWorkOrderTypeIdsAsync(
            IEnumerable<long>? rawIds)
        {
            var ids = (rawIds ?? Enumerable.Empty<long>())
                .Where(x => x > 0)
                .ToList();

            var distinctIds = ids
                .Distinct()
                .ToList();

            if (distinctIds.Count == 0)
                return (distinctIds, null);

            var existingIds = await _uow.Repository
                .GetQueryable<WorkOrderType>()
                .AsNoTracking()
                .Where(x => distinctIds.Contains(x.Id))
                .Select(x => x.Id)
                .ToListAsync();

            var invalidIds = distinctIds
                .Except(existingIds)
                .ToList();

            if (invalidIds.Count > 0)
                return (new List<long>(),
                    $"Geçersiz iş emri türü ID'leri: {string.Join(", ", invalidIds)}");

            return (distinctIds, null);
        }

        private void SyncYkbWorkOrderTypes(
            YkbServicesRequest request,
            IReadOnlyCollection<long> workOrderTypeIds)
        {
            var requestedIds = workOrderTypeIds.ToHashSet();

            var currentRelations = request.YkbServicesRequestWorkOrderTypes.ToList();

            // Artık seçili olmayanları kaldır
            foreach (var relation in currentRelations
                .Where(x => !requestedIds.Contains(x.WorkOrderTypeId))
                .ToList())
            {
                _uow.Repository.HardDelete(relation);
            }

            var currentIds = currentRelations
                .Select(x => x.WorkOrderTypeId)
                .ToHashSet();

            // Yeni seçilenleri ekle
            foreach (var workOrderTypeId in requestedIds.Where(x => !currentIds.Contains(x)))
            {
                _uow.Repository.Add(new YkbServicesRequestWorkOrderType
                {
                    YkbServicesRequestId = request.Id,
                    WorkOrderTypeId = workOrderTypeId
                });
            }
        }

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


        //Manitou Test Zone ile ilgili işlemler 
        public async Task<ResponseModel<WorkingStatusDto>> StartWorking(StartWorkingDto dto)
        {
            try
            {
                if (dto is null || string.IsNullOrWhiteSpace(dto.RequestNo))
                    return ResponseModel<WorkingStatusDto>.Fail("Talep numarası zorunludur.", StatusCode.BadRequest);

                var context = await GetTechnicalServiceContextAsync(dto.RequestNo);

                if (context is null)
                    return ResponseModel<WorkingStatusDto>.Fail("Akış / servis talebi / müşteri / teknik servis bilgisi bulunamadı.", StatusCode.NotFound);

                var (wf, request, customer, technicalService) = context.Value;

                var isManitouTestEnabled = await IsManitouTechnicalServiceTestEnabledAsync(customer.TenantId);

                if (!isManitouTestEnabled)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Manitou test alma özelliği bu tenant için aktif değildir.",
                        StatusCode.NotFound);
                }

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<WorkingStatusDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.Conflict);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<WorkingStatusDto>.Fail("İlgili akış tamamlanmış.", StatusCode.Conflict);

                if (technicalService.ServicesStatus != TechnicalServiceStatus.InProgress)
                    return ResponseModel<WorkingStatusDto>.Fail("Çalışma başlatmak için teknik servis önce başlatılmalıdır.", StatusCode.Conflict);

                if (!customer.SerialNo.HasValue || customer.SerialNo.Value <= 0)
                    return ResponseModel<WorkingStatusDto>.Fail("Müşteri için Manitou SerialNo bilgisi bulunamadı.", StatusCode.BadRequest);

                var existingActiveSession = await _uow.Repository
                    .GetQueryable<YkbTechnicalServiceWorkSession>()
                    .FirstOrDefaultAsync(x =>
                        x.RequestNo == dto.RequestNo &&
                        x.IsActive &&
                        !x.IsCompleted &&
                        !x.IsDeleted);

                if (existingActiveSession is not null)
                    return await GetWorkingStatus(dto.RequestNo);

                var accessToken = await _manitouApiService.LoginAsync();

                if (string.IsNullOrWhiteSpace(accessToken))
                    return ResponseModel<WorkingStatusDto>.Fail("Manitou oturum anahtarı alınamadı.", StatusCode.Error);

                var serialNo = customer.SerialNo.Value;

                // Aynı müşteri için aktif test var mı?
                var expiredSessionCheck = await CompleteExpiredCustomerWorkingBeforeNewStartAsync((long)request.CustomerId, dto.RequestNo, accessToken);

                if (!expiredSessionCheck.CanStart)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        expiredSessionCheck.Message!,
                        expiredSessionCheck.StatusCode);
                }

                var nowUtc = DateTimeOffset.UtcNow;
                var plannedEndUtc = nowUtc.AddHours(1);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                var technicianName =
                        me?.TechnicianName ??
                        me?.Name ??
                        me?.Email ??
                        "Bilinmeyen Teknisyen";

                var startDescription = BuildManitouTestDescription(
                    dto.RequestNo,
                    technicianName,
                    "başlatıldı");

                await _manitouApiService.BeginSystemTestAsync(accessToken, serialNo);

                await _manitouApiService.SetCustomerOnTestAsync(
                    accessToken,
                    new ManitouOnTestRequest
                    {
                        SerialNo = serialNo,
                        Description = startDescription,
                        UtcFrom = ToManitouUtcText(nowUtc),
                        UtcTo = ToManitouUtcText(plannedEndUtc),
                        IsNew = true
                    });

                var zones = await _manitouApiService.QuerySystemTestAsync(accessToken, serialNo);

                var outOfServiceRecords = await _manitouApiService.GetOutOfServiceAsync(accessToken, serialNo);

                var relatedOutOfServiceRecord = GetRelatedOutOfServiceRecord(outOfServiceRecords, serialNo, dto.RequestNo);

                var session = new YkbTechnicalServiceWorkSession
                {
                    RequestNo = dto.RequestNo,
                    WorkFlowId = wf.Id,
                    TechnicalServiceId = technicalService.Id,
                    CustomerId = (long)request.CustomerId,
                    SerialNo = serialNo,
                    StartedAtUtc = nowUtc,
                    PlannedEndAtUtc = plannedEndUtc,
                    IsActive = true,
                    IsCompleted = false,
                    ExtendCount = 0,
                    ManitouLogSequence = relatedOutOfServiceRecord?.LogSequence,
                    CreatedDate = DateTime.Now,
                    CreatedUser = meId,
                    IsDeleted = false
                };

                _uow.Repository.Add(session);

                await _activationRecord.LogYkbAsync(
                    WorkFlowActionType.TechnicalServiceStarted,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "TS",
                    "TS",
                    "Manitou çalışma/test başlatıldı",
                    new
                    {
                        SerialNo = serialNo,
                        StartedAtUtc = nowUtc,
                        PlannedEndAtUtc = plannedEndUtc,
                        ZoneCount = zones.Count
                    });

                await _uow.Repository.CompleteAsync();

                return await GetWorkingStatus(dto.RequestNo);
            }
            catch (ManitouApiException ex)
            {
                _logger.LogError(ex, "StartWorking Manitou hatası. RequestNo={RequestNo}", dto?.RequestNo);

                return ResponseModel<WorkingStatusDto>.Fail(
                    $"Manitou çalışma başlatma sırasında hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartWorking hatası. RequestNo={RequestNo}", dto?.RequestNo);

                return ResponseModel<WorkingStatusDto>.Fail(
                    $"Çalışma başlatılırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
        public async Task<ResponseModel<FinishWorkingResultDto>> FinishWorking(FinishWorkingDto dto)
        {
            try
            {
                if (dto is null || string.IsNullOrWhiteSpace(dto.RequestNo))
                    return ResponseModel<FinishWorkingResultDto>.Fail("Talep numarası zorunludur.", StatusCode.BadRequest);

                var session = await _uow.Repository
                    .GetQueryable<YkbTechnicalServiceWorkSession>()
                    .FirstOrDefaultAsync(x =>
                        x.RequestNo == dto.RequestNo &&
                        x.IsActive &&
                        !x.IsCompleted &&
                        !x.IsDeleted);

                if (session is null)
                    return ResponseModel<FinishWorkingResultDto>.Fail("Aktif çalışma kaydı bulunamadı.", StatusCode.NotFound);

                var context = await GetTechnicalServiceContextAsync(dto.RequestNo);

                if (context is null)
                    return ResponseModel<FinishWorkingResultDto>.Fail("Akış / servis talebi / müşteri / teknik servis bilgisi bulunamadı.", StatusCode.NotFound);

                var (wf, request, customer, technicalService) = context.Value;

                var accessToken = await _manitouApiService.LoginAsync();

                if (string.IsNullOrWhiteSpace(accessToken))
                    return ResponseModel<FinishWorkingResultDto>.Fail("Manitou oturum anahtarı alınamadı.", StatusCode.Error);

                var zones = await _manitouApiService.QuerySystemTestAsync(
                    accessToken,
                    session.SerialNo);

                var receivedZones = GetReceivedZones(zones);
                var missingZones = GetMissingZones(zones);

                if (missingZones.Count > 0 && !dto.ForceFinish)
                {
                    return ResponseModel<FinishWorkingResultDto>.Success(
                        new FinishWorkingResultDto
                        {
                            RequestNo = dto.RequestNo,
                            SerialNo = session.SerialNo,
                            IsFinished = false,
                            NeedConfirmation = true,
                            Message = "Uyarı, bütün bölgelerden alarm göndermediniz. Yine de çalışmayı bitirmek istiyor musunuz?",
                            ReceivedZones = receivedZones,
                            MissingZones = missingZones
                        },
                        "Eksik alarm bölgesi var. Kullanıcı onayı gerekiyor.",
                        StatusCode.Ok);
                }

                var nowUtc = DateTimeOffset.UtcNow;

                var outOfServiceRecords = await _manitouApiService.GetOutOfServiceAsync(
                        accessToken,
                        session.SerialNo);

                var relatedOutOfServiceRecord = GetRelatedOutOfServiceRecord(
                    outOfServiceRecords,
                    session.SerialNo,
                    dto.RequestNo);

                var logSequence = relatedOutOfServiceRecord?.LogSequence
                                  ?? session.ManitouLogSequence
                                  ?? 0;

                if (logSequence <= 0)
                {
                    return ResponseModel<FinishWorkingResultDto>.Fail(
                        "Manitou çalışma kaydı logSequence bilgisi bulunamadı. Test kapatılamadı.",
                        StatusCode.Error);
                }

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                var technicianName =
                      me?.TechnicianName ??
                      me?.Name ??
                      me?.Email ??
                      "Bilinmeyen Teknisyen";

                var finishDescription = BuildManitouTestDescription(
                       dto.RequestNo,
                       technicianName,
                       "bitirildi");
                await _manitouApiService.SetCustomerOffTestAsync(
                    accessToken,
                    new ManitouOffTestRequest
                    {
                        SerialNo = session.SerialNo,
                        LogSequence = logSequence,
                        Description = finishDescription,
                        IsNew = false,
                        UtcFrom = ToManitouUtcText(session.StartedAtUtc),
                        UtcTo = ToManitouUtcText(nowUtc)
                    });

                if (missingZones.Count > 0)
                {
                    await SendMissingZoneWarningMailAsync(
                        technicianName,
                        customer.SubscriberCompany ?? customer.ContactName1 ?? "-",
                        dto.RequestNo,
                        receivedZones,
                        missingZones);
                }


                session.IsActive = false;
                session.IsCompleted = true;
                session.FinishedAtUtc = nowUtc;
                session.ManitouLogSequence = logSequence;
                session.HasMissingZoneOnFinish = missingZones.Count > 0;
                session.ReceivedZonesText = string.Join(",", receivedZones);
                session.MissingZonesText = string.Join(",", missingZones);
                session.FinishDescription = missingZones.Count > 0
                    ? "Eksik zone ile kullanıcı onayı sonrası çalışma bitirildi."
                    : "Tüm zonlardan alarm alındı. Çalışma bitirildi.";
                session.UpdatedDate = DateTime.Now;
                session.UpdatedUser = meId;

                _uow.Repository.Update(session);

                await _activationRecord.LogYkbAsync(
                    WorkFlowActionType.TechnicalServiceFinished,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "TS",
                    "TS",
                    "Manitou çalışma/test bitirildi",
                    new
                    {
                        SerialNo = session.SerialNo,
                        ReceivedZones = receivedZones,
                        MissingZones = missingZones,
                        ForceFinish = dto.ForceFinish,
                        FinishedAtUtc = nowUtc
                    });

                await _uow.Repository.CompleteAsync();

                return ResponseModel<FinishWorkingResultDto>.Success(
                    new FinishWorkingResultDto
                    {
                        RequestNo = dto.RequestNo,
                        SerialNo = session.SerialNo,
                        IsFinished = true,
                        NeedConfirmation = false,
                        Message = "Çalışma başarıyla bitirildi. Müşteri test modundan çıkarıldı.",
                        ReceivedZones = receivedZones,
                        MissingZones = missingZones
                    },
                    "Çalışma başarıyla bitirildi.",
                    StatusCode.Ok);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinishWorking hatası. RequestNo={RequestNo}", dto?.RequestNo);

                return ResponseModel<FinishWorkingResultDto>.Fail(
                    $"Çalışma bitirilirken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
        private async Task<(YkbWorkFlow wf, YkbServicesRequest request, Customer customer, YkbTechnicalService technicalService)?> GetTechnicalServiceContextAsync(string requestNo)
        {
            var wf = await _uow.Repository
                .GetQueryable<YkbWorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (wf is null)
                return null;

            var request = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
                .Include(x => x.Customer)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (request is null || request.Customer is null)
                return null;

            var technicalService = await _uow.Repository
                .GetQueryable<YkbTechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (technicalService is null)
                return null;

            return (wf, request, request.Customer, technicalService);
        }
        private static string ToManitouUtcText(DateTimeOffset value)
        {
            return value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        private static List<string> GetReceivedZones(List<ManitouSystemTestZoneResult> zones)
        {
            return zones
                .Where(x => x.TestSignalCount > 0)
                .Select(x => string.IsNullOrWhiteSpace(x.ZoneId) ? "-" : x.ZoneId!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
        private static List<string> GetMissingZones(List<ManitouSystemTestZoneResult> zones)
        {
            return zones
                .Where(x => x.TestSignalCount <= 0)
                .Select(x => string.IsNullOrWhiteSpace(x.ZoneId) ? "-" : x.ZoneId!)
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }
        public async Task<ResponseModel<WorkingStatusDto>> GetWorkingStatus(string requestNo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(requestNo))
                    return ResponseModel<WorkingStatusDto>.Fail("Talep numarası zorunludur.", StatusCode.BadRequest);

                var context = await GetTechnicalServiceContextAsync(requestNo);

                if (context is null)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Akış / servis talebi / müşteri / teknik servis bilgisi bulunamadı.",
                        StatusCode.BadRequest);
                }

                var (_, _, customer, _) = context.Value;

                var isManitouTestEnabled =
                    await IsManitouTechnicalServiceTestEnabledAsync(customer.TenantId);

                if (!isManitouTestEnabled)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Manitou test alma özelliği bu tenant için aktif değildir.",
                        StatusCode.BadRequest);
                }

                var session = await _uow.Repository
                    .GetQueryable<YkbTechnicalServiceWorkSession>()
                    .AsNoTracking()
                    .OrderByDescending(x => x.Id)
                    .FirstOrDefaultAsync(x =>
                        x.RequestNo == requestNo &&
                        x.IsActive &&
                        !x.IsCompleted &&
                        !x.IsDeleted);

                if (session is null)
                    return ResponseModel<WorkingStatusDto>.Fail("Aktif çalışma kaydı bulunamadı.", StatusCode.NotFound);

                var accessToken = await _manitouApiService.LoginAsync();

                if (string.IsNullOrWhiteSpace(accessToken))
                    return ResponseModel<WorkingStatusDto>.Fail("Manitou oturum anahtarı alınamadı.", StatusCode.Error);

                var zones = await _manitouApiService.QuerySystemTestAsync(
                    accessToken,
                    session.SerialNo);

                var activity = await _manitouApiService.GetCustomerActivityAsync(
                    accessToken,
                    session.SerialNo,
                    days: 1);

                var receivedZones = GetReceivedZones(zones);
                var missingZones = GetMissingZones(zones);

                var remainingSeconds = Convert.ToInt64(
                    Math.Max(0, (session.PlannedEndAtUtc - DateTimeOffset.UtcNow).TotalSeconds));

                var result = new WorkingStatusDto
                {
                    RequestId = session.WorkFlowId,
                    RequestNo = session.RequestNo,
                    SerialNo = session.SerialNo,
                    IsActive = session.IsActive,
                    IsCompleted = session.IsCompleted,
                    StartedAtUtc = session.StartedAtUtc,
                    PlannedEndAtUtc = session.PlannedEndAtUtc,
                    RemainingSeconds = remainingSeconds,
                    ExtendCount = session.ExtendCount,
                    Zones = zones,
                    Activity = activity,
                    ReceivedZones = receivedZones,
                    MissingZones = missingZones
                };

                return ResponseModel<WorkingStatusDto>.Success(
                    result,
                    "Çalışma durumu getirildi.",
                    StatusCode.Ok);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetWorkingStatus hatası. RequestNo={RequestNo}", requestNo);

                return ResponseModel<WorkingStatusDto>.Fail(
                    $"Çalışma durumu alınırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
        public async Task<ResponseModel<WorkingStatusDto>> ExtendWorking(ExtendWorkingDto dto)
        {
            try
            {
                if (dto is null || string.IsNullOrWhiteSpace(dto.RequestNo))
                    return ResponseModel<WorkingStatusDto>.Fail("Talep numarası zorunludur.", StatusCode.BadRequest);

                if (dto.ExtendMinutes <= 0)
                    dto.ExtendMinutes = 30;

                var session = await _uow.Repository
                    .GetQueryable<YkbTechnicalServiceWorkSession>()
                    .FirstOrDefaultAsync(x =>
                        x.RequestNo == dto.RequestNo &&
                        x.IsActive &&
                        !x.IsCompleted &&
                        !x.IsDeleted);

                if (session is null)
                    return ResponseModel<WorkingStatusDto>.Fail("Aktif çalışma kaydı bulunamadı.", StatusCode.NotFound);

                var context = await GetTechnicalServiceContextAsync(dto.RequestNo);

                if (context is null)
                    return ResponseModel<WorkingStatusDto>.Fail("Akış / servis talebi / müşteri / teknik servis bilgisi bulunamadı.", StatusCode.NotFound);

                var (wf, request, customer, technicalService) = context.Value;

                var isManitouTestEnabled = await IsManitouTechnicalServiceTestEnabledAsync(customer.TenantId);

                if (!isManitouTestEnabled)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Manitou test alma özelliği bu tenant için aktif değildir.",
                        StatusCode.NotFound);
                }

                var accessToken = await _manitouApiService.LoginAsync();

                if (string.IsNullOrWhiteSpace(accessToken))
                    return ResponseModel<WorkingStatusDto>.Fail("Manitou oturum anahtarı alınamadı.", StatusCode.Error);

                var nowUtc = DateTimeOffset.UtcNow;

                // Süre henüz bitmediyse uzatma yapılmasın.
                // Frontend butonu zaten bu durumda pasif göstermeli.
                if (session.PlannedEndAtUtc > nowUtc)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Çalışma süresi henüz dolmadı. Süre dolduktan sonra uzatma yapılabilir.",
                        StatusCode.Conflict);
                }

                if (dto.ExtendMinutes <= 0)
                    dto.ExtendMinutes = 30;

                // Manitou tek işlemde en fazla 1 saat kabul ediyor.
                if (dto.ExtendMinutes > 60)
                {
                    return ResponseModel<WorkingStatusDto>.Fail(
                        "Manitou üzerinde çalışma süresi tek işlemde en fazla 60 dakika uzatılabilir.",
                        StatusCode.BadRequest);
                }

                // Yeni çalışma periyodu şu andan itibaren başlar.
                var newStartUtc = nowUtc;
                var newEndUtc = newStartUtc.AddMinutes(dto.ExtendMinutes);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                var technicianName =
                      me?.TechnicianName ??
                      me?.Name ??
                      me?.Email ??
                      "Bilinmeyen Teknisyen";



                var extendDescription = BuildManitouTestDescription(
                dto.RequestNo,
                technicianName,
                "uzatıldı");

                await _manitouApiService.SetCustomerOnTestAsync(
                    accessToken,
                    new ManitouOnTestRequest
                    {
                        SerialNo = session.SerialNo,
                        Description = extendDescription,
                        UtcFrom = ToManitouUtcText(newStartUtc),
                        UtcTo = ToManitouUtcText(newEndUtc),
                        IsNew = false
                    });

                var outOfServiceRecords = await _manitouApiService.GetOutOfServiceAsync(
                        accessToken,
                        session.SerialNo);

                var relatedOutOfServiceRecord = GetRelatedOutOfServiceRecord(
                    outOfServiceRecords,
                    session.SerialNo,
                    dto.RequestNo);

                if (relatedOutOfServiceRecord is not null)
                {
                    session.ManitouLogSequence = relatedOutOfServiceRecord.LogSequence;
                }

                session.PlannedEndAtUtc = newEndUtc;
                session.ExtendCount += 1;
                session.UpdatedDate = DateTime.Now;
                session.UpdatedUser = meId;

                _uow.Repository.Update(session);

                await _activationRecord.LogYkbAsync(
                    WorkFlowActionType.TechnicalServiceStarted,
                    dto.RequestNo,
                    wf.Id,
                    request.CustomerId,
                    "TS",
                    "TS",
                    "Manitou çalışma süresi uzatıldı",
                    new
                    {
                        SerialNo = session.SerialNo,
                        ExtendMinutes = dto.ExtendMinutes,
                        NewEndUtc = newEndUtc
                    });

                await _uow.Repository.CompleteAsync();

                return await GetWorkingStatus(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExtendWorking hatası. RequestNo={RequestNo}", dto?.RequestNo);

                return ResponseModel<WorkingStatusDto>.Fail(
                    $"Çalışma uzatılırken hata oluştu: {ex.Message}",
                    StatusCode.Error);
            }
        }
        private async Task SendMissingZoneWarningMailAsync(string technicianName, string customerName, string requestNo, List<string> receivedZones, List<string> missingZones)
        {
            var me = await _currentUser.GetAsync();

            // Listelerin null olma ihtimalini ve dolu olup olmadığını kontrol ediyoruz.
            bool hasReceived = receivedZones?.Count > 0;
            bool hasMissing = missingZones?.Count > 0;

            string receivedText = hasReceived ? string.Join(", ", receivedZones) : string.Empty;
            string missingText = hasMissing ? string.Join(", ", missingZones) : string.Empty;

            string messageDetail;

            // Senaryolara göre cümlenin aksiyon bildiren kısmını oluşturuyoruz.
            if (hasReceived && hasMissing)
            {
                // 1. Senaryo: Hem alınan hem de eksik bölgeler var.
                messageDetail = $"{receivedText} bölgelerinden alarm aldı fakat {missingText} bölgelerinden alarm almadı.";
            }
            else if (!hasReceived && hasMissing)
            {
                // 2. Senaryo: Hiç alarm alınmadı ama eksik/beklenen bölgeler var. (Senin bahsettiğin senaryo)
                messageDetail = $"hiçbir bölgeden alarm almadı. {missingText} bölgelerinden alarm bekleniyor.";
            }
            else if (hasReceived && !hasMissing)
            {
                // 3. Senaryo: Alarmlar geldi, eksik bölge yok.
                messageDetail = $"{receivedText} bölgelerinden alarm aldı. Eksik veya beklenen alarm bölgesi bulunmamaktadır.";
            }
            else
            {
                // 4. Senaryo: İki liste de boş.
                messageDetail = $"hiçbir bölgeden alarm almadı. Sistemde beklenen eksik alarm bölgesi de bulunmamaktadır.";
            }

            var subject = $"Eksik alarm bölgesi ile çalışma bitirildi - {requestNo}";

            // Ana gövde ile dinamik oluşturduğumuz detayı birleştiriyoruz.
            var body = $"{technicianName}, {customerName} müşterisinde {requestNo} talebinde yaptığı çalışmada {messageDetail}";

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
            {
                _logger.LogWarning(
                    "Eksik zone uyarı maili gönderilemedi. TechnicalServiceManagerEmails tanımlı değil. RequestNo={RequestNo}",
                    requestNo);

                return;
            }

            await _mailPush.EnqueueAsync(new MailOutbox
            {
                RequestNo = requestNo,
                FromStepCode = "TS",
                ToStepCode = "TS",
                ToRecipients = string.Join(";", managerMails),
                Subject = subject,
                BodyHtml = body,
                CreatedUser = me?.Id
            });
        }
        private static string BuildManitouTestDescription(string requestNo, string technicianName, string action)
        {
            return
                $"FlowAssist YKB Teknik Servis Testi [FA:{requestNo}] - " +
                $"{technicianName} tarafından {action}.";
        }
        private static ManitouOutOfServiceResult? GetRelatedOutOfServiceRecord(IEnumerable<ManitouOutOfServiceResult> records, int serialNo, string requestNo)
        {
            var requestMarker = $"[FA:{requestNo}]";

            var customerRecords = records
                .Where(x => x.SerialNo == serialNo)
                .Where(x => x.AdvancedOnTest)
                .Where(x => x.LogSequence > 0)
                .ToList();

            if (customerRecords.Count == 0)
                return null;

            // Sadece bu FlowAssist talebine ait kaydı bul.
            return customerRecords
                .Where(x => !string.IsNullOrWhiteSpace(x.Description))
                .Where(x => x.Description!.Contains(
                    requestMarker,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.UtcTo ?? DateTime.MinValue)
                .ThenByDescending(x => x.LogSequence)
                .FirstOrDefault();
        }
        private async Task<(bool CanStart, StatusCode StatusCode, string? Message)> CompleteExpiredCustomerWorkingBeforeNewStartAsync(long customerId, string newRequestNo, string accessToken)
        {
            var nowUtc = DateTimeOffset.UtcNow;

            // Aynı müşterinin başka bir talebindeki aktif çalışma kaydını bul.
            var activeCustomerSession = await _uow.Repository
                .GetQueryable<YkbTechnicalServiceWorkSession>()
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(x =>
                    x.CustomerId == customerId &&
                    x.IsActive &&
                    !x.IsCompleted &&
                    !x.IsDeleted);

            // Aktif kayıt yoksa yeni çalışma serbest.
            if (activeCustomerSession is null)
                return (true, StatusCode.Ok, null);

            // Aynı talebin aktif kaydı varsa StartWorking zaten yukarıda GetWorkingStatus dönecek.
            if (string.Equals(
                    activeCustomerSession.RequestNo,
                    newRequestNo,
                    StringComparison.OrdinalIgnoreCase))
            {
                return (true, StatusCode.Ok, null);
            }

            // Süresi devam eden başka bir test varsa yeni test başlatılamaz.
            if (activeCustomerSession.PlannedEndAtUtc > nowUtc)
            {
                return (
                    false,
                    StatusCode.Conflict,
                    $"Bu müşteri için '{activeCustomerSession.RequestNo}' numaralı talepte aktif bir çalışma bulunmaktadır. " +
                    $"Planlanan bitiş zamanı: {activeCustomerSession.PlannedEndAtUtc:dd.MM.yyyy HH:mm}.");
            }

            // Buraya geldiyse başka talepteki çalışma aktif görünmekte,
            // ancak planlanan süresi dolmuş.
            var zones = await _manitouApiService.QuerySystemTestAsync(
                accessToken,
                activeCustomerSession.SerialNo);

            var receivedZones = GetReceivedZones(zones);
            var missingZones = GetMissingZones(zones);

            var outOfServiceRecords = await _manitouApiService.GetOutOfServiceAsync(
                accessToken,
                activeCustomerSession.SerialNo);

            var relatedOutOfServiceRecord = GetRelatedOutOfServiceRecord(
                outOfServiceRecords,
                activeCustomerSession.SerialNo,
                activeCustomerSession.RequestNo);

            var logSequence = relatedOutOfServiceRecord?.LogSequence
                              ?? activeCustomerSession.ManitouLogSequence
                              ?? 0;

            if (logSequence <= 0)
            {
                return (
                    false,
                    StatusCode.Error,
                    $"'{activeCustomerSession.RequestNo}' numaralı süresi dolmuş çalışma için " +
                    "Manitou logSequence bilgisi bulunamadı. Yeni çalışma başlatılamadı.");
            }

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var technicianName =
                me?.TechnicianName ??
                me?.Name ??
                me?.Email ??
                "Bilinmeyen Teknisyen";

            var autoFinishDescription = BuildManitouTestDescription(
                activeCustomerSession.RequestNo,
                technicianName,
                $"planlanan süresi dolduğu için '{newRequestNo}' talebi başlatılmadan önce otomatik bitirildi");

            // Önce Manitou tarafında test modundan çıkar.
            await _manitouApiService.SetCustomerOffTestAsync(
                accessToken,
                new ManitouOffTestRequest
                {
                    SerialNo = activeCustomerSession.SerialNo,
                    LogSequence = logSequence,
                    Description = autoFinishDescription,
                    IsNew = false,
                    UtcFrom = ToManitouUtcText(activeCustomerSession.StartedAtUtc),
                    UtcTo = ToManitouUtcText(nowUtc)
                });

            // Eski oturumu DB tarafında kapat.
            activeCustomerSession.IsActive = false;
            activeCustomerSession.IsCompleted = true;
            activeCustomerSession.FinishedAtUtc = nowUtc;
            activeCustomerSession.ManitouLogSequence = logSequence;
            activeCustomerSession.HasMissingZoneOnFinish = missingZones.Count > 0;
            activeCustomerSession.ReceivedZonesText = string.Join(",", receivedZones);
            activeCustomerSession.MissingZonesText = string.Join(",", missingZones);
            activeCustomerSession.FinishDescription =
                $"Planlanan çalışma süresi dolduğu için '{newRequestNo}' talebi başlatılmadan önce otomatik bitirildi.";
            activeCustomerSession.UpdatedDate = DateTime.Now;
            activeCustomerSession.UpdatedUser = meId;

            _uow.Repository.Update(activeCustomerSession);

            await _activationRecord.LogYkbAsync(
                WorkFlowActionType.TechnicalServiceFinished,
                activeCustomerSession.RequestNo,
                activeCustomerSession.WorkFlowId,
                activeCustomerSession.CustomerId,
                "TS",
                "TS",
                "Manitou çalışma/test süresi dolduğu için otomatik bitirildi",
                new
                {
                    SerialNo = activeCustomerSession.SerialNo,
                    StartedAtUtc = activeCustomerSession.StartedAtUtc,
                    PlannedEndAtUtc = activeCustomerSession.PlannedEndAtUtc,
                    AutoFinishedAtUtc = nowUtc,
                    NewRequestNo = newRequestNo,
                    ReceivedZones = receivedZones,
                    MissingZones = missingZones
                });

            // Eski kaydın gerçekten kapanmış olması önemli.
            // Yeni StartWorking kaydı açılmadan önce DB'ye yazıyoruz.
            await _uow.Repository.CompleteAsync();

            return (true, StatusCode.Ok, null);
        }
        private async Task<(bool Success, string? ErrorMessage)> ForceFinishActiveWorkingByRequestNoAsync(string requestNo, string reason)
        {
            var activeSession = await _uow.Repository
                .GetQueryable<YkbTechnicalServiceWorkSession>()
                .FirstOrDefaultAsync(x =>
                    x.RequestNo == requestNo &&
                    x.IsActive &&
                    !x.IsCompleted &&
                    !x.IsDeleted);

            // Aktif çalışma yoksa normal şekilde devam edilebilir.
            if (activeSession is null)
                return (true, null);

            return await CloseActiveWorkingSessionAsync(activeSession, reason);
        }
        private async Task<(bool Success, string? ErrorMessage)> CloseActiveWorkingSessionAsync(YkbTechnicalServiceWorkSession session, string reason)
        {
            try
            {
                var nowUtc = DateTimeOffset.UtcNow;

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;

                var technicianName =
                    me?.TechnicianName ??
                    me?.Name ??
                    me?.Email ??
                    "Bilinmeyen Teknisyen";

                var accessToken = await _manitouApiService.LoginAsync();

                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return (
                        false,
                        $"'{session.RequestNo}' numaralı çalışmanın kapatılması için Manitou oturum anahtarı alınamadı.");
                }
                List<ManitouSystemTestZoneResult> zones = new();
                var zoneInfoAvailable = true;

                try
                {
                    zones = await _manitouApiService.QuerySystemTestAsync(
                        accessToken,
                        session.SerialNo);
                }
                catch (Exception ex)
                {
                    zoneInfoAvailable = false;

                    _logger.LogWarning(
                        ex,
                        "Zorunlu çalışma kapatma sırasında zone bilgisi alınamadı. RequestNo={RequestNo}, SerialNo={SerialNo}",
                        session.RequestNo,
                        session.SerialNo);
                }

                var receivedZones = zoneInfoAvailable
                    ? GetReceivedZones(zones)
                    : new List<string>();

                var missingZones = zoneInfoAvailable
                    ? GetMissingZones(zones)
                    : new List<string>();


                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    return (
                        false,
                        $"'{session.RequestNo}' numaralı çalışmanın kapatılması için Manitou oturum anahtarı alınamadı.");
                }

                // Önce session'da saklanan logSequence kullanılır.
                // Yoksa Manitou OutOfService listesinden bulunmaya çalışılır.
                var logSequence = session.ManitouLogSequence ?? 0;

                if (logSequence <= 0)
                {
                    var outOfServiceRecords = await _manitouApiService.GetOutOfServiceAsync(
                        accessToken,
                        session.SerialNo);

                    var relatedOutOfServiceRecord = GetRelatedOutOfServiceRecord(
                        outOfServiceRecords,
                        session.SerialNo,
                        session.RequestNo);

                    logSequence = relatedOutOfServiceRecord?.LogSequence ?? 0;
                }

                if (logSequence <= 0)
                {
                    return (
                        false,
                        $"'{session.RequestNo}' numaralı aktif çalışma için Manitou logSequence bilgisi bulunamadı. " +
                        "Çalışma kapatılamadığı için işleme devam edilmedi.");
                }

                var finishDescription = BuildManitouTestDescription(
                    session.RequestNo,
                    technicianName,
                    reason);

                await _manitouApiService.SetCustomerOffTestAsync(
                    accessToken,
                    new ManitouOffTestRequest
                    {
                        SerialNo = session.SerialNo,
                        LogSequence = logSequence,
                        Description = finishDescription,
                        IsNew = false,
                        UtcFrom = ToManitouUtcText(session.StartedAtUtc),
                        UtcTo = ToManitouUtcText(nowUtc)
                    });

                session.IsActive = false;
                session.IsCompleted = true;
                session.FinishedAtUtc = nowUtc;
                session.ManitouLogSequence = logSequence;
                session.HasMissingZoneOnFinish = zoneInfoAvailable && missingZones.Count > 0;
                session.ReceivedZonesText = zoneInfoAvailable
                    ? string.Join(",", receivedZones)
                    : null;
                session.MissingZonesText = zoneInfoAvailable
                    ? string.Join(",", missingZones)
                    : null;
                session.FinishDescription = zoneInfoAvailable
                    ? reason
                    : $"{reason} Zone bilgisi Manitou'dan alınamadı.";
                session.UpdatedDate = DateTime.Now;
                session.UpdatedUser = meId;

                _uow.Repository.Update(session);

                await _activationRecord.LogYkbAsync(
                    WorkFlowActionType.TechnicalServiceFinished,
                    session.RequestNo,
                    session.WorkFlowId,
                    session.CustomerId,
                    "TS",
                    "TS",
                    "Manitou çalışma/test zorunlu olarak bitirildi",
                    new
                    {
                        session.SerialNo,
                        session.StartedAtUtc,
                        session.PlannedEndAtUtc,
                        FinishedAtUtc = nowUtc,
                        Reason = reason,
                        ZoneInfoAvailable = zoneInfoAvailable,
                        ReceivedZones = receivedZones,
                        MissingZones = missingZones
                    });

                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Aktif çalışma zorunlu kapatma hatası. RequestNo={RequestNo}, SerialNo={SerialNo}",
                    session.RequestNo,
                    session.SerialNo);

                return (
                    false,
                    $"'{session.RequestNo}' numaralı aktif çalışma kapatılırken hata oluştu: {ex.Message}");
            }
        }
        private async Task<bool> IsManitouTechnicalServiceTestEnabledAsync(long? tenantId, CancellationToken cancellationToken = default)
        {
            if (!tenantId.HasValue || tenantId.Value <= 0)
                return false;

            return await _uow.Repository
                .GetQueryable<Tenant>()
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Id == tenantId.Value &&
                    x.Code == CommonConstants.ManitouTestTenantCodeYKB &&
                    x.IsTechnicalServiceTestEnabled,
                    cancellationToken);
        }


        
        // Kontrol ve Onaylama adımında dosya yükleme /silme/değiştirme işlemleri için gerekli validasyonlar ve fiziksel dosya yönetimi.
        private sealed class WorkflowAttachmentChangeSet
        {
            public List<string> NewStoredFileNames { get; } = new();

            public List<string> OldStoredFileNames { get; } = new();
        }
        private sealed class WorkflowAttachmentSettings
        {
            public int MaxFileCount { get; init; }

            public long MaxFileSizeMb { get; init; }

            public long MaxFileSizeBytes { get; init; }

            public HashSet<string> AllowedExtensions { get; init; } =
                new(StringComparer.OrdinalIgnoreCase);
        }
       
        private static void ValidateWorkflowAttachment(IFormFile file, WorkflowAttachmentSettings settings)
        {
            if (file is null || file.Length <= 0)
                throw new InvalidDataException("Boş dosya yüklenemez.");

            if (file.Length > settings.MaxFileSizeBytes)
            {
                throw new InvalidDataException(
                    $"{Path.GetFileName(file.FileName)} dosyası " +
                    $"{settings.MaxFileSizeMb} MB sınırını aşamaz.");
            }

            var originalFileName = Path.GetFileName(file.FileName);

            if (string.IsNullOrWhiteSpace(originalFileName))
                throw new InvalidDataException("Dosya adı geçersiz.");

            var extension = NormalizeFileExtension(
                Path.GetExtension(originalFileName));

            if (string.IsNullOrWhiteSpace(extension) ||
                !settings.AllowedExtensions.Contains(extension))
            {
                var allowedExtensionText = string.Join(
                    ", ",
                    settings.AllowedExtensions.OrderBy(x => x));

                throw new InvalidDataException(
                    $"Desteklenmeyen dosya türü: {originalFileName}. " +
                    $"Desteklenen türler: {allowedExtensionText}");
            }
        }
        private static string GetWorkflowAttachmentUploadRoot()
        {
            var uploadRoot = Path.Combine(
                Directory.GetCurrentDirectory(),
                "UploadsStorage");

            Directory.CreateDirectory(uploadRoot);

            return uploadRoot;
        }

        private static async Task<string> SaveWorkflowAttachmentAsync(
            IFormFile file,
            CancellationToken cancellationToken)
        {
            var extension = Path.GetExtension(file.FileName)
                .ToLowerInvariant();

            var storedFileName = $"{Guid.NewGuid():N}{extension}";

            var physicalPath = Path.Combine(
                GetWorkflowAttachmentUploadRoot(),
                storedFileName);

            await using var inputStream = file.OpenReadStream();

            await using var outputStream = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1024 * 64,
                options: FileOptions.Asynchronous |
                         FileOptions.SequentialScan);

            await inputStream.CopyToAsync(
                outputStream,
                1024 * 64,
                cancellationToken);

            return storedFileName;
        }

        private void DeleteWorkflowAttachmentPhysicalFiles(
            IEnumerable<string> storedFileNames)
        {
            var uploadRoot = GetWorkflowAttachmentUploadRoot();

            foreach (var storedFileName in storedFileNames
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct())
            {
                try
                {
                    // Veritabanından gelen değeri doğrudan path olarak kullanmıyoruz.
                    var safeFileName = Path.GetFileName(storedFileName);
                    var physicalPath = Path.Combine(uploadRoot, safeFileName);

                    if (File.Exists(physicalPath))
                        File.Delete(physicalPath);
                }
                catch (Exception ex)
                {
                    // Veritabanı işlemini bozmasın; temizlik ayrıca yapılabilir.
                    _logger.LogWarning(
                        ex,
                        "YKB dosyası fiziksel olarak silinemedi. FileName: {FileName}",
                        storedFileName);
                }
            }
        }

        private async Task<WorkflowAttachmentChangeSet> ApplyWorkflowAttachmentChangesAsync(
                string requestNo,
                IEnumerable<IFormFile>? attachments,
                IEnumerable<long>? deletedAttachmentIds,
                IEnumerable<YkbWorkflowAttachmentReplaceDto>? replacedAttachments,
                string stepCode,
                CancellationToken cancellationToken = default)
        {
            if (stepCode is not ("PRC" or "APR"))
            {
                throw new InvalidDataException(
                    "Dosya değişikliği yalnızca PRC veya APR adımında yapılabilir.");
            }
            var attachmentSettings =await GetWorkflowAttachmentSettings();
            var newFiles = attachments?
                .Where(x => x is not null && x.Length > 0)
                .ToList() ?? new List<IFormFile>();

            var deleteIds = deletedAttachmentIds?
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet() ?? new HashSet<long>();

            var replacements = replacedAttachments?
                .Where(x => x is not null &&
                            x.AttachmentId > 0 &&
                            x.File is not null)
                .GroupBy(x => x.AttachmentId)
                .Select(x => x.First())
                .ToList() ?? new List<YkbWorkflowAttachmentReplaceDto>();

            foreach (var file in newFiles)
                ValidateWorkflowAttachment(file, attachmentSettings);

            foreach (var replacement in replacements)
                ValidateWorkflowAttachment(replacement.File, attachmentSettings);

            var replacementIds = replacements
                .Select(x => x.AttachmentId)
                .ToHashSet();

            if (deleteIds.Overlaps(replacementIds))
            {
                throw new InvalidDataException(
                    "Aynı dosya hem silme hem değiştirme listesinde bulunamaz.");
            }

            var existingAttachments = await _uow.Repository
                .GetQueryable<YkbWorkflowAttachment>()
                .Where(x =>
                    x.RequestNo == requestNo)
                .ToListAsync(cancellationToken);

            var existingIds = existingAttachments
                .Select(x => x.Id)
                .ToHashSet();

            var requestedExistingIds = deleteIds
                .Concat(replacementIds)
                .ToHashSet();

            var invalidIds = requestedExistingIds
                .Except(existingIds)
                .ToList();

            if (invalidIds.Count > 0)
            {
                throw new InvalidDataException(
                    "Silinmek veya değiştirilmek istenen dosyalardan biri bulunamadı.");
            }

            var finalAttachmentCount =
                existingAttachments.Count -
                deleteIds.Count +
                newFiles.Count;
            if (finalAttachmentCount >
                attachmentSettings.MaxFileCount)
            {
                throw new InvalidDataException(
                    $"Bir talebe en fazla " +
                    $"{attachmentSettings.MaxFileCount} adet dosya eklenebilir.");
            }

            var changeSet = new WorkflowAttachmentChangeSet();

            try
            {
                // Mevcut dosyaları yenileriyle değiştir.
                foreach (var replacement in replacements)
                {
                    var entity = existingAttachments.First(
                        x => x.Id == replacement.AttachmentId);

                    var newStoredFileName =
                        await SaveWorkflowAttachmentAsync(
                            replacement.File,
                            cancellationToken);

                    changeSet.NewStoredFileNames.Add(newStoredFileName);
                    changeSet.OldStoredFileNames.Add(entity.StoredFileName);

                    entity.OriginalFileName =
                        Path.GetFileName(replacement.File.FileName);

                    entity.StoredFileName = newStoredFileName;

                    entity.Extension = Path
                        .GetExtension(replacement.File.FileName)
                        .ToLowerInvariant();

                    entity.ContentType =
                        string.IsNullOrWhiteSpace(replacement.File.ContentType)
                            ? "application/octet-stream"
                            : replacement.File.ContentType;

                    entity.SizeBytes = replacement.File.Length;
                    entity.LastUpdatedStepCode = stepCode;

                    _uow.Repository.Update(entity);
                }

                // Yeni dosyaları ekle.
                foreach (var file in newFiles)
                {
                    var storedFileName =
                        await SaveWorkflowAttachmentAsync(
                            file,
                            cancellationToken);

                    changeSet.NewStoredFileNames.Add(storedFileName);

                    var entity = new YkbWorkflowAttachment
                    {
                        RequestNo = requestNo,
                        OriginalFileName = Path.GetFileName(file.FileName),
                        StoredFileName = storedFileName,
                        Extension = Path
                            .GetExtension(file.FileName)
                            .ToLowerInvariant(),
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                            ? "application/octet-stream"
                            : file.ContentType,
                        SizeBytes = file.Length,
                        UploadedStepCode = stepCode,
                        LastUpdatedStepCode = stepCode,
                    };

                    await _uow.Repository.AddAsync(entity);
                }

                // Silinen kayıtları kaldır.
                foreach (var entity in existingAttachments
                             .Where(x => deleteIds.Contains(x.Id)))
                {
                    changeSet.OldStoredFileNames.Add(entity.StoredFileName);
                    _uow.Repository.HardDelete(entity);
                }

                return changeSet;
            }
            catch
            {
                // Henüz DB commit edilmediği için yeni oluşturulan fiziksel
                // dosyaları temizle.
                DeleteWorkflowAttachmentPhysicalFiles(
                    changeSet.NewStoredFileNames);

                throw;
            }
        }

        private async Task<List<YkbWorkflowAttachmentGetDto>> GetWorkflowAttachmentsAsync(
        string requestNo,
        CancellationToken cancellationToken = default)
        {
            var entities = await _uow.Repository
                .GetQueryable<YkbWorkflowAttachment>()
                .AsNoTracking()
                .Where(x =>
                    x.RequestNo == requestNo)
                .ToListAsync(cancellationToken);

            var appSettings =
                ServiceTool.ServiceProvider
                    .GetService<IOptionsSnapshot<AppSettings>>();

            var baseUrl =
                appSettings?.Value.FileUrl?.TrimEnd('/') ?? string.Empty;

            return entities.Select(x =>
            {
                var relativeUrl = $"/uploads/{x.StoredFileName}";

                return new YkbWorkflowAttachmentGetDto
                {
                    Id = x.Id,
                    RequestNo = x.RequestNo,
                    OriginalFileName = x.OriginalFileName,
                    ContentType = x.ContentType,
                    Extension = x.Extension,
                    SizeBytes = x.SizeBytes,
                    UploadedStepCode = x.UploadedStepCode,
                    LastUpdatedStepCode = x.LastUpdatedStepCode,
                    Url = string.IsNullOrWhiteSpace(baseUrl)
                        ? relativeUrl
                        : $"{baseUrl}{relativeUrl}"
                };
            }).ToList();
        }
        private async Task<WorkflowAttachmentSettings> GetWorkflowAttachmentSettings()
        {
            var maxCountValue = await _uow.Repository
               .GetQueryable<Configuration>()
               .AsNoTracking()
               .FirstOrDefaultAsync(x => x.Name == "MaxWorkflowAttachmentCount");

            if (!int.TryParse(
                    maxCountValue?.Value?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var maxFileCount) ||
                maxFileCount <= 0)
            {
                throw new InvalidOperationException(
                    $"MaxWorkflowAttachmentCount " +
                    "parametresi pozitif bir tam sayı olmalıdır.");
            }

            var maxSizeValue = await _uow.Repository
               .GetQueryable<Configuration>()
               .AsNoTracking()
               .FirstOrDefaultAsync(x => x.Name == "MaxWorkflowAttachmentSize");

            if (!long.TryParse(
                    maxSizeValue?.Value?.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var maxFileSizeMb) ||
                maxFileSizeMb <= 0)
            {
                throw new InvalidOperationException(
                    $"MaxWorkflowAttachmentSize  " +
                    "parametresi pozitif bir tam sayı olmalıdır.");
            }

            var allowedExtensionsValue = await _uow.Repository
            .GetQueryable<Configuration>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == "AllowedWorkflowAttachmentExtensions");

            var allowedExtensions = (allowedExtensionsValue?.Value ?? string.Empty)
                .Split(
                    new[] { ';', ',' },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Select(NormalizeFileExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (allowedExtensions.Count == 0)
            {
                throw new InvalidOperationException(
                    $"AllowedWorkflowAttachmentExtensions " +
                    "parametresinde en az bir dosya uzantısı tanımlanmalıdır.");
            }

            long maxFileSizeBytes;

            try
            {
                maxFileSizeBytes = checked(
                    maxFileSizeMb * 1024L * 1024L);
            }
            catch (OverflowException)
            {
                throw new InvalidOperationException(
                    "Dosya boyutu parametresi desteklenen sınırların üzerindedir.");
            }

            return new WorkflowAttachmentSettings
            {
                MaxFileCount = maxFileCount,
                MaxFileSizeMb = maxFileSizeMb,
                MaxFileSizeBytes = maxFileSizeBytes,
                AllowedExtensions = allowedExtensions
            };
        }

        private static string NormalizeFileExtension(string extension)
        {
            extension = extension.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(extension))
                return string.Empty;

            return extension.StartsWith('.')
                ? extension
                : $".{extension}";
        }
    }
}
