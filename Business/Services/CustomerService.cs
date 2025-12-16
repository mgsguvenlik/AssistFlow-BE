using Business.Interfaces;
using Business.Services.Base;
using Business.UnitOfWork;
using Core.Common;
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