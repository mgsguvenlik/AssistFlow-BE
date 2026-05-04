using Core.Common;
using Model.Dtos.OvertimeReport;

namespace Business.Interfaces.Qnb
{
    public interface IQnbOvertimeReportService
    {
        /// <summary>
        /// QNB - Belirli bir teknisyenin fazla mesai raporunu getirir
        /// </summary>
        Task<ResponseModel<QnbTechnicianOvertimeReportDto>> GetTechnicianOvertimeReportAsync(
            long technicianId,
            DateTime startDate,
            DateTime endDate,
            bool includeCustomerDetails = false);

        /// <summary>
        /// QNB - Tüm teknisyenlerin fazla mesai özetini getirir
        /// </summary>
        Task<ResponseModel<QnbAllTechniciansOvertimeSummaryDto>> GetAllTechniciansOvertimeSummaryAsync(
            DateTime startDate,
            DateTime endDate);

        /// <summary>
        /// QNB - Fazla mesai raporunu Excel formatýnda export eder
        /// </summary>
        Task<ResponseModel<byte[]>> ExportOvertimeReportToExcelAsync(
            long? technicianId,
            DateTime startDate,
            DateTime endDate);
    }
}