using Azure.Core;
using Business.Interfaces;
using Business.UnitOfWork;
using ClosedXML.Excel;
using Core.Common;
using Core.Enums;
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
using Model.Concrete.WorkFlows;
using Model.Dtos.Customer;
using Model.Dtos.CustomerGroup;
using Model.Dtos.ProgressApprover;
using Model.Dtos.Role;
using Model.Dtos.User;
using Model.Dtos.WorkFlowDtos.FinalApproval;
using Model.Dtos.WorkFlowDtos.Pricing;
using Model.Dtos.WorkFlowDtos.Report;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowReviewLog;
using Model.Dtos.WorkFlowDtos.WorkFlowStep;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;

namespace Business.Services
{
    public class WorkFlowService : IWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly TypeAdapterConfig _config;
        private readonly IActivationRecordService _activationRecord;
        private readonly ILogger<WorkFlowService> _logger;
        private readonly IMailPushService _mailPush;
        private readonly ICurrentUser _currentUser;
        private readonly AppDataContext _ctx;
        public WorkFlowService(IUnitOfWork uow, TypeAdapterConfig config, IAuthService authService, IActivationRecordService activationRecord, ILogger<WorkFlowService> logger, IMailPushService mailPush, ICurrentUser currentUser, AppDataContext ctx)
        {
            _uow = uow;
            _config = config;
            _activationRecord = activationRecord;
            _logger = logger;
            _mailPush = mailPush;
            _currentUser = currentUser;
            _ctx = ctx;
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

                _logger.LogError(ex, "CreateRequestAsync");
                return ResponseModel<ServicesRequestGetDto>.Fail($"Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //2.1 Depoya Gönderim  (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> SendWarehouseAsync(SendWarehouseDto dto)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendWarehouseAsync");
                return ResponseModel<WarehouseGetDto>.Fail($"Depo gönderim sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //2.2 Depo Teslimatı ve Teknik servise Gönderim (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> CompleteDeliveryAsync(CompleteDeliveryDto dto)
        {

            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "CompleteDeliveryAsync");
                return ResponseModel<WarehouseGetDto>.Fail($" Depo Teslimatı  sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        //2.3 Teknik Servis Gönderim  (Ürün yok ise)
        public async Task<ResponseModel<TechnicalServiceGetDto>> SendTechnicalServiceAsync(SendTechnicalServiceDto dto)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendTechnicalServiceAsync");
                return ResponseModel<TechnicalServiceGetDto>.Fail($"Teknik Servis Gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        // 4️ Teknik Servis Servisi Başlatma 
        public async Task<ResponseModel<TechnicalServiceGetDto>> StartService(StartTechnicalServiceDto dto)
        {

            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "StartService");
                return ResponseModel<TechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Başlatma   sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        // 5 Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi
        public async Task<ResponseModel<TechnicalServiceGetDto>> FinishService(FinishTechnicalServiceDto dto)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinishService");
                return ResponseModel<TechnicalServiceGetDto>.Fail($" Teknik Servis Servisi Tamamlama  ve Fiyatlamaya gönderimi   sırasında hata: {ex.Message}", StatusCode.Error);
            }


        }

        // 6 Fiyatlama onay ve kontrole gönderim.
        public async Task<ResponseModel<PricingGetDto>> ApprovePricing(PricingUpdateDto dto)
        {
            try
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


                var servicesRequest = await _uow.Repository
                  .GetQueryable<ServicesRequest>()
                  .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);
                if (servicesRequest is null)
                    return ResponseModel<PricingGetDto>.Fail("Servis talebi kaydı bulunamadı.", StatusCode.BadRequest);

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

                #region Son Onaya Gönderim 
                var finalApproval = await _uow.Repository
                        .GetQueryable<FinalApproval>()
                        .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);
                if (finalApproval is null)
                {
                    finalApproval = new FinalApproval
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApprovePricing");
                return ResponseModel<PricingGetDto>.Fail($" Fiyatlama onay ve kontrole gönderim  sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }


        // 7) Kontrol ve Son Onay (FinalApproval) — CREATE
        public async Task<ResponseModel<FinalApprovalGetDto>> FinalApprovalAsync(FinalApprovalUpdateDto dto)
        {
            try
            {
                #region  Validasyonlar/Kontroller
                // 1) WorkFlow & Request kontrolleri
                var wf = await _uow.Repository
                    .GetQueryable<WorkFlow>()
                    .Include(x => x.ApproverTechnician)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo && !x.IsDeleted);

                if (wf is null)
                    return ResponseModel<FinalApprovalGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Cancelled)
                    return ResponseModel<FinalApprovalGetDto>.Fail("İlgili akış iptal edilmiş.", StatusCode.NotFound);

                if (wf.WorkFlowStatus == WorkFlowStatus.Complated)
                    return ResponseModel<FinalApprovalGetDto>.Fail("İlgili akış tamamlanmış.", StatusCode.NotFound);

                var request = await _uow.Repository
                    .GetQueryable<ServicesRequest>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (request is null)
                    return ResponseModel<FinalApprovalGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

                // 2) Hedef adım: APR (Approval / Final Approval)
                var targetStep = await _uow.Repository
                    .GetQueryable<WorkFlowStep>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Code == "APR");

                if (targetStep is null)
                    return ResponseModel<FinalApprovalGetDto>.Fail("Hedef iş akışı adımı (APR) tanımlı değil.", StatusCode.BadRequest);



                // 3) FinalApproval var mı? (unique: RequestNo)
                var existsFinalApproval = await _uow.Repository
                    .GetQueryable<FinalApproval>()
                    .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

                if (existsFinalApproval is null)
                    return ResponseModel<FinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.BadRequest);

                var me = await _currentUser.GetAsync();
                var meId = me?.Id ?? 0;
                #endregion

                #region Workflow Güncelleme
                if (wf is not null)
                {
                    wf.CurrentStepId = targetStep.Id;
                    wf.UpdatedDate = DateTime.Now;
                    wf.UpdatedUser = meId;
                    wf.WorkFlowStatus = dto.WorkFlowStatus;
                    _uow.Repository.Update(wf);
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

                #region Ürün Fiyat Sabitleme
                // Tamamlama/İptal anında zorunlu fiyat capture; diğer durumlarda da çalıştırmak istersen force=true verebilirsin
                if (dto.WorkFlowStatus == WorkFlowStatus.Complated || dto.WorkFlowStatus == WorkFlowStatus.Cancelled)
                {
                    await EnsurePricesCapturedAsync(dto.RequestNo);
                }
                else
                {
                    // İsteğe bağlı: satırlar capture edilmemişse, toplam hesap doğru olsun diye tetikleyebilirsin
                    await EnsurePricesCapturedAsync(dto.RequestNo, force: false);
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
                _uow.Repository.Update(existsFinalApproval);
                #endregion

                #region Hareket Kaydı
                await _activationRecord.LogAsync(
                  WorkFlowActionType.FinalApprovalUpdated,
                  dto.RequestNo,
                  wf?.Id,
                  fromStepCode: wf?.CurrentStep?.Code ?? "APR",
                  toStepCode: "APR",
                   "Kontrol ve Son Onay kaydı güncellendi.",
                  new { dto.Notes, dto.WorkFlowStatus, meId, DateTime.Now }
              );

                #endregion

                await _uow.Repository.CompleteAsync();

                return await GetFinalApprovalByRequestNoAsync(dto.RequestNo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FinalApprovalAsync");
                return ResponseModel<FinalApprovalGetDto>.Fail($"  Kontrol ve Son Onay sırasında hata: {ex.Message}", StatusCode.Error);
            }

        }

        //Lokasyon Kontrolü  Ezme Maili 
        public async Task<ResponseModel> RequestLocationOverrideAsync(OverrideLocationCheckDto dto)
        {
            // 1) Talep & WorkFlow & Customer & TechnicalService kontrolleri
            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

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
            var now = DateTimeOffset.UtcNow;

            // 1) Ana DTO: SR + (WF last) + Customer (warranty türetmeleri)
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking()
                where sr.Id == id

                // left join: aynı RequestNo’ya sahip ve silinmemiş workflow’lar
                join wf0 in _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)  // “en güncel” workflow tercih ediliyorsa
                    .Take(1)
                    .DefaultIfEmpty()
                select new ServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.OracleNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    ServicesCostStatus = sr.ServicesCostStatus,
                    Description = sr.Description,
                    IsProductRequirement = sr.IsProductRequirement,

                    IsMailSended = sr.IsMailSended,
                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerApproverName = sr.CustomerApprover.FullName != null ? sr.CustomerApprover.FullName : wf.CustomerApproverName,

                    CustomerId = sr.CustomerId,
                    CustomerName = sr.Customer != null ? sr.Customer.SubscriberCompany : null,

                    ServiceTypeId = sr.ServiceTypeId,
                    ServiceTypeName = sr.ServiceType != null ? sr.ServiceType.Name : null,
                    WorkFlowStepName = sr.WorkFlowStep != null ? sr.WorkFlowStep.Name : null,

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
                        CustomerTypeId = sr.Customer.CustomerTypeId
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // 2) Ürünler (tek bağımsız sorgu — sadece ihtiyaç alanlarını seç)
            baseDto.ServicesRequestProducts = await _uow.Repository
                     .GetQueryable<ServicesRequestProduct>()
                     .AsNoTracking()
                     .Where(p => p.RequestNo == baseDto.RequestNo)
                     .Select(p => new ServicesRequestProductGetDto
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
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == baseDto.RequestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new WorkFlowReviewLogDto
                {
                    Id = x.Id,
                    WorkFlowId = x.WorkFlowId,
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

            return ResponseModel<ServicesRequestGetDto>.Success(baseDto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> GetServiceRequestByRequestNoAsync(string requestNo)
        {
            var now = DateTimeOffset.UtcNow;

            // 1) Ana DTO: SR + (WF last) + Customer (warranty türetmeleri)
            var baseDto = await (
                from sr in _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking()
                where sr.RequestNo == requestNo

                // left join: aynı RequestNo’ya sahip ve silinmemiş workflow’lar
                join wf0 in _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().Where(w => !w.IsDeleted)
                    on sr.RequestNo equals wf0.RequestNo into wfJoin
                from wf in wfJoin
                    .OrderByDescending(x => x.CreatedDate)  // “en güncel” workflow tercih ediliyorsa
                    .Take(1)
                    .DefaultIfEmpty()
                select new ServicesRequestGetDto
                {
                    Id = sr.Id,
                    RequestNo = sr.RequestNo,
                    OracleNo = sr.OracleNo,
                    ServicesDate = sr.ServicesDate,
                    PlannedCompletionDate = sr.PlannedCompletionDate,
                    ServicesCostStatus = sr.ServicesCostStatus,
                    Description = sr.Description,
                    IsProductRequirement = sr.IsProductRequirement,

                    IsMailSended = sr.IsMailSended,
                    CustomerApproverId = sr.CustomerApproverId,
                    CustomerApproverName = sr.CustomerApprover.FullName != null ? sr.CustomerApprover.FullName : wf.CustomerApproverName,

                    CustomerId = sr.CustomerId,
                    CustomerName = sr.Customer != null ? sr.Customer.SubscriberCompany : null,

                    ServiceTypeId = sr.ServiceTypeId,
                    ServiceTypeName = sr.ServiceType != null ? sr.ServiceType.Name : null,
                    WorkFlowStepName = sr.WorkFlowStep != null ? sr.WorkFlowStep.Name : null,

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
                        CustomerTypeId = sr.Customer.CustomerTypeId
                    }
                }
            ).FirstOrDefaultAsync();

            if (baseDto is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);


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
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == requestNo)
                .Select(p => new ServicesRequestProductGetDto
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
                .GetQueryable<WorkFlowReviewLog>(x => x.RequestNo == requestNo && (x.FromStepCode == "SR" || x.ToStepCode == "SR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new WorkFlowReviewLogDto
                {
                    Id = x.Id,
                    WorkFlowId = x.WorkFlowId,
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

            return ResponseModel<ServicesRequestGetDto>.Success(baseDto);
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
            return await GetServiceRequestByRequestNoAsync(entity.RequestNo);
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
                case "PRC": // Teknik Servis Adımı (TechnicalService)
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
        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByIdAsync(long id)
        {
            var qWarehouse = _uow.Repository.GetQueryable<Warehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();
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
                            CustomerTypeId = sr.Customer.CustomerTypeId
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
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: depo aşamasında fiyat yok
            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
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
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new WorkFlowReviewLogDto
                {
                    Id = x.Id,
                    WorkFlowId = x.WorkFlowId,
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

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }
        public async Task<ResponseModel<WarehouseGetDto>> GetWarehouseByRequestNoAsync(string requestNo)
        {
            var qWarehouse = _uow.Repository.GetQueryable<Warehouse>().AsNoTracking();
            var qWorkFlow = _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().Where(w => !w.IsDeleted);
            var qServices = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();
            var qUsers = _uow.Repository.GetQueryable<User>().AsNoTracking(); // <-- eklendi

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
                            CustomerTypeId = sr.Customer.CustomerTypeId
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
                return ResponseModel<WarehouseGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: depo aşamasında fiyat yok
            dto.WarehouseProducts = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
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
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "WH" || x.ToStepCode == "WH"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .Select(x => new WorkFlowReviewLogDto
                {
                    Id = x.Id,
                    WorkFlowId = x.WorkFlowId,
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

            return ResponseModel<WarehouseGetDto>.Success(dto);
        }


        // -------------------- Teknical Services --------------------
        public async Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<TechnicalService>();

            // HEADER (mevcut mapster config'ine göre)
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

            // --- Customer: ServicesRequest üzerinden tek sorguda projeksiyon ---
            dto.Customer = await _uow.Repository
                .GetQueryable<ServicesRequest>()
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

                    // garanti: sadece süreyi geçiriyoruz (türetilmiş alan yok)
                    InstallationDate = sr.Customer.InstallationDate,
                    WarrantyYears = sr.Customer.WarrantyYears,

                    CustomerGroupId = sr.Customer.CustomerGroupId,
                    CustomerTypeId = sr.Customer.CustomerTypeId,
                })
                .FirstOrDefaultAsync();

            // ÜRÜNLER: teknisyen fiyat görmeyecek → price alanlarını projekte etmiyoruz
            dto.Products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,

                    // temel alanlar (fiyat YOK)
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    PriceCurrency = p.Product != null ? p.Product.PriceCurrency : null,

                    Quantity = p.Quantity

                    // Not: ProductPrice / EffectivePrice maplenmedi (teknisyen görmeyecek)
                    // DTO'nun TotalPrice'ı EffectivePrice'a bağlıysa, UI tarafında gizleyin ya da DTO'dan çıkartın.
                })
                .ToListAsync();

            // GÖZDEN GEÇİR (TS adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "TS" || x.ToStepCode == "TS"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            return ResponseModel<TechnicalServiceGetDto>.Success(dto);
        }
        /// ------------------ Pricing -----------------------------------
        public async Task<ResponseModel<PricingGetDto>> GetPricingByRequestNoAsync(string requestNo)
        {
            var qPricing = _uow.Repository.GetQueryable<Pricing>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: Pricing (zorunlu) + ServicesRequest (left) + Customer (projection)
            var dto = await (
                from pr in qPricing
                where pr.RequestNo == requestNo
                join sr0 in qRequest on pr.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new PricingGetDto
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
                    OracleNo = sr != null ? sr.OracleNo : null,
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
                            CustomerTypeId = sr.Customer.CustomerTypeId
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<PricingGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // ÜRÜNLER: Include yok; EffectivePrice server-side hesaplanır
            dto.Products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,

                    // Ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    PriceCurrency = p.Product != null ? p.Product.PriceCurrency : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,

                    // 1) Grup fiyatı → 2) Müşteri özel fiyatı → 3) Liste fiyatı → 0
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
                    // TotalPrice DTO'da => Quantity * EffectivePrice
                })
                .ToListAsync();

            // REVIEW LOG’LARI (Pricing adımı)
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

        //----------------------FinalApproval ---------------------------------------------------

        public async Task<ResponseModel<FinalApprovalGetDto>> GetFinalApprovalByRequestNoAsync(string requestNo)
        {
            var qFinal = _uow.Repository.GetQueryable<FinalApproval>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: FinalApproval + (left) ServicesRequest -> Customer
            var dto = await (
                from fa in qFinal
                where fa.RequestNo == requestNo
                join sr0 in qRequest on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new FinalApprovalGetDto
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
                            CustomerTypeId = sr.Customer.CustomerTypeId
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<FinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // PRODUCTS: Include yok; EffectivePrice server-side hesaplanır
            dto.Products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,

                    // ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    PriceCurrency = p.Product != null ? p.Product.PriceCurrency : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,

                    // 1) Grup fiyatı → 2) Müşteri özel fiyatı → 3) Liste fiyatı → 0
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
                    // TotalPrice: DTO'da => Quantity * EffectivePrice
                })
                .ToListAsync();

            // REVIEW LOG’ları (APR adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            return ResponseModel<FinalApprovalGetDto>.Success(dto);
        }
        public async Task<ResponseModel<FinalApprovalGetDto>> GetFinalApprovalByIdAsync(long id)
        {
            var qFinal = _uow.Repository.GetQueryable<FinalApproval>().AsNoTracking();
            var qRequest = _uow.Repository.GetQueryable<ServicesRequest>().AsNoTracking();

            // HEADER: FinalApproval + (left) ServicesRequest -> Customer
            var dto = await (
                from fa in qFinal
                where fa.Id == id
                join sr0 in qRequest on fa.RequestNo equals sr0.RequestNo into srj
                from sr in srj.DefaultIfEmpty()
                select new FinalApprovalGetDto
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
                            CustomerTypeId = sr.Customer.CustomerTypeId
                        }
                        : null
                }
            ).FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<FinalApprovalGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // PRODUCTS: Include yok; EffectivePrice server-side hesaplanır
            dto.Products = await _uow.Repository
                .GetQueryable<ServicesRequestProduct>()
                .AsNoTracking()
                .Where(p => p.RequestNo == dto.RequestNo)
                .Select(p => new ServicesRequestProductGetDto
                {
                    Id = p.Id,
                    RequestNo = p.RequestNo,
                    ProductId = p.ProductId,
                    Quantity = p.Quantity,

                    // ürün temel alanları
                    ProductName = p.Product != null ? p.Product.Description : null,
                    ProductCode = p.Product != null ? p.Product.ProductCode : null,
                    PriceCurrency = p.Product != null ? p.Product.PriceCurrency : null,
                    ProductPrice = (p.Product != null ? (decimal?)p.Product.Price : null) ?? 0m,

                    // 1) Grup fiyatı → 2) Müşteri özel fiyatı → 3) Liste fiyatı → 0
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
                    // TotalPrice: DTO'da => Quantity * EffectivePrice
                })
                .ToListAsync();

            // REVIEW LOG’ları (APR adımı)
            dto.ReviewLogs = await _uow.Repository
                .GetQueryable<WorkFlowReviewLog>(x =>
                    x.RequestNo == dto.RequestNo &&
                    (x.FromStepCode == "APR" || x.ToStepCode == "APR"))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedDate)
                .ProjectToType<WorkFlowReviewLogDto>(_config)
                .ToListAsync();

            return ResponseModel<FinalApprovalGetDto>.Success(dto);
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

            var me = await _currentUser.GetAsync();

            var roles = me?.Roles.Select(x => x.Code).ToHashSet();

            bool isAdmin = roles?.Contains("ADMIN") ?? false;
            bool isWarehouse = roles?.Contains("WAREHOUSE")??false;
            bool isTechnician = roles?.Contains("TECHNICIAN") ?? false;
            bool isSubcontractor = roles?.Contains("SUBCONTRACTOR") ?? false;
            bool isProjectEngineer = roles?.Contains("PROJECTENGINEER") ?? false;

            var pendingStatus = WorkFlowStatus.Pending;

            var wfBase = _uow.Repository.GetQueryable<WorkFlow>()
                 .AsNoTracking()
                 .Where(x => !x.IsDeleted && x.WorkFlowStatus == pendingStatus);


            if (isAdmin || isSubcontractor || isProjectEngineer)
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

        //------------------------ Report ------------------------
        public async Task<ResponseModel<WorkFlowReportDto>> GetReportAsync(string requestNo)
        {
            // 1) WorkFlow + CurrentStep + ApproverTechnician
            var wf = await _uow.Repository.GetQueryable<WorkFlow>()
                .Include(x => x.CurrentStep)
                .Include(x => x.ApproverTechnician)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == requestNo && !x.IsDeleted);

            if (wf is null)
                return ResponseModel<WorkFlowReportDto>.Fail("Akış bulunamadı.", StatusCode.NotFound);

            var dto = new WorkFlowReportDto
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
            var sr = await _uow.Repository.GetQueryable<ServicesRequest>()
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
                    OracleNo = sr.OracleNo,
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
            var lines = await _uow.Repository.GetQueryable<ServicesRequestProduct>()
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
            var ts = await _uow.Repository.GetQueryable<TechnicalService>()
                .AsNoTracking()
                .Include(t => t.ServicesImages)
                .Include(t => t.ServiceRequestFormImages)
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
                    ServiceImages = ts.ServicesImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList(),
                    FormImages = ts.ServiceRequestFormImages.Select(i => new ImageDto { Id = i.Id, Url = i.Url, Caption = i.Caption }).ToList()
                };
            }

            // 5) Warehouse
            var wh = await _uow.Repository.GetQueryable<Warehouse>()
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
            var pr = await _uow.Repository.GetQueryable<Pricing>()
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
            var fa = await _uow.Repository.GetQueryable<FinalApproval>()
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
            dto.ReviewLogs = await _uow.Repository.GetQueryable<WorkFlowReviewLog>()
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

            return ResponseModel<WorkFlowReportDto>.Success(dto);
        }
        public async Task<PagedResult<WorkFlowReportListItemDto>> GetReportsAsync(ReportQueryParams q)
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
                    "dbo.usp_ReportSearch",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: commandTimeoutSeconds
                ));

                var list = new List<WorkFlowReportListItemDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    // Total’ı her satırdan alıyoruz (window COUNT), ilk satırdaki değer sayfa için yeterli
                    if (total == 0) total = r.TotalCount;

                    list.Add(new WorkFlowReportListItemDto
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

                return new PagedResult<WorkFlowReportListItemDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                // 5) Bağlantıyı biz açtıysak kibarca kapat
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }
        public async Task<PagedResult<WorkFlowReportLineDto>> GetReportLinesAsync(ReportQueryParams q)
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
                var rows = await conn.QueryAsync<ReportLineRowDto>(new CommandDefinition(
                    "dbo.usp_ReportSearch_Lines",
                    p,
                    transaction: efTx,
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 60
                ));

                var list = new List<WorkFlowReportLineDto>();
                int total = 0;

                foreach (var r in rows)
                {
                    if (total == 0) total = r.TotalCount;

                    list.Add(new WorkFlowReportLineDto
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

                return new PagedResult<WorkFlowReportLineDto>(list, total, q.Page, q.PageSize);
            }
            finally
            {
                if (mustClose && conn.State == ConnectionState.Open)
                    await conn.CloseAsync();
            }
        }

        //excel export 
        public async Task<(byte[] Content, string FileName, string ContentType)> ExportReportLinesAsync(ReportQueryParams q)
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
                var rows = await conn.QueryAsync<ReportLineRowDto>(new CommandDefinition(
                    "dbo.usp_ReportSearch_Lines",
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

        /// Servis Üürnleri 
        private async Task<ResponseModel> EnsurePricesCapturedAsync(string requestNo, bool force = false)
        {
            // Customer + Product navigations lazım → include’ları aç
            var q = _uow.Repository.GetQueryable<ServicesRequestProduct>()
                .Include(x => x.Product)
                .Include(x => x.Customer)
                    .ThenInclude(c => c.CustomerGroup).ThenInclude(g => g.GroupProductPrices)
                .Include(x => x.Customer)
                    .ThenInclude(c => c.CustomerProductPrices)
                .Where(x => x.RequestNo == requestNo);

            if (!force)
                q = q.Where(x => !x.IsPriceCaptured);

            var list = await q.ToListAsync();
            if (list.Count == 0) return ResponseModel.Success();

            foreach (var p in list)
            {
                // Kaynak ve birim fiyatı belirle
                CapturedPriceSource src;
                decimal unit;

                var grp = p.Customer?.CustomerGroup?.GroupProductPrices?.FirstOrDefault(g => g.ProductId == p.ProductId);
                if (grp is not null)
                {
                    src = CapturedPriceSource.Group;
                    unit = grp.Price;
                }
                else
                {
                    var cust = p.Customer?.CustomerProductPrices?.FirstOrDefault(c => c.ProductId == p.ProductId);
                    if (cust is not null)
                    {
                        src = CapturedPriceSource.Customer;
                        unit = cust.Price;
                    }
                    else
                    {
                        src = CapturedPriceSource.Standard;
                        unit = p.Product?.Price ?? 0m;
                    }
                }

                var currency = p.Product?.PriceCurrency ?? "TRY";
                var total = unit * p.Quantity;

                p.CapturedSource = src;
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
