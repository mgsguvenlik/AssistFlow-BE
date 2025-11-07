using Azure.Core;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;
using System.Globalization;
using System.Security.Cryptography;

namespace Business.Services
{
    public class WorkFlowService : IWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMailService _mailService;
        private readonly TypeAdapterConfig _config;
        private readonly IActivationRecordService _activationRecord;
        private readonly ILogger<WorkFlowService> _logger;
        private readonly IMailPushService _mailPush;
        private readonly ICurrentUser _currentUser;
        public WorkFlowService(IUnitOfWork uow, TypeAdapterConfig config, IAuthService authService, IMailService mailService, IActivationRecordService activationRecord, ILogger<WorkFlowService> logger, IMailPushService mailPush, ICurrentUser currentUser)
        {
            _uow = uow;
            _config = config;
            _mailService = mailService;
            _activationRecord = activationRecord;
            _logger = logger;
            _mailPush = mailPush;
            _currentUser = currentUser;
        }

        /// -------------------- ServicesRequest --------------------
        //1 Servis Talebi oluşturma akışı:
        public async Task<ResponseModel<ServicesRequestGetDto>> CreateRequestAsync(ServicesRequestCreateDto dto)
        {
            try
            {
                #region Validasyon/Kontroller
                // Başlangıç WorkFlowStep'i Bul
                var initialStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "SR"); // Örn: 'SR' (Services Request) kodu ile başlangıç adımı

                if (initialStep is null)
                    return ResponseModel<ServicesRequestGetDto>.Fail("İş akışı başlangıç adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                // RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("SR");
                    if (!rn.IsSuccess)
                        return ResponseModel<ServicesRequestGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                bool exists = await _uow.Repository.GetQueryable<WorkFlow>().Include(x => x.ApproverTechnician).AsNoTracking().AnyAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (exists)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Aynı akış numarasi ile başka bir kayıt zaten var.", StatusCode.Conflict);

                var serviceTypeExist = await _uow.Repository.GetQueryable<ServiceType>().AsNoTracking().AnyAsync(s => s.Id == dto.ServiceTypeId);
                if (!serviceTypeExist)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Service tipi bulunamadı.", StatusCode.Conflict);

                var customerExist = await _uow.Repository.GetQueryable<Customer>().AsNoTracking().AnyAsync(c => c.Id == dto.CustomerId);
                if (!customerExist)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Müşteri bulunamadı.", StatusCode.Conflict);

                var customerApproverExist = dto.CustomerApproverId.HasValue ? await _uow.Repository.GetQueryable<ProgressApprover>().AsNoTracking().AnyAsync(ca => ca.Id == dto.CustomerApproverId.Value) : true;
                if (!customerApproverExist)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Müşteri yetkilisi bulunamadı.", StatusCode.Conflict);


                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Servis talebi güncelleme 
                var request = dto.Adapt<ServicesRequest>(_config);
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
                        await _uow.Repository.AddAsync(new ServicesRequestProduct
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

                var wf = new WorkFlow
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
                await _activationRecord.LogAsync(
                      WorkFlowActionType.ServiceRequestCreated,
                      request.RequestNo,
                      null,
                      null,
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
                return ResponseModel<ServicesRequestGetDto>.Fail($"Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //2.1 Depoya Gönderim  (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> SendWarehouseAsync(SendWarehouseDto dto)
        {

            #region Validasyon/Kontroller
            //Talep getir (tracking kapalı)
            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<WarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            if (request.ServicesRequestStatus == ServicesRequestStatus.WarehouseSubmitted)
                return ResponseModel<WarehouseGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);


            var product = await _uow.Repository.GetQueryable<ServicesRequestProduct>(x => x.RequestNo == dto.RequestNo).ToListAsync();
            if (product is null || product.Count == 0)
                return ResponseModel<WarehouseGetDto>.Fail("Bu talep için kayıtlı ürün bulunamadı. Depoya gönderim için en az bir ürün eklenmiş olmalıdır.", StatusCode.BadRequest);

            //WorkFlow getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<WarehouseGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);



            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == "WH");

            //Warehouse kaydını getir (varsa)
            var warehouse = await _uow.Repository
                .GetQueryable<Warehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            #endregion

            #region Depo Ekle/Güncelle
            //Yoksa oluştur
            if (warehouse == null)
            {
                warehouse = new Warehouse
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
            request.WorkFlowStep = null;
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
            await _activationRecord.LogAsync(
                 WorkFlowActionType.WarehouseSent,
                 request.RequestNo,
                 wf.Id,
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

            //Güncel talebi döndür
            return await GetWarehouseByRequestNoAsync(request.RequestNo);
        }

        //2.2 Depo Teslimatı ve Teknik servise Gönderim (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> CompleteDeliveryAsync(CompleteDeliveryDto dto)
        {
            #region Validasyon/Kontroller
            var wf = await _uow.Repository
               .GetQueryable<WorkFlow>()
               .Include(x => x.ApproverTechnician)
               .AsNoTracking()
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            var exists = await _uow.Repository
                .GetQueryable<TechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);
            if (exists is not null && exists.ServicesStatus != TechnicalServiceStatus.AwaitingReview)
                return ResponseModel<WarehouseGetDto>.Fail("Aynı akış numarası ile başka bir kayıt zaten var.", StatusCode.Conflict);

            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<WarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var warehouse = await _uow.Repository
                .GetQueryable<Warehouse>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (warehouse is null)
                return ResponseModel<WarehouseGetDto>.Fail("Depo kaydı bulunamadı.", StatusCode.NotFound);


            // 🔹 WorkFlow güncelle
            var targetStep = await _uow.Repository
                .GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .Where(x => x.Code != null && x.Code == "TS")
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (targetStep is null)
                return ResponseModel<WarehouseGetDto>.Fail("WorkFlowStep içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);


            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            #endregion

            #region Teknik servis kaydı Ekle/Güncelle

            var technicalService = await _uow.Repository
                .GetQueryable<TechnicalService>()
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
                technicalService = new TechnicalService
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
                .GetMultipleAsync<ServicesRequestProduct>(
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
                var newEntity = new ServicesRequestProduct
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
            await _activationRecord.LogAsync(
                    WorkFlowActionType.WorkFlowStepChanged,
                    dto.RequestNo,
                    wf.Id,
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

            // 🔹 Son durumu döndür
            return await GetWarehouseByIdAsync(warehouse.Id);
        }

        //2.3 Teknik Servis Gönderim  (Ürün yok ise)
        public async Task<ResponseModel<TechnicalServiceGetDto>> SendTechnicalServiceAsync(SendTechnicalServiceDto dto)
        {
            #region Validasyonlar/Kontroller

            var wf = await _uow.Repository
              .GetQueryable<WorkFlow>()
              .Include(x => x.ApproverTechnician)
              .AsNoTracking()
              .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);


            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Code == "TS");
            if (targetStep is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);


            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            #endregion

            #region Teknik servis kaydını Ekle/Güncelle
            var technicalService = await _uow.Repository
                 .GetQueryable<TechnicalService>()
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
                technicalService = new TechnicalService
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

            request.ServicesRequestStatus = ServicesRequestStatus.TechnicialServiceSubmitted;

            #region Workflow Güncelle
            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = meId;
            _uow.Repository.Update(wf);
            #endregion

            #region Hareket Kaydı
            await _activationRecord.LogAsync(
                    WorkFlowActionType.WorkFlowStepChanged,
                    dto.RequestNo,
                    wf.Id,
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

            // 🔹 Son durumu döndür
            return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
        }

        // 4️ Teknik Servis Servisi Başlatma 
        public async Task<ResponseModel<TechnicalServiceGetDto>> StartService(StartTechnicalServiceDto dto)
        {

            #region Validasyon/Kontroller
            //WorkFlow getir
            var wf = await _uow.Repository
            .GetQueryable<WorkFlow>()
            .Include(x => x.ApproverTechnician)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);



            var request = await _uow.Repository
               .GetQueryable<ServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var customer = await _uow.Repository
                .GetQueryable<Customer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

            if (customer is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

            var technicalService = await _uow.Repository
                .GetQueryable<TechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            if (technicalService.ServicesStatus == TechnicalServiceStatus.InProgress)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Teknik servis zaten başlatılmış", StatusCode.Conflict);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            #endregion

            #region Lokasyon kontrolü
            if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
            {
                if (string.IsNullOrEmpty(dto.Longitude) && !string.IsNullOrEmpty(dto.Latitude))
                {
                    return ResponseModel<TechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.InvalidCustomerLocation);
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
                           "TS",
                           "TS",
                           "Lokasyon kontrolü başarısız",
                           new { locationResult.Message }
                       );
                        #endregion

                        return ResponseModel<TechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
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
            await _activationRecord.LogAsync(
                WorkFlowActionType.TechnicalServiceStarted,
                dto.RequestNo,
                wf.Id,
                "TS",
                "TS",
                "Teknik servis başlatıldı",
                new { dto.StartLocation, technicalService.Id }
            );

            #endregion

            await _uow.Repository.CompleteAsync();
            return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
        }

        // 5 Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi
        public async Task<ResponseModel<TechnicalServiceGetDto>> FinishService(FinishTechnicalServiceDto dto)
        {

            #region Validasyon/Kontroller
            var wf = await _uow.Repository
               .GetQueryable<WorkFlow>()
               .Include(x => x.ApproverTechnician)
               .AsNoTracking()
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);


            var request = await _uow.Repository
               .GetQueryable<ServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var customer = await _uow.Repository
                .GetQueryable<Customer>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CustomerId);

            if (customer is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili müşteri kaydı bulunamadı.", StatusCode.NotFound);

            var technicalService = await _uow.Repository
                .GetQueryable<TechnicalService>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == "PRC");
            if (targetStep is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (PRC) tanımlı değil.", StatusCode.BadRequest);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            #endregion

            #region Lokasyon kontrolü
            if (technicalService.IsLocationCheckRequired) //Lokasyon kontrolü gerekli ise
            {
                if (!dto.Longitude.HasValue && !dto.Latitude.HasValue)
                {
                    return ResponseModel<TechnicalServiceGetDto>.Fail("Lokasyon bilgileri gönderilmemiş.", StatusCode.BadRequest);
                }
                else
                {
                    var latStr = dto.Latitude.Value.ToString(CultureInfo.InvariantCulture);
                    var lonStr = dto.Longitude.Value.ToString(CultureInfo.InvariantCulture);
                    var locationResult = await IsTechnicianInValidLocation(customer.Latitude, customer.Longitude, latStr, lonStr);
                    if (!locationResult.IsSuccess)
                    {
                        return ResponseModel<TechnicalServiceGetDto>.Fail(locationResult.Message, locationResult.StatusCode);
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
            .GetQueryable<Pricing>()
            .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (pricing is null)
            {
                pricing = new Pricing()
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
            var baseUrl = appSettings?.Value.AppUrl?.TrimEnd('/') ?? "";
            var uploadRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
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
                await using var write = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                                       bufferSize: 1024 * 64,
                                                       options: FileOptions.Asynchronous | FileOptions.SequentialScan);
                await read.CopyToAsync(write, 1024 * 64, CancellationToken.None);

                return string.IsNullOrEmpty(baseUrl) ? $"/uploads/{name}" : $"{baseUrl}/uploads/{name}";
            }

            var toAddImages = new List<TechnicalServiceImage>();
            var toAddFormImages = new List<TechnicalServiceFormImage>();
            var savedFiles = new List<string>(); // olası temizlik için

            try
            {
                if (dto.ServiceImages is not null)
                {
                    foreach (var f in dto.ServiceImages)
                    {
                        var url = await SaveAsync(f, CancellationToken.None);
                        if (url is null) continue;
                        toAddImages.Add(new TechnicalServiceImage
                        {
                            TechnicalServiceId = technicalService.Id,
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
                        toAddFormImages.Add(new TechnicalServiceFormImage
                        {
                            TechnicalServiceId = technicalService.Id,
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
                .GetMultipleAsync<ServicesRequestProduct>(
                    asNoTracking: false,
                    whereExpression: x => x.RequestNo == dto.RequestNo
                );

            // Dictionary ile hızlı karşılaştırma
            var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x) ?? new Dictionary<long, ServicesRequestProductCreateDto>();
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
                var newEntity = new ServicesRequestProduct
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
            await _activationRecord.LogAsync(
                 WorkFlowActionType.TechnicalServiceFinished,
                 dto.RequestNo,
                 wf.Id,
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

            return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
        }

        // 6 Fiyatlama onay ve kontrole gönderim.
        public async Task<ResponseModel<PricingGetDto>> ApprovePricing(PricingUpdateDto dto)
        {
            #region Validasyonlar/Kontroller

            var wf = await _uow.Repository
              .GetQueryable<WorkFlow>()
              .Include(x => x.ApproverTechnician)
              .AsNoTracking()
              .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<PricingGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<PricingGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<PricingGetDto>.Fail("İlgili akış iptal tamamlanmış.", StatusCode.NotFound);

            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<PricingGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);


            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == "APR");
            if (targetStep is null)
                return ResponseModel<PricingGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

            var pricing = await _uow.Repository
               .GetQueryable<Pricing>()
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (pricing is null)
                return ResponseModel<PricingGetDto>.Fail("Fiyatlama kaydı tanımlı değil.", StatusCode.BadRequest);

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

            #region Ürünler Güncellemesi
            // 🔹 ServicesRequestProduct senkronizasyonu
            var existingProducts = await _uow.Repository
                .GetMultipleAsync<ServicesRequestProduct>(
                    asNoTracking: false,
                    whereExpression: x => x.RequestNo == dto.RequestNo
                );

            // Dictionary ile hızlı karşılaştırma
            var deliveredDict = dto?.Products?.ToDictionary(x => x.ProductId, x => x) ?? new Dictionary<long, ServicesRequestProductCreateDto>();
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
                    //  listede yok → Sil
                    _uow.Repository.HardDelete(existing);
                }
            }

            // 2️ Yeni ürünleri ekle (Listede olup DB'de olmayanlar)
            foreach (var newItem in deliveredDict.Values)
            {
                var newEntity = new ServicesRequestProduct
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
            await _activationRecord.LogAsync(
                 WorkFlowActionType.PricingApproved,
                 dto.RequestNo,
                 wf.Id,
                 "PRC",
                 "APR",
                 "Fiyatlama tamamlandı ve onay aşamasına geçildi",
                 new
                 {
                     dto.Notes,
                     dto.Currency,
                     Products = dto.Products?.Select(p => new { p.ProductId, p.Quantity })
                 }
             );

            #endregion

            await _uow.Repository.CompleteAsync();
            return await GetPricingByRequestNoAsync(dto.RequestNo);
        }

        //Lokasyon Kontrolü  Ezme Maili 
        public async Task<ResponseModel> RequestLocationOverrideAsync(OverrideLocationCheckDto dto)
        {

            var request = await _uow.Repository
               .GetQueryable<ServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);


            //WorkFlow getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
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
                .GetQueryable<TechnicalService>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);




            if (technicalService.IsLocationCheckRequired == false)
                return ResponseModel.Fail("Lokasyon kontrolü zaten devre dışı bırakılmış.", StatusCode.Conflict);

            var me = await _currentUser.GetAsync();
            var techUserId = me?.Id ?? 0;
            var techUserName = me?.TechnicianName ?? me?.Email ?? "Bilinmiyor";

            string custLat = customer.Latitude ?? "-";
            string custLon = customer.Longitude ?? "-";
            string techLat = dto.TechnicianLatitude ?? "-";
            string techLon = dto.TechnicianLongitude ?? "-";

            string mapsLinkCustomer = (custLat != "-" && custLon != "-")
                ? $"https://www.google.com/maps?q={custLat},{custLon}"
                : "#";

            string mapsLinkTechnician = (techLat != "-" && techLon != "-")
                ? $"https://www.google.com/maps?q={techLat},{techLon}"
                : "#";

            // İsteğe bağlı: mesafeyi hesaplayıp maile eklemek istersen, elindeki helper'ı kullan:
            double? distanceKm = null;
            if (double.TryParse(techLat, out var tlat) && double.TryParse(techLon, out var tlon) &&
                double.TryParse(custLat, out var clat) && double.TryParse(custLon, out var clon))
            {
                distanceKm = GetDistanceInKm(tlat, tlon, clat, clon); // mevcut helper
            }

            var appSettings = ServiceTool.ServiceProvider.GetService<IOptionsSnapshot<AppSettings>>();
            var baseUrl = appSettings?.Value.AppUrl?.TrimEnd('/');
            var subject = $"[Lokasyon Onayı] RequestNo: {dto.RequestNo} – {request.Customer?.ContactName1}";
            var distanceInfo = distanceKm.HasValue ? $"{Math.Round(distanceKm.Value, 2)} km" : "Hesaplanamadı";
            // Link parçaları (önce hazırla)
            var customerLink = mapsLinkCustomer != "#"
                ? $"<a href=\"{mapsLinkCustomer}\">Google Maps</a>"
                : string.Empty;

            var technicianLink = mapsLinkTechnician != "#"
                ? $"<a href=\"{mapsLinkTechnician}\">Google Maps</a>"
                : string.Empty;

            var viewLink = baseUrl is not null
                ? $"<p><a href=\"{baseUrl}/technical-service/{dto.RequestNo}\">Kaydı görüntüle</a></p>"
                : string.Empty;

            // Ana HTML (tek bir $@ ile)
            var html = $@"
                    <div style=""font-family:Arial,sans-serif;font-size:14px"">
                        <h3>Teknik Servis Lokasyon Kontrol Aşımı Bilgisi</h3>
                        <p><b>Talep No:</b> {dto.RequestNo}</p>
                        <p><b>Talep Başlığı:</b> {wf.RequestTitle}</p>
                        <p><b>Müşteri:</b> {(request.Customer?.ContactName1 ?? "-")} (Id: {request.CustomerId})</p>
                        <p><b>Teknisyen:</b> {techUserName} (Id: {techUserId})</p>
                        <hr/>
                        <p><b>Müşteri Konumu:</b> {custLat}, {custLon} {customerLink}</p>
                        <p><b>Teknisyen Konumu:</b> {techLat}, {techLon} {technicianLink}</p>
                        <p><b>Kuş Uçuşu Mesafe:</b> {distanceInfo}</p>
                        {(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $"<p><b>Açıklama:</b> {System.Net.WebUtility.HtmlEncode(dto.Reason)}</p>")}
                        <hr/>
                        <p>Bilgi: Bu talep ile teknik servis için lokasyon kontrolü devre dışı bırakılmıştır </p>
                        {viewLink}
                    </div>";

            // Mail alıcıları (tercihen Config/DB'den)
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

            ///MZK : Mail gönderimi responsu işlenecek
            //var result = await _mailService.SendLocationOverrideMailAsync(managerMails, subject, html);
            //if (result.IsSuccess)
            //{
            //    technicalService.IsLocationCheckRequired = false;
            //    technicalService.UpdatedDate = DateTime.Now;
            //    technicalService.UpdatedUser = techUserId;
            //    _uow.Repository.Update(technicalService);
            //}
            //_ = await _mailService.SendLocationOverrideMailAsync(managerMails, subject, html);
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

            // Son durumu döndür
            return ResponseModel.Success("Lokasyon kontrolü devre dışı bırakma talebi iletildi ve ilgili yöneticilere e-posta gönderildi.");
        }
        ///-----------------------------

        // -------------------- Services Request --------------------
        private static Func<IQueryable<ServicesRequest>, IIncludableQueryable<ServicesRequest, object>>? RequestIncludes()
            => q => q
                .Include(x => x.Customer).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(x => x.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.ServiceType)
                .Include(x => x.CustomerApprover)
                .Include(x => x.CustomerApprover)
                .Include(x => x.WorkFlowStep);

        public async Task<ResponseModel<PagedResult<ServicesRequestGetDto>>> GetRequestsAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<ServicesRequest>();
            query = RequestIncludes()!(query);

            // basit filtre/sıralama örneği
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.RequestNo.Contains(q.Search) || x.Description!.Contains(q.Search));

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<ServicesRequestGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<ServicesRequestGetDto>>
                .Success(new PagedResult<ServicesRequestGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByIdAsync(long id)
        {
            var query = _uow.Repository.GetQueryable<ServicesRequest>();
            query = RequestIncludes()!(query);

            var dto = await query
                .AsNoTracking()
                .Where(x => x.Id == id)
                .ProjectToType<ServicesRequestGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // İlgili WorkFlow
            var workflow = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            // Gözden Geçirme Logları (NEW)
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            // DTO doldurma
            dto.ServicesRequestProducts = products;
            if (workflow is not null)
            {
                dto.ApproverTechnicianId = workflow.ApproverTechnicianId;
                dto.CustomerApproverName = string.IsNullOrEmpty(dto.CustomerApproverName)
                    ? workflow.CustomerApproverName
                    : dto.CustomerApproverName;
                dto.IsLocationValid = workflow.IsLocationValid;
                dto.Priority = workflow.Priority;
            }

            // NEW: ReviewLogs ata
            dto.ReviewLogs = reviewLogs;

            return ResponseModel<ServicesRequestGetDto>.Success(dto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<ServicesRequest>();
            query = RequestIncludes()!(query);

            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ProjectToType<ServicesRequestGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // İlgili WorkFlow
            var workflow = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            // Gözden Geçir Logları
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            // DTO doldurma
            dto.ServicesRequestProducts = products;

            if (workflow is not null)
            {
                dto.ApproverTechnicianId = workflow.ApproverTechnicianId;
                dto.IsLocationValid = workflow.IsLocationValid;
                dto.Priority = workflow.Priority;

                // Var olan değeri koru; boşsa workflow'dan çek
                dto.CustomerApproverName = string.IsNullOrWhiteSpace(dto.CustomerApproverName)
                    ? workflow.CustomerApproverName
                    : dto.CustomerApproverName;
            }

            dto.ReviewLogs = reviewLogs; // <-- Yeni

            return ResponseModel<ServicesRequestGetDto>.Success(dto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> UpdateServiceRequestAsync(ServicesRequestUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<ServicesRequest>(
                false,
                x => x.RequestNo == dto.RequestNo,
                includeExpression: RequestIncludes());

            if (entity is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var wf = await _uow.Repository
            .GetQueryable<WorkFlow>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);


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
                .GetMultipleAsync<ServicesRequestProduct>(
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
                existingProducts ??= new List<ServicesRequestProduct>();

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
                    var entityProd = new ServicesRequestProduct
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
            return await GetServiceRequestByNoAsync(entity.RequestNo);
        }
        public async Task<ResponseModel> DeleteRequestAsync(long id)
        {

            // 1) Entity’yi getir (tracked olsun ki güncelleme/replace çalışsın)
            var entity = await _uow.Repository.GetSingleAsync<Model.Concrete.WorkFlows.ServicesRequest>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.IsDeleted = true;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa

            // 3) SoftDelete çağrısı -> 2 tip argümanı verin ve entity gönderin
            await _uow.Repository.SoftDeleteAsync<Model.Concrete.WorkFlows.ServicesRequest, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        //-------------------------Akışı bir önceki adıma geri alma işlemi----------------------------
        public async Task<ResponseModel<WorkFlowGetDto>> SendBackForReviewAsync(string requestNo, string reviewNotes)
        {
            //WorkFlow'u (Akışı) Getir
            var wf = await _uow.Repository.GetQueryable<WorkFlow>(x => x.RequestNo == requestNo)
                .FirstOrDefaultAsync();

            if (wf is null)
                return ResponseModel<WorkFlowGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled || wf.WorkFlowStatus == WorkFlowStatus.Complated)
                return ResponseModel<WorkFlowGetDto>.Fail("İptal edilmiş veya tamamlanmış akışlar geri alınamaz.", StatusCode.Conflict);

            var servicesRequest = await _uow.Repository
               .GetQueryable<ServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
            if (servicesRequest is null)
                return ResponseModel<WorkFlowGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            var currentStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .Select(s => new { s.Id, s.Code })
                .FirstOrDefaultAsync(s => s.Id == wf.CurrentStepId);

            if (currentStep is null)
                return ResponseModel<WorkFlowGetDto>.Fail("Akışın mevcut adımı bulunamadı.", StatusCode.NotFound);

            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;

            var targetStep = new WorkFlowStep();
            var warehouse = new Warehouse();
            var technicalService = new TechnicalService();
            var pricing = new Pricing();
            // Mevcut Adım Koduna Göre Dinamik Güncelleme
            switch (currentStep.Code)
            {
                case "APR": // Teknik Servis Adımı (TechnicalService)
                    pricing = await _uow.Repository
                       .GetQueryable<Pricing>()
                       .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (pricing != null)
                    {
                        targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                          .AsNoTracking()
                          .FirstOrDefaultAsync(s => s.Code == "TS");
                        if (targetStep is null)
                            return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı adımı (TS) tanımlı değil.", StatusCode.BadRequest);

                        technicalService = await _uow.Repository
                             .GetQueryable<TechnicalService>()
                             .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                        if (technicalService is null)
                            return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı Teknik Servis tanımlı değil.", StatusCode.BadRequest);

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
                       .GetQueryable<TechnicalService>()
                       .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (technicalService != null)
                    {
                        //Ürün var ise depoya geri gönder
                        if (servicesRequest.IsProductRequirement)
                        {
                            //Depo Adımına Geri
                            targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                               .AsNoTracking()
                               .FirstOrDefaultAsync(s => s.Code == "WH");
                            if (targetStep is null)
                                return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı adımı (WH) tanımlı değil.", StatusCode.BadRequest);

                            warehouse = await _uow.Repository
                           .GetQueryable<Warehouse>()
                           .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                            if (warehouse is null)
                                return ResponseModel<WorkFlowGetDto>.Fail("Depo Kaydı Bulunamadı.", StatusCode.BadRequest);

                            warehouse.WarehouseStatus = WarehouseStatus.Pending;
                            warehouse.UpdatedDate = DateTime.Now;
                            warehouse.UpdatedUser = meId;
                            _uow.Repository.Update(warehouse);
                        }
                        //Ürün yok ise direkt servis talebine geri gönder
                        else
                        {
                            targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                           .AsNoTracking()
                           .FirstOrDefaultAsync(s => s.Code == "SR");
                            if (targetStep is null)
                                return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);

                            servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;

                            servicesRequest.UpdatedDate = DateTime.Now;
                            servicesRequest.UpdatedUser = meId;
                            _uow.Repository.Update(servicesRequest);
                        }

                        technicalService.ServicesStatus = TechnicalServiceStatus.AwaitingReview;

                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = meId;
                        _uow.Repository.Update(technicalService);
                    }

                    break;

                case "WH": // Depo Adımı (Warehouse)
                           // Depo adımında bir durum (status) alanı olmadığını varsayarak sadece IsSended bayrağını sıfırlayabiliriz
                    warehouse = await _uow.Repository
                        .GetQueryable<Warehouse>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);

                    if (warehouse != null)
                    {

                        targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                         .AsNoTracking()
                         .FirstOrDefaultAsync(s => s.Code == "SR");
                        if (targetStep is null)
                            return ResponseModel<WorkFlowGetDto>.Fail("Hedef iş akışı adımı (SR) tanımlı değil.", StatusCode.BadRequest);


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
                        .GetQueryable<ServicesRequest>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (serviceRequest != null)
                    {
                        serviceRequest.UpdatedDate = DateTime.Now;
                        serviceRequest.UpdatedUser = meId;
                        _uow.Repository.Update(serviceRequest);
                    }
                    break;

                default:
                    break;
            }
            if (targetStep.Code is null)
                return ResponseModel<WorkFlowGetDto>.Fail("Herhangi bir işlem yapılamadı.", StatusCode.BadRequest);
            //Ana WorkFlow'u Yeni Adıma Güncelle
            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = meId;
            _uow.Repository.Update(wf);

            ///Aktivite Kaydı Yaz
            await _activationRecord.LogAsync(
                WorkFlowActionType.WorkFlowStepChanged,
                requestNo,
                wf.Id,
                currentStep.Code,
                targetStep.Code,
                "Akış geri gönderildi",
                new { reviewNotes, targetStep = targetStep.Name }
            );

            /// Gözden geçirme logu yaz
            var reviewLog = new WorkFlowReviewLog
            {
                WorkFlowId = wf.Id,
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




            /// Dönüş tipi WorkFlow GetDto olarak ayarlandı.
            return ResponseModel<WorkFlowGetDto>.Success(
                wf.Adapt<WorkFlowGetDto>(_config)
            );
        }

        // -------------------- Warehouse --------------------
        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync_(long id)
        {
            var query = _uow.Repository.GetQueryable<Warehouse>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.Id == id)
                .ProjectToType<WarehouseGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            var workflow = await query
                .AsNoTracking()
                .Where(x => x.Id == id)
                .ProjectToType<WorkFlow>(_config)
                .FirstOrDefaultAsync();

            // Gözden Geçir Logları
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.WarehouseProducts = products;
            dto.ReviewLogs = reviewLogs; // <-- Yeni

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }


        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync(long id)
        {
            var qWarehouse = _uow.Repository.GetQueryable<Warehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking();
            var qServices = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: Warehouse + (left) WorkFlow + (left) ServicesRequest
            var dto = await (
                from w in qWarehouse
                where w.Id == id
                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj.DefaultIfEmpty()
                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new WarehouseGetDto
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
                    ServicesRequestDescription = sr != null ? sr.Description : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // REVIEW LOG’LARI
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.WarehouseProducts = products;
            dto.ReviewLogs = reviewLogs;

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }

        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync_(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<Warehouse>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ProjectToType<WarehouseGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            var workflow = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ProjectToType<WorkFlow>(_config)
                .FirstOrDefaultAsync();

            // Gözden Geçir (Review) Logları
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.WarehouseProducts = products;
            dto.ReviewLogs = reviewLogs; // <-- eklendi

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }

        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo)
        {
            var qWarehouse = _uow.Repository.GetQueryable<Warehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking();
            var qServices = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: Warehouse + (left) WorkFlow + (left) ServicesRequest
            var dto = await (
                from w in qWarehouse
                where w.RequestNo == requestNo
                join wf0 in qWorkFlow on w.RequestNo equals wf0.RequestNo into wfj
                from wf in wfj.DefaultIfEmpty()
                join sr0 in qServices on w.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new WarehouseGetDto
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
                    ServicesRequestDescription = sr != null ? sr.Description : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // REVIEW LOG’LARI
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.WarehouseProducts = products;
            dto.ReviewLogs = reviewLogs;

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }


        // -------------------- Teknical Services --------------------
        public async Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<TechnicalService>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .AsSplitQuery()
                .Include(x => x.ServiceRequestFormImages)
                .Include(x => x.ServicesImages)
                .Include(x => x.ServiceType)
                .ProjectToType<TechnicalServiceGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            var workflow = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.RequestNo == requestNo);

            // Gözden Geçir (Review) Logları — YENİ
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "TS" || x.ToStepCode == "TS"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.Products = products;
            dto.ReviewLogs = reviewLogs;

            return ResponseModel<TechnicalServiceGetDto>.Success(dto);
        }

        /// ------------------ Pricing -----------------------------------
        public async Task<ResponseModel<PricingGetDto>> GetPricingByRequestNoAsync_(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<Pricing>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ProjectToType<PricingGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<PricingGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // Ürünler
            var products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // Gözden Geçir (Review) Logları — YENİ
            var reviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == dto.RequestNo && (x.FromStepCode == "PRC" || x.ToStepCode == "PRC"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            dto.Products = products;
            dto.ReviewLogs = reviewLogs;

            return ResponseModel<PricingGetDto>.Success(dto);

        }

        public async Task<ResponseModel<PricingGetDto>> GetPricingByRequestNoAsync(string requestNo)
        {
            var qPricing = _uow.Repository.GetQueryable<Pricing>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: Pricing (zorunlu) + ServicesRequest (left)
            var dto = await (
                from pr in qPricing
                where pr.RequestNo == requestNo
                join sr0 in qRequest on pr.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new PricingGetDto
                {
                    // Pricing’ten
                    Id = pr.Id,
                    RequestNo = pr.RequestNo,
                    Status = pr.Status,
                    Currency = pr.Currency,
                    Notes = pr.Notes,
                    TotalAmount = pr.TotalAmount,

                    // ✅ AUDIT (Pricing tablosundan)
                    CreatedDate = pr.CreatedDate,
                    CreatedUser = pr.CreatedUser,
                    UpdatedDate = pr.UpdatedDate,
                    UpdatedUser = pr.UpdatedUser,

                    // ServicesRequest’ten
                    OracleNo = sr != null ? sr.OracleNo : null,
                    ServicesCostStatus = sr != null ? sr.ServicesCostStatus : ServicesCostStatus.Unknown
                    // (Unknown yoksa enum’un default değeri)
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<PricingGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER
            dto.Products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .ProjectToType<ServicesRequestProductGetDto>(_config)
                .ToListAsync();

            // REVIEW LOG’LARI
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "PRC" || x.ToStepCode == "PRC"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            return ResponseModel<PricingGetDto>.Success(dto);
        }


        // -------------------- WorkFlowStep --------------------
        public async Task<ResponseModel<PagedResult<WorkFlowStepGetDto>>> GetStepsAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<WorkFlowStep>();
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.Name.Contains(q.Search) || (x.Code ?? "").Contains(q.Search));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Name)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<WorkFlowStepGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<WorkFlowStepGetDto>>
                .Success(new PagedResult<WorkFlowStepGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<WorkFlowStepGetDto>> GetStepByIdAsync(long id)
        {
            var dto = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .Where(x => x.Id == id)
                .ProjectToType<WorkFlowStepGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            return ResponseModel<WorkFlowStepGetDto>.Success(dto);
        }

        public async Task<ResponseModel<WorkFlowStepGetDto>> CreateStepAsync(WorkFlowStepCreateDto dto)
        {
            var entity = dto.Adapt<WorkFlowStep>(_config);
            await _uow.Repository.AddAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }

        public async Task<ResponseModel<WorkFlowStepGetDto>> UpdateStepAsync(WorkFlowStepUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<WorkFlowStep>(false, x => x.Id == dto.Id);
            if (entity is null)
                return ResponseModel<WorkFlowStepGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Adapt(entity, _config);
            await _uow.Repository.CompleteAsync();
            return await GetStepByIdAsync(entity.Id);
        }
        public async Task<ResponseModel> DeleteStepAsync(long id)
        {
            // 1) Kaydı (tracked) getir
            var entity = await _uow.Repository.GetSingleAsync<WorkFlowStep>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);
            // 2) Soft delete uygula (entity + 2 tip argümanı ver)
            await _uow.Repository.HardDeleteAsync<WorkFlowStep, long>(entity);

            // 3) Commit
            await _uow.Repository.CompleteAsync();

            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        // -------------------- WorkFlow (tanım) --------------------

        //Benzersiz SerivesRequest.RequestNo üretimi
        public async Task<ResponseModel<string>> GetRequestNoAsync(string? prefix = "SR")
        {
            prefix ??= "SR";
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");

            // En fazla 10 deneme: çakışma olursa tekrar üret
            for (int i = 0; i < 10; i++)
            {
                // Kriptografik güvenli 4 haneli sayı
                int rnd = RandomNumberGenerator.GetInt32(1000, 10000);
                string candidate = $"{prefix}-{datePart}-{rnd}";

                // WorkFlow tablosunda var mı?
                var query = _uow.Repository.GetQueryable<WorkFlow>();
                bool exists = await query.AsNoTracking()
                                         .AnyAsync(x => x.RequestNo == candidate && !x.IsDeleted);

                if (!exists)
                    return ResponseModel<string>.Success(candidate, "Yeni Akış Numarası üretildi.");
            }
            // Çok istisnai durumda buraya düşer
            return ResponseModel<string>.Fail("Benzersiz RequestNo üretilemedi, lütfen tekrar deneyin.");
        }

        public async Task<ResponseModel<PagedResult<WorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q)
        {
            var wfBase = _uow.Repository.GetQueryable<WorkFlow>()
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            if (!string.IsNullOrWhiteSpace(q.Search))
                // parantez önemli: OR'un kapsamını netleştirir
                wfBase = wfBase.Where(x => x.RequestNo.Contains(q.Search) || x.RequestTitle.Contains(q.Search));

            // LEFT JOIN: WorkFlow.RequestNo == ServicesRequest.RequestNo
            var qJoined =
                from wf in wfBase
                join sr0 in _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking()
                     on wf.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new { wf, sr };

            var total = await qJoined.CountAsync();

            var items = await qJoined
                .OrderByDescending(x => x.wf.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(x => new WorkFlowGetDto
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
                                   : new WorkFlowStepGetDto
                                   {
                                       Id = x.wf.CurrentStep.Id,
                                       Name = x.wf.CurrentStep.Name,
                                       Code = x.wf.CurrentStep.Code
                                   }
                })
                .ToListAsync();

            return ResponseModel<PagedResult<WorkFlowGetDto>>
                .Success(new PagedResult<WorkFlowGetDto>(items, total, q.Page, q.PageSize));
        }


        public async Task<ResponseModel<WorkFlowGetDto>> GetWorkFlowByIdAsync(long id)
        {
            var dto = await _uow.Repository.GetQueryable<WorkFlow>()
                .Where(x => x.Id == id && !x.IsDeleted)
                .ProjectToType<WorkFlowGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WorkFlowGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            return ResponseModel<WorkFlowGetDto>.Success(dto);
        }

        public async Task<ResponseModel<WorkFlowGetDto>> CreateWorkFlowAsync(WorkFlowCreateDto dto)
        {
            var entity = dto.Adapt<WorkFlow>(_config);
            await _uow.Repository.AddAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetWorkFlowByIdAsync(entity.Id);
        }

        public async Task<ResponseModel<WorkFlowGetDto>> UpdateWorkFlowAsync(WorkFlowUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<WorkFlow>(false, x => x.Id == dto.Id);
            if (entity is null)
                return ResponseModel<WorkFlowGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Adapt(entity, _config);
            await _uow.Repository.CompleteAsync();
            return await GetWorkFlowByIdAsync(entity.Id);
        }
        public async Task<ResponseModel> DeleteWorkFlowAsync(long id)
        {
            var me = await _currentUser.GetAsync();
            var meId = me?.Id ?? 0;
            // 1) Entity’yi getir (tracked olsun ki güncelleme/replace çalışsın)
            var entity = await _uow.Repository.GetSingleAsync<Model.Concrete.WorkFlows.WorkFlow>(
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
            var entity = await _uow.Repository.GetSingleAsync<Model.Concrete.WorkFlows.WorkFlow>(
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
        private static string? GetTechnicianEmail(WorkFlow wf)
        {
            return wf?.ApproverTechnician?.TechnicianEmail;
        }
        private async Task PushTransitionMailsAsync(WorkFlow wf, string fromCode, string toCode, string requestNo, string? customerName)
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


    }
}
