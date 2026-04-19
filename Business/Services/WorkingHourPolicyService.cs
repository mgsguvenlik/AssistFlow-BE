using Business.Interfaces;
using Business.UnitOfWork;
using Core.Common;
using Core.Enums;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Concrete;
using Model.Dtos.WorkingHourPolicy;
using System.Text.Json;

namespace Business.Services
{
    public class WorkingHourPolicyService : IWorkingHourPolicyService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<WorkingHourPolicyService> _logger;
        private readonly ICurrentUser _currentUser;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TypeAdapterConfig _config;

        public WorkingHourPolicyService(
            IUnitOfWork uow,
            ILogger<WorkingHourPolicyService> logger,
            ICurrentUser currentUser,
            IHttpClientFactory httpClientFactory,
            TypeAdapterConfig config)
        {
            _uow = uow;
            _logger = logger;
            _currentUser = currentUser;
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public async Task<ResponseModel<List<WorkingHourPolicyGetDto>>> GetAllPoliciesAsync()
        {
            try
            {
                var policies = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.Priority)
                    .ThenBy(x => x.Name)
                    .ToListAsync();

                var result = policies.Select(p => new WorkingHourPolicyGetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    PolicyType = p.PolicyType,
                    PolicyTypeText = GetPolicyTypeText(p.PolicyType),
                    SpecificDate = p.SpecificDate,
                    Year = p.Year,
                    DayOfWeek = p.DayOfWeek,
                    DayOfWeekText = p.DayOfWeek.HasValue ? GetDayOfWeekText(p.DayOfWeek.Value) : null,
                    WorkStartTime = p.WorkStartTime,
                    WorkEndTime = p.WorkEndTime,
                    IsActive = p.IsActive,
                    Priority = p.Priority,
                    CountryCode = p.CountryCode,
                    IsPublicHoliday = p.IsPublicHoliday
                }).ToList();

                return ResponseModel<List<WorkingHourPolicyGetDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetAllPoliciesAsync");
                return ResponseModel<List<WorkingHourPolicyGetDto>>.Fail(
                    $"Politikalar getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<List<WorkingHourPolicyGetDto>>> GetPoliciesForDateAsync(DateOnly date)
        {
            try
            {
                var policies = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.IsActive)
                    .ToListAsync();

                var applicablePolicies = policies
                    .Where(p => IsPolicyApplicableForDate(p, date))
                    .OrderByDescending(p => p.Priority)
                    .Select(p => new WorkingHourPolicyGetDto
                    {
                        Id = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        PolicyType = p.PolicyType,
                        PolicyTypeText = GetPolicyTypeText(p.PolicyType),
                        SpecificDate = p.SpecificDate,
                        Year = p.Year,
                        DayOfWeek = p.DayOfWeek,
                        DayOfWeekText = p.DayOfWeek.HasValue ? GetDayOfWeekText(p.DayOfWeek.Value) : null,
                        WorkStartTime = p.WorkStartTime,
                        WorkEndTime = p.WorkEndTime,
                        IsActive = p.IsActive,
                        Priority = p.Priority,
                        CountryCode = p.CountryCode,
                        IsPublicHoliday = p.IsPublicHoliday
                    })
                    .ToList();

                return ResponseModel<List<WorkingHourPolicyGetDto>>.Success(applicablePolicies);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPoliciesForDateAsync - Date: {Date}", date);
                return ResponseModel<List<WorkingHourPolicyGetDto>>.Fail(
                    $"Tarih için politikalar getirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<(TimeOnly? StartTime, TimeOnly? EndTime)> GetWorkingHoursForDateAsync(DateOnly date)
        {
            try
            {
                var policies = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.IsActive)
                    .ToListAsync();

                // En yüksek öncelikli geçerli politikayý bul
                var applicablePolicy = policies
                    .Where(p => IsPolicyApplicableForDate(p, date))
                    .OrderByDescending(p => p.Priority)
                    .FirstOrDefault();

                if (applicablePolicy != null)
                {
                    return (applicablePolicy.WorkStartTime, applicablePolicy.WorkEndTime);
                }

                // Default hafta içi politikasý
                return (new TimeOnly(9, 0), new TimeOnly(18, 0));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetWorkingHoursForDateAsync - Date: {Date}", date);
                return (new TimeOnly(9, 0), new TimeOnly(18, 0));
            }
        }

        public async Task<bool> IsOvertimeAsync(DateTimeOffset dateTime)
        {
            try
            {
                var date = DateOnly.FromDateTime(dateTime.Date);
                var time = TimeOnly.FromDateTime(dateTime.DateTime);

                var (startTime, endTime) = await GetWorkingHoursForDateAsync(date);

                // Eðer start/end null ise, tüm gün fazla mesai
                if (!startTime.HasValue || !endTime.HasValue)
                    return true;

                // Normal mesai saatleri dýþýnda mý?
                return time < startTime.Value || time >= endTime.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IsOvertimeAsync - DateTime: {DateTime}", dateTime);
                return false;
            }
        }

        public async Task<ResponseModel<WorkingHourPolicyGetDto>> CreatePolicyAsync(WorkingHourPolicyCreateDto dto)
        {
            try
            {
                // Validasyon
                if (dto.PolicyType == WorkingHourPolicyType.SpecificDate && !dto.SpecificDate.HasValue)
                    return ResponseModel<WorkingHourPolicyGetDto>.Fail(
                        "Belirli tarih tipi için tarih belirtilmelidir.",
                        StatusCode.BadRequest);

                if (dto.PolicyType == WorkingHourPolicyType.WeekDay && !dto.DayOfWeek.HasValue)
                    return ResponseModel<WorkingHourPolicyGetDto>.Fail(
                        "Hafta günü tipi için gün belirtilmelidir.",
                        StatusCode.BadRequest);

                var policy = new WorkingHourPolicy
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    PolicyType = dto.PolicyType,
                    SpecificDate = dto.SpecificDate,
                    Year = dto.Year,
                    DayOfWeek = dto.DayOfWeek,
                    WorkStartTime = dto.WorkStartTime,
                    WorkEndTime = dto.WorkEndTime,
                    IsActive = dto.IsActive,
                    Priority = dto.Priority,
                    CountryCode = dto.CountryCode,
                    IsPublicHoliday = dto.IsPublicHoliday,
                    CreatedUser = _currentUser.Id,
                    CreatedDate = DateTimeOffset.Now,
                    //TenantId = _currentUser.TenantId
                };

                await _uow.Repository.AddAsync(policy);
                await _uow.Repository.CompleteAsync();

                var result = new WorkingHourPolicyGetDto
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    Description = policy.Description,
                    PolicyType = policy.PolicyType,
                    PolicyTypeText = GetPolicyTypeText(policy.PolicyType),
                    SpecificDate = policy.SpecificDate,
                    Year = policy.Year,
                    DayOfWeek = policy.DayOfWeek,
                    DayOfWeekText = policy.DayOfWeek.HasValue ? GetDayOfWeekText(policy.DayOfWeek.Value) : null,
                    WorkStartTime = policy.WorkStartTime,
                    WorkEndTime = policy.WorkEndTime,
                    IsActive = policy.IsActive,
                    Priority = policy.Priority,
                    CountryCode = policy.CountryCode,
                    IsPublicHoliday = policy.IsPublicHoliday
                };

                return ResponseModel<WorkingHourPolicyGetDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreatePolicyAsync");
                return ResponseModel<WorkingHourPolicyGetDto>.Fail(
                    $"Politika oluþturulurken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<WorkingHourPolicyGetDto>> UpdatePolicyAsync(WorkingHourPolicyUpdateDto dto)
        {
            try
            {
                var policy = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .FirstOrDefaultAsync(x => x.Id == dto.Id && !x.IsDeleted);

                if (policy == null)
                    return ResponseModel<WorkingHourPolicyGetDto>.Fail(
                        "Politika bulunamadý.",
                        StatusCode.NotFound);

                policy.WorkStartTime = dto.WorkStartTime;
                policy.WorkEndTime = dto.WorkEndTime;
                policy.IsActive = dto.IsActive;
                policy.Description = dto.Description;
                policy.Priority = dto.Priority;
                policy.UpdatedUser = _currentUser.Id;
                policy.UpdatedDate = DateTimeOffset.Now;

                _uow.Repository.Update(policy);
                await _uow.Repository.CompleteAsync();

                var result = new WorkingHourPolicyGetDto
                {
                    Id = policy.Id,
                    Name = policy.Name,
                    Description = policy.Description,
                    PolicyType = policy.PolicyType,
                    PolicyTypeText = GetPolicyTypeText(policy.PolicyType),
                    SpecificDate = policy.SpecificDate,
                    Year = policy.Year,
                    DayOfWeek = policy.DayOfWeek,
                    DayOfWeekText = policy.DayOfWeek.HasValue ? GetDayOfWeekText(policy.DayOfWeek.Value) : null,
                    WorkStartTime = policy.WorkStartTime,
                    WorkEndTime = policy.WorkEndTime,
                    IsActive = policy.IsActive,
                    Priority = policy.Priority,
                    CountryCode = policy.CountryCode,
                    IsPublicHoliday = policy.IsPublicHoliday
                };

                return ResponseModel<WorkingHourPolicyGetDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdatePolicyAsync - Id: {Id}", dto.Id);
                return ResponseModel<WorkingHourPolicyGetDto>.Fail(
                    $"Politika güncellenirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<bool>> DeletePolicyAsync(long id)
        {
            try
            {
                var policy = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (policy == null)
                    return ResponseModel<bool>.Fail(
                        "Politika bulunamadý.",
                        StatusCode.NotFound);

                policy.IsDeleted = true;
                policy.UpdatedUser = _currentUser.Id;
                policy.UpdatedDate = DateTimeOffset.Now;

                _uow.Repository.Update(policy);
                await _uow.Repository.CompleteAsync();

                return ResponseModel<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeletePolicyAsync - Id: {Id}", id);
                return ResponseModel<bool>.Fail(
                    $"Politika silinirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<bool>> TogglePolicyAsync(long id, bool isActive)
        {
            try
            {
                var policy = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

                if (policy == null)
                    return ResponseModel<bool>.Fail(
                        "Politika bulunamadý.",
                        StatusCode.NotFound);

                policy.IsActive = isActive;
                policy.UpdatedUser = _currentUser.Id;
                policy.UpdatedDate = DateTimeOffset.Now;

                _uow.Repository.Update(policy);
                await _uow.Repository.CompleteAsync();

                return ResponseModel<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TogglePolicyAsync - Id: {Id}", id);
                return ResponseModel<bool>.Fail(
                    $"Politika durumu deðiþtirilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<SyncPublicHolidaysDto>> SyncPublicHolidaysFromApiAsync(int year)
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var apiUrl = $"https://date.nager.at/api/v3/PublicHolidays/{year}/TR";

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                    return ResponseModel<SyncPublicHolidaysDto>.Fail(
                        $"API'den veri çekilirken hata: {response.StatusCode}",
                        StatusCode.Error);

                var jsonContent = await response.Content.ReadAsStringAsync();
                var holidays = JsonSerializer.Deserialize<List<NagerHolidayResponseDto>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (holidays == null || holidays.Count == 0)
                    return ResponseModel<SyncPublicHolidaysDto>.Fail(
                        "API'den tatil günü verisi alýnamadý.",
                        StatusCode.NotFound);

                var existingPolicies = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .Where(x => !x.IsDeleted && x.Year == year && x.IsPublicHoliday)
                    .ToListAsync();

                int newAdded = 0;
                int updated = 0;
                var addedHolidayNames = new List<string>();

                foreach (var apiHoliday in holidays)
                {
                    var date = DateOnly.Parse(apiHoliday.Date);
                    var existing = existingPolicies.FirstOrDefault(x => x.SpecificDate == date);

                    if (existing == null)
                    {
                        var newPolicy = new WorkingHourPolicy
                        {
                            Name = apiHoliday.LocalName,
                            Description = apiHoliday.Name,
                            PolicyType = WorkingHourPolicyType.PublicHoliday,
                            SpecificDate = date,
                            Year = year,
                            WorkStartTime = null, // Tüm gün tatil
                            WorkEndTime = null,
                            IsActive = true,
                            Priority = 100, // Resmi tatiller en yüksek öncelik
                            CountryCode = apiHoliday.CountryCode,
                            IsPublicHoliday = true,
                            HolidayTypes = apiHoliday.Types != null ? JsonSerializer.Serialize(apiHoliday.Types) : null,
                            CreatedUser = _currentUser.Id,
                            CreatedDate = DateTimeOffset.Now,
                            //TenantId = _currentUser.TenantId
                        };

                        await _uow.Repository.AddAsync(newPolicy);
                        newAdded++;
                        addedHolidayNames.Add(apiHoliday.LocalName);
                    }
                    else
                    {
                        existing.Name = apiHoliday.LocalName;
                        existing.Description = apiHoliday.Name;
                        existing.HolidayTypes = apiHoliday.Types != null ? JsonSerializer.Serialize(apiHoliday.Types) : null;
                        existing.UpdatedUser = _currentUser.Id;
                        existing.UpdatedDate = DateTimeOffset.Now;

                        _uow.Repository.Update(existing);
                        updated++;
                    }
                }

                await _uow.Repository.CompleteAsync();

                var result = new SyncPublicHolidaysDto
                {
                    Year = year,
                    TotalFetched = holidays.Count,
                    NewAdded = newAdded,
                    Updated = updated,
                    Skipped = 0,
                    AddedHolidays = addedHolidayNames,
                    Message = $"{year} yýlý için {newAdded} yeni resmi tatil eklendi, {updated} tatil güncellendi."
                };

                return ResponseModel<SyncPublicHolidaysDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncPublicHolidaysFromApiAsync - Year: {Year}", year);
                return ResponseModel<SyncPublicHolidaysDto>.Fail(
                    $"Resmi tatiller senkronize edilirken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        public async Task<ResponseModel<bool>> CreateDefaultPoliciesAsync()
        {
            try
            {
                var existingPolicies = await _uow.Repository
                    .GetQueryable<WorkingHourPolicy>()
                    .Where(x => !x.IsDeleted)
                    .Where(x => x.PolicyType == WorkingHourPolicyType.WeekdayDefault ||
                               x.PolicyType == WorkingHourPolicyType.WeekendDefault)
                    .ToListAsync();

                if (existingPolicies.Any())
                    return ResponseModel<bool>.Fail(
                        "Default politikalar zaten mevcut.",
                        StatusCode.BadRequest);

                var policies = new List<WorkingHourPolicy>
                {
                    // Hafta içi default (Pazartesi-Cuma 09:00-18:00)
                    new WorkingHourPolicy
                    {
                        Name = "Hafta Ýçi Mesai Saatleri",
                        Description = "Pazartesi-Cuma arasý normal mesai saatleri (09:00-18:00)",
                        PolicyType = WorkingHourPolicyType.WeekdayDefault,
                        WorkStartTime = new TimeOnly(9, 0),
                        WorkEndTime = new TimeOnly(18, 0),
                        IsActive = true,
                        Priority = 10,
                        CountryCode = "TR",
                        IsPublicHoliday = false,
                        CreatedUser = _currentUser.Id,
                        CreatedDate = DateTimeOffset.Now,
                        //TenantId = _currentUser.TenantId
                    },
                    // Cumartesi (Tüm gün fazla mesai)
                    new WorkingHourPolicy
                    {
                        Name = "Cumartesi Günü",
                        Description = "Cumartesi günü tüm gün fazla mesai",
                        PolicyType = WorkingHourPolicyType.WeekDay,
                        DayOfWeek = DayOfWeek.Saturday,
                        WorkStartTime = null,
                        WorkEndTime = null,
                        IsActive = true,
                        Priority = 50,
                        CountryCode = "TR",
                        IsPublicHoliday = false,
                        CreatedUser = _currentUser.Id,
                        CreatedDate = DateTimeOffset.Now,
                        //TenantId = _currentUser.TenantId
                    },
                    // Pazar (Tüm gün fazla mesai)
                    new WorkingHourPolicy
                    {
                        Name = "Pazar Günü",
                        Description = "Pazar günü tüm gün fazla mesai",
                        PolicyType = WorkingHourPolicyType.WeekDay,
                        DayOfWeek = DayOfWeek.Sunday,
                        WorkStartTime = null,
                        WorkEndTime = null,
                        IsActive = true,
                        Priority = 50,
                        CountryCode = "TR",
                        IsPublicHoliday = false,
                        CreatedUser = _currentUser.Id,
                        CreatedDate = DateTimeOffset.Now,
                        //TenantId = _currentUser.TenantId
                    }
                };

                await _uow.Repository.AddRangeAsync(policies);
                await _uow.Repository.CompleteAsync();

                return ResponseModel<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateDefaultPoliciesAsync");
                return ResponseModel<bool>.Fail(
                    $"Default politikalar oluþturulurken hata: {ex.Message}",
                    StatusCode.Error);
            }
        }

        // Helper metodlar
        private bool IsPolicyApplicableForDate(WorkingHourPolicy policy, DateOnly date)
        {
            return policy.PolicyType switch
            {
                WorkingHourPolicyType.WeekdayDefault => date.DayOfWeek >= DayOfWeek.Monday && date.DayOfWeek <= DayOfWeek.Friday,
                WorkingHourPolicyType.WeekendDefault => date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday,
                WorkingHourPolicyType.WeekDay => policy.DayOfWeek.HasValue && date.DayOfWeek == policy.DayOfWeek.Value,
                WorkingHourPolicyType.PublicHoliday => policy.SpecificDate == date && policy.Year == date.Year,
                WorkingHourPolicyType.SpecificDate => policy.SpecificDate == date,
                WorkingHourPolicyType.CustomDay => policy.SpecificDate == date,
                _ => false
            };
        }

        private string GetPolicyTypeText(WorkingHourPolicyType type) => type switch
        {
            WorkingHourPolicyType.WeekdayDefault => "Hafta Ýçi Default",
            WorkingHourPolicyType.WeekendDefault => "Hafta Sonu Default",
            WorkingHourPolicyType.WeekDay => "Hafta Günü",
            WorkingHourPolicyType.PublicHoliday => "Resmi Tatil",
            WorkingHourPolicyType.SpecificDate => "Belirli Tarih",
            WorkingHourPolicyType.CustomDay => "Özel Gün",
            _ => "Bilinmiyor"
        };

        private string GetDayOfWeekText(DayOfWeek day) => day switch
        {
            DayOfWeek.Monday => "Pazartesi",
            DayOfWeek.Tuesday => "Salý",
            DayOfWeek.Wednesday => "Çarþamba",
            DayOfWeek.Thursday => "Perþembe",
            DayOfWeek.Friday => "Cuma",
            DayOfWeek.Saturday => "Cumartesi",
            DayOfWeek.Sunday => "Pazar",
            _ => "Bilinmiyor"
        };
    }
}