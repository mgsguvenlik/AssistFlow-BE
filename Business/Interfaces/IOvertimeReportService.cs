using Core.Common;
using Model.Dtos.OvertimeReport;

namespace Business.Interfaces
{
    /// <summary>
    /// Fazla mesai raporu servisi
    /// </summary>
    public interface IOvertimeReportService
    {
        /// <summary>
        /// Belirli bir teknisyenin fazla mesai raporunu getirir
        /// </summary>
        Task<ResponseModel<TechnicianOvertimeReportDto>> GetTechnicianOvertimeReportAsync(
            long technicianId,
            DateTime startDate,
            DateTime endDate,
            bool includeCustomerDetails = false);

        /// <summary>
        /// Tüm teknisyenlerin fazla mesai özetini getirir
        /// </summary>
        Task<ResponseModel<AllTechniciansOvertimeSummaryDto>> GetAllTechniciansOvertimeSummaryAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// Fazla mesai raporunu Excel formatýnda export eder
        /// </summary>
        Task<ResponseModel<byte[]>> ExportOvertimeReportToExcelAsync(
            long? technicianId,
            DateTime startDate,
            DateTime endDate);
    }
}