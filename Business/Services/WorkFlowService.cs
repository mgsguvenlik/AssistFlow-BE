using Azure.Core;
using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
using Model.Dtos.WorkFlowDtos.ServicesRequestProduct;
using Model.Dtos.WorkFlowDtos.TechnicalService;
using Model.Dtos.WorkFlowDtos.Warehouse;
using Model.Dtos.WorkFlowDtos.WorkFlow;
using Model.Dtos.WorkFlowDtos.WorkFlowStatus;
using System.Security.Cryptography;

namespace Business.Services
{
    public class WorkFlowService : IWorkFlowService
    {
        private readonly IUnitOfWork _uow;
        private readonly IMapper _mapper;
        private readonly IAuthService _authService;
        private readonly TypeAdapterConfig _config;
        public WorkFlowService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, IAuthService authService)
        {
            _uow = uow;
            _mapper = mapper;
            _config = config;
            _authService = authService;
        }

        // -------------------- ServicesRequest --------------------

        //1 Servis Talebi oluşturma akışı:
        public async Task<ResponseModel<ServicesRequestGetDto>> CreateRequestAsync(ServicesRequestCreateDto dto)
        {
            try
            {
                // 1) RequestNo yoksa üret
                if (string.IsNullOrWhiteSpace(dto.RequestNo))
                {
                    var rn = await GetRequestNoAsync("SR");
                    if (!rn.IsSuccess)
                        return ResponseModel<ServicesRequestGetDto>.Fail(rn.Message, rn.StatusCode);
                    dto.RequestNo = rn.Data!;
                }

                bool exists = await _uow.Repository.GetQueryable<WorkFlow>().AsNoTracking().AnyAsync(x => x.RequestNo == dto.RequestNo);
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

                var statuExist = await _uow.Repository.GetQueryable<WorkFlowStatus>().AsNoTracking().AnyAsync(s => s.Id == dto.StatuId);
                if (!statuExist)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Durum (Statu) bulunamadı.", StatusCode.Conflict);


                // 2) ServicesRequest map + ürün bağları (N-N join)
                var request = dto.Adapt<ServicesRequest>(_config);

                request.CreatedDate = DateTime.Now;
                request.CreatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;

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
                // 3) WorkFlow oluştur (aynı RequestNo ile)
                var wf = new WorkFlow
                {
                    RequestNo = request.RequestNo,
                    RequestTitle = "Servis Talebi",
                    Priority = dto.Priority,
                    StatuId = dto.StatuId,
                    CreatedDate = DateTime.Now,
                    CreatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0,
                    IsCancelled = false,
                    IsComplated = false,
                    ReconciliationStatus = WorkFlowReconciliationStatus.Pending,
                    IsLocationValid = dto.IsLocationValid,
                    ApproverTechnicianId = dto.ApproverTechnicianId
                };

                await _uow.Repository.AddAsync(wf);
                await _uow.Repository.CompleteAsync();

                // 5) Tekrar oku ve DTO döndür (include’ları uygula)
                return await GetRequestByIdAsync(request.Id);
            }
            catch (Exception ex)
            {
                //await tx.RollbackAsync();//MZK Rollback işlemi uow içinde yapılıyor mu yapılmıyor ise düzenle.
                return ResponseModel<ServicesRequestGetDto>.Fail($"Oluşturma sırasında hata: {ex.Message}", StatusCode.Error);
            }
        }

        //2.1 Depoya Gönderim  (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> SendWarehouseAsync(SendWarehouseDto dto)
        {
            // 1️ Talep getir (tracking kapalı)
            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<WarehouseGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            if (request.IsSended)
                return ResponseModel<WarehouseGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);


            // 2️ WorkFlow getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo);

            if (wf is null)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            if (wf.IsCancelled)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış iptal edilmiştir.", StatusCode.NotFound);

            // 3️ “Depoda” statüsünü bul
            var depoda = await _uow.Repository
                .GetQueryable<WorkFlowStatus>()
                .AsNoTracking()
                .Where(x => x.Code != null && (x.Code == "INHOUSE" || x.Code == "DEPODA"))
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (depoda is null)
                return ResponseModel<WarehouseGetDto>.Fail("WorkFlowStatus içinde 'Depoda' statüsü tanımlı değil.", StatusCode.BadRequest);

            // 4️ Warehouse kaydını getir (varsa)
            var warehouse = await _uow.Repository
                .GetQueryable<Warehouse>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            // 5️ Yoksa oluştur
            if (warehouse == null)
            {
                warehouse = new Warehouse
                {
                    RequestNo = request.RequestNo,
                    DeliveryDate = dto.DeliveryDate,
                    Description = string.Empty,
                    IsSended = false,
                };
                warehouse.CreatedDate = DateTime.Now;
                warehouse.CreatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;

                warehouse = await _uow.Repository.AddAsync(warehouse);
            }
            else
            {
                return ResponseModel<WarehouseGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);
            }

            request.IsSended = true;
            request.SendedStatusId = depoda.Id;
            request.WorkFlowStatus = null; // opsiyonel, FK resetlenebilir
            request.UpdatedDate = DateTime.Now;
            request.UpdatedUser = _authService.MeAsync().Result?.Data?.Id ?? 0;
            _uow.Repository.Update(request);


            //6 WorkFlow güncelle
            wf.StatuId = depoda.Id;
            wf.IsCancelled = false;
            wf.IsComplated = false;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

            //7 Commit
            await _uow.Repository.CompleteAsync();

            //8 Güncel talebi döndür
            return await GetWarehouseByRequestNoAsync(request.RequestNo);
        }

        //2.2 Depo Teslimatı ve Teknik servise Gönderim (Ürün var ise)
        public async Task<ResponseModel<WarehouseGetDto>> CompleteDeliveryAsync(CompleteDeliveryDto dto)
        {
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (wf is null)
                return ResponseModel<WarehouseGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            bool exists = await _uow.Repository
                .GetQueryable<TechnicalService>()
                .AsNoTracking()
                .AnyAsync(x => x.RequestNo == dto.RequestNo);
            if (exists)
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

            // 🔹 Teknik servis kaydı oluştur
            var technicalService = new TechnicalService
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

            // 🔹 Warehouse bilgilerini güncelle
            warehouse.IsSended = true;
            warehouse.DeliveryDate = dto.DeliveryDate;
            warehouse.Description = dto.Description;
            _uow.Repository.Update(warehouse);

            // 🔹 WorkFlow güncelle
            var statu = await _uow.Repository
                .GetQueryable<WorkFlowStatus>()
                .AsNoTracking()
                .Where(x => x.Code != null && x.Code == "TECNICALSERVICE")
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (statu is null)
                return ResponseModel<WarehouseGetDto>.Fail("WorkFlowStatus içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);

            wf.StatuId = statu.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

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

            // 🔹 Değişiklikleri kaydet
            await _uow.Repository.CompleteAsync();

            // 🔹 Son durumu döndür
            return await GetWarehouseByIdAsync(warehouse.Id);
        }

        //2.3 Teknik Servis Gönderim  (Ürün yok ise)
        public async Task<ResponseModel<TechnicalServiceGetDto>> SendTechnicalServiceAsync(SendTechnicalServiceDto dto)
        {
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            bool exists = await _uow.Repository
                .GetQueryable<TechnicalService>()
                .AsNoTracking()
                .AnyAsync(x => x.RequestNo == dto.RequestNo);
            if (exists)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Aynı akış numarası ile başka bir kayıt zaten var.", StatusCode.Conflict);

            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.Customer)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            // 🔹 Teknik servis kaydı oluştur
            var technicalService = new TechnicalService
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


            // 🔹 WorkFlow güncelle
            var statu = await _uow.Repository
                .GetQueryable<WorkFlowStatus>()
                .AsNoTracking()
                .Where(x => x.Code != null && x.Code == "TECNICALSERVICE")
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (statu is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("WorkFlowStatus içinde 'Teknik Servis' statüsü tanımlı değil.", StatusCode.BadRequest);

            wf.StatuId = statu.Id;
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            _uow.Repository.Update(wf);

            // 🔹 Değişiklikleri kaydet
            await _uow.Repository.CompleteAsync();

            // 🔹 Son durumu döndür
            return await GetTechnicalServiceByRequestNoAsync(dto.RequestNo);
        }

        // 4️ Teknik Servis Servisi Başlatma 
        public async Task<ResponseModel<TechnicalServiceGetDto>> StartService(TechnicalServiceUpdateDto dto)
        {
            // 2️ WorkFlow getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (wf is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);

            var technicalService = await _uow.Repository
           .GetQueryable<TechnicalService>()
           .AsNoTracking()
           .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (technicalService is null)
                return ResponseModel<TechnicalServiceGetDto>.Fail("İlgili teknik servis kaydı bulunamadı.", StatusCode.NotFound);

            technicalService.Adapt(dto, _config);
            technicalService.StartTime = DateTime.Now;
            technicalService.ServicesStatus = TechnicalServiceStatus.Started;


            return null;
        }
        // 45 Teknik Servis Servisi Tamamlama 
        public async Task<ResponseModel<TechnicalServiceGetDto>> FinishService(TechnicalServiceUpdateDto dto)
        {
            return null;
        }
        //-----------------------------

        private static Func<IQueryable<ServicesRequest>, IIncludableQueryable<ServicesRequest, object>>? RequestIncludes()
            => q => q
                .Include(x => x.Customer).ThenInclude(x => x.CustomerProductPrices)
                .Include(x => x.Customer).ThenInclude(x => x.CustomerGroup).ThenInclude(x => x.GroupProductPrices)
                .Include(x => x.ServiceType)
                .Include(x => x.CustomerApprover)
                .Include(x => x.CustomerApprover)
                .Include(x => x.WorkFlowStatus);

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

        public async Task<ResponseModel<ServicesRequestGetDto>> GetRequestByIdAsync(long id)
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
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            dto.ApproverTechnicianId = workflow?.ApproverTechnicianId ?? 0;
            dto.ServicesRequestProducts = products; // DTO’da ürün listesi property’si olmalı
            return ResponseModel<ServicesRequestGetDto>.Success(dto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> GetRequestByNoAsync(string requestNo)
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
               .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            dto.ApproverTechnicianId = workflow?.ApproverTechnicianId ?? 0;
            dto.ServicesRequestProducts = products; // DTO’da ürün listesi property’si olmalı
            return ResponseModel<ServicesRequestGetDto>.Success(dto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> UpdateRequestAsync(ServicesRequestUpdateDto dto)
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
            .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (wf is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("İlgili akış kaydı bulunamadı.", StatusCode.NotFound);
            // Ana talep bilgilerini güncelle
            wf.UpdatedDate = DateTime.Now;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;
            wf.IsLocationValid = dto.IsLocationValid;
            _uow.Repository.Update(wf);



            dto.Adapt(entity, _config);

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
            return await GetRequestByIdAsync(entity.Id);
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

        public async Task<ResponseModel<TechnicalServiceGetDto>> GetTechnicalServiceByRequestNoAsync(string requestNo)
        {
            var query = _uow.Repository.GetQueryable<TechnicalService>();
            var dto = await query
                .AsNoTracking()
                .Where(x => x.RequestNo == requestNo)
                .Include(x => x.UsedMaterials)
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


        // -------------------- WorkFlowStatus --------------------
        public async Task<ResponseModel<PagedResult<WorkFlowStatusGetDto>>> GetStatusesAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<WorkFlowStatus>();
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.Name.Contains(q.Search) || (x.Code ?? "").Contains(q.Search));

            var total = await query.CountAsync();
            var items = await query
                .OrderBy(x => x.Name)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ProjectToType<WorkFlowStatusGetDto>(_config)
                .ToListAsync();

            return ResponseModel<PagedResult<WorkFlowStatusGetDto>>
                .Success(new PagedResult<WorkFlowStatusGetDto>(items, total, q.Page, q.PageSize));
        }

        public async Task<ResponseModel<WorkFlowStatusGetDto>> GetStatusByIdAsync(long id)
        {
            var dto = await _uow.Repository.GetQueryable<WorkFlowStatus>()
                .Where(x => x.Id == id)
                .ProjectToType<WorkFlowStatusGetDto>(_config)
                .FirstOrDefaultAsync();

            if (dto is null)
                return ResponseModel<WorkFlowStatusGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            return ResponseModel<WorkFlowStatusGetDto>.Success(dto);
        }

        public async Task<ResponseModel<WorkFlowStatusGetDto>> CreateStatusAsync(WorkFlowStatusCreateDto dto)
        {
            var entity = dto.Adapt<WorkFlowStatus>(_config);
            await _uow.Repository.AddAsync(entity);
            await _uow.Repository.CompleteAsync();
            return await GetStatusByIdAsync(entity.Id);
        }

        public async Task<ResponseModel<WorkFlowStatusGetDto>> UpdateStatusAsync(WorkFlowStatusUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<WorkFlowStatus>(false, x => x.Id == dto.Id);
            if (entity is null)
                return ResponseModel<WorkFlowStatusGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            dto.Adapt(entity, _config);
            await _uow.Repository.CompleteAsync();
            return await GetStatusByIdAsync(entity.Id);
        }

        public async Task<ResponseModel> DeleteStatusAsync(long id)
        {
            // 1) Kaydı (tracked) getir
            var entity = await _uow.Repository.GetSingleAsync<WorkFlowStatus>(
                asNoTracking: false,
                x => x.Id == id);

            if (entity is null)
                return ResponseModel.Fail("Silinecek kayıt bulunamadı.", StatusCode.NotFound);
            // 2) Soft delete uygula (entity + 2 tip argümanı ver)
            await _uow.Repository.HardDeleteAsync<WorkFlowStatus, long>(entity);

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
                                         .AnyAsync(x => x.RequestNo == candidate);

                if (!exists)
                    return ResponseModel<string>.Success(candidate, "Yeni Akış Numarası üretildi.");
            }
            // Çok istisnai durumda buraya düşer
            return ResponseModel<string>.Fail("Benzersiz RequestNo üretilemedi, lütfen tekrar deneyin.");
        }
        public async Task<ResponseModel<PagedResult<WorkFlowGetDto>>> GetWorkFlowsAsync(QueryParams q)
        {
            var query = _uow.Repository.GetQueryable<WorkFlow>();
            if (!string.IsNullOrWhiteSpace(q.Search))
                query = query.Where(x => x.RequestNo.Contains(q.Search) || x.RequestTitle.Contains(q.Search));

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
                .Where(x => x.Id == id)
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
                                               // entity.DeletedByUserId = currentUserId;   // varsa

            // 3) SoftDelete çağrısı -> 2 tip argümanı verin ve entity gönderin
            await _uow.Repository.SoftDeleteAsync<Model.Concrete.WorkFlows.WorkFlow, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }
    }
}
