using Core.Common;
using Model.Dtos.OvertimeReport;

namespace Business.Interfaces.Ekb
{
    public interface IEkbOvertimeReportService
    {
        /// <summary>
        /// EKB - Belirli bir teknisyenin fazla mesai raporunu getirir
        /// </summary>
        Task<ResponseModel<EkbTechnicianOvertimeReportDto>> GetTechnicianOvertimeReportAsync(
            long technicianId,
            DateTime startDate,
            DateTime endDate,
            bool includeCustomerDetails = false);

        /// <summary>
        /// EKB - Tüm teknisyenlerin fazla mesai özetini getirir
        /// </summary>
        Task<ResponseModel<EkbAllTechniciansOvertimeSummaryDto>> GetAllTechniciansOvertimeSummaryAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// EKB - Fazla mesai raporunu Excel formatında export eder
        /// </summary>
        Task<ResponseModel<byte[]>> ExportOvertimeReportToExcelAsync(
            long? technicianId,
            DateTime startDate,
            DateTime endDate);
    }
}