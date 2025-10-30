using Azure.Core;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Settings.Concrete;
using Core.Utilities.IoC;
using Mapster;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowActivityRecord;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Business.Services
{
    public class WorkFlowService : IWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;
        private readonly IMailService _mailService;
        private readonly TypeAdapterConfig _config;
        private readonly IActivationRecordService _activationRecord;

        public WorkFlowService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IAuthService authService, IMailService mailService, IActivationRecordService activationRecord)
        {
            _uow = uow;
            _mapper = mapper;
            _config = config;
            _authService = authService;
            _mailService = mailService;
            _activationRecord = activationRecord;
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

                bool exists = await _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().AnyAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
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

                #endregion

                //ServicesRequest map + ürün bağları (N-N join)
                var request = dto.Adapt<ServicesRequest>(_config);
                request.CreatedDate = DateTime.Now;
                request.CreatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
                request.ServicesRequestStatus = ServicesRequestStatus.Draft;

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

                var res = await _uow.Repository.AddAsync(request);

                // WorkFlow oluştur (aynı RequestNo ile)
                var wf = new WorkFlow
                {
                    RequestNo = request.RequestNo,
                    RequestTitle = "Servis Talebi",
                    Priority = dto.Priority,
                    CurrentStepId = initialStep.Id,
                    CreatedDate = DateTime.Now,
                    CreatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0,
                    WorkFlowStatus = WorkFlowStatus.Pending,
                    IsAgreement = null,
                    IsLocationValid = dto.IsLocationValid,
                    ApproverTechnicianId = dto.ApproverTechnicianId,
                    CustomerApproverName = dto.CustomerApproverName
                };

                await _uow.Repository.AddAsync(wf);

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

                //Tekrar oku ve DTO döndür (include’ları uygula)
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
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<WarehouseGetDto>.Fail("İlg  kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);



            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Code == "WH");

            //Warehouse kaydını getir (varsa)
            var warehouse = await _uow.Repository
                .GetQueryable<Warehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

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
                warehouse.CreatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;

                warehouse = await _uow.Repository.AddAsync(warehouse);
            }

            //Varsa güncelle
            else
            {
                warehouse.UpdatedDate = DateTime.Now;
                warehouse.UpdatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
                warehouse.DeliveryDate = dto.DeliveryDate;
                warehouse.WarehouseStatus = WarehouseStatus.Pending;
                _uow.Repository.Update(warehouse);
            }

            request.WorkFlowStepId = targetStep.Id;
            request.WorkFlowStep = null;
            request.UpdatedDate = DateTime.Now;
            request.UpdatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
            request.ServicesRequestStatus = ServicesRequestStatus.WarehouseSubmitted;
            _uow.Repository.Update(request);


            //WorkFlow güncelle
            wf.CurrentStepId = targetStep.Id;
            wf.IsAgreement = null;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);


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
            #endregion


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
                };
                _uow.Repository.Add(technicalService);
            }

            // 🔹 Warehouse bilgilerini güncelle
            warehouse.DeliveryDate = dto.DeliveryDate;
            warehouse.Description = dto.Description;
            warehouse.WarehouseStatus = WarehouseStatus.Shipped;
            _uow.Repository.Update(warehouse);

            // 🔹 WorkFlow güncelle
            var statu = await _uow.Repository
                .GetQueryable<WorkFlowStep>()
                .AsNoTracking()
                .Where(x => x.Code != null && x.Code == "TS")
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (statu is null)
                return ResponseModel<WarehouseGetDto>.Fail("WorkFlowStep içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);

            wf.CurrentStepId = statu.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

            #region Ürünler 
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
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

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

            #endregion

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


            request.ServicesRequestStatus = ServicesRequestStatus.TechnicialServiceSubmitted;
            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);


            #region Hareket Kaydı
            await _activationRecord.LogAsync(
                    WorkFlowActionType.WorkFlowStepChanged,
                    dto.RequestNo,
                    wf.Id,
                    "SR",
                    "TS",
                    "Teknik servise gönderildi (ürünsüz)",
                    new
                    {
                        tecnicianName = wf?.ApproverTechnician?.TechnicianName ?? "",
                        technicalServiceId = technicalService.Id,
                    }
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
            //WorkFlow getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

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

            //Lokasyon kontrolü
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


            technicalService.StartTime = DateTime.Now;
            technicalService.ServicesStatus = TechnicalServiceStatus.InProgress;
            technicalService.StartLocation = dto.StartLocation;
            technicalService.EndLocation = string.Empty;//Henüz servis bitmediği için boş bırakılıyor
            technicalService.UpdatedDate = DateTime.Now;
            technicalService.UpdatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
            _uow.Repository.Update(technicalService);

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

        // 5 Teknik Servis Servisi Tamamlama 
        public async Task<ResponseModel<TechnicalServiceGetDto>> FinishService(FinishTechnicalServiceDto dto)
        {

            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

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

            //------------------ Lokasyon kontrolü----------------
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

            technicalService.EndTime = DateTime.Now;
            technicalService.ServicesStatus = TechnicalServiceStatus.Completed;
            technicalService.ProblemDescription = dto.ProblemDescription;
            technicalService.ResolutionAndActions = dto.ResolutionAndActions;
            technicalService.ServiceTypeId = dto.ServiceTypeId;
            technicalService.EndLocation = dto.EndLocation;
            technicalService.ServicesCostStatus = dto.ServicesCostStatus;
            technicalService.UpdatedDate = DateTime.Now;
            technicalService.UpdatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
            _uow.Repository.Update(technicalService);

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


            var targetStep = await _uow.Repository.GetQueryable<WorkFlowStep>()
           .AsNoTracking()
           .FirstOrDefaultAsync(s => s.Code == "PRC");
            if (targetStep is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Hedef iş akışı adımı (PRC) tanımlı değil.", StatusCode.BadRequest);

            wf.CurrentStepId = targetStep.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

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

        //Lokasyon Kontrol  Ezme Maili 
        public async Task<ResponseModel> RequestLocationOverrideAsync(OverrideLocationCheckDto dto)
        {
            var request = await _uow.Repository
               .GetQueryable<ServicesRequest>()
               .Include(x => x.Customer)
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

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

            // (İdempotent davranış) Zaten kapalıysa gereksiz mail atmayalım
            if (technicalService.IsLocationCheckRequired == false)
                return ResponseModel.Fail("Lokasyon kontrolü zaten devre dışı bırakılmış.", StatusCode.Conflict);

            //  Mail içeriğini hazırla
            var me = (await _authService.MeAsync())?.Data;
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
                        <p><b>Request No:</b> {dto.RequestNo}</p>
                        <p><b>Müşteri:</b> {(request.Customer?.ContactName1 ?? "-")} (Id: {request.CustomerId})</p>
                        <p><b>Teknisyen:</b> {techUserName} (Id: {techUserId})</p>
                        <hr/>
                        <p><b>Müşteri Konumu:</b> {custLat}, {custLon} {customerLink}</p>
                        <p><b>Teknisyen Konumu:</b> {techLat}, {techLon} {technicianLink}</p>
                        <p><b>Kuş Uçuşu Mesafe:</b> {distanceInfo}</p>
                        {(string.IsNullOrWhiteSpace(dto.Reason) ? "" : $"<p><b>Açıklama:</b> {System.Net.WebUtility.HtmlEncode(dto.Reason)}</p>")}
                        <hr/>
                        <p>Bilgi: Bu talep ile teknik servis için lokasyon kontrolü devre dışı bırakılmıştır (<b>IsLocationCheckRequired = false</b>).</p>
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

            //MZK : Mail gönderimi responsu işlenecek
            //var result = await _mailService.SendLocationOverrideMailAsync(managerMails, subject, html);
            //if (result.IsSuccess)
            //{
            //    technicalService.IsLocationCheckRequired = false;
            //    technicalService.UpdatedDate = DateTime.Now;
            //    technicalService.UpdatedUser = techUserId;
            //    _uow.Repository.Update(technicalService);
            //}

            _ = await _mailService.SendLocationOverrideMailAsync(managerMails, subject, html);
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
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            dto.ApproverTechnicianId = workflow?.ApproverTechnicianId ?? 0;
            dto.CustomerApproverName = workflow?.CustomerApproverName;
            dto.IsLocationValid = workflow.IsLocationValid;
            dto.ServicesRequestProducts = products; // DTO’da ürün listesi property’si olmalı
            dto.CustomerApproverName = string.IsNullOrEmpty(dto.CustomerApproverName) ? workflow.CustomerApproverName : dto.CustomerApproverName;
            dto.Priority = workflow.Priority;
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

            var products = await _uow.Repository
               .GetQueryable<ServicesRequestProduct>()
               .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
               .AsNoTracking()
               .Where(p => p.RequestNo == requestNo)
               .ProjectToType<ServicesRequestProductGetDto>(_config)
               .ToListAsync();

            var workflow = await _uow.Repository
               .GetQueryable<WorkFlow>()
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

            dto.ApproverTechnicianId = workflow?.ApproverTechnicianId ?? 0;
            dto.CustomerApproverName = workflow?.CustomerApproverName;
            dto.ServicesRequestProducts = products; // DTO’da ürün listesi property’si olmalı
            dto.IsLocationValid = workflow.IsLocationValid;
            dto.CustomerApproverName = string.IsNullOrEmpty(dto.CustomerApproverName) ? workflow.CustomerApproverName : dto.CustomerApproverName;
            dto.Priority = workflow.Priority;
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
            // Ana talep bilgilerini güncelle
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
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


      
        //Akışı bir önceki adıma geri alma işlemi
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
            var targetStep = new WorkFlowStep();
            var warehouse = new Warehouse();

            var technicalService = new TechnicalService();
            // Mevcut Adım Koduna Göre Dinamik Güncelleme
            switch (currentStep.Code)
            {
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
                            warehouse.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
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
                            servicesRequest.Description = $"REVİZYON TALEBİ: {reviewNotes}. Hedef Adım: {targetStep.Name}";
                            servicesRequest.UpdatedDate = DateTime.Now;
                            servicesRequest.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
                            _uow.Repository.Update(servicesRequest);
                        }

                        technicalService.ServicesStatus = TechnicalServiceStatus.AwaitingReview;
                        technicalService.ResolutionAndActions = $"REVİZYON TALEBİ: {reviewNotes}. Hedef Adım: {targetStep.Name}";
                        technicalService.UpdatedDate = DateTime.Now;
                        technicalService.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
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
                        warehouse.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
                        servicesRequest.ServicesRequestStatus = ServicesRequestStatus.Draft;
                        servicesRequest.Description = $"REVİZYON TALEBİ: {reviewNotes}. Hedef Adım: {targetStep.Name}";
                        servicesRequest.UpdatedDate = DateTime.Now;
                        servicesRequest.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
                        _uow.Repository.Update(servicesRequest);
                    }
                    break;

                case "SR": // Servis Talebi Adımı (ServicesRequest)
                    var serviceRequest = await _uow.Repository
                        .GetQueryable<ServicesRequest>()
                        .FirstOrDefaultAsync(x => x.RequestNo == requestNo);
                    if (serviceRequest != null)
                    {
                        serviceRequest.Description = $"REVİZYON TALEBİ: {reviewNotes}. Hedef Adım: {targetStep.Name}";
                        serviceRequest.UpdatedDate = DateTime.Now;
                        serviceRequest.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
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
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

            await _activationRecord.LogAsync(
                WorkFlowActionType.WorkFlowStepChanged,
                requestNo,
                wf.Id,
                currentStep.Code,
                targetStep.Code,
                "Akış geri gönderildi",
                new { reviewNotes, targetStep = targetStep.Name }
            );

            //Değişiklikleri Kaydet
            await _uow.Repository.CompleteAsync();

            // Dönüş tipi WorkFlow GetDto olarak ayarlandı.
            return ResponseModel<WorkFlowGetDto>.Success(
                wf.Adapt<WorkFlowGetDto>(_config)
            );
        }

        // -------------------- Warehouse --------------------
        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync(long id)
        {
            var query = _uow.Repository.GetQueryable<Warehouse>();

            var dto = await query
                .AsNoTracking()
                .Where(x => x.Id == id)
                .ProjectToType<WarehouseGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var products = await _uow.Repository
                 .GetQueryable<ServicesRequestProduct>()
                 .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
                 .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                 .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
                 .AsNoTracking()
                 .Where(p => p.RequestNo == dto.RequestNo)
                 .ProjectToType<ServicesRequestProductGetDto>(_config)
                 .ToListAsync();
            dto.WarehouseProducts = products; // DTO’da ürün listesi property’si olmalı
            return ResponseModel<WarehouseGetDto>.Success(dto);
        }
        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<Warehouse>();
            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .ProjectToType<WarehouseGetDto>(_config)
                .FirstOrDefaultAsync();
            if (dto is null)
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var products = await _uow.Repository
               .GetQueryable<ServicesRequestProduct>()
               .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
               .AsNoTracking()
               .Where(p => p.RequestNo == dto.RequestNo)
               .ProjectToType<ServicesRequestProductGetDto>(_config)
               .ToListAsync();
            dto.WarehouseProducts = products; // DTO’da ürün listesi property’si olmalı
            return ResponseModel<WarehouseGetDto>.Success(dto);

        }

        // -------------------- Teknical Services --------------------
        public async Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<TechnicalService>();
            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .Include(x => x.ServiceRequestFormImages)
                .Include(x => x.ServicesImages)
                .Include(x => x.ServiceType)
                .ProjectToType<TechnicalServiceGetDto>(_config)
                .FirstOrDefaultAsync();
            if (dto is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var products = await _uow.Repository
               .GetQueryable<ServicesRequestProduct>()
               .Include(x => x.Product).ThenInclude(x => x.CustomerProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
               .Include(x => x.Customer).ThenInclude(z => z.CustomerProductPrices)
               .AsNoTracking()
               .Where(p => p.RequestNo == dto.RequestNo)
               .ProjectToType<ServicesRequestProductGetDto>(_config)
               .ToListAsync();
            dto.Products = products; // DTO’da ürün listesi property’si olmalı

            return ResponseModel<TechnicalServiceGetDto>.Success(dto);
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
            var query = _uow.Repository.GetQueryable<WorkFlow>().Where(x => !x.IsDeleted);
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.RequestNo.Contains(q.Search) || x.RequestTitle.Contains(q.Search) && !x.IsDeleted);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(x => x.CreatedDate)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<WorkFlowGetDto>(_config)
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
            // 1) Entity’yi getir (tracked olsun ki güncelleme/replace çalışsın)
            var entity = await _uow.Repository.GetSingleAsync<Model.Concrete.WorkFlows.WorkFlow>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.IsDeleted = true;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa
            entity.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        public async Task<ResponseModel> CancelWorkFlowAsync(long id)
        {
            var entity = await _uow.Repository.GetSingleAsync<Model.Concrete.WorkFlows.WorkFlow>(
              asNoTracking: false,
              x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("İptal edilecek kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Soft-delete işaretleri (sizde BaseEntity/Auditable’da ne varsa)
            entity.WorkFlowStatus = WorkFlowStatus.Cancelled;                // varsa
            entity.UpdatedDate = DateTime.Now; // varsa
            entity.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(entity);
            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }
        //-------------Private-------------
        private async Task<ResponseModel> IsTechnicianInValidLocation(string lat1, string lon1, string lat2, string lon2)
        {
            var data = await _uow.Repository.GetSingleAsync<Configuration>(false, x => x.Name == "TechnicianCustomerMinDistanceKm");
            if (data is null)
                return ResponseModel.Fail("Konum kontrolü için gerekli 'TechnicianCustomerMinDistanceKm' tanımı bulunamadı.", StatusCode.NotFound);

            double minDistanceKm = double.Parse(data.Value ?? "0");
            double latitude1 = double.Parse(lat1, CultureInfo.InvariantCulture);
            double longitude1 = double.Parse(lon1, CultureInfo.InvariantCulture);
            double latitude2 = double.Parse(lat2, CultureInfo.InvariantCulture);
            double longitude2 = double.Parse(lon2, CultureInfo.InvariantCulture);
            double distance = GetDistanceInKm(latitude1, longitude1, latitude2, longitude2);
            // 🔹 Virgülden sonra 2 basamak formatla
            string distanceFormatted = distance.ToString("F2", CultureInfo.InvariantCulture);
            string minDistanceFormatted = minDistanceKm.ToString("F2", CultureInfo.InvariantCulture);
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
    }
}
