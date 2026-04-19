using Core.Common;
using Model.Dtos.WorkingHourPolicy;

namespace Business.Interfaces
{
    public interface IWorkingHourPolicyService
    {
        /// <summary>
        /// Tüm aktif mesai politikalarýný getirir
        /// </summary>
        Task<ResponseModel<List<WorkingHourPolicyGetDto>>> GetAllPoliciesAsync();

        /// <summary>
        /// Belirli bir tarihe uygulanan politikalarý getirir (öncelik sýrasýna göre)
        /// </summary>
        Task<ResponseModel<List<WorkingHourPolicyGetDto>>> GetPoliciesForDateAsync(DateOnly date);

        /// <summary>
        /// Belirli bir tarihte normal mesai saatlerini döndürür
        /// </summary>
        Task<(TimeOnly? StartTime, TimeOnly? EndTime)> GetWorkingHoursForDateAsync(DateOnly date);

        /// <summary>
        /// Belirli bir tarih ve saatin fazla mesai olup olmadýðýný kontrol eder
        /// </summary>
        Task<bool> IsOvertimeAsync(DateTimeOffset dateTime);

        /// <summary>
        /// Politika oluþturur
        /// </summary>
        Task<ResponseModel<WorkingHourPolicyGetDto>> CreatePolicyAsync(WorkingHourPolicyCreateDto dto);

        /// <summary>
        /// Politika günceller
        /// </summary>
        Task<ResponseModel<WorkingHourPolicyGetDto>> UpdatePolicyAsync(WorkingHourPolicyUpdateDto dto);

        /// <summary>
        /// Politika siler
        /// </summary>
        Task<ResponseModel<bool>> DeletePolicyAsync(long id);

        /// <summary>
        /// Politika aktif/pasif yapar
        /// </summary>
        Task<ResponseModel<bool>> TogglePolicyAsync(long id, bool isActive);

        /// <summary>
        /// Nager.Date API'den resmi tatilleri çeker ve WorkingHourPolicy olarak kaydeder
        /// </summary>
        Task<ResponseModel<SyncPublicHolidaysDto>> SyncPublicHolidaysFromApiAsync(int year);

        /// <summary>
        /// Default hafta içi ve hafta sonu politikalarýný oluþturur
        /// </summary>
        Task<ResponseModel<bool>> CreateDefaultPoliciesAsync();
    }
}