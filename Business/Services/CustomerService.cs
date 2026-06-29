using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Core.Utilities.Constants;
using Mapster;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Model.Concrete;
using Model.Dtos.Customer;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Business.Services
{
    public class CustomerService
      : CrudServiceBase<Customer, long, CustomerCreateDto, CustomerUpdateDto, CustomerGetDto>,
        ICustomerService
    {
        public CustomerService(IUnitOfWork uow, IMapper mapper, TypeAdapterConfig config, ICurrentUser currentUser)
            : base(uow, mapper, config, currentUser) { }

        protected override long ReadKey(Customer e) => e.Id;
        protected override Expression<Func<Customer, bool>> KeyPredicate(long id) => x => x.Id == id;

        protected override Func<IQueryable<Customer>, IIncludableQueryable<Customer, object>>? IncludeExpression()
            => q => q
          .Include(c => c.CustomerType)
          .Include(c => c.CustomerGroup)
          .Include(c => c.CustomerSystemAssignments)
            .ThenInclude(a => a.CustomerSystem);

        protected override Task<Customer?> ResolveEntityForUpdateAsync(CustomerUpdateDto dto)
          => _unitOfWork.Repository.GetSingleAsync<Customer>(
            asNoTracking: false,
            x => x.Id == dto.Id,
            includeExpression: q => q
                .Include(c => c.CustomerType)
                .Include(c => c.CustomerGroup)
                .Include(c => c.CustomerSystemAssignments)
                    .ThenInclude(a => a.CustomerSystem)  // 🔹 yeni
        );

        public override async Task<ResponseModel<CustomerGetDto>> UpdateAsync(CustomerUpdateDto dto)
        {
            var response = new ResponseModel<CustomerGetDto>();

            // 1) Entity’yi include’lu çek
            var entity = await ResolveEntityForUpdateAsync(dto);
            if (entity == null)
            {
                response.IsSuccess = false;          // kendi ResponseModel alanlarına göre düzelt
                response.Message = "Customer not found.";
                return response;
            }

            // 2) Scalar alanları map et (CustomerSystems Mapster config’inde ignore)
            _mapper.Map(dto, entity);

            // 3) SystemIds varsa müşteri-sistem ilişkilerini (CustomerSystemAssignment) güncelle
            if (dto.SystemIds != null)
            {
                var systemIds = dto.SystemIds.Distinct().ToList();

                var systems = await _unitOfWork.Repository
                    .GetQueryable<CustomerSystem>()
                    .Where(s => systemIds.Contains(s.Id))
                    .ToListAsync();

                entity.CustomerSystemAssignments ??= new List<CustomerSystemAssignment>();

                // Mevcut assignment’ları listele
                var existingAssignments = entity.CustomerSystemAssignments.ToList();

                // DTO’da artık olmayan sistemler için assignment’ları sil
                foreach (var assignment in existingAssignments)
                {
                    if (!systemIds.Contains(assignment.CustomerSystemId))
                    {
                        entity.CustomerSystemAssignments.Remove(assignment);
                    }
                }

                // DTO’da gelen sistemler için eksik assignment’ları ekle
                var existingSystemIds = entity.CustomerSystemAssignments
                    .Select(a => a.CustomerSystemId)
                    .ToHashSet();

                foreach (var system in systems)
                {
                    if (!existingSystemIds.Contains(system.Id))
                    {
                        entity.CustomerSystemAssignments.Add(new CustomerSystemAssignment
                        {
                            CustomerId = entity.Id,
                            CustomerSystemId = system.Id,

                            // 🔹 Şimdilik “seçili sistemler” = “bakım anlaşması var” şeklinde yorumladım.
                            // İleride ayrı bir DTO ile HasMaintenanceContract bilgisini de dışarı açabiliriz.
                            HasMaintenanceContract = true
                        });
                    }
                }
            }

            // 4) Kaydet
            await _unitOfWork.Repository.CompleteAsync();

            // 5) DTO’ya map et ve ResponseModel ile dön
            var resultDto = _mapper.Map<CustomerGetDto>(entity);

            response.IsSuccess = true;               // kendi ResponseModel yapına göre
            response.Data = resultDto;
            response.Message = "Customer updated successfully.";

            return response;
        }

        public override async Task<ResponseModel<CustomerGetDto>> CreateAsync(CustomerCreateDto dto)
        {
            var response = new ResponseModel<CustomerGetDto>();

            var entity = _mapper.Map<Customer>(dto);

            // Create sırasında da sistem ataması yap (CustomerSystemAssignment)
            if (dto.SystemIds != null && dto.SystemIds.Any())
            {
                var systemIds = dto.SystemIds.Distinct().ToList();

                var systems = await _unitOfWork.Repository
                    .GetQueryable<CustomerSystem>()
                    .Where(s => systemIds.Contains(s.Id))
                    .ToListAsync();

                entity.CustomerSystemAssignments = systems
                    .Select(s => new CustomerSystemAssignment
                    {
                        Customer = entity,
                        CustomerSystem = s,
                        HasMaintenanceContract = true   // 🔹 varsayılan: seçili sistemler için bakım var
                    })
                    .ToList();
            }

            await _unitOfWork.Repository.AddAsync(entity);
            await _unitOfWork.Repository.CompleteAsync();

            var resultDto = _mapper.Map<CustomerGetDto>(entity);

            response.IsSuccess = true;
            response.Data = resultDto;
            response.Message = "Customer created successfully.";

            return response;
        }


        public override async Task<ResponseModel<PagedResult<CustomerGetDto>>> GetPagedAsync(QueryParams q)
        {
            try
            {
                q ??= new QueryParams();

                var page = q.Page < 1 ? 1 : q.Page;
                var pageSize = q.PageSize < 1 ? 20 : q.PageSize;

                var query = _unitOfWork.Repository
                            .GetQueryable<Customer>()
                            .Include(c => c.CustomerType)
                            .Include(c => c.CustomerGroup)
                            .Include(c => c.Tenant)
                            .Include(c => c.CustomerSystemAssignments)
                                .ThenInclude(a => a.CustomerSystem)
                            .AsQueryable();

                // Tenant filtresi BİLEREK uygulanmıyor.
                // ApplyTenantFilterIfNeeded(query) çağrısı yok.

                // Soft-delete kayıtları gösterme
                query = query.Where(c => !c.IsDeleted);

                // Arama
                if (!string.IsNullOrWhiteSpace(q.Search))
                {
                    var searchText = q.Search.Trim();
                    var searchTerm = searchText.ToLower();

                    var isNumericSearch = int.TryParse(searchText, out var numericValue);

                    query = query.Where(c =>
                        // Müşteri temel bilgileri
                        (c.SubscriberCode != null &&
                            c.SubscriberCode.ToLower().Contains(searchTerm)) ||

                        (c.SubscriberCompany != null &&
                            c.SubscriberCompany.ToLower().Contains(searchTerm)) ||

                        (c.SubscriberAddress != null &&
                            c.SubscriberAddress.ToLower().Contains(searchTerm)) ||

                        (c.City != null &&
                            c.City.ToLower().Contains(searchTerm)) ||

                        (c.District != null &&
                            c.District.ToLower().Contains(searchTerm)) ||

                        (c.LocationCode != null &&
                            c.LocationCode.ToLower().Contains(searchTerm)) ||

                        (c.CustomerShortCode != null &&
                            c.CustomerShortCode.ToLower().Contains(searchTerm)) ||

                        (c.CorporateLocationId != null &&
                            c.CorporateLocationId.ToLower().Contains(searchTerm)) ||

                        // İletişim bilgileri
                        (c.ContactName1 != null &&
                            c.ContactName1.ToLower().Contains(searchTerm)) ||

                        (c.ContactName2 != null &&
                            c.ContactName2.ToLower().Contains(searchTerm)) ||

                        (c.Phone1 != null &&
                            c.Phone1.Contains(searchText)) ||

                        (c.Phone2 != null &&
                            c.Phone2.Contains(searchText)) ||

                        (c.Email1 != null &&
                            c.Email1.ToLower().Contains(searchTerm)) ||

                        (c.Email2 != null &&
                            c.Email2.ToLower().Contains(searchTerm)) ||

                        // Yeni müşteri alanları
                        (c.LockType != null &&
                            c.LockType.ToLower().Contains(searchTerm)) ||

                        (c.CashCenter != null &&
                            c.CashCenter.ToLower().Contains(searchTerm)) ||

                        (c.Note != null &&
                            c.Note.ToLower().Contains(searchTerm)) ||

                        // Tenant bilgileri
                        (c.Tenant != null &&
                            c.Tenant.Code != null &&
                            c.Tenant.Code.ToLower().Contains(searchTerm)) ||

                        (c.Tenant != null &&
                            c.Tenant.Name != null &&
                            c.Tenant.Name.ToLower().Contains(searchTerm)) ||

                        // Sayısal alanlar: örn. SerialNo veya MonitoringStatus
                        (isNumericSearch &&
                            (c.SerialNo == numericValue ||
                             c.MonitoringStatus == numericValue))
                    );
                }

                // Sıralama
                var sort = q.Sort?.Trim().ToLowerInvariant();

                query = sort switch
                {
                    "name" or "subscribercompany" =>
                        q.Desc
                            ? query.OrderByDescending(c => c.SubscriberCompany)
                            : query.OrderBy(c => c.SubscriberCompany),

                    "code" or "subscribercode" =>
                        q.Desc
                            ? query.OrderByDescending(c => c.SubscriberCode)
                            : query.OrderBy(c => c.SubscriberCode),

                    "city" =>
                        q.Desc
                            ? query.OrderByDescending(c => c.City)
                            : query.OrderBy(c => c.City),

                    "createddate" =>
                        q.Desc
                            ? query.OrderByDescending(c => c.CreatedDate)
                            : query.OrderBy(c => c.CreatedDate),

                    "id" =>
                        q.Desc
                            ? query.OrderByDescending(c => c.Id)
                            : query.OrderBy(c => c.Id),

                    _ => query.OrderByDescending(c => c.Id)
                };

                var total = await query.CountAsync();

                var items = await query
                    .AsNoTracking()
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ProjectToType<CustomerGetDto>(_config)
                    .ToListAsync();

                return ResponseModel<PagedResult<CustomerGetDto>>.Success(
                    new PagedResult<CustomerGetDto>(
                        items,
                        total,
                        page,
                        pageSize
                    )
                );
            }
            catch (Exception ex)
            {
                return ResponseModel<PagedResult<CustomerGetDto>>.Fail(
                    $"{Messages.UnexpectedError}: {ex.Message}",
                    StatusCode.Error
                );
            }
        }
        public async Task<ResponseModel<int>> ImportFromFileAsync(string filePath)
        {
            var response = new ResponseModel<int>();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                response.IsSuccess = false;
                response.Message = "Dosya yolu boş.";
                return response;
            }

            if (!File.Exists(filePath))
            {
                response.IsSuccess = false;
                response.Message = $"Dosya bulunamadı: {filePath}";
                return response;
            }

            await using var stream = File.OpenRead(filePath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            ExternalCustomerRoot? root;
            try
            {
                root = await JsonSerializer.DeserializeAsync<ExternalCustomerRoot>(stream, options);
            }
            catch (Exception ex)
            {
                response.IsSuccess = false;
                response.Message = $"JSON deserialize edilirken hata oluştu: {ex.Message}";
                return response;
            }

            if (root == null || root.Results == null || root.Results.Count == 0)
            {
                response.IsSuccess = false;
                response.Message = "JSON içinden geçerli 'results' verisi okunamadı.";
                return response;
            }

            // Asıl işi yapan metodu çağırıyoruz
            return await ImportFromExternalAsync(root.Results);
        }

        public async Task<ResponseModel<int>> ImportFromExternalAsync(IEnumerable<ExternalCustomerRow> rows)
        {
            var response = new ResponseModel<int>();

            if (rows == null)
            {
                response.IsSuccess = false;
                response.Message = "Veri bulunamadı.";
                return response;
            }

            var rowList = rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Id))
                .ToList();

            if (!rowList.Any())
            {
                response.IsSuccess = false;
                response.Message = "Geçerli kayıt bulunamadı.";
                return response;
            }

            // 1) Tüm id ve dealerId listelerini hazırla
            var subscriberCodes = rowList
                .Select(r => r.Id.Trim())
                .Distinct()
                .ToList();

            var dealerCodes = rowList
                .Where(r => !string.IsNullOrWhiteSpace(r.DealerId))
                .Select(r => r.DealerId.Trim())
                .Distinct()
                .ToList();

            // 2) Zaten var olan müşterileri çek → tekrar ekleme
            var existingCodes = await _unitOfWork.Repository
                .GetQueryable<Customer>()
                .Where(c => subscriberCodes.Contains(c.SubscriberCode!))
                .Select(c => c.SubscriberCode!)
                .ToListAsync();

            var existingSet = new HashSet<string>(existingCodes);

            // 3) CustomerGroups tablosundan dealerId (Code) eşlemesi
            var customerGroups = await _unitOfWork.Repository
                .GetQueryable<CustomerGroup>()
                .Where(g => dealerCodes.Contains(g.Code))
                .ToListAsync();

            var groupDict = customerGroups
                .GroupBy(g => g.Code)
                .ToDictionary(g => g.Key, g => g.First());

            var now = DateTime.Now;
            var insertedCount = 0;

            foreach (var row in rowList)
            {
                var subscriberCode = row.Id.Trim();

                // Aynı SubscriberCode daha önce eklenmişse atla
                if (existingSet.Contains(subscriberCode))
                    continue;

                // dealerId üzerinden CustomerGroup bul
                CustomerGroup? group = null;
                if (!string.IsNullOrWhiteSpace(row.DealerId))
                {
                    groupDict.TryGetValue(row.DealerId.Trim(), out group);
                }

                // contPoint email mi telefon mu?
                bool isEmail = IsEmail(row.ContPoint);
                string? phone = isEmail ? null : CleanPhone(row.ContPoint);
                string? email = isEmail ? row.ContPoint?.Trim() : null;

                // Adres birleştirme: addr1 + addr2 + postcode + city
                var address = BuildAddress(
                    row.Addr1,
                    row.Addr2,
                    row.Postcode,
                    row.City
                );

                var entity = new Customer
                {
                    SubscriberCode = subscriberCode,
                    // CustomerGroups tablosundaki Name → SubscriberCompany
                    SubscriberCompany = group?.GroupName,
                    SubscriberAddress = address,
                    City = row.City,
                    District = row.Postcode,
                    LocationCode = null,

                    ContactName1 = row.Name,
                    Phone1 = phone,
                    Email1 = email,

                    ContactName2 = null,
                    Phone2 = null,
                    Email2 = null,

                    CustomerShortCode = null,
                    CorporateLocationId = null,
                    Longitude = null,
                    Latitude = null,
                    InstallationDate = null,

                    CustomerGroupId = group?.Id,
                    CustomerTypeId = 4,

                    CreatedDate = now,
                    UpdatedDate = null,
                    CreatedUser = 0,
                    UpdatedUser = 0,
                    IsDeleted = false,
                    WarrantyYears = 2,
                    Note = null,
                };

                // DTO → Entity

                // İstersen ilişkiyi de set edebilirsin
                if (group != null)
                {
                    entity.CustomerGroupId = group.Id;
                    entity.CustomerGroup = group;
                }

                await _unitOfWork.Repository.AddAsync(entity);
                insertedCount++;
            }

            await _unitOfWork.Repository.CompleteAsync();

            response.IsSuccess = true;
            response.Data = insertedCount;
            response.Message = $"{insertedCount} adet müşteri eklendi.";

            return response;
        }
        private static string BuildAddress(string? addr1, string? addr2, string? postcode, string? city)
        {
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(addr1))
                parts.Add(addr1.Trim());

            if (!string.IsNullOrWhiteSpace(addr2))
                parts.Add(addr2.Trim());

            if (!string.IsNullOrWhiteSpace(postcode))
                parts.Add(postcode.Trim());

            if (!string.IsNullOrWhiteSpace(city))
                parts.Add(city.Trim());

            return string.Join(" ", parts);
        }

        private static bool IsEmail(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Basit kontrol senaryon için yeterli
            return value.Contains("@");
        }

        private static string? CleanPhone(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Baştaki '>' gibi karakterleri temizle, sadece rakam ve artı işaretini bırak
            var chars = value.Where(c => char.IsDigit(c) || c == '+' || c == ' ');
            var phone = new string(chars.ToArray()).Trim();

            return string.IsNullOrWhiteSpace(phone) ? null : phone;
        }

      public async Task<ResponseModel<PaginatedList<CustomerGetDto>>> GetByTenantCodeAsync(string? tenantCode, QueryParams queryParams)
        {
            var response = new ResponseModel<PaginatedList<CustomerGetDto>>();
        
            try
            {
                // Tenant'ı kod ile bul
                long? tenantId = null;
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    var tenant = await _unitOfWork.Repository
                        .GetQueryable<Tenant>()
                        .Where(t => t.Code == tenantCode.Trim())
                        .FirstOrDefaultAsync();
        
                    if (tenant == null)
                    {
                        response.IsSuccess = false;
                        response.Message = $"Tenant bulunamadı: {tenantCode}";
                        return response;
                    }
        
                    tenantId = tenant.Id;
                }
        
                // Customer sorgusu
                var query = _unitOfWork.Repository
                    .GetQueryable<Customer>()
                    .Include(c => c.CustomerType)
                    .Include(c => c.CustomerGroup)
                    .Include(c => c.CustomerSystemAssignments)
                        .ThenInclude(a => a.CustomerSystem)
                    .Include(c => c.Tenant)
                    .AsQueryable();
        
                // TenantId ile filtrele
                if (tenantId.HasValue)
                {
                    query = query.Where(c => c.TenantId == tenantId.Value);
                }
        
                // Search parametresi varsa
                if (!string.IsNullOrWhiteSpace(queryParams.Search))
                {
                    var searchTerm = queryParams.Search.ToLower();
                    query = query.Where(c =>
                        (c.SubscriberCode != null && c.SubscriberCode.ToLower().Contains(searchTerm)) ||
                        (c.SubscriberCompany != null && c.SubscriberCompany.ToLower().Contains(searchTerm)) ||
                        (c.ContactName1 != null && c.ContactName1.ToLower().Contains(searchTerm)) ||
                        (c.Phone1 != null && c.Phone1.Contains(searchTerm)) ||
                        (c.Email1 != null && c.Email1.ToLower().Contains(searchTerm)) ||
                        (c.City != null && c.City.ToLower().Contains(searchTerm))
                    );
                }
        
                // Sıralama
                if (!string.IsNullOrWhiteSpace(queryParams.Sort))
                {
                    query = queryParams.Sort.ToLower() switch
                    {
                        "name" => queryParams.Desc ? query.OrderByDescending(c => c.SubscriberCompany) : query.OrderBy(c => c.SubscriberCompany),
                        "code" => queryParams.Desc ? query.OrderByDescending(c => c.SubscriberCode) : query.OrderBy(c => c.SubscriberCode),
                        "city" => queryParams.Desc ? query.OrderByDescending(c => c.City) : query.OrderBy(c => c.City),
                        "createdate" => queryParams.Desc ? query.OrderByDescending(c => c.CreatedDate) : query.OrderBy(c => c.CreatedDate),
                        _ => queryParams.Desc ? query.OrderByDescending(c => c.Id) : query.OrderBy(c => c.Id)
                    };
                }
                else
                {
                    query = query.OrderByDescending(c => c.Id);
                }
        
                // Toplam kayıt sayısı
                var totalCount = await query.CountAsync();
        
                // Sayfalama
                var page = queryParams.Page < 1 ? 1 : queryParams.Page;
                var pageSize = queryParams.PageSize < 1 ? 20 : queryParams.PageSize > 100 ? 100 : queryParams.PageSize;
        
                var items = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();
        
                // DTO'ya map
                var dtos = _mapper.Map<List<CustomerGetDto>>(items);
        
                var paginatedList = new PaginatedList<CustomerGetDto>(dtos, totalCount, page, pageSize);
        
                response.IsSuccess = true;
                response.Data = paginatedList;
                response.Message = "Müşteriler başarıyla getirildi.";
            }
            catch (Exception ex)
            {
                response.IsSuccess = false;
                response.Message = $"Hata oluştu: {ex.Message}";
            }
        
            return response;
        }
    }
}

public class ExternalCustomerRoot
{
    [JsonPropertyName("columns")]
    public List<string>? Columns { get; set; }

    [JsonPropertyName("maxRows")]
    public int MaxRows { get; set; }

    [JsonPropertyName("results")]
    public List<ExternalCustomerRow> Results { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
public class RootObject
{
    public List<ExternalCustomerRow> Results { get; set; } = new();
}
public class ExternalCustomerRow
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("dealerId")]
    public string DealerId { get; set; } = string.Empty;

    [JsonPropertyName("addr1")]
    public string? Addr1 { get; set; }

    [JsonPropertyName("addr2")]
    public string? Addr2 { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("postcode")]
    public string? Postcode { get; set; }

    [JsonPropertyName("contPoint")]
    public string? ContPoint { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}