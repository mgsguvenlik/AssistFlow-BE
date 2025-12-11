using Azure.Core;
using Business.Interfaces;
using Business.Interfaces.Ykb;
using Business.UnitOfWork;
using ClosedXML.Excel;
using Core.Common;
using Core.Enums;
using Core.Enums.Ykb;
using Core.Settings.Concrete;
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
using Model.Concrete;
using Model.Concrete.Ykb;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.CustomerSystemAssignment;
using Model.Dtos.Notification;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Role;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.TechnicalServiceImage;
using Model.Dtos.WorkFlowDtos.WorkFlowArchive;
using Model.Dtos.WorkFlowDtos.YkbDtos.YkbArchive;
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
using Newtonsoft.Json;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;

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
        private readonly AppDataContext _ctx;


        public YkbWorkFlowService(IUnitOfWork uow, TypeAdapterConfig config, IAuthService authService, IActivationRecordService activationRecord,
            ILogger<YkbWorkFlowService> logger, IMailPushService mailPush, ICurrentUser currentUser, AppDataContext ctx, INotificationService notification)
        {
            _uow = uow;
            _config = config;
            _activationRecord = activationRecord;
            _logger = logger;
            _mailPush = mailPush;
            _currentUser = currentUser;
            _ctx = ctx;
            _notification = notification;

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
                return await GetCustomerFormByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateCustomerForm");
                return ResponseModel<YkbCustomerFormGetDto>.Fail($"CreateCustomerForm Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //0.1 Müşteri Formunun Servis talebine gönderilmesi.  Şimdilik Kullanılmıyor
        public async Task<ResponseModel<YkbServicesRequestGetDto>> SendCustomerFormToService(YkbCustomerFormSendDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                var customerForm = await _uow.Repository.GetQueryable<YkbCustomerForm>()
                   .AsNoTracking()
                   .FirstOrDefaultAsync(s => s.RequestNo == dto.RequestNo);
                if (customerForm is null)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("Müşteri formu bulunamadı.", StatusCode.Conflict);

                // Hedef WorkFlowStep'i Bul
                var targetStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "SR"); // Örn: 'SR' (Services Request) kodu ile başlangıç adımı

                if (targetStep is null)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("İş akışı hedef  adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                // RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("YKB");
                    if (!rn.IsSuccess)
                        return ResponseModel<YkbServicesRequestGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                //WorkFlow getir
                var wf = await _uow.Repository
                    .GetQueryable<YkbWorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("İlgili akış  kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Servis talebi oluşturma 
                var request = customerForm.Adapt<YkbServicesRequest>(_config);

                request.CreatedDate = DateTime.Now;
                request.CreatedUser = meId;
                request.ServicesRequestStatus = ServicesRequestStatus.Draft;
                request.Id = 0;
                await _uow.Repository.AddAsync(request);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
                      WorkFlowActionType.ServiceRequestCreated,
                      request.RequestNo,
                      null,
                      request.CustomerId,
                      targetStep.Code,
                      "SR",
                      "Müşteriden servis talebine form gönderildi",
                      new
                      {
                          dto,
                          request.Id,
                      });
                #endregion

                #region WorkFlow güncelle
                wf.CurrentStepId = targetStep.Id;
                wf.IsAgreement = null;
                wf.UpdatedDate = DateTime.Now;
                wf.UpdatedUser = meId;
                _uow.Repository.Update(wf);
                #endregion

                await _uow.Repository.CompleteAsync();

                return await GetServiceRequestByRequestNoAsync(request.RequestNo);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "CreateRequestAsync");
                return ResponseModel<YkbServicesRequestGetDto>.Fail($"Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
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
            await _uow.Repository.UpdateAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetServiceRequestByRequestNoAsync(entity.RequestNo);
        }

        //1.1 Servis Talebi oluşturma adımı:  MZK Buna gerek yok aslında. 
        public async Task<ResponseModel<YkbServicesRequestGetDto>> CreateRequestAsync(YkbServicesRequestCreateDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                // Başlangıç WorkFlowStep'i Bul
                var initialStep = await _uow.Repository.GetQueryable<YkbWorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "SR"); // Örn: 'SR' (Services Request) kodu ile başlangıç adımı

                if (initialStep is null)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("İş akışı başlangıç adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                // RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("SR");
                    if (!rn.IsSuccess)
                        return ResponseModel<YkbServicesRequestGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                bool exists = await _uow.Repository.GetQueryable<YkbWorkFlow>().Include(x => x.ApproverTechnician).AsNoTracking().AnyAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (exists)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("Aynı akış numarasi ile başka bir kayıt zaten var.", StatusCode.Conflict);

                var serviceTypeExist = await _uow.Repository.GetQueryable<ServiceType>().AsNoTracking().AnyAsync(s => s.Id == dto.ServiceTypeId);
                if (!serviceTypeExist)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("Service tipi bulunamadı.", StatusCode.Conflict);

                var customerExist = await _uow.Repository.GetQueryable<Customer>().AsNoTracking().AnyAsync(c => c.Id == dto.CustomerId);
                if (!customerExist)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("Müşteri bulunamadı.", StatusCode.Conflict);

                var customerApproverExist = dto.CustomerApproverId.HasValue ? await _uow.Repository.GetQueryable<ProgressApprover>().AsNoTracking().AnyAsync(ca => ca.Id == dto.CustomerApproverId.Value) : true;
                if (!customerApproverExist)
                    return ResponseModel<YkbServicesRequestGetDto>.Fail("Müşteri yetkilisi bulunamadı.", StatusCode.Conflict);


                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Servis talebi güncelleme 
                var request = dto.Adapt<YkbServicesRequest>(_config);
                request.CreatedDate = DateTime.Now;
                request.CreatedUser = meId;
                request.ServicesRequestStatus = ServicesRequestStatus.Draft;
                await _uow.Repository.AddAsync(request);
                #endregion

                #region Ürün Ekleme
                // Varsa ürünleri ekle
                if (dto.Products is not null)
                {
                    foreach (var p in dto.Products)
                    {
                        await _uow.Repository.AddAsync(new YkbServicesRequestProduct
                        {
                            RequestNo = request.RequestNo,
                            ProductId = p.ProductId,
                            Quantity = p.Quantity,
                            CustomerId = request.CustomerId
                        });
                    }
                }
                #endregion

                #region  WorkFlow oluştur (aynı RequestNo ile)

                var wf = new YkbWorkFlow
                {
                    RequestNo = request.RequestNo,
                    RequestTitle = "Servis Talebi",
                    Priority = dto.Priority,
                    CurrentStepId = initialStep.Id,
                    CreatedDate = DateTime.Now,
                    CreatedUser = meId,
                    WorkFlowStatus = WorkFlowStatus.Pending,
                    IsAgreement = null,
                    IsLocationValid = dto.IsLocationValid,
                    ApproverTechnicianId = dto.ApproverTechnicianId,
                    CustomerApproverName = dto.CustomerApproverName
                };

                await _uow.Repository.AddAsync(wf);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogYkbAsync(
                      WorkFlowActionType.ServiceRequestCreated,
                      request.RequestNo,
                      null,
                      dto.CustomerId,
                      initialStep.Code,
                      "SR",
                      "Servis talebi oluşturuldu",
                      new
                      {
                          dto,
                          request.Id,
                          Products = dto.Products?.Select(p => new { p.ProductId, p.Quantity })
                      });
                #endregion

                await _uow.Repository.CompleteAsync();

                return await GetServiceRequestByIdAsync(request.Id);
            }
            catch (Exception ex)
            {

                _logger.LogError(ex, "CreateRequestAsync");
                return ResponseModel<YkbServicesRequestGetDto>.Fail($"Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
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
                await _notification.CreateForRoleAsync(
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
                    roleCode: "WAREHOUSE" // sizin depocu rol kodunuz (ResolveWarehouseEmailsAsync'teki gibi)
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

                var exists = await _uow.Repository
                    .GetQueryable<YkbTechnicalService>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);
                if (exists is not null && exists.ServicesStatus != TechnicalServiceStatus.AwaitingReview)
                    return ResponseModel<YkbWarehouseGetDto>.Fail("Aynı akış numarası ile başka bir kayıt zaten var.", StatusCode.Conflict);

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
                            Message = $"Akış {"SR"} → {"TS"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "SR",
                            ToStepCode = "TS",
                            Payload = new { wfId = wf.Id }
                        },
                        wf.ApproverTechnicianId.Value
                    );
                }
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
                #endregion

                #region Lokasyon kontrolü
                if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
                {
                    if (!dto.Longitude.HasValue && !dto.Latitude.HasValue)
                    {
                        return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.BadRequest);
                    }
                    else
                    {
                        var latStr = dto.Latitude.Value.ToString(CultureInfo.InvariantCulture);
                        var lonStr = dto.Longitude.Value.ToString(CultureInfo.InvariantCulture);
                        var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, latStr, lonStr);
                        if (!locationResult.IsSuccess)
                        {
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
                await _notification.CreateForRoleAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} fiyatlamaya gönderildi",
                        Message = $"Akış {"TS"} → {"PRC"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "TS",
                        ToStepCode = "PRC",
                    },
                    roleCode: "PROJECTENGINEER"
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

                #region Ürün Fiyat Sabitleme (4. Adım)
                // 🔹 Artık fiyatı dto.Products listesinden alıyoruz
                await EnsurePricesCapturedFromDtoAsync(dto.RequestNo, dto.Products);
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

                #region Notification Kaydı 
                await _notification.CreateForRoleAsync(
                    new NotificationCreateDto
                    {
                        Type = NotificationType.WorkflowStepChanged,
                        Title = $"Talep {dto.RequestNo} son oanaya  gönderildi",
                        Message = $"Akış {"PRC"} → {"APR"} geçti. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                        RequestNo = dto.RequestNo,
                        FromStepCode = "PRC",
                        ToStepCode = "APR",
                    },
                    roleCode: "PROJECTENGINEER"
                );
                #endregion

                return await GetPricingByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApprovePricing");
                return ResponseModel<YkbPricingGetDto>.Fail($" Fiyatlama onay ve kontrole gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        // 5  Kontrol ve Son Onay (FinalApproval) — CREATE
        public async Task<ResponseModel<YkbFinalApprovalGetDto>> FinalApprovalAsync(YkbFinalApprovalUpdateDto dto)
        {
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

                // 💡 yeni alanlar
                existsFinalApproval.DiscountPercent = dto.DiscountPercent;
                existsFinalApproval.Status = dto.FinalApprovalStatus;

                _uow.Repository.Update(existsFinalApproval);
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

                #region Aeşivleme
                // 🔹 Eğer süreç tamamlandıysa arşive at
                if (dto.WorkFlowStatus == WorkFlowStatus.Complated || dto.WorkFlowStatus == WorkFlowStatus.Cancelled)
                {
                    var reason = dto.WorkFlowStatus == WorkFlowStatus.Complated ? "Completed" : "Cancelled";
                    await ArchiveWorkflowAsync(dto.RequestNo, reason);
                }
                #endregion

                await _uow.Repository.CompleteAsync();

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

                    await _notification.CreateForRoleAsync(
                        new NotificationCreateDto
                        {
                            Type = NotificationType.WorkflowStepChanged,
                            Title = $"Talep {dto.RequestNo} tamamlandı",
                            Message = $"YKB son onayı alındı. Müşteri: {request.Customer?.ContactName1 ?? "-"}",
                            RequestNo = dto.RequestNo,
                            FromStepCode = "CAPR",
                            ToStepCode = "APR",
                        },
                        roleCode: "READ-ONLY" // veya ilgili rol/grup kimse
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
            var now = DateTimeOffset.UtcNow;

            // 1) Ana DTO: SR + (WF last) + Customer (warranty türetmeleri)
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<YkbCustomerForm>().AsNoTracking()
                where sr.RequestNo == requestNo

                // left join: aynı RequestNo’ya sahip ve silinmemiş workflow’lar
                join wf0 in _uow.Repository.GetQueryable<YkbWorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)  // “en güncel” workflow tercih ediliyorsa
                    .Take(1)
                    .DefaultIfEmpty()
                select new YkbCustomerFormGetDto
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
                return ResponseModel<YkbCustomerFormGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);


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

            // 2) Ürünler (tek bağımsız sorgu — sadece ihtiyaç alanlarını seç)
            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    // Ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,

                    Quantity = p.Quantity,

                    // --- EF-translatable EffectivePrice ---
                    // 1) CustomerGroup fiyatı → 2) Customer özel fiyatı → 3) Ürün liste fiyatı → 0
                    EffectivePrice =
                             p.Customer.CustomerGroup.GroupProductPrices
                                 .Where(gp => gp.ProductId == p.ProductId)
                                 .Select(gp => (decimal?)gp.Price)
                                 .FirstOrDefault()
                             ?? p.Customer.CustomerProductPrices
                                 .Where(cp => cp.ProductId == p.ProductId)
                                 .Select(cp => (decimal?)cp.Price)
                                 .FirstOrDefault()
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
            // 🔐 1. Giriş yapan kullanıcı + roller
            var me = await _currentUser.GetAsync();

            var roles = me?.Roles
                .Select(x => x.Code)
                .ToHashSet() ?? new HashSet<string>();

            bool isAdmin = roles.Contains("ADMIN");
            bool isWarehouse = roles.Contains("WAREHOUSE");
            bool isTechnician = roles.Contains("TECHNICIAN") || roles.Contains("SUBCONTRACTOR");
            bool isProjectEngineer = roles.Contains("PROJECTENGINEER");

            var pendingStatus = WorkFlowStatus.Pending;

            // 🧱 2. Role göre filtrelenmiş WorkFlow sorgusu
            var wfBase = _uow.Repository.GetQueryable<YkbWorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (isAdmin || isProjectEngineer)
            {
                // Ek filtre yok; Pending + IsDeleted=false zaten uygulandı.
            }
            else if (isWarehouse)
            {
                wfBase = wfBase.Where(x =>
                    x.CurrentStep != null &&
                    x.CurrentStep.Code == "WH");
            }
            else if (isTechnician)
            {
                wfBase = wfBase.Where(x =>
                    x.CurrentStep != null &&
                    x.CurrentStep.Code == "TS" &&
                    x.ApproverTechnicianId == me.Id);
            }
            else
            {
                // Yetkisi olmayanlar için boş WF seti
                wfBase = wfBase.Where(x => false);
            }

            // Bu kullanıcının görebileceği RequestNo’lar
            var allowedRequestNos = wfBase.Select(x => x.RequestNo);

            // 🧱 3. ServicesRequest base query + include'lar
            var query = _uow.Repository.GetQueryable<YkbServicesRequest>();
            query = RequestIncludes()!(query);


            // WorkFlow ile ilişkiye göre filtre:
            query = query.Where(sr => allowedRequestNos.Contains(sr.RequestNo));

            // 🔍 4. Search filtresi
            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();
                query = query.Where(x =>
                    x.RequestNo.Contains(term) ||
                    (x.Description != null && x.Description.Contains(term)));
            }

            // 📄 5. Toplam kayıt + paging + Mapster
            var total = await query.CountAsync();

            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<YkbServicesRequestGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<YkbServicesRequestGetDto>>
                .Success(new PagedResult<YkbServicesRequestGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByIdAsync(long id)
        {
            var now = DateTimeOffset.UtcNow;

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

            // 2) Ürünler (tek bağımsız sorgu — sadece ihtiyaç alanlarını seç)
            baseDto.ServicesRequestProducts = await _uow.Repository
                     .GetQueryable<YkbServicesRequestProduct>()
                     .AsNoTracking()
                     .Where(p => p.RequestNo == baseDto.RequestNo)
                     .Select(p => new YkbServicesRequestProductGetDto
                     {
                         Id = p.Id,
                         RequestNo = p.RequestNo,
                         ProductId = p.ProductId,

                         // Ürün temel alanları
                         ProductName = p.Product != null ? p.Product.Description : null,
                         ProductCode = p.Product != null ? p.Product.ProductCode : null,
                         ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                         PriceCurrency = p.Product.PriceCurrency,

                         Quantity = p.Quantity,

                         // --- EF-translatable EffectivePrice ---
                         // 1) CustomerGroup fiyatı → 2) Customer özel fiyatı → 3) Ürün liste fiyatı → 0
                         EffectivePrice =
                             p.Customer.CustomerGroup.GroupProductPrices
                                 .Where(gp => gp.ProductId == p.ProductId)
                                 .Select(gp => (decimal?)gp.Price)
                                 .FirstOrDefault()
                             ?? p.Customer.CustomerProductPrices
                                 .Where(cp => cp.ProductId == p.ProductId)
                                 .Select(cp => (decimal?)cp.Price)
                                 .FirstOrDefault()
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

            return ResponseModel<YkbServicesRequestGetDto>.Success(baseDto);
        }

        public async Task<ResponseModel<YkbServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo)
        {
            var now = DateTimeOffset.UtcNow;

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

            // 2) Ürünler (tek bağımsız sorgu — sadece ihtiyaç alanlarını seç)
            baseDto.ServicesRequestProducts = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new YkbServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    // Ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,
                    PriceCurrency = p.Product.PriceCurrency,

                    Quantity = p.Quantity,

                    // --- EF-translatable EffectivePrice ---
                    // 1) CustomerGroup fiyatı → 2) Customer özel fiyatı → 3) Ürün liste fiyatı → 0
                    EffectivePrice =
                             p.Customer.CustomerGroup.GroupProductPrices
                                 .Where(gp => gp.ProductId == p.ProductId)
                                 .Select(gp => (decimal?)gp.Price)
                                 .FirstOrDefault()
                             ?? p.Customer.CustomerProductPrices
                                 .Where(cp => cp.ProductId == p.ProductId)
                                 .Select(cp => (decimal?)cp.Price)
                                 .FirstOrDefault()
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
                    ServicesRequestDescription = sr != null ? sr.Description : null,

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
                    ServicesRequestDescription = sr != null ? sr.Description : null,

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
            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .AsSplitQuery()
                .Include(x => x.YkbServiceRequestFormImages)
                .Include(x => x.YkbServicesImages)
                .Include(x => x.ServiceType)
                .ProjectToType<YkbTechnicalServiceGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<YkbTechnicalServiceGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // --- Customer: ServicesRequest üzerinden tek sorguda projeksiyon ---
            dto.Customer = await _uow.Repository
                .GetQueryable<YkbServicesRequest>()
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
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    // Fiyat sabitlenmiş mi?
                    bool captured = p.IsPriceCaptured;

                    // 1) Birim fiyat
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)          // sabitlenmiş ise buradan
                        : p.GetEffectivePrice();              // sabitlenmemiş ise hesapla

                    // 2) Para birimi
                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    // 3) DTO doldur
                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,

                        // Para birimi: sabitse Captured, değilse Product
                        PriceCurrency = currency,

                        // Ürün fiyatı: ekranda kullanılacak birim fiyat
                        ProductPrice = effectivePrice,

                        // EffectivePrice: her zaman ekranda görünen “esas” fiyat
                        EffectivePrice = effectivePrice,
                        TotalPrice = effectivePrice * p.Quantity
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


            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    // Fiyat sabitlenmiş mi?
                    bool captured = p.IsPriceCaptured;

                    // 1) Birim fiyat
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)          // sabitlenmiş ise buradan
                        : p.GetEffectivePrice();              // sabitlenmemiş ise hesapla

                    // 2) Para birimi
                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    // 3) DTO doldur
                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,

                        // Para birimi: sabitse Captured, değilse Product
                        PriceCurrency = currency,

                        // Ürün fiyatı: ekranda kullanılacak birim fiyat
                        ProductPrice = effectivePrice,

                        // EffectivePrice: her zaman ekranda görünen “esas” fiyat
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

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    // Fiyat sabitlenmiş mi?
                    bool captured = p.IsPriceCaptured;

                    // 1) Birim fiyat
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)          // sabitlenmiş ise buradan
                        : p.GetEffectivePrice();              // sabitlenmemiş ise hesapla

                    // 2) Para birimi
                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    // 3) DTO doldur
                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,

                        // Para birimi: sabitse Captured, değilse Product
                        PriceCurrency = currency,

                        // Ürün fiyatı: ekranda kullanılacak birim fiyat
                        ProductPrice = effectivePrice,

                        // EffectivePrice: her zaman ekranda görünen “esas” fiyat
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
                return ResponseModel<YkbFinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır
            var productEntities = await _uow.Repository
                .GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Where(p => p.RequestNo == dto.RequestNo)
                .ToListAsync();

            dto.Products = productEntities
                .Select(p =>
                {
                    // Fiyat sabitlenmiş mi?
                    bool captured = p.IsPriceCaptured;

                    // 1) Birim fiyat
                    decimal effectivePrice = captured
                        ? (p.CapturedUnitPrice ?? 0m)          // sabitlenmiş ise buradan
                        : p.GetEffectivePrice();              // sabitlenmemiş ise hesapla

                    // 2) Para birimi
                    string? currency = captured
                        ? (p.CapturedCurrency ?? p.Product?.PriceCurrency)
                        : p.Product?.PriceCurrency;

                    // 3) DTO doldur
                    return new YkbServicesRequestProductGetDto
                    {
                        Id = p.Id,
                        RequestNo = p.RequestNo,
                        ProductId = p.ProductId,
                        Quantity = p.Quantity,

                        ProductName = p.Product?.Description,
                        ProductCode = p.Product?.ProductCode,

                        // Para birimi: sabitse Captured, değilse Product
                        PriceCurrency = currency,

                        // Ürün fiyatı: ekranda kullanılacak birim fiyat
                        ProductPrice = effectivePrice,

                        // EffectivePrice: her zaman ekranda görünen “esas” fiyat
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
                        .Select(img => new TechnicalServiceFormImageGetDto
                        {
                            Id = img.Id,
                            Url = NormalizeImageUrl(img.Url) ?? string.Empty,
                            Caption = img.Caption
                        })
                        .ToList();
                }
            }
            // --------------------------------------------------------------------

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
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");

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

        public async Task<ResponseModel<PagedResult<YkbWorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q)
        {

            var me = await _currentUser.GetAsync();

            var roles = me?.Roles.Select(x => x.Code).ToHashSet();

            bool isAdmin = roles?.Contains("ADMIN") ?? false;
            bool isWarehouse = roles?.Contains("WAREHOUSE") ?? false;
            bool isTechnician = roles?.Contains("TECHNICIAN") ?? false;
            bool isSubcontractor = roles?.Contains("SUBCONTRACTOR") ?? false;
            bool isProjectEngineer = roles?.Contains("PROJECTENGINEER") ?? false;
            bool isCustomer = roles?.Contains("CUSTOMER") ?? false;

            var pendingStatus = WorkFlowStatus.Pending;

            var wfBase = _uow.Repository.GetQueryable<YkbWorkFlow>()
                 .Include(x => x.CurrentStep)
                 .AsNoTracking()
                 .Where(x => !x.IsDeleted && x.WorkFlowStatus == pendingStatus);


            if (isAdmin || isProjectEngineer)
            {
                // Ek filtre yok; Pending + IsDeleted=false zaten uygulandı.
            }
            else if (isWarehouse)
            {
                wfBase = wfBase.Where(x => x.CurrentStep != null && x.CurrentStep.Code == "WH");
            }
            else if (isTechnician)
            {
                wfBase = wfBase.Where(x =>
                    x.CurrentStep != null && x.CurrentStep.Code == "TS" &&
                    x.ApproverTechnicianId == me.Id);
            }
            else if (isCustomer)
            {
                wfBase = wfBase.Where(x => x.CurrentStep != null && (x.CurrentStep.Code == "CF" || x.CurrentStep.Code == "CAPR"));
            }

            else
            {
                // Yetkisi olmayanlar için boş sonuç
                wfBase = wfBase.Where(x => false);
            }

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var term = q.Search.Trim();
                wfBase = wfBase.Where(x => x.RequestNo.Contains(term) || x.RequestTitle.Contains(term));
            }

            // LEFT JOIN: WorkFlow.RequestNo == ServicesRequest.RequestNo
            var qJoined =
                from wf in wfBase
                join sr0 in _uow.Repository.GetQueryable<YkbServicesRequest>().AsNoTracking()
                     on wf.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { wf, sr };

            var total = await qJoined.CountAsync();


            var items = await qJoined
                .OrderByDescending(x => x.wf.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(x => new YkbWorkFlowGetDto
                {
                    // WorkFlow alanları
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
                                   : new YkbWorkFlowStepGetDto
                                   {
                                       Id = x.wf.CurrentStep.Id,
                                       Name = x.wf.CurrentStep.Name,
                                       Code = x.wf.CurrentStep.Code
                                   }
                })
                .ToListAsync();

            return ResponseModel<PagedResult<YkbWorkFlowGetDto>>
                .Success(new PagedResult<YkbWorkFlowGetDto>(items, total, q.Page, q.PageSize));
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

            // 3) Ürün satırları (captured-first)
            var lines = await _uow.Repository.GetQueryable<YkbServicesRequestProduct>()
                .AsNoTracking()
                .Include(p => p.Product)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerGroup).ThenInclude(g => g.GroupProductPrices)
                .Include(p => p.Customer)
                    .ThenInclude(c => c.CustomerProductPrices)
                .Where(p => p.RequestNo == requestNo)
                .ToListAsync();

            foreach (var p in lines)
            {
                bool captured = p.IsPriceCaptured;
                decimal unit = captured ? (p.CapturedUnitPrice ?? 0m) : p.GetEffectivePrice();
                string currency = captured ? (p.CapturedCurrency ?? p.Product?.PriceCurrency ?? "TRY")
                                           : (p.Product?.PriceCurrency ?? "TRY");
                decimal total = captured ? (p.CapturedTotal ?? unit * p.Quantity)
                                         : unit * p.Quantity;
                string src = captured
                    ? (p.CapturedSource?.ToString() ?? "Standard")
                    : (p.Customer?.CustomerGroup?.GroupProductPrices?.Any(g => g.ProductId == p.ProductId) == true ? "Group"
                       : p.Customer?.CustomerProductPrices?.Any(c => c.ProductId == p.ProductId) == true ? "Customer"
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
        private async Task<ResponseModel> EnsurePricesCapturedFromDtoAsync(
            string requestNo,
            IEnumerable<YkbServicesRequestProductCreateDto>? productsDto
        )
        {
            // DTO boş ise iş yapma
            var dtoDict = (productsDto ?? Enumerable.Empty<YkbServicesRequestProductCreateDto>())
                .ToDictionary(x => x.ProductId, x => x);

            if (!dtoDict.Any())
                return ResponseModel.Success();

            // İlgili request’in ürünlerini çek
            var list = await _uow.Repository.GetQueryable<YkbServicesRequestProduct>()
                .Include(x => x.Product) // Para birimi vs için
                .Where(x => x.RequestNo == requestNo)
                .ToListAsync();

            if (list.Count == 0)
                return ResponseModel.Success();

            foreach (var p in list)
            {
                // DTO’da karşılığı yoksa o satırı atla (istersen burada 0 fiyat da yazabilirsin)
                if (!dtoDict.TryGetValue(p.ProductId, out var dtoItem))
                    continue;

                // 1) Birim fiyat: artık DTO’dan geliyor
                var unit = dtoItem.Price; // ← DTO’daki Price

                // 2) Para birimi: eskisi gibi ürün tablosundan
                var currency = p.Product?.PriceCurrency ?? "TRY";

                var total = unit * p.Quantity;

                // İstersen CapturedSource için yeni enum (Manual) ekleyebilirsin,
                // şimdilik mevcut enum’lardan birini kullanıyorum.
                p.CapturedSource = CapturedPriceSource.Standard; // veya CapturedPriceSource.CustomerGroupPriceManual vs
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
                    Base64 = await ReadBase64Async(img.Url)
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
                    Base64 = await ReadBase64Async(img.Url)
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
    }
}
