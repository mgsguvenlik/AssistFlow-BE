using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete.WorkFlows;
using Model.Dtos.WorkFlowDtos.ServicesRequest;
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

                var query = _uow.Repository.GetQueryable<WorkFlow>();
                bool exists = await query.AsNoTracking()
                                         .AnyAsync(x => x.RequestNo == dto.RequestNo);

                if (exists)
                    return ResponseModel<ServicesRequestGetDto>.Fail("Aynı akış numarasi ile başka bir kayıt zaten var.", StatusCode.Conflict);


                // 2) ServicesRequest map + ürün bağları (N-N join)
                var request = dto.Adapt<ServicesRequest>(_config);

                if (request.ServicesRequestProducts is null)
                    request.ServicesRequestProducts = new List<ServicesRequestProduct>();

                if (dto.ProductIds is not null)
                {
                    foreach (var pid in dto.ProductIds.Distinct())
                        request.ServicesRequestProducts.Add(new ServicesRequestProduct { ProductId = pid });
                }

                var res = await _uow.Repository.AddAsync(request);
                await _uow.Repository.CompleteAsync(); // Id üretildi

                // 3) WorkFlow oluştur (aynı RequestNo ile)
                var wf = new WorkFlow
                {
                    RequestNo = request.RequestNo,
                    RequestTitle = "Servis Talebi",
                    Priority = dto.Priority,
                    StatuId = dto.StatuId,
                    CreatedDate = DateTimeOffset.UtcNow,
                    CreatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0,
                    IsCancelled = false,
                    IsComplated = false,
                    ReconciliationStatus = WorkFlowReconciliationStatus.Pending,

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
        //-----------------------------


        //Depoya Gönderim
        public async Task<ResponseModel<ServicesRequestGetDto>> SendWarehouseAsync(SendWarehouseDto dto)
        {
            // 1) Talep getir
            var request = await _uow.Repository
                .GetQueryable<ServicesRequest>()
                .Include(x => x.ServicesRequestProducts)
                .FirstOrDefaultAsync(x => x.RequestNo == dto.RequestNo);

            if (request is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Servis talebi bulunamadı.", StatusCode.NotFound);

            if (request.IsSended)
                return ResponseModel<ServicesRequestGetDto>.Fail("Bu talep zaten depoya gönderilmiş.", StatusCode.Conflict);

            // 2) İlgili WorkFlow’u RequestNo üzerinden getir
            var wf = await _uow.Repository
                .GetQueryable<WorkFlow>()
                .FirstOrDefaultAsync(x => x.RequestNo == request.RequestNo);

            if (wf is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("İlgili WorkFlow kaydı bulunamadı.", StatusCode.NotFound);

            // 3) “Depoda” durumunu bul (Code veya Name ile esnek arama)
            var depoda = await _uow.Repository
                .GetQueryable<WorkFlowStatus>()
                .Where(x =>x.Code != null && (x.Code == "INHOUSE" || x.Code == "DEPODA"))
                .Select(x => new { x.Id })
                .FirstOrDefaultAsync();

            if (depoda is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("WorkFlowStatus içinde 'Depoda' statüsü tanımlı değil.", StatusCode.BadRequest);

            // 4) Warehouse kaydını oluştur
            var productIds = (dto.ProductIds is { Count: > 0 })
                ? dto.ProductIds.Distinct().ToList()
                : request.ServicesRequestProducts?.Select(p => p.ProductId).Distinct().ToList() ?? new List<long>();

            var warehouse = new Warehouse
            {
                RequestNo = request.RequestNo,
                DeliveryDate = dto.DeliveryDate,
                ApproverTechnicianId = dto.ApproverTechnicianId,
                Description = dto.Description,
                IsSended = true,
                WarehouseProducts = new List<ServicesRequestProduct>()
            };

            foreach (var pid in productIds)
                warehouse.WarehouseProducts.Add(new ServicesRequestProduct { ProductId = pid });

            await _uow.Repository.AddAsync(warehouse);

            // 5) ServicesRequest’i güncelle
            request.IsSended = true;
            request.SendedStatusId = depoda.Id;              // FK lookup
            request.WorkFlowStatus = null;                   // (opsiyonel) sadece FK ile güncelleyecekseniz nav’ı null bırakabilirsiniz

            // 6) WorkFlow’u güncelle
            wf.StatuId = depoda.Id;
            wf.IsCancelled = false;
            wf.IsComplated = false;
            wf.UpdatedDate = DateTimeOffset.UtcNow;
            wf.UpdatedUser = (await _authService.MeAsync())?.Data?.Id ?? 0;

            // 7) Commit
            await _uow.Repository.CompleteAsync();

            // 8) Güncel talebi döndür
            return await GetRequestByIdAsync(request.Id);
        }
        //-----------------------------

        private static Func<IQueryable<ServicesRequest>, IIncludableQueryable<ServicesRequest, object>>? RequestIncludes()
            => q => q
                .Include(x => x.Customer)
                .Include(x => x.ServiceType)
                .Include(x => x.CustomerApprover)
                .Include(x => x.WorkFlowStatus)
                .Include(x => x.ServicesRequestProducts).ThenInclude(sr => sr.Product);

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

            return ResponseModel<ServicesRequestGetDto>.Success(dto);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> UpdateRequestAsync(ServicesRequestUpdateDto dto)
        {
            var entity = await _uow.Repository.GetSingleAsync<ServicesRequest>(
                false,
                 x => x.Id == dto.Id,
                 includeExpression: RequestIncludes());

            if (entity is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            // map partial
            dto.Adapt(entity, _config);

            // ürün listesi değişimi istenirse:
            if (dto.ProductIds is not null)
            {
                var desired = dto.ProductIds.Distinct().ToHashSet();
                var current = entity.ServicesRequestProducts.Select(p => p.ProductId).ToHashSet();

                var toAdd = desired.Except(current);
                var toRemove = current.Except(desired);

                if (toRemove.Any())
                    entity.ServicesRequestProducts =
                        entity.ServicesRequestProducts.Where(p => !toRemove.Contains(p.ProductId)).ToList();

                foreach (var pid in toAdd)
                    entity.ServicesRequestProducts.Add(new ServicesRequestProduct { ProductId = pid, ServicesRequestId = entity.Id });
            }

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
            entity.UpdatedDate = DateTimeOffset.UtcNow; // varsa

            // 3) SoftDelete çağrısı -> 2 tip argümanı verin ve entity gönderin
            await _uow.Repository.SoftDeleteAsync<Model.Concrete.WorkFlows.ServicesRequest, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }

        public async Task<ResponseModel<ServicesRequestGetDto>> ReplaceRequestProductsAsync(long requestId, IEnumerable<long> productIds)
        {
            var entity = await _uow.Repository.GetSingleAsync<ServicesRequest>(
                false,
                x => x.Id == requestId,
                q => q.Include(x => x.ServicesRequestProducts));

            if (entity is null)
                return ResponseModel<ServicesRequestGetDto>.Fail("Kayıt bulunamadı.", StatusCode.NotFound);

            var desired = (productIds ?? Enumerable.Empty<long>()).Distinct().ToList();

            entity.ServicesRequestProducts.Clear();
            foreach (var pid in desired)
                entity.ServicesRequestProducts.Add(new ServicesRequestProduct { ServicesRequestId = entity.Id, ProductId = pid });

            await _uow.Repository.CompleteAsync();
            return await GetRequestByIdAsync(entity.Id);
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
            entity.UpdatedDate = DateTimeOffset.UtcNow; // varsa
                                                        // entity.DeletedByUserId = currentUserId;   // varsa

            // 3) SoftDelete çağrısı -> 2 tip argümanı verin ve entity gönderin
            await _uow.Repository.SoftDeleteAsync<Model.Concrete.WorkFlows.WorkFlow, long>(entity);

            await _uow.Repository.CompleteAsync();
            return ResponseModel.Success(status: StatusCode.NoContent);
        }
    }
}
